namespace CheckApps.Domain.Checks;

/// <summary>Um app dentro de uma checagem. Identidade composta (CheckId, AppId).</summary>
public class CheckItem
{
    public required string CheckId { get; init; }
    public required string AppId { get; init; }
    public CheckItemStatus Status { get; set; } = CheckItemStatus.Pendente;
    public string? Notes { get; set; }
    public required DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// Enum, e não string livre como o status de Squad/App, porque o percentual da
/// checagem depende de saber quais valores contam como concluídos. Na API e no banco
/// circula pelo código de <see cref="CheckCodes"/> (ex.: "em-andamento").
/// </summary>
public enum CheckItemStatus
{
    Pendente,
    EmAndamento,
    Conforme,
    NaoSeAplica,
    Concluido
}
