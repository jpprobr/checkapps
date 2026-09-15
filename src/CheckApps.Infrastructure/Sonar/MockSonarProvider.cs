using CheckApps.Application.Sonar;
using CheckApps.Domain;

namespace CheckApps.Infrastructure.Sonar;

/// <summary>
/// Gera métricas determinísticas por projeto (mesma projectKey sempre produz o mesmo
/// resultado), simulando uma leitura pontual do Sonar sem depender de um servidor real.
/// </summary>
public class MockSonarProvider : ISonarProvider
{
    private static readonly string[] Ratings = ["A", "B", "C", "D", "E"];

    public Task<QualityMetrics> GetCurrentMetricsAsync(string projectKey, CancellationToken cancellationToken = default)
    {
        var random = new Random(StableHash(projectKey));

        var metrics = new QualityMetrics
        {
            Coverage = Math.Round(random.NextDouble() * 100, 1),
            Bugs = random.Next(0, 15),
            Vulnerabilities = random.Next(0, 5),
            CodeSmells = random.Next(0, 300),
            Duplications = Math.Round(random.NextDouble() * 30, 1),
            Reliability = Ratings[random.Next(Ratings.Length)],
            Maintainability = Ratings[random.Next(Ratings.Length)],
            Security = Ratings[random.Next(Ratings.Length)]
        };

        return Task.FromResult(metrics);
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            var hash = 17;
            foreach (var c in value)
            {
                hash = hash * 31 + c;
            }
            return hash;
        }
    }
}
