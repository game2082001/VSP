namespace VSP.Domain.Entities;

public class Camera
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string StreamUrl { get; set; } = string.Empty;
}