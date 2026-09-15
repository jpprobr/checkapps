using CheckApps.Domain;
using CheckApps.Domain.QualityAssessment;
using Repository = CheckApps.Domain.Repository;

namespace CheckApps.Infrastructure.Seeding;

/// <summary>
/// Constrói uma massa de dados fictícia com histórico de 3 meses (Jun/Jul/Ago) por app,
/// cobrindo evolução positiva, negativa e estável, além de apps com um e múltiplos
/// repositórios/projetos Sonar.
/// </summary>
public class DemoDataFactory(IQualityAssessmentEngine assessmentEngine)
{
    private static readonly DateTimeOffset Jun = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Jul = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Aug = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

    public DemoDataSet Build()
    {
        var squads = new List<Squad>
        {
            new() { Id = "sq-1", Name = "Squad Pagamentos", Description = "Checkout, cobrança e faturamento" },
            new() { Id = "sq-2", Name = "Squad Plataforma", Description = "Autenticação e notificações" }
        };

        var apps = new List<App>();
        var repositories = new List<Repository>();
        var sonarProjects = new List<SonarProject>();
        var snapshots = new List<QualitySnapshot>();

        AddApp(apps, repositories, sonarProjects, snapshots,
            appId: "app-1", squadId: "sq-1", name: "checkout-api", criticality: "high",
            repoCount: 1, sonarProjectCount: 1,
            history:
            [
                (Jun, Metrics(coverage: 75, bugs: 4, vulnerabilities: 0, codeSmells: 60, duplications: 8, reliability: "B", maintainability: "A", security: "A")),
                (Jul, Metrics(coverage: 76, bugs: 3, vulnerabilities: 0, codeSmells: 55, duplications: 7, reliability: "B", maintainability: "A", security: "A")),
                (Aug, Metrics(coverage: 82, bugs: 1, vulnerabilities: 0, codeSmells: 30, duplications: 5, reliability: "A", maintainability: "A", security: "A"))
            ]);

        AddApp(apps, repositories, sonarProjects, snapshots,
            appId: "app-2", squadId: "sq-1", name: "billing-service", criticality: "high",
            repoCount: 2, sonarProjectCount: 1,
            history:
            [
                (Jun, Metrics(coverage: 70, bugs: 5, vulnerabilities: 1, codeSmells: 90, duplications: 9, reliability: "B", maintainability: "A", security: "A")),
                (Jul, Metrics(coverage: 55, bugs: 9, vulnerabilities: 2, codeSmells: 140, duplications: 10, reliability: "B", maintainability: "B", security: "A")),
                (Aug, Metrics(coverage: 52, bugs: 10, vulnerabilities: 2, codeSmells: 150, duplications: 11, reliability: "B", maintainability: "B", security: "A"))
            ]);

        AddApp(apps, repositories, sonarProjects, snapshots,
            appId: "app-3", squadId: "sq-1", name: "invoices-worker", criticality: "medium",
            repoCount: 1, sonarProjectCount: 1,
            history:
            [
                (Jun, Metrics(coverage: 85, bugs: 0, vulnerabilities: 0, codeSmells: 20, duplications: 4, reliability: "A", maintainability: "A", security: "A")),
                (Jul, Metrics(coverage: 84, bugs: 0, vulnerabilities: 0, codeSmells: 22, duplications: 4, reliability: "A", maintainability: "A", security: "A")),
                (Aug, Metrics(coverage: 86, bugs: 0, vulnerabilities: 0, codeSmells: 18, duplications: 3, reliability: "A", maintainability: "A", security: "A"))
            ]);

        AddApp(apps, repositories, sonarProjects, snapshots,
            appId: "app-4", squadId: "sq-2", name: "auth-gateway", criticality: "high",
            repoCount: 1, sonarProjectCount: 1,
            history:
            [
                (Jun, Metrics(coverage: 65, bugs: 6, vulnerabilities: 1, codeSmells: 110, duplications: 12, reliability: "C", maintainability: "B", security: "B")),
                (Jul, Metrics(coverage: 64, bugs: 6, vulnerabilities: 1, codeSmells: 115, duplications: 13, reliability: "C", maintainability: "B", security: "B")),
                (Aug, Metrics(coverage: 63, bugs: 7, vulnerabilities: 1, codeSmells: 118, duplications: 13, reliability: "C", maintainability: "B", security: "B"))
            ]);

        AddApp(apps, repositories, sonarProjects, snapshots,
            appId: "app-5", squadId: "sq-2", name: "notifications-service", criticality: "medium",
            repoCount: 1, sonarProjectCount: 2,
            history:
            [
                (Jun, Metrics(coverage: 62, bugs: 8, vulnerabilities: 2, codeSmells: 130, duplications: 15, reliability: "D", maintainability: "B", security: "B")),
                (Jul, Metrics(coverage: 65, bugs: 5, vulnerabilities: 1, codeSmells: 95, duplications: 15, reliability: "C", maintainability: "B", security: "B")),
                (Aug, Metrics(coverage: 70, bugs: 2, vulnerabilities: 0, codeSmells: 60, duplications: 10, reliability: "B", maintainability: "A", security: "B"))
            ]);

        return new DemoDataSet(squads, apps, repositories, sonarProjects, snapshots);
    }

