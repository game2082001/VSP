using VSP.Core.MVVM;

namespace VSP.UI.ViewModels;

public class CameraEditorViewModel : ObservableObject
{
    private string _deviceName = "";
    public string DeviceName
    {
        get => _deviceName;
        set => SetProperty(ref _deviceName, value);
    }

    private string _ipAddress = "";
    public string IpAddress
    {
        get => _ipAddress;
        set => SetProperty(ref _ipAddress, value);
    }

    private string _brand = "Hikvision";
    public string Brand
    {
        get => _brand;
        set => SetProperty(ref _brand, value);
    }

    private string _model = "";
    public string Model
    {
        get => _model;
        set => SetProperty(ref _model, value);
    }

    private string _location = "";
    public string Location
    {
        get => _location;
        set => SetProperty(ref _location, value);
    }

    private int _httpPort = 80;
    public int HttpPort
    {
        get => _httpPort;
        set => SetProperty(ref _httpPort, value);
    }

    private int _rtspPort = 554;
    public int RtspPort
    {
        get => _rtspPort;
        set => SetProperty(ref _rtspPort, value);
    }

    private int _sdkPort = 8000;
    public int SdkPort
    {
        get => _sdkPort;
        set => SetProperty(ref _sdkPort, value);
    }

    private string _username = "";
    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    private string _password = "";
    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    private string _rtspUrl = "";
    public string RtspUrl
    {
        get => _rtspUrl;
        set => SetProperty(ref _rtspUrl, value);
    }
}