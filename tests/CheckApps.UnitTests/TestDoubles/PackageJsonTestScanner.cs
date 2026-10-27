using System.Text.Json;
using CheckApps.Application.Analysis;
using CheckApps.Domain.Analysis;

namespace CheckApps.UnitTests.TestDoubles;

/// <summary>
/// Segundo scanner, **só de teste**: prova que o contrato <see cref="IRepositoryScanner"/> aceita outra tecnologia sem mudar
/// tabelas, API de leitura ou telas (spec 013, SC-005). Lê <c>dependencies</c> e <c>devDependencies</c> de um <c>package.json</c>.
/// </summary>
public sealed class PackageJsonTestScanner : IRepositoryScanner
{
    public string Id => "npm-test";

    public string Version => "0.0.1";

    public bool AppliesTo(IReadOnlyList<string> paths) => SelectFiles(paths).Count > 0;

    public IReadOnlyList<string> SelectFiles(IReadOnlyList<string> paths) =>
        paths.Where(p => p.EndsWith("package.json", StringComparison.OrdinalIgnoreCase)
                && !p.Contains("node_modules/", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .ToList();

    public ScannerResult Analyze(IReadOnlyDictionary<string, string> files)
    {
        var facts = new List<ScannerFact>();
        var warnings = new List<string>();

        foreach (var (path, content) in files.OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            try
            {
                using var document = JsonDocument.Parse(content);
                foreach (var section in new[] { "dependencies", "devDependencies" })
                {
                    if (!document.RootElement.TryGetProperty(section, out var dependencies)
                        || dependencies.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    foreach (var dependency in dependencies.EnumerateObject())
                    {
                        var version = dependency.Value.GetString() ?? string.Empty;
                        var ranged = version.StartsWith('^') || version.StartsWith('~');
                        facts.Add(new ScannerFact(
                            "npm", FactKinds.Package, dependency.Name, version, path, path,
                            ranged ? Confidences.Medium : Confidences.High,
                            ranged ? "versão em intervalo, guardada como declarada" : null));
                    }
                }
            }
            catch (JsonException)
            {
                warnings.Add($"{path}: JSON inválido; arquivo ignorado.");
            }
        }

        return new ScannerResult(facts, warnings);
    }
}
