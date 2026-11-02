using CheckApps.Application.Persistence;
using CheckApps.Domain.Analysis;

namespace CheckApps.Application.Analysis;

/// <summary>
/// Vínculos host→app (spec 017), sempre por decisão de uma pessoa. Grava a decisão (<c>confirmado</c> ou
/// <c>rejeitado</c>), remove e lista; o vínculo efetivo de cada host é resolvido pelo <see cref="HostLinkResolver"/>.
/// Não dispara análise e não adivinha: o estado <c>sugerido</c> existe no domínio, mas nada o produz aqui.
/// </summary>
public class HostLinkService(
    IAppRepository apps, IHostLinkRepository links, IAnalysisRepository analyses, AnalysisOptions options, TimeProvider clock)
{
    public const int MaxNoteLength = 500;

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

/// <param name="Seen">Alguma integração das análises correntes casa com este vínculo; <c>false</c> = "não visto na última análise".</param>
public record HostLinkListItem(
    string Id, string HostKey, bool IsTemplate, string AppId, string AppName, string State, string Origin,
    DateTimeOffset DecidedAt, string? Note, bool Seen);
