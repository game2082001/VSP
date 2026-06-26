namespace VSP.Domain.Entities;

public class Building
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public List<Floor> Floors { get; set; } = new();
}