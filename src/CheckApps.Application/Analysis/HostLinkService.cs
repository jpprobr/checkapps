using CheckApps.Application.Persistence;
using CheckApps.Domain.Analysis;

namespace CheckApps.Application.Analysis;

/// <summary>
/// Vínculos host→app (spec 017), sempre por decisão de uma pessoa. Grava a decisão (<c>confirmado</c> ou
/// <c>rejeitado</c>), remove e lista; o vínculo efetivo de cada host é resolvido pelo <see cref="HostLinkResolver"/>.
/// Não dispara análise e não adivinha: o estado <c>sugerido</c> existe no domínio, mas nada o produz aqui. Também marca
/// hosts como <b>externos</b> ao catálogo (spec 019) e aplica a mesma decisão a vários hosts de uma vez.
/// </summary>
public class HostLinkService(
    IAppRepository apps, IHostLinkRepository links, IHostExternalRepository externals, IAnalysisRepository analyses,
    AnalysisOptions options, TimeProvider clock)
{
    public const int MaxNoteLength = 500;
    public const int MaxNameLength = 200;
    public const int MaxBatchSize = 200;

    /// <summary>A decisão do lote que marca o host como externo ao catálogo (não é um estado de <see cref="HostLinkStates"/>).</summary>
    public const string ExternalDecision = LinkStatuses.External;

    /// <summary>Cria o vínculo ou troca o estado do par (host, app).</summary>
    /// <exception cref="ArgumentException">Chave de host ou estado inválido, ou nota longa demais.</exception>
    /// <exception cref="KeyNotFoundException">O app não existe.</exception>
    /// <exception cref="DomainConflictException">O host já está confirmado para outro app.</exception>
    public async Task<HostLinkMatch> SetAsync(
        string? hostKey, string? appId, string? state, string? note, CancellationToken cancellationToken = default)
    {
        var key = HostKeyMatcher.Normalize(hostKey);

        if (state is null || !HostLinkStates.Decidable.Contains(state))
        {
            throw new ArgumentException(
                $"Estado inválido: \"{state}\". Use {string.Join(" ou ", HostLinkStates.Decidable)}.");
        }

        var cleanNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (cleanNote is { Length: > MaxNoteLength })
        {
            throw new ArgumentException($"A nota passa de {MaxNoteLength} caracteres.");
        }

        var app = string.IsNullOrWhiteSpace(appId) ? null : await apps.GetAsync(appId, cancellationToken);
        if (app is null)
        {
            throw new KeyNotFoundException($"App '{appId}' não encontrado.");
        }

        if (state == HostLinkStates.Confirmed)
        {
            var external = (await externals.ListAsync(cancellationToken)).FirstOrDefault(e => e.HostKey == key);
            if (external is not null)
            {
                throw new DomainConflictException(
                    $"O host '{key}' está marcado como externo{(external.Name is null ? "" : $" ('{external.Name}')")}. Remova a marca antes de confirmar um app.");
            }

            var other = (await links.ListAsync(cancellationToken))
                .FirstOrDefault(l => l.HostKey == key && l.State == HostLinkStates.Confirmed && l.AppId != app.Id);
            if (other is not null)
            {
                var otherName = (await apps.GetAsync(other.AppId, cancellationToken))?.Name ?? other.AppId;
                throw new DomainConflictException(
                    $"O host '{key}' já está confirmado para o app '{otherName}'. Remova esse vínculo antes de confirmar outro app.");
            }
        }

        var existing = await links.GetAsync(key, app.Id, cancellationToken);
        var link = new HostLink
        {
            Id = existing?.Id ?? Guid.NewGuid().ToString("n"),
            HostKey = key,
            AppId = app.Id,
            State = state,
            DecidedAt = clock.GetUtcNow(),
            Note = cleanNote
        };
        await links.UpsertAsync(link, cancellationToken);

        return new HostLinkMatch(link.Id, key, app.Id, app.Name, state, HostKeyMatcher.ViaExact, null, link.DecidedAt, cleanNote);
    }

    /// <exception cref="ArgumentException">Chave de host inválida.</exception>
    /// <exception cref="KeyNotFoundException">O vínculo não existe.</exception>
    public async Task RemoveAsync(string? hostKey, string appId, CancellationToken cancellationToken = default)
    {
        var key = HostKeyMatcher.Normalize(hostKey);
        if (!await links.DeleteAsync(key, appId, cancellationToken))
        {
            throw new KeyNotFoundException($"Não há vínculo do host '{key}' com o app '{appId}'.");
        }
    }

    /// <summary>Marca o host (ou o modelo <c>{env}</c>) como serviço externo ao catálogo; cria ou troca o nome e a nota.</summary>
    /// <exception cref="ArgumentException">Chave de host inválida, ou nome/nota longos demais.</exception>
    /// <exception cref="DomainConflictException">O host já está confirmado para um app.</exception>
    public async Task<HostExternalMatch> SetExternalAsync(
        string? hostKey, string? name, string? note, CancellationToken cancellationToken = default)
    {
        var key = HostKeyMatcher.Normalize(hostKey);
        var cleanName = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        var cleanNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (cleanName is { Length: > MaxNameLength })
        {
            throw new ArgumentException($"O nome passa de {MaxNameLength} caracteres.");
        }

        if (cleanNote is { Length: > MaxNoteLength })
        {
            throw new ArgumentException($"A nota passa de {MaxNoteLength} caracteres.");
        }

        var confirmed = (await links.ListAsync(cancellationToken))
            .FirstOrDefault(l => l.HostKey == key && l.State == HostLinkStates.Confirmed);
        if (confirmed is not null)
        {
            var appName = (await apps.GetAsync(confirmed.AppId, cancellationToken))?.Name ?? confirmed.AppId;
            throw new DomainConflictException(
                $"O host '{key}' já está confirmado para o app '{appName}'. Remova esse vínculo antes de marcá-lo como externo.");
        }

        var existing = (await externals.ListAsync(cancellationToken)).FirstOrDefault(e => e.HostKey == key);
        var external = new HostExternal
        {
            Id = existing?.Id ?? Guid.NewGuid().ToString("n"),
            HostKey = key,
            Name = cleanName,
            DecidedAt = clock.GetUtcNow(),
            Note = cleanNote
        };
        await externals.UpsertAsync(external, cancellationToken);

        return new HostExternalMatch(external.Id, key, cleanName, HostKeyMatcher.ViaExact, null, external.DecidedAt, cleanNote);
    }

    /// <exception cref="ArgumentException">Chave de host inválida.</exception>
    /// <exception cref="KeyNotFoundException">O host não está marcado como externo.</exception>
    public async Task RemoveExternalAsync(string? hostKey, CancellationToken cancellationToken = default)
    {
        var key = HostKeyMatcher.Normalize(hostKey);
        if (!await externals.DeleteAsync(key, cancellationToken))
        {
            throw new KeyNotFoundException($"O host '{key}' não está marcado como externo.");
        }
    }

    /// <summary>
    /// Aplica a mesma decisão a vários hosts (spec 019). Cada host segue as regras individuais; uma falha (409, chave
    /// inválida) não desfaz os outros e vem no resultado do host. A decisão em si inválida faz o lote inteiro falhar.
    /// </summary>
    /// <param name="state"><c>confirmado</c>, <c>rejeitado</c> ou <c>externo</c>; <paramref name="appId"/> é obrigatório salvo em <c>externo</c>.</param>
    /// <exception cref="ArgumentException">Lista vazia ou grande demais, ou decisão inválida.</exception>
    public async Task<IReadOnlyList<HostBatchResult>> ApplyBatchAsync(
        IReadOnlyList<string>? hostKeys, string? state, string? appId, string? name, string? note,
        CancellationToken cancellationToken = default)
    {
        if (hostKeys is null || hostKeys.Count == 0)
        {
            throw new ArgumentException("Informe ao menos um host.");
        }

        if (hostKeys.Count > MaxBatchSize)
        {
            throw new ArgumentException($"Um lote aceita no máximo {MaxBatchSize} hosts.");
        }

        var isExternal = state == ExternalDecision;
        if (!isExternal && (state is null || !HostLinkStates.Decidable.Contains(state)))
        {
            throw new ArgumentException(
                $"Estado inválido: \"{state}\". Use {string.Join(", ", [.. HostLinkStates.Decidable, ExternalDecision])}.");
        }

        if (!isExternal && string.IsNullOrWhiteSpace(appId))
        {
            throw new ArgumentException("Informe o app (appId) para confirmar ou rejeitar.");
        }

        var results = new List<HostBatchResult>();
        foreach (var hostKey in hostKeys.Select(k => k?.Trim() ?? "").Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (isExternal)
                {
                    await SetExternalAsync(hostKey, name, note, cancellationToken);
                }
                else
                {
                    await SetAsync(hostKey, appId, state, note, cancellationToken);
                }

                results.Add(new HostBatchResult(hostKey, true, null));
            }
            catch (Exception ex) when (ex is ArgumentException or KeyNotFoundException or DomainConflictException)
            {
                results.Add(new HostBatchResult(hostKey, false, ex.Message));
            }
        }

        return results;
    }

    /// <summary>Todos os vínculos, com o nome do app e se alguma integração corrente ainda os usa.</summary>
    public async Task<IReadOnlyList<HostLinkListItem>> ListAsync(CancellationToken cancellationToken = default)
    {
        var all = await links.ListAsync(cancellationToken);
        var names = (await apps.ListAllAsync(cancellationToken)).ToDictionary(a => a.Id, a => a.Name);
        var env = options.EffectiveEnvPlaceholderValues;

        var seenHosts = (await analyses.ListIntegrationRowsAsync(null, null, cancellationToken))
            .Select(r => IntegrationService.HostKey(r.Integration))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return all
            .Select(l => new HostLinkListItem(
                l.Id, l.HostKey, HostKeyMatcher.IsTemplate(l.HostKey), l.AppId, names.GetValueOrDefault(l.AppId, l.AppId),
                l.State, l.Origin, l.DecidedAt, l.Note,
                seenHosts.Any(h => HostKeyMatcher.Cover(l.HostKey, h, env) is not null)))
            .OrderBy(i => i.HostKey, StringComparer.Ordinal)
            .ThenBy(i => i.AppName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

/// <param name="Error">O texto do erro quando <paramref name="Ok"/> é <c>false</c>.</param>
public record HostBatchResult(string HostKey, bool Ok, string? Error);

/// <param name="Seen">Alguma integração das análises correntes casa com este vínculo; <c>false</c> = "não visto na última análise".</param>
public record HostLinkListItem(
    string Id, string HostKey, bool IsTemplate, string AppId, string AppName, string State, string Origin,
    DateTimeOffset DecidedAt, string? Note, bool Seen);
