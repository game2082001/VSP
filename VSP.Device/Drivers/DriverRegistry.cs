using VSP.Device.Drivers.Abstractions;
using VSP.Device.Drivers.Plugins;
using VSP.Domain.Enums;

namespace VSP.Device.Drivers;

public sealed class DriverRegistry
{
    private readonly List<DriverDescriptor> _descriptors = [];
    private readonly Dictionary<string, DriverDescriptor> _descriptorsByDriverId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<DeviceConnectionType, DriverDescriptor> _descriptorsByConnectionType = [];

    public IReadOnlyList<DriverDescriptor> GetAll()
    {
        return _descriptors.AsReadOnly();
    }

    public void Register(DriverDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        EnsureNoRegistryConflict(descriptor);
        RegisterCore(descriptor);
    }

    public void RegisterPlugin(IDriverPlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);

        if (string.IsNullOrWhiteSpace(plugin.PluginId))
        {
            throw new ArgumentException("PluginId must not be empty.", nameof(plugin));
        }

        if (string.IsNullOrWhiteSpace(plugin.DisplayName))
        {
            throw new ArgumentException("DisplayName must not be empty.", nameof(plugin));
        }

        var descriptors = plugin.GetDriverDescriptors()
            ?? throw new InvalidOperationException($"Plugin '{plugin.PluginId}' returned null driver descriptors.");
        var descriptorList = descriptors.ToList();
        EnsurePluginRegistrationIsValid(plugin, descriptorList);

        foreach (var descriptor in descriptorList)
        {
            RegisterCore(descriptor);
        }
    }

    public DriverDescriptor? GetByDriverId(string driverId)
    {
        if (string.IsNullOrWhiteSpace(driverId))
        {
            return null;
        }

        return _descriptorsByDriverId.TryGetValue(driverId, out var descriptor)
            ? descriptor
            : null;
    }

    public DriverDescriptor? GetByConnectionType(DeviceConnectionType connectionType)
    {
        return _descriptorsByConnectionType.TryGetValue(connectionType, out var descriptor)
            ? descriptor
            : null;
    }

    public ICameraDriver? CreateCameraDriver(DeviceConnectionType connectionType)
    {
        return GetByConnectionType(connectionType)?.Factory();
    }

    public static DriverRegistry CreateDefault()
    {
        var registry = new DriverRegistry();
        registry.RegisterPlugin(new BuiltInCameraDriverPlugin());
        return registry;
    }

    private void EnsurePluginRegistrationIsValid(IDriverPlugin plugin, IReadOnlyList<DriverDescriptor> descriptors)
    {
        var seenDriverIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenConnectionTypes = new HashSet<DeviceConnectionType>();

        foreach (var descriptor in descriptors)
        {
            if (descriptor is null)
            {
                throw new InvalidOperationException($"Plugin '{plugin.PluginId}' contains a null driver descriptor.");
            }

            if (!seenDriverIds.Add(descriptor.DriverId))
            {
                throw new InvalidOperationException(
                    $"Plugin '{plugin.PluginId}' contains duplicate DriverId '{descriptor.DriverId}'.");
            }

            if (!seenConnectionTypes.Add(descriptor.ConnectionType))
            {
                throw new InvalidOperationException(
                    $"Plugin '{plugin.PluginId}' contains duplicate DeviceConnectionType '{descriptor.ConnectionType}'.");
            }

            EnsureNoRegistryConflict(descriptor);
        }
    }

    private void EnsureNoRegistryConflict(DriverDescriptor descriptor)
    {
        if (_descriptorsByDriverId.ContainsKey(descriptor.DriverId))
        {
            throw new InvalidOperationException($"DriverId '{descriptor.DriverId}' is already registered.");
        }

        if (_descriptorsByConnectionType.ContainsKey(descriptor.ConnectionType))
        {
            throw new InvalidOperationException($"DeviceConnectionType '{descriptor.ConnectionType}' is already registered.");
        }
    }

    private void RegisterCore(DriverDescriptor descriptor)
    {
        _descriptors.Add(descriptor);
        _descriptorsByDriverId.Add(descriptor.DriverId, descriptor);
        _descriptorsByConnectionType.Add(descriptor.ConnectionType, descriptor);
    }
}
