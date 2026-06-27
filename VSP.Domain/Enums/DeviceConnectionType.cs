namespace VSP.Domain.Enums;

public enum DeviceConnectionType
{
    Unknown = 0,

    // Generic
    RTSP,
    ONVIF,

    // Hikvision
    HikvisionISAPI,
    HikvisionSDK,

    // Dahua
    DahuaNetSDK,

    // Axis
    AxisVAPIX
}