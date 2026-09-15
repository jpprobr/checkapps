namespace CheckApps.Domain;

public class Squad
{
    public required string Id { get; init; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? Status { get; set; }
}
