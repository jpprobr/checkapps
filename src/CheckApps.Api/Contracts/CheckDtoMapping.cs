using CheckApps.Application.Checks;
using CheckApps.Domain.Checks;

namespace CheckApps.Api.Contracts;

public static class CheckDtoMapping
{
    public static CheckProgressDto ToDto(this CheckProgress progress) => new(
        progress.Total, progress.Done, progress.Percent,
        progress.CountsByStatus.ToDictionary(c => c.Key.ToCode(), c => c.Value));

    public static CheckSummaryDto ToDto(this CheckSummary summary) => new(
        summary.Check.Id, summary.Check.Title, summary.Check.Category, summary.Check.Author,
        summary.Check.StartDate, summary.Check.EndDate, summary.Check.Status.ToCode(),
        summary.Check.CreatedAt, summary.Check.ClosedAt, summary.Progress.ToDto());

    public static CheckDetailDto ToDto(this CheckDetails details) => new(
        details.Check.Id, details.Check.Title, details.Check.Description, details.Check.Category, details.Check.Author,
        details.Check.StartDate, details.Check.EndDate, details.Check.Status.ToCode(),
        details.Check.CreatedAt, details.Check.ClosedAt, details.Progress.ToDto(),
        details.Items.Select(i => i.ToDto()).ToList());

    public static CheckItemDto ToDto(this CheckItemView item) => new(
        item.AppId, item.AppName, item.SquadId, item.SquadName, item.AppStatus,
        item.Status.ToCode(), item.Notes, item.UpdatedAt);

    public static AppCheckDto ToDto(this AppCheckEntry entry) => new(
        entry.CheckId, entry.Title, entry.CheckStatus.ToCode(), entry.ClosedAt, entry.ItemStatus.ToCode(), entry.Notes);

    public static CheckCandidateDto ToDto(this CheckCandidateApp candidate) => new(
        candidate.AppId, candidate.Name, candidate.SquadId, candidate.SquadName, candidate.Status, candidate.Tags);

    public static CheckEventDto ToDto(this CheckEvent checkEvent) => new(
        checkEvent.Id, checkEvent.AppId, checkEvent.Type.ToCode(),
        checkEvent.FromStatus?.ToCode(), checkEvent.ToStatus?.ToCode(), checkEvent.OccurredAt);

    public static CheckFields ToFields(this UpsertCheckRequest request) => new(
        request.Title, Blank(request.Description), Blank(request.Category), Blank(request.Author),
        request.StartDate, request.EndDate);

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
