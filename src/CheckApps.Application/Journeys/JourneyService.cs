using CheckApps.Application.Analysis;
using CheckApps.Application.Persistence;
using CheckApps.Domain;
using CheckApps.Domain.Analysis;

namespace CheckApps.Application.Journeys;

/// <summary>
/// Jornadas em árvore (spec 022, ADR-012): cadastro, passos, resolução dos passos contra o contrato corrente e impacto das mudanças
/// de contrato. A estrutura é montada em memória. Erros pelo padrão do projeto: entrada inválida → <see cref="ArgumentException"/>
/// (400); a jornada da URL inexistente → <see cref="KeyNotFoundException"/> (404); regra de estrutura → <see cref="DomainConflictException"/> (409).
/// </summary>
public class JourneyService(
    IJourneyRepository journeys, ISquadRepository squads, IAppRepository apps, IContractReader contracts, TimeProvider timeProvider)
{
    private static readonly HashSet<string> HttpMethods = new(StringComparer.Ordinal)
    {
        "GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS", "TRACE"
    };

    private const int MaxTags = 20;
    private const int MaxTagLength = 60;

    // ---- Leitura -------------------------------------------------------------------------------------------------------

    public async Task<IReadOnlyList<JourneySummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        var all = await journeys.ListAsync(cancellationToken);
        var stepCounts = (await journeys.ListAllStepsAsync(cancellationToken)).GroupBy(s => s.JourneyId).ToDictionary(g => g.Key, g => g.Count());
        var squadNames = await SquadNamesAsync(cancellationToken);

        return all
            .OrderBy(j => j.Name, StringComparer.OrdinalIgnoreCase)
            .Select(j => Summarize(j, all, stepCounts, squadNames))
            .ToList();
    }

    /// <exception cref="KeyNotFoundException">A jornada não existe.</exception>
    public async Task<JourneyDetail> GetDetailAsync(string journeyId, CancellationToken cancellationToken = default)
    {
        var journey = await GetRequiredAsync(journeyId, cancellationToken);
        var all = await journeys.ListAsync(cancellationToken);
        var allSteps = await journeys.ListAllStepsAsync(cancellationToken);
        var stepCounts = allSteps.GroupBy(s => s.JourneyId).ToDictionary(g => g.Key, g => g.Count());
        var squadNames = await SquadNamesAsync(cancellationToken);

        var children = all
            .Where(j => j.ParentId == journeyId)
            .OrderBy(j => j.Name, StringComparer.OrdinalIgnoreCase)
            .Select(j => Summarize(j, all, stepCounts, squadNames))
            .ToList();

        var steps = allSteps.Where(s => s.JourneyId == journeyId).OrderBy(s => s.Position).ToList();
        var subtree = JourneyTree.DescendantIds(all, journeyId).Append(journeyId).ToHashSet(StringComparer.Ordinal);
        var subtreeSteps = allSteps.Where(s => subtree.Contains(s.JourneyId)).ToList();

        var contractsByApp = new Dictionary<string, AppContracts?>(StringComparer.Ordinal);
        foreach (var appId in subtreeSteps.Select(s => s.AppId).Distinct(StringComparer.Ordinal))
        {
            contractsByApp[appId] = await TryReadContractsAsync(appId, cancellationToken);
        }

        var appNames = await AppNamesAsync(subtreeSteps.Select(s => s.AppId), cancellationToken);
        var stepViews = steps.Select(step =>
        {
            var (resolution, endpoint) = JourneyResolver.Resolve(step, contractsByApp.GetValueOrDefault(step.AppId));
            return new JourneyStepView(step, appNames.GetValueOrDefault(step.AppId, step.AppId), resolution, endpoint);
        }).ToList();

        var derivedApps = subtreeSteps
            .GroupBy(s => s.AppId, StringComparer.Ordinal)
            .Select(g => new JourneyApp(g.Key, appNames.GetValueOrDefault(g.Key, g.Key), g.Count()))
            .OrderBy(a => a.AppName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new JourneyDetail(
            journey, journey.SquadId is { } squadId ? squadNames.GetValueOrDefault(squadId) : null,
            JourneyTree.PathTo(all, journey), children, stepViews, derivedApps);
    }

    /// <summary>O impacto das mudanças de contrato nos passos da jornada e da subárvore dela.</summary>
    /// <exception cref="KeyNotFoundException">A jornada não existe.</exception>
    public async Task<JourneyImpact> GetImpactAsync(string journeyId, CancellationToken cancellationToken = default)
    {
        await GetRequiredAsync(journeyId, cancellationToken);
        var all = await journeys.ListAsync(cancellationToken);
        var subtree = JourneyTree.DescendantIds(all, journeyId).Append(journeyId).ToHashSet(StringComparer.Ordinal);
        var names = all.ToDictionary(j => j.Id, j => j.Name, StringComparer.Ordinal);
        var steps = (await journeys.ListAllStepsAsync(cancellationToken)).Where(s => subtree.Contains(s.JourneyId)).ToList();

        var appIds = steps.Select(s => s.AppId).Distinct(StringComparer.Ordinal).ToList();
        var appNames = await AppNamesAsync(appIds, cancellationToken);
        var changesByApp = new Dictionary<string, AppChangeSet>(StringComparer.Ordinal);

        foreach (var appId in appIds)
        {
            try
            {
                changesByApp[appId] = AppChangeSet.From(appNames.GetValueOrDefault(appId, appId), await contracts.GetChangesForAppAsync(appId, cancellationToken));
            }
            catch (KeyNotFoundException)
            {
                // App que sumiu: o passo fica sem comparação, não derruba a leitura.
            }
        }

        return JourneyImpactCalculator.Compute(
            journeyId, steps.Select(s => new ImpactStepInput(s, names.GetValueOrDefault(s.JourneyId, s.JourneyId))).ToList(), changesByApp);
    }

    // ---- Cadastro ------------------------------------------------------------------------------------------------------

    /// <exception cref="ArgumentException">Nome vazio, nível ou tag inválidos, squad ou pai que não existem.</exception>
    /// <exception cref="DomainConflictException">Pai com passos, nome repetido entre irmãos ou profundidade máxima.</exception>
    public async Task<Journey> CreateAsync(JourneyInput input, CancellationToken cancellationToken = default)
    {
        var fields = await ValidateAsync(input, cancellationToken);
        var all = await journeys.ListAsync(cancellationToken);

        if (fields.ParentId is not null)
        {
            await EnsureParentCanHostChildrenAsync(all, fields.ParentId, cancellationToken);
            EnsureDepth(JourneyTree.Depth(all, fields.ParentId) + 1);
        }

        EnsureNameFreeAmongSiblings(all, fields.Name, fields.ParentId, exceptId: null);

        var now = timeProvider.GetUtcNow();
        Journey? created = null;
        await SlugIdGenerator.GenerateUniqueAsync(fields.Name, async (candidateId, ct) =>
        {
            var journey = new Journey
            {
                Id = candidateId, Name = fields.Name, Description = fields.Description, Level = fields.Level, SquadId = fields.SquadId,
                ParentId = fields.ParentId, Tags = fields.Tags, Source = JourneySources.Manual, CreatedAt = now, UpdatedAt = now
            };

            if (!await journeys.TryCreateAsync(journey, ct))
            {
                return false;
            }

            created = journey;
            return true;
        }, cancellationToken);

        return created!;
    }

    /// <exception cref="KeyNotFoundException">A jornada não existe.</exception>
    public async Task<Journey> UpdateAsync(string journeyId, JourneyInput input, CancellationToken cancellationToken = default)
    {
        var existing = await GetRequiredAsync(journeyId, cancellationToken);
        var fields = await ValidateAsync(input, cancellationToken);
        var all = await journeys.ListAsync(cancellationToken);

        var parentChanged = !string.Equals(existing.ParentId, fields.ParentId, StringComparison.Ordinal);
        if (parentChanged && fields.ParentId is not null)
        {
            if (fields.ParentId == journeyId || JourneyTree.DescendantIds(all, journeyId).Contains(fields.ParentId))
            {
                throw new DomainConflictException($"A jornada '{existing.Name}' não pode ficar dentro dela mesma nem de uma jornada dela (isso formaria um ciclo).");
            }

            await EnsureParentCanHostChildrenAsync(all, fields.ParentId, cancellationToken);
            EnsureDepth(JourneyTree.Depth(all, fields.ParentId) + JourneyTree.Height(all, journeyId));
        }

        if (parentChanged || !string.Equals(existing.Name, fields.Name, StringComparison.Ordinal))
        {
            EnsureNameFreeAmongSiblings(all, fields.Name, fields.ParentId, exceptId: journeyId);
        }

        var updated = existing with
        {
            Name = fields.Name, Description = fields.Description, Level = fields.Level, SquadId = fields.SquadId,
            ParentId = fields.ParentId, Tags = fields.Tags, UpdatedAt = timeProvider.GetUtcNow()
        };

        await journeys.UpdateAsync(updated, cancellationToken);
        return updated;
    }

    /// <exception cref="KeyNotFoundException">A jornada não existe.</exception>
    /// <exception cref="DomainConflictException">A jornada tem filhos.</exception>
    public async Task DeleteAsync(string journeyId, CancellationToken cancellationToken = default)
    {
        var journey = await GetRequiredAsync(journeyId, cancellationToken);
        var children = (await journeys.ListAsync(cancellationToken)).Count(j => j.ParentId == journeyId);
        if (children > 0)
        {
            throw new DomainConflictException(
                $"A jornada '{journey.Name}' tem {children} jornada(s) dentro e não pode ser removida. Mova ou remova as de dentro primeiro.");
        }

        await journeys.DeleteAsync(journeyId, cancellationToken);
    }

    /// <summary>Substitui a lista inteira de passos, em ordem. Lista vazia remove todos.</summary>
    /// <exception cref="KeyNotFoundException">A jornada não existe.</exception>
    /// <exception cref="ArgumentException">App inexistente, método que não é HTTP, rota que não começa com <c>/</c> ou passos demais.</exception>
    /// <exception cref="DomainConflictException">A jornada tem filhos: só as jornadas-folha têm passos.</exception>
    public async Task ReplaceStepsAsync(string journeyId, IReadOnlyList<StepInput> inputs, CancellationToken cancellationToken = default)
    {
        var journey = await GetRequiredAsync(journeyId, cancellationToken);

        if (inputs.Count > JourneyLimits.MaxSteps)
        {
            throw new ArgumentException($"Uma jornada aceita no máximo {JourneyLimits.MaxSteps} passos.");
        }

        if (inputs.Count > 0 && (await journeys.ListAsync(cancellationToken)).Any(j => j.ParentId == journeyId))
        {
            throw new DomainConflictException(
                $"A jornada '{journey.Name}' tem jornadas dentro e não aceita passos: só as jornadas-folha têm passos.");
        }

        var knownApps = new HashSet<string>(StringComparer.Ordinal);
        var steps = new List<JourneyStep>();

        foreach (var (input, position) in inputs.Select((input, position) => (input, position)))
        {
            var appId = input.AppId?.Trim();
            if (string.IsNullOrEmpty(appId))
            {
                throw new ArgumentException($"Passo {position + 1}: informe o app.");
            }

            if (!knownApps.Contains(appId))
            {
                if (await apps.GetAsync(appId, cancellationToken) is null)
                {
                    throw new ArgumentException($"Passo {position + 1}: o app '{appId}' não foi encontrado.");
                }

                knownApps.Add(appId);
            }

            var method = input.Method?.Trim().ToUpperInvariant() ?? "";
            if (!HttpMethods.Contains(method))
            {
                throw new ArgumentException($"Passo {position + 1}: o método deve ser um método HTTP (GET, POST, PUT, PATCH, DELETE…).");
            }

            var path = input.Path?.Trim() ?? "";
            if (!path.StartsWith('/'))
            {
                throw new ArgumentException($"Passo {position + 1}: a rota deve começar com '/'.");
            }

            steps.Add(new JourneyStep
            {
                Id = Guid.NewGuid().ToString("n"), JourneyId = journeyId, Position = position, AppId = appId, Method = method, Path = path,
                PathKey = ApiPath.KeyOf(path), Label = Limit(input.Label, JourneyLimits.MaxLabelLength, "rótulo", position),
                Note = Limit(input.Note, JourneyLimits.MaxNoteLength, "nota", position)
            });
        }

        await journeys.ReplaceStepsAsync(journeyId, steps, cancellationToken);
    }

    // ---- Regras --------------------------------------------------------------------------------------------------------

    private sealed record ValidFields(string Name, string? Description, string? Level, string? SquadId, string? ParentId, IReadOnlyList<string> Tags);

    private async Task<ValidFields> ValidateAsync(JourneyInput input, CancellationToken cancellationToken)
    {
        var name = input.Name?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Informe o nome da jornada.");
        }

        if (name.Length > JourneyLimits.MaxNameLength)
        {
            throw new ArgumentException($"O nome da jornada aceita até {JourneyLimits.MaxNameLength} caracteres.");
        }

        var level = string.IsNullOrWhiteSpace(input.Level) ? null : input.Level.Trim();
        if (level is { Length: > JourneyLimits.MaxLevelLength })
        {
            throw new ArgumentException($"O nível aceita até {JourneyLimits.MaxLevelLength} caracteres.");
        }

        var squadId = string.IsNullOrWhiteSpace(input.SquadId) ? null : input.SquadId.Trim();
        if (squadId is not null && await squads.GetAsync(squadId, cancellationToken) is null)
        {
            throw new ArgumentException($"A squad '{squadId}' não foi encontrada.");
        }

        var parentId = string.IsNullOrWhiteSpace(input.ParentId) ? null : input.ParentId.Trim();
        if (parentId is not null && await journeys.GetAsync(parentId, cancellationToken) is null)
        {
            throw new ArgumentException($"A jornada '{parentId}' (pai) não foi encontrada.");
        }

        return new ValidFields(
            name, string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim(), level, squadId, parentId, NormalizeTags(input.Tags));
    }

    private static IReadOnlyList<string> NormalizeTags(IReadOnlyCollection<string>? tags)
    {
        var normalized = (tags ?? [])
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Count > MaxTags)
        {
            throw new ArgumentException($"Uma jornada aceita no máximo {MaxTags} tags.");
        }

        if (normalized.Any(t => t.Length > MaxTagLength))
        {
            throw new ArgumentException($"Cada tag aceita até {MaxTagLength} caracteres.");
        }

        return normalized;
    }

    private async Task EnsureParentCanHostChildrenAsync(IReadOnlyList<Journey> all, string parentId, CancellationToken cancellationToken)
    {
        if ((await journeys.GetStepsAsync(parentId, cancellationToken)).Count > 0)
        {
            var name = all.FirstOrDefault(j => j.Id == parentId)?.Name ?? parentId;
            throw new DomainConflictException(
                $"A jornada '{name}' tem passos e não pode ter jornadas dentro: só as jornadas-folha têm passos. Remova os passos primeiro.");
        }
    }

    private static void EnsureDepth(int depth)
    {
        if (depth > JourneyLimits.MaxDepth)
        {
            throw new DomainConflictException($"A árvore de jornadas aceita no máximo {JourneyLimits.MaxDepth} níveis.");
        }
    }

    private static void EnsureNameFreeAmongSiblings(IReadOnlyList<Journey> all, string name, string? parentId, string? exceptId)
    {
        var clash = all.FirstOrDefault(j =>
            j.Id != exceptId && string.Equals(j.ParentId, parentId, StringComparison.Ordinal)
            && string.Equals(j.Name, name, StringComparison.OrdinalIgnoreCase));

        if (clash is not null)
        {
            throw new DomainConflictException($"Já existe uma jornada chamada '{clash.Name}' no mesmo lugar da árvore.");
        }
    }

    private static string? Limit(string? value, int max, string field, int position)
    {
        var trimmed = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (trimmed is not null && trimmed.Length > max)
        {
            throw new ArgumentException($"Passo {position + 1}: o {field} aceita até {max} caracteres.");
        }

        return trimmed;
    }

    // ---- Apoio ---------------------------------------------------------------------------------------------------------

    private async Task<Journey> GetRequiredAsync(string journeyId, CancellationToken cancellationToken) =>
        await journeys.GetAsync(journeyId, cancellationToken) ?? throw new KeyNotFoundException($"Jornada '{journeyId}' não encontrada.");

    private static JourneySummary Summarize(
        Journey journey, IReadOnlyList<Journey> all, IReadOnlyDictionary<string, int> stepCounts, IReadOnlyDictionary<string, string> squadNames) =>
        new(
            journey, journey.SquadId is { } squadId ? squadNames.GetValueOrDefault(squadId) : null,
            stepCounts.GetValueOrDefault(journey.Id), all.Count(j => j.ParentId == journey.Id));

    private async Task<Dictionary<string, string>> SquadNamesAsync(CancellationToken cancellationToken) =>
        (await squads.ListAsync(cancellationToken)).ToDictionary(s => s.Id, s => s.Name, StringComparer.Ordinal);

    private async Task<Dictionary<string, string>> AppNamesAsync(IEnumerable<string> appIds, CancellationToken cancellationToken)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var appId in appIds.Distinct(StringComparer.Ordinal))
        {
            if (await apps.GetAsync(appId, cancellationToken) is { } app)
            {
                names[appId] = app.Name;
            }
        }

        return names;
    }

    private async Task<AppContracts?> TryReadContractsAsync(string appId, CancellationToken cancellationToken)
    {
        try
        {
            return await contracts.GetForAppAsync(appId, cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }
}
