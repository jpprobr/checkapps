namespace CheckApps.Domain;

public class SonarProject
{
    public required string Id { get; init; }
    public required string AppId { get; init; }
    public required string ProjectKey { get; set; }
    public string? Name { get; set; }
    public string? Url { get; set; }
    public string? ExternalId { get; set; }
    public string? Status { get; set; }
    public required string Source { get; set; }
}
