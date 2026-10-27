using CheckApps.Domain.Analysis;

namespace CheckApps.Application.Analysis;

/// <summary>
/// Normaliza o nome do ambiente de um arquivo <c>appsettings.&lt;Nome&gt;.json</c> para uma categoria (<c>dev</c>,
/// <c>hom</c>, <c>prod</c>). Os repositórios não seguem uma convenção única (<c>Dev</c>/<c>Development</c>,
/// <c>Hom</c>/<c>Homolog</c>/<c>Staging</c>, <c>Prod</c>/<c>Production</c>), então a tabela é de apelidos e é configurável.
/// Nome desconhecido devolve <c>null</c>: nunca é palpite.
/// </summary>
public static class EnvironmentNames
{
    public static readonly IReadOnlyDictionary<string, string[]> DefaultAliases = new Dictionary<string, string[]>
    {
        [EnvironmentKinds.Dev] = ["Dev", "Development", "Desenvolvimento"],
        [EnvironmentKinds.Hom] = ["Hom", "Homolog", "Homologacao", "Homologação", "Staging"],
        [EnvironmentKinds.Prod] = ["Prod", "Production", "Producao", "Produção"]
    };

    public static string? Classify(string? name, IReadOnlyDictionary<string, string[]> aliases)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var trimmed = name.Trim();
        foreach (var (kind, names) in aliases)
        {
            if (names.Any(alias => string.Equals(alias, trimmed, StringComparison.OrdinalIgnoreCase)))
            {
                return kind;
            }
        }

        return null;
    }
}
