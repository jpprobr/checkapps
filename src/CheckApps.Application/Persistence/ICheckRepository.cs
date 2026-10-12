using CheckApps.Application.Checks;
using CheckApps.Domain.Checks;

namespace CheckApps.Application.Persistence;

/// <summary>
/// Persistência de checagens (spec 003). Toda mudança registrada no histórico recebe o
/// <see cref="CheckEvent"/> pronto do serviço e o grava na mesma transação — o
/// repositório não decide o que vira evento, só garante a atomicidade.
/// </summary>
public interface ICheckRepository
{
    /// <summary>Insert simples: false se já existir uma checagem com esse id.</summary>
    Task<bool> TryCreateAsync(Check check, CancellationToken cancellationToken = default);

    /// <summary>Grava só os metadados (título, descrição, categoria, autor, datas). Status muda por <see cref="SetStatusAsync"/>.</summary>
    Task SaveAsync(Check check, CancellationToken cancellationToken = default);

    Task<Check?> GetAsync(string checkId, CancellationToken cancellationToken = default);

    /// <summary>Mais recentes primeiro; só abertas, a menos que <paramref name="includeClosed"/>.</summary>
    Task<IReadOnlyList<CheckSummary>> ListAsync(bool includeClosed, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CheckItemView>> GetItemsAsync(string checkId, CancellationToken cancellationToken = default);

    Task<CheckItem?> GetItemAsync(string checkId, string appId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AppCheckEntry>> ListByAppAsync(string appId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CheckCandidateApp>> ListCandidateAppsAsync(string checkId, CancellationToken cancellationToken = default);

    /// <summary>Idempotente: se o app já está na checagem, não insere nem grava o evento, e devolve false.</summary>
    Task<bool> AddItemAsync(CheckItem item, CheckEvent added, CancellationToken cancellationToken = default);

    Task RemoveItemAsync(string checkId, string appId, CheckEvent removed, CancellationToken cancellationToken = default);

    /// <summary>Grava status e observação; o evento só vem quando o status mudou.</summary>
    Task UpdateItemAsync(CheckItem item, CheckEvent? statusChanged, CancellationToken cancellationToken = default);

    /// <summary>Grava status e closed_at da checagem, com o evento de fechar ou reabrir.</summary>
    Task SetStatusAsync(Check check, CheckEvent changed, CancellationToken cancellationToken = default);

    /// <summary>Apaga a checagem e o histórico dela. Não usar se houver itens — a FK recusa.</summary>
    Task DeleteAsync(string checkId, CancellationToken cancellationToken = default);

    Task<bool> IsAppInAnyCheckAsync(string appId, CancellationToken cancellationToken = default);

    /// <summary>Mais recente primeiro.</summary>
    Task<IReadOnlyList<CheckEvent>> GetHistoryAsync(string checkId, CancellationToken cancellationToken = default);
}
