using CheckApps.Domain.Analysis;

namespace CheckApps.Application.Analysis;

/// <summary>Quanto uma mudança de contrato ameaça quem o consome. Texto na API; a tela mostra em texto, nunca só por cor.</summary>
public static class ContractChangeSeverities
{
    public const string Breaking = "quebra";
    public const string Attention = "atencao";
    public const string Safe = "segura";
}

/// <summary>O que mudou. Texto na API.</summary>
public static class ContractChangeKinds
{
    public const string EndpointRemoved = "endpoint-removido";
    public const string EndpointAdded = "endpoint-novo";
    public const string EndpointDeprecated = "endpoint-deprecated";
    public const string FieldAdded = "campo-novo";
    public const string FieldRemoved = "campo-removido";
    public const string BecameRequired = "ficou-obrigatorio";
    public const string BecameOptional = "ficou-opcional";
    public const string TypeChanged = "tipo-alterado";
    public const string FormatChanged = "formato-alterado";
    public const string EnumValueRemoved = "enum-removido";
    public const string EnumValueAdded = "enum-novo";
}

/// <summary>Uma diferença entre duas fotos do contrato.</summary>
/// <param name="FieldPath">Nulo quando a mudança é no endpoint inteiro.</param>
/// <param name="Description">Em texto, para quem lê: nunca traz mais do que a estrutura do contrato.</param>
public sealed record ContractChange(
    string Severity, string Kind, string Method, string Path, string? Location, string? StatusCode, string? MediaType,
    string? FieldPath, string Description);

/// <param name="Warnings">O que é preciso saber antes de confiar na comparação.</param>
public sealed record ContractComparison(IReadOnlyList<ContractChange> Changes, IReadOnlyList<string> Warnings);

/// <summary>
/// Compara duas fotos do contrato (spec 021). Função pura: sem banco nem relógio, e o resultado não é gravado, então uma regra
/// melhor vale também para o histórico. Endpoint é método + rota com os parâmetros neutralizados, e campo é lugar + status +
/// mídia + caminho. Renomear aparece como remover e criar: não se tenta adivinhar.
/// </summary>
public static class ContractComparer
{
    private const string PartialExplanation = "uma das análises é parcial: pode não ter sumido de verdade.";

    /// <param name="anyPartial">
    /// Alguma das análises é parcial. Um arquivo que não pôde ser lido faria tudo o que está nele parecer removido, então o que
    /// sumiu vira atenção, com a explicação.
    /// </param>
    public static ContractComparison Compare(
        IReadOnlyList<RepositoryApiEndpoint> before, IReadOnlyList<RepositoryApiEndpoint> after, bool anyPartial)
    {
        var warnings = new List<string>();
        var changes = new List<ContractChange>();

        var duplicated = Duplicated(before).Union(Duplicated(after)).ToHashSet();
        foreach (var (method, pathKey) in duplicated.OrderBy(d => d.PathKey, StringComparer.Ordinal).ThenBy(d => d.Method, StringComparer.Ordinal))
        {
            warnings.Add($"{method} {pathKey} aparece em mais de um arquivo: a comparação foi feita arquivo a arquivo.");
        }

        var beforeByKey = before.ToDictionary(e => KeyOf(e, duplicated));
        var afterByKey = after.ToDictionary(e => KeyOf(e, duplicated));

        foreach (var (key, endpoint) in beforeByKey)
        {
            if (!afterByKey.TryGetValue(key, out var current))
            {
                changes.Add(anyPartial
                    ? ForEndpoint(endpoint, ContractChangeSeverities.Attention, ContractChangeKinds.EndpointRemoved, $"Endpoint não encontrado: {PartialExplanation}")
                    : ForEndpoint(endpoint, ContractChangeSeverities.Breaking, ContractChangeKinds.EndpointRemoved, "Endpoint removido."));
                continue;
            }

            if (!endpoint.Deprecated && current.Deprecated)
            {
                changes.Add(ForEndpoint(current, ContractChangeSeverities.Attention, ContractChangeKinds.EndpointDeprecated, "Endpoint marcado como deprecated."));
            }

            CompareFields(endpoint, current, anyPartial, changes);
        }

        foreach (var (key, endpoint) in afterByKey)
        {
            if (!beforeByKey.ContainsKey(key))
            {
                changes.Add(ForEndpoint(endpoint, ContractChangeSeverities.Safe, ContractChangeKinds.EndpointAdded, "Endpoint novo."));
            }
        }

        var versions = before.Select(e => e.ScannerVersion).Concat(after.Select(e => e.ScannerVersion)).Distinct(StringComparer.Ordinal).Count();
        if (versions > 1)
        {
            warnings.Add("As análises usaram versões diferentes do scanner: parte das diferenças pode vir do scanner, não do contrato.");
        }

        var ordered = changes
            .OrderBy(c => SeverityOrder(c.Severity))
            .ThenBy(c => c.Path, StringComparer.Ordinal)
            .ThenBy(c => c.Method, StringComparer.Ordinal)
            .ThenBy(c => c.Location, StringComparer.Ordinal)
            .ThenBy(c => c.FieldPath, StringComparer.Ordinal)
            .ThenBy(c => c.Description, StringComparer.Ordinal)
            .ToList();

        return new ContractComparison(ordered, warnings);
    }

