using CheckApps.Application.Persistence;
using CheckApps.Domain.Checks;

namespace CheckApps.Application.Checks;

/// <summary>
/// Regras das checagens (spec 003). Exceções seguem a convenção dos outros serviços:
/// <see cref="KeyNotFoundException"/> para a checagem ou o item endereçado (404),
/// <see cref="ArgumentException"/> para input inválido do cliente — título vazio, app
/// inexistente na lista a adicionar (400) — e <see cref="DomainConflictException"/> (409).
/// </summary>
public class CheckService(ICheckRepository checks, IAppRepository apps, TimeProvider timeProvider)
{
    public Task<IReadOnlyList<CheckSummary>> ListAsync(bool includeClosed, CancellationToken cancellationToken = default) =>
        checks.ListAsync(includeClosed, cancellationToken);

    public async Task<CheckDetails?> GetAsync(string checkId, CancellationToken cancellationToken = default)
    {
        var check = await checks.GetAsync(checkId, cancellationToken);
        if (check is null)
        {
            return null;
        }

        var items = await checks.GetItemsAsync(checkId, cancellationToken);
        return new CheckDetails(check, items, CheckProgress.From(items.Select(i => i.Status)));
    }

    public Task<IReadOnlyList<AppCheckEntry>> ListByAppAsync(string appId, CancellationToken cancellationToken = default) =>
        checks.ListByAppAsync(appId, cancellationToken);

    public async Task<IReadOnlyList<CheckCandidateApp>> ListCandidatesAsync(string checkId, CancellationToken cancellationToken = default)
    {
        await GetCheckOrThrowAsync(checkId, cancellationToken);
        return await checks.ListCandidateAppsAsync(checkId, cancellationToken);
    }

    public async Task<IReadOnlyList<CheckEvent>> GetHistoryAsync(string checkId, CancellationToken cancellationToken = default)
    {
        await GetCheckOrThrowAsync(checkId, cancellationToken);
        return await checks.GetHistoryAsync(checkId, cancellationToken);
    }

    public async Task<Check> CreateAsync(CheckFields fields, CancellationToken cancellationToken = default)
    {
        Validate(fields);
        var createdAt = timeProvider.GetUtcNow();
        Check? created = null;

        await SlugIdGenerator.GenerateUniqueAsync(fields.Title, async (candidateId, ct) =>
        {
            var check = new Check { Id = candidateId, Title = fields.Title, CreatedAt = createdAt };
            Apply(check, fields);

            if (!await checks.TryCreateAsync(check, ct))
            {
                return false;
            }

            created = check;
            return true;
        }, cancellationToken);

        return created!;
    }

    public async Task<Check> UpdateAsync(string checkId, CheckFields fields, CancellationToken cancellationToken = default)
    {
        Validate(fields);
        var check = await GetCheckOrThrowAsync(checkId, cancellationToken);

        Apply(check, fields);
        await checks.SaveAsync(check, cancellationToken);
        return check;
    }

    public async Task<Check> CloseAsync(string checkId, CancellationToken cancellationToken = default)
    {
        var check = await GetCheckOrThrowAsync(checkId, cancellationToken);
        if (check.Status == CheckStatus.Closed)
        {
            throw new DomainConflictException($"A checagem '{checkId}' já está fechada.");
        }

        var now = timeProvider.GetUtcNow();
        check.Status = CheckStatus.Closed;
        check.ClosedAt = now;
        await checks.SetStatusAsync(check, NewEvent(checkId, CheckEventType.Closed, now), cancellationToken);
        return check;
    }

    public async Task<Check> ReopenAsync(string checkId, CancellationToken cancellationToken = default)
    {
        var check = await GetCheckOrThrowAsync(checkId, cancellationToken);
        if (check.Status == CheckStatus.Open)
        {
            throw new DomainConflictException($"A checagem '{checkId}' já está aberta.");
        }

        check.Status = CheckStatus.Open;
        check.ClosedAt = null;
        await checks.SetStatusAsync(check, NewEvent(checkId, CheckEventType.Reopened, timeProvider.GetUtcNow()), cancellationToken);
        return check;
    }

    /// <summary>Só sem itens (FR-012). Com itens, a checagem é fechada, não removida — espelha o ADR-005.</summary>
    public async Task DeleteAsync(string checkId, CancellationToken cancellationToken = default)
    {
        await GetCheckOrThrowAsync(checkId, cancellationToken);

        if ((await checks.GetItemsAsync(checkId, cancellationToken)).Count > 0)
        {
            throw new DomainConflictException(
                $"A checagem '{checkId}' tem apps e não pode ser removida. Feche a checagem em vez de removê-la.");
        }

        await checks.DeleteAsync(checkId, cancellationToken);
    }

