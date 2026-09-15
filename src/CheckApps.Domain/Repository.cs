namespace CheckApps.Domain;

public class Repository
{
    public required string Id { get; init; }
    public required string AppId { get; init; }
    public required string Name { get; set; }
    public string? Provider { get; set; }
    public string? Url { get; set; }
    public string? ExternalId { get; set; }
    public string? Status { get; set; }
}
