namespace VSP.Domain.Entities;

public class Site
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public List<Building> Buildings { get; set; } = new();
}