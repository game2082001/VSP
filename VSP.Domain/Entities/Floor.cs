namespace VSP.Domain.Entities;

public class Floor
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public List<Camera> Cameras { get; set; } = new();
}