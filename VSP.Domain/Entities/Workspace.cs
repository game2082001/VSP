namespace VSP.Domain.Entities;

public class Workspace
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public List<Site> Sites { get; set; } = new();
}