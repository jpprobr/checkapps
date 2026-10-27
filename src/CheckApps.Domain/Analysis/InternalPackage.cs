namespace CheckApps.Domain.Analysis;

/// <summary>
/// Pacote interno = o nome começa com o prefixo da empresa (spec 013, FR-021). Calculado **na leitura**, a partir
/// de uma configuração local, e nunca gravado: mudar o prefixo vale também para o histórico. Sem prefixo
/// configurado, nada é interno.
/// </summary>
public static class InternalPackage
{
    /// <summary>
    /// Prefixo sem espaços e sempre terminado em ponto (<c>Empresa</c> e <c>Empresa.</c> valem o mesmo), para
    /// <c>Empresa</c> não marcar <c>EmpresaFoo</c> por engano. Vazio = ignorado.
    /// </summary>
    public static string? NormalizePrefix(string? prefix)
    {
        var trimmed = prefix?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.EndsWith('.') ? trimmed : trimmed + ".";
    }

    public static bool IsInternal(string? packageName, IEnumerable<string>? prefixes)
    {
        if (string.IsNullOrWhiteSpace(packageName) || prefixes is null)
        {
            return false;
        }

        return prefixes
            .Select(NormalizePrefix)
            .Any(p => p is not null && packageName.StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }
}