    private static void CompareFields(
        RepositoryApiEndpoint before, RepositoryApiEndpoint after, bool anyPartial, List<ContractChange> changes)
    {
        var beforeFields = before.Fields.ToDictionary(f => FieldKey(before, f));
        var afterFields = after.Fields.ToDictionary(f => FieldKey(after, f));

        foreach (var (key, field) in beforeFields)
        {
            if (!afterFields.TryGetValue(key, out var current))
            {
                changes.Add(Removed(after, field, anyPartial));
                continue;
            }

            CompareField(after, field, current, changes);
        }

        foreach (var (key, field) in afterFields)
        {
            if (!beforeFields.ContainsKey(key))
            {
                var breaks = field.Required && IsInput(field);
                changes.Add(ForField(
                    after, field, breaks ? ContractChangeSeverities.Breaking : ContractChangeSeverities.Safe, ContractChangeKinds.FieldAdded,
                    field.Required ? "Campo obrigatório novo." : "Campo opcional novo."));
            }
        }
    }

    private static ContractChange Removed(RepositoryApiEndpoint endpoint, RepositoryApiField field, bool anyPartial)
    {
        if (anyPartial)
        {
            return ForField(endpoint, field, ContractChangeSeverities.Attention, ContractChangeKinds.FieldRemoved, $"Campo não encontrado: {PartialExplanation}");
        }

        return IsInput(field)
            ? ForField(endpoint, field, ContractChangeSeverities.Attention, ContractChangeKinds.FieldRemoved, "Campo removido do pedido: pode ou não afetar quem ainda o envia.")
            : ForField(endpoint, field, ContractChangeSeverities.Breaking, ContractChangeKinds.FieldRemoved, "Campo removido da resposta.");
    }

