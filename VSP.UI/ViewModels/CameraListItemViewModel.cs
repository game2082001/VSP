using VSP.Core.MVVM;
using VSP.Domain.Entities;

namespace VSP.UI.ViewModels;

public class CameraListItemViewModel : ObservableObject
{
    private bool _isSelected;

    public Camera SourceCamera { get; init; } = new();
    public string Name { get; init; } = string.Empty;
    public string IpAddress { get; init; } = string.Empty;
    public string Brand { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public static CameraListItemViewModel FromCamera(Camera camera)
    {
        ArgumentNullException.ThrowIfNull(camera);

        return new CameraListItemViewModel
        {
            SourceCamera = camera,
            Name = camera.Name,
            IpAddress = camera.IpAddress,
            Brand = camera.Brand.ToString(),
            Status = camera.Status.ToString(),
            Location = camera.Location
        };
    }
}
