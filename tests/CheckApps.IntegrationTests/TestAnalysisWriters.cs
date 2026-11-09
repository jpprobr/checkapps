using CheckApps.Domain.Analysis;
using CheckApps.Infrastructure.Sqlite;

namespace CheckApps.IntegrationTests;

/// <summary>Os escritores de fato de produção, para os testes montarem o <see cref="AnalysisRepository"/> sem a injeção de dependência.</summary>
public static class TestAnalysisWriters
{
    public static IReadOnlyList<IAnalysisFactWriter> All { get; } = [new DependencyFactWriter(), new IntegrationFactWriter(), new ApiContractFactWriter()];

    /// <summary>Atalho dos testes: grava pacotes e integrações sem montar a lista de fatos à mão.</summary>
    public static Task AddAsync(
        this AnalysisRepository analyses, RepositoryAnalysis analysis, IReadOnlyList<RepositoryDependency> facts,
        IReadOnlyList<RepositoryIntegration>? integrations = null, CancellationToken cancellationToken = default) =>
        analyses.SaveAsync(analysis, [.. facts, .. integrations ?? []], cancellationToken);
}
