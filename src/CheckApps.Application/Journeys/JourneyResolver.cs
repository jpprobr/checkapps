using CheckApps.Application.Analysis;
using CheckApps.Domain;

namespace CheckApps.Application.Journeys;

/// <summary>
/// Resolve um passo contra o contrato **corrente** do app (spec 022). Função pura. O casamento é por método e chave da rota, a
/// mesma da comparação de contratos: renomear <c>{id}</c> para <c>{fundoId}</c> no contrato não quebra o passo.
/// </summary>
public static class JourneyResolver
{
    /// <param name="contracts">Os contratos correntes do app; <c>null</c> se não puderam ser lidos.</param>
    public static (string Resolution, ResolvedEndpoint? Endpoint) Resolve(JourneyStep step, AppContracts? contracts)
    {
        var withEndpoints = contracts?.Repositories.Where(r => r.Endpoints.Count > 0).ToList() ?? [];
        if (withEndpoints.Count == 0)
        {
            return (StepResolutions.AppWithoutContract, null);
        }

        foreach (var repository in withEndpoints)
        {
            var endpoint = repository.Endpoints.FirstOrDefault(e =>
                string.Equals(e.Method, step.Method, StringComparison.Ordinal) && string.Equals(e.PathKey, step.PathKey, StringComparison.Ordinal));

            if (endpoint is not null)
            {
                return (StepResolutions.Cataloged, new ResolvedEndpoint(
                    endpoint.Summary, endpoint.Security, endpoint.Deprecated, endpoint.Confidence, repository.Repository.Name, endpoint.SourceFile));
            }
        }

        return (StepResolutions.OutsideContract, null);
    }
}
