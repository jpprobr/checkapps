using CheckApps.Application.Analysis;
using CheckApps.Application.Persistence;
using CheckApps.Domain.Analysis;

namespace CheckApps.Api.Contracts;

public static class DependencyDtoMapping
{
    public static AppDependenciesDto ToDto(this AppDependencies dependencies) => new(
        dependencies.AppId,
        dependencies.InternalPrefixesConfigured,
        dependencies.Repositories.Select(r => new RepositoryDependenciesDto(
            r.Repository.Id, r.Repository.Name, r.Repository.Status, r.Repository.Role, r.Repository.Url, r.Repository.Sigla,
            r.Analysis?.ToDto(), r.LastFailure?.ToDto(),
            r.Frameworks.Select(f => f.ToDto()).ToList(),
            r.Packages.Select(f => f.ToDto()).ToList())).ToList());

    public static AnalysisDto ToDto(this RepositoryAnalysis analysis) =>
        new(analysis.Id, analysis.AnalyzedAt, analysis.CommitSha, analysis.Status, analysis.Warnings);

    public static DependencyFactDto ToDto(this DependencyFact fact) => new(
        fact.Fact.Id, fact.Fact.Ecosystem, fact.Fact.Kind, fact.Fact.Name, fact.Fact.Version, fact.Fact.ProjectPath,
        fact.Fact.SourceFile, fact.Fact.Origin, fact.Fact.Confidence, fact.Fact.ConfidenceNote, fact.Fact.ScannerId,
        fact.Fact.ScannerVersion, fact.IsInternal);

    public static PackageSearchResultDto ToDto(this PackageSearchResult result) => new(
        result.Usages.Select(u => new PackageUsageDto(
            u.Row.AppId, u.Row.AppName, u.Row.SquadId, u.Row.SquadName, u.Row.RepositoryId, u.Row.RepositoryName,
            u.Row.RepositoryStatus, u.Row.Fact.Name, u.Row.Fact.Version, u.Row.Fact.ProjectPath, u.Row.Fact.Confidence,
            u.Row.Fact.ConfidenceNote, u.IsInternal, u.Row.AnalyzedAt)).ToList(),
        result.Versions.Select(v => new PackageVersionSummaryDto(v.Name, v.Version, v.AppCount, v.IsInternal)).ToList(),
        result.Coverage.ToDto(),
        result.InternalPrefixesConfigured);

    public static FrameworkOverviewDto ToDto(this FrameworkOverview overview) => new(
        overview.Cells.Select(c => new FrameworkCellDto(
            c.Framework, c.SquadId, c.SquadName,
            c.Apps.Select(a => new FrameworkAppDto(a.AppId, a.AppName, a.Repositories)).ToList())).ToList(),
        overview.Coverage.ToDto());

    public static AnalysisCoverageDto ToDto(this AnalysisCoverage coverage) =>
        new(coverage.UnanalyzedRepositories, coverage.LatestAnalyzedAt);
}
