namespace CheckApps.Domain.Checks;

/// <summary>
/// Códigos textuais dos enums de checagem — o vocabulário do glossário. São o que
/// circula na API e o que o banco grava. Existem porque nomes C# não aceitam hífen
/// ("em-andamento", "item-added") e o JsonStringEnumConverter global geraria
/// "EmAndamento".
/// </summary>
public static class CheckCodes
{
    private static readonly Dictionary<CheckItemStatus, string> ItemStatusCodes = new()
    {
        [CheckItemStatus.Pendente] = "pendente",
        [CheckItemStatus.EmAndamento] = "em-andamento",
        [CheckItemStatus.Conforme] = "conforme",
        [CheckItemStatus.NaoSeAplica] = "nao-se-aplica",
        [CheckItemStatus.Concluido] = "concluido"
    };

    private static readonly Dictionary<CheckStatus, string> StatusCodes = new()
    {
        [CheckStatus.Open] = "open",
        [CheckStatus.Closed] = "closed"
    };

    private static readonly Dictionary<CheckEventType, string> EventTypeCodes = new()
    {
        [CheckEventType.Closed] = "closed",
        [CheckEventType.Reopened] = "reopened",
        [CheckEventType.ItemAdded] = "item-added",
        [CheckEventType.ItemRemoved] = "item-removed",
        [CheckEventType.ItemStatusChanged] = "item-status-changed"
    };

    public static string ToCode(this CheckItemStatus status) => ItemStatusCodes[status];

    public static string ToCode(this CheckStatus status) => StatusCodes[status];

    public static string ToCode(this CheckEventType type) => EventTypeCodes[type];

    public static CheckItemStatus ParseItemStatus(string code) => Parse(ItemStatusCodes, code, "status de item");

    public static CheckStatus ParseStatus(string code) => Parse(StatusCodes, code, "status de checagem");

    public static CheckEventType ParseEventType(string code) => Parse(EventTypeCodes, code, "tipo de evento");

    public static bool TryParseItemStatus(string? code, out CheckItemStatus status) =>
        TryParse(ItemStatusCodes, code, out status);

    private static T Parse<T>(Dictionary<T, string> codes, string code, string description) where T : struct, Enum =>
        TryParse(codes, code, out var value)
            ? value
            : throw new ArgumentException($"Código de {description} desconhecido: \"{code}\".", nameof(code));

    private static bool TryParse<T>(Dictionary<T, string> codes, string? code, out T value) where T : struct, Enum
    {
        foreach (var (candidate, candidateCode) in codes)
        {
            if (candidateCode == code)
            {
                value = candidate;
                return true;
            }
        }

        value = default;
        return false;
    }
}