    private static void CompareField(RepositoryApiEndpoint endpoint, RepositoryApiField before, RepositoryApiField after, List<ContractChange> changes)
    {
        if (!before.Required && after.Required)
        {
            changes.Add(ForField(
                endpoint, after, IsInput(after) ? ContractChangeSeverities.Breaking : ContractChangeSeverities.Safe,
                ContractChangeKinds.BecameRequired, "Campo opcional virou obrigatório."));
        }
        else if (before.Required && !after.Required)
        {
            changes.Add(ForField(
                endpoint, after, IsInput(after) ? ContractChangeSeverities.Safe : ContractChangeSeverities.Attention,
                ContractChangeKinds.BecameOptional, "Campo obrigatório virou opcional."));
        }

        if (!string.Equals(before.Type, after.Type, StringComparison.Ordinal))
        {
            // Tipo desconhecido (referência não lida, por exemplo) não prova que o campo mudou.
            var known = before.Type is not null && after.Type is not null;
            changes.Add(ForField(
                endpoint, after, known ? ContractChangeSeverities.Breaking : ContractChangeSeverities.Attention, ContractChangeKinds.TypeChanged,
                $"Tipo mudou de {before.Type ?? "desconhecido"} para {after.Type ?? "desconhecido"}."));
        }

        if (!string.Equals(before.Format, after.Format, StringComparison.Ordinal))
        {
            changes.Add(ForField(
                endpoint, after, ContractChangeSeverities.Breaking, ContractChangeKinds.FormatChanged,
                $"Formato mudou de {before.Format ?? "nenhum"} para {after.Format ?? "nenhum"}."));
        }

        foreach (var value in before.EnumValues.Except(after.EnumValues, StringComparer.Ordinal))
        {
            changes.Add(ForField(
                endpoint, after, IsInput(after) ? ContractChangeSeverities.Breaking : ContractChangeSeverities.Safe,
                ContractChangeKinds.EnumValueRemoved, $"Valor de enum removido: {value}."));
        }

        // Sair de "sem enum" para "com enum" não é um valor novo: é uma restrição nova, que a lista abaixo não cobre.
        if (before.EnumValues.Count > 0)
        {
            foreach (var value in after.EnumValues.Except(before.EnumValues, StringComparer.Ordinal))
            {
                changes.Add(ForField(
                    endpoint, after, IsInput(after) ? ContractChangeSeverities.Safe : ContractChangeSeverities.Attention,
                    ContractChangeKinds.EnumValueAdded, $"Valor de enum novo: {value}."));
            }
        }
    }

    private static bool IsInput(RepositoryApiField field) => field.Location != ApiFieldLocations.Response;

    private static int SeverityOrder(string severity) => severity switch
    {
        ContractChangeSeverities.Breaking => 0,
        ContractChangeSeverities.Attention => 1,
        _ => 2
    };

    private static (string Method, string PathKey, string? File) KeyOf(RepositoryApiEndpoint endpoint, HashSet<(string Method, string PathKey)> duplicated) =>
        (endpoint.Method, endpoint.PathKey, duplicated.Contains((endpoint.Method, endpoint.PathKey)) ? endpoint.SourceFile : null);

    private static IEnumerable<(string Method, string PathKey)> Duplicated(IReadOnlyList<RepositoryApiEndpoint> endpoints) =>
        endpoints.GroupBy(e => (e.Method, e.PathKey)).Where(g => g.Count() > 1).Select(g => g.Key);

    /// <summary>
    /// Parâmetro de rota é identificado pela **posição** na rota: renomear <c>{id}</c> para <c>{fundoId}</c> muda o contrato no
    /// papel, mas não muda nada para quem chama.
    /// </summary>
    private static (string Location, string? Status, string? Media, string Path) FieldKey(RepositoryApiEndpoint endpoint, RepositoryApiField field)
    {
        var position = field.Location == ApiFieldLocations.Path ? ApiPath.ParameterPosition(endpoint.Path, field.Path) : -1;
        return (field.Location, field.StatusCode, field.MediaType, position >= 0 ? $"#{position}" : field.Path);
    }

    private static ContractChange ForEndpoint(RepositoryApiEndpoint endpoint, string severity, string kind, string description) =>
        new(severity, kind, endpoint.Method, endpoint.Path, null, null, null, null, description);

    private static ContractChange ForField(RepositoryApiEndpoint endpoint, RepositoryApiField field, string severity, string kind, string description) =>
        new(severity, kind, endpoint.Method, endpoint.Path, field.Location, field.StatusCode, field.MediaType, field.Path, description);
}