    /// <summary>Valida todos os apps antes de gravar qualquer um. Apps já presentes são ignorados; devolve quantos entraram.</summary>
    public async Task<int> AddAppsAsync(string checkId, IReadOnlyCollection<string> appIds, CancellationToken cancellationToken = default)
    {
        await GetCheckOrThrowAsync(checkId, cancellationToken);

        var distinctIds = appIds.Distinct().ToList();
        foreach (var appId in distinctIds)
        {
            if (await apps.GetAsync(appId, cancellationToken) is null)
            {
                throw new ArgumentException($"App '{appId}' não encontrado.");
            }
        }

        var added = 0;
        foreach (var appId in distinctIds)
        {
            var now = timeProvider.GetUtcNow();
            var item = new CheckItem { CheckId = checkId, AppId = appId, Status = CheckItemStatus.Pendente, UpdatedAt = now };
            var addedEvent = NewEvent(checkId, CheckEventType.ItemAdded, now, appId, to: CheckItemStatus.Pendente);

            if (await checks.AddItemAsync(item, addedEvent, cancellationToken))
            {
                added++;
            }
        }

        return added;
    }

    public async Task RemoveAppAsync(string checkId, string appId, CancellationToken cancellationToken = default)
    {
        var item = await GetItemOrThrowAsync(checkId, appId, cancellationToken);

        await checks.RemoveItemAsync(checkId, appId,
            NewEvent(checkId, CheckEventType.ItemRemoved, timeProvider.GetUtcNow(), appId, from: item.Status),
            cancellationToken);
    }

    /// <summary>Grava status e observação; só a mudança de status vai para o histórico (FR-015).</summary>
    public async Task<CheckItem> UpdateItemAsync(
        string checkId, string appId, CheckItemStatus status, string? notes, CancellationToken cancellationToken = default)
    {
        var item = await GetItemOrThrowAsync(checkId, appId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var previousStatus = item.Status;

        item.Status = status;
        item.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes;
        item.UpdatedAt = now;

        var statusChanged = previousStatus == status
            ? null
            : NewEvent(checkId, CheckEventType.ItemStatusChanged, now, appId, previousStatus, status);

        await checks.UpdateItemAsync(item, statusChanged, cancellationToken);
        return item;
    }

    private async Task<Check> GetCheckOrThrowAsync(string checkId, CancellationToken cancellationToken) =>
        await checks.GetAsync(checkId, cancellationToken)
            ?? throw new KeyNotFoundException($"Checagem '{checkId}' não encontrada.");

    private async Task<CheckItem> GetItemOrThrowAsync(string checkId, string appId, CancellationToken cancellationToken)
    {
        await GetCheckOrThrowAsync(checkId, cancellationToken);
        return await checks.GetItemAsync(checkId, appId, cancellationToken)
            ?? throw new KeyNotFoundException($"O app '{appId}' não está na checagem '{checkId}'.");
    }

    private static void Validate(CheckFields fields)
    {
        if (string.IsNullOrWhiteSpace(fields.Title))
        {
            throw new ArgumentException("O título é obrigatório.");
        }

        if (fields.StartDate is not null && fields.EndDate is not null && fields.EndDate < fields.StartDate)
        {
            throw new ArgumentException("A data de fim não pode ser anterior à de início.");
        }
    }

    private static void Apply(Check check, CheckFields fields)
    {
        check.Title = fields.Title.Trim();
        check.Description = fields.Description;
        check.Category = fields.Category;
        check.Author = fields.Author;
        check.StartDate = fields.StartDate;
        check.EndDate = fields.EndDate;
    }

    private static CheckEvent NewEvent(
        string checkId, CheckEventType type, DateTimeOffset at, string? appId = null,
        CheckItemStatus? from = null, CheckItemStatus? to = null) => new()
    {
        Id = Guid.NewGuid().ToString("n"),
        CheckId = checkId,
        AppId = appId,
        Type = type,
        FromStatus = from,
        ToStatus = to,
        OccurredAt = at
    };
}

/// <summary>Os metadados editáveis de uma checagem (FR-001/FR-002).</summary>
public record CheckFields(
    string Title, string? Description, string? Category, string? Author, DateOnly? StartDate, DateOnly? EndDate);

/// <summary>Uma checagem aberta na tela: metadados, itens e percentual (AP-2 + AP-6).</summary>
public record CheckDetails(Check Check, IReadOnlyList<CheckItemView> Items, CheckProgress Progress);
