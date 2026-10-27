using CheckApps.Application.Analysis;

namespace CheckApps.UnitTests.Analysis;

/// <summary>
/// Os repositórios fictícios de <c>Fixtures/Dotnet/</c> (um por pasta, com o resultado esperado no <c>EXPECTED.md</c>).
/// Copiados para a pasta de saída do projeto de testes.
/// </summary>
internal static class FixtureFiles
{
    private static string Root(string scenario) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "Dotnet", scenario);

    /// <summary>Todos os caminhos do repositório fictício, relativos à raiz e com <c>/</c>, sem o <c>EXPECTED.md</c>.</summary>
    public static IReadOnlyList<string> Paths(string scenario)
    {
        var root = Root(scenario);
        Assert.True(Directory.Exists(root), $"Fixture não encontrada na saída do build: {root}");

        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .Where(p => p != "EXPECTED.md")
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    public static string Read(string scenario, string path) => File.ReadAllText(Path.Combine(Root(scenario), path));

    /// <summary>O mesmo caminho que o serviço percorre: lista, o scanner escolhe, lê-se só o escolhido, o scanner analisa.</summary>
    public static ScannerResult Scan(IRepositoryScanner scanner, string scenario)
    {
        var paths = Paths(scenario);
        var files = scanner.SelectFiles(paths).ToDictionary(p => p, p => Read(scenario, p));
        return scanner.Analyze(files);
    }

    /// <summary>Uma linha por fato: <c>projeto|tipo|nome|versão|confiança</c>, ordenada, para comparar com o EXPECTED.md.</summary>
    public static IReadOnlyList<string> Rows(ScannerResult result) =>
        result.Facts
            .Select(f => $"{f.ProjectPath}|{f.Kind}|{f.Name}|{f.Version}|{f.Confidence}")
            .Order(StringComparer.Ordinal)
            .ToList();

    public static IReadOnlyList<string> Sorted(params string[] rows) => rows.Order(StringComparer.Ordinal).ToList();
}
