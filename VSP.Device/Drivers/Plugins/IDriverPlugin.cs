namespace VSP.Device.Drivers.Plugins;

public interface IDriverPlugin
{
    string PluginId { get; }

    string DisplayName { get; }

    IReadOnlyList<DriverDescriptor> GetDriverDescriptors();
}
