namespace CheckApps.Domain;

public class AppLink
{
    public required string Id { get; init; }
    public required string AppId { get; init; }
    public required string Description { get; set; }
    public required string Uri { get; set; }
}
