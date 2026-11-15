using CheckApps.Application.Analysis;
using CheckApps.Domain;
using CheckApps.Domain.Analysis;

namespace CheckApps.Application.Journeys;

/// <summary>O que mudou nos contratos de um app, já reunido de todos os repositórios dele.</summary>
/// <param name="Comparison">
/// <c>comparado</c> se algum repositório tem uma análise anterior para comparar; senão <c>primeira-analise</c> ou <c>sem-contrato</c>.
/// </param>
public sealed record AppChangeSet(string AppName, string Comparison, IReadOnlyList<ContractChange> Changes, IReadOnlyList<string> Warnings)
{
    /// <summary>Reúne as mudanças de todos os repositórios de contrato do app.</summary>
    public static AppChangeSet From(string appName, AppContractChanges changes)
    {
        var comparison = changes.Repositories.Any(r => r.State == ContractChangeStates.Compared)
            ? ContractChangeStates.Compared
            : changes.Repositories.Any(r => r.State == ContractChangeStates.FirstAnalysis)
                ? ContractChangeStates.FirstAnalysis
                : ContractChangeStates.NoContract;

        return new AppChangeSet(
            appName, comparison, changes.Repositories.SelectMany(r => r.Changes).ToList(),
            changes.Repositories.SelectMany(r => r.Warnings).Distinct(StringComparer.Ordinal).ToList());
    }
}

public sealed record ImpactStepInput(JourneyStep Step, string JourneyName);

/// <param name="Comparison">Um de <see cref="ContractChangeStates"/>: se há ou não com o que comparar. "Sem comparação" não é "sem impacto".</param>
/// <param name="EndpointRemoved">O endpoint saiu do contrato.</param>
public sealed record JourneyImpactStep(
    JourneyStep Step, string JourneyName, string AppName, string Comparison, bool EndpointRemoved, IReadOnlyList<ContractChange> Changes);

/// <param name="Breaking">Quantas mudanças que quebram atingem os passos (cada mudança conta uma vez por passo).</param>
/// <param name="BreakingSteps">Quantos passos têm pelo menos uma quebra.</param>
/// <param name="StepsWithoutComparison">Passos de apps sem análise anterior: o impacto deles é desconhecido.</param>
public sealed record JourneyImpact(
    string JourneyId, int Breaking, int Attention, int Safe, int BreakingSteps, int StepsWithoutComparison,
    IReadOnlyList<JourneyImpactStep> Steps, IReadOnlyList<string> Warnings);

/// <summary>
/// Cruza os passos de uma jornada (e da subárvore dela) com as mudanças de contrato da spec 021, por (método, chave da rota). Função
/// pura, calculada na leitura e nunca gravada: uma regra melhor vale para o histórico sem migração.
/// </summary>
public static class JourneyImpactCalculator
{
    public static JourneyImpact Compute(
        string journeyId, IReadOnlyList<ImpactStepInput> steps, IReadOnlyDictionary<string, AppChangeSet> changesByApp)
    {
        var impactSteps = new List<JourneyImpactStep>();
        var warnings = new List<string>();

        foreach (var (step, journeyName) in steps.Select(s => (s.Step, s.JourneyName)))
        {
            if (!changesByApp.TryGetValue(step.AppId, out var set))
            {
                impactSteps.Add(new JourneyImpactStep(step, journeyName, step.AppId, ContractChangeStates.NoContract, false, []));
                continue;
            }

            var matching = set.Changes
                .Where(c => string.Equals(c.Method, step.Method, StringComparison.Ordinal) && ApiPath.KeyOf(c.Path) == step.PathKey)
                .ToList();

            warnings.AddRange(set.Warnings);
            impactSteps.Add(new JourneyImpactStep(
                step, journeyName, set.AppName, set.Comparison,
                matching.Any(c => c.Kind == ContractChangeKinds.EndpointRemoved), matching));
        }

        var all = impactSteps.SelectMany(s => s.Changes).ToList();
        return new JourneyImpact(
            journeyId,
            all.Count(c => c.Severity == ContractChangeSeverities.Breaking),
            all.Count(c => c.Severity == ContractChangeSeverities.Attention),
            all.Count(c => c.Severity == ContractChangeSeverities.Safe),
            impactSteps.Count(s => s.Changes.Any(c => c.Severity == ContractChangeSeverities.Breaking)),
            impactSteps.Count(s => s.Comparison != ContractChangeStates.Compared),
            impactSteps.OrderBy(s => s.Changes.Count == 0).ThenBy(s => s.JourneyName, StringComparer.Ordinal).ThenBy(s => s.Step.Position).ToList(),
            warnings.Distinct(StringComparer.Ordinal).ToList());
    }
}