    private void AddApp(
        List<App> apps,
        List<Repository> repositories,
        List<SonarProject> sonarProjects,
        List<QualitySnapshot> snapshots,
        string appId,
        string squadId,
        string name,
        string criticality,
        int repoCount,
        int sonarProjectCount,
        (DateTimeOffset CollectedAt, QualityMetrics Metrics)[] history)
    {
        for (var i = 1; i <= repoCount; i++)
        {
            repositories.Add(new Repository
            {
                Id = $"{appId}-repo-{i}",
                AppId = appId,
                Name = repoCount == 1 ? name : $"{name}-{(i == 1 ? "legacy" : "v2")}",
                Provider = "github",
                Url = $"https://github.com/checkapps-demo/{name}-{i}",
                Status = i == repoCount ? "active" : "legacy"
            });
        }

        for (var i = 1; i <= sonarProjectCount; i++)
        {
            sonarProjects.Add(new SonarProject
            {
                Id = $"{appId}-sonar-{i}",
                AppId = appId,
                ProjectKey = $"{name}-{i}",
                Name = sonarProjectCount == 1 ? name : $"{name} (módulo {i})",
                Source = "SONAR_MOCK",
                Status = "active"
            });
        }

        var primarySonarProjectId = $"{appId}-sonar-1";

        QualitySnapshot? latest = null;
        foreach (var (collectedAt, metrics) in history)
        {
            var level = assessmentEngine.Assess(metrics);
            var snapshot = new QualitySnapshot
            {
                Id = $"{appId}-snap-{collectedAt:yyyyMM}",
                AppId = appId,
                SonarProjectId = primarySonarProjectId,
                CollectedAt = collectedAt,
                Source = "SONAR_MOCK",
                Metrics = metrics,
                QualityLevel = level,
                AssessmentRulesVersion = assessmentEngine.RulesVersion
            };
            snapshots.Add(snapshot);
            latest = snapshot;
        }

        apps.Add(new App
        {
            Id = appId,
            SquadId = squadId,
            Name = name,
            Criticality = criticality,
            Status = "active",
            Tags = ["demo"],
            CurrentQualityLevel = latest!.QualityLevel,
            CurrentMetrics = latest.Metrics,
            LastSnapshotAt = latest.CollectedAt,
            LastSnapshotId = latest.Id,
            AssessmentRulesVersion = latest.AssessmentRulesVersion
        });
    }

    private static QualityMetrics Metrics(
        double coverage, int bugs, int vulnerabilities, int codeSmells, double duplications,
        string reliability, string maintainability, string security) => new()
        {
            Coverage = coverage,
            Bugs = bugs,
            Vulnerabilities = vulnerabilities,
            CodeSmells = codeSmells,
            Duplications = duplications,
            Reliability = reliability,
            Maintainability = maintainability,
            Security = security
        };
}
