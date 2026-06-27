using System.Windows;
using System.Windows.Controls;
using VSP.Domain.Entities;

namespace VSP.UI.Views;

public partial class AddDeviceWindow : Window
{
    public string DeviceName { get; private set; } = "";
    public string IpAddress { get; private set; } = "";
    public string Brand { get; private set; } = "";
    public string Model { get; private set; } = "";
    public string Location { get; private set; } = "";
    public int HttpPort { get; private set; } = 80;
    public int RtspPort { get; private set; } = 554;
    public int SdkPort { get; private set; } = 8000;
    public string Username { get; private set; } = "";
    public string Password { get; private set; } = "";
    public string RtspUrl { get; private set; } = "";

    public AddDeviceWindow()
    {
        InitializeComponent();
        BrandComboBox.SelectedIndex = 0;
    }

    public AddDeviceWindow(Camera camera) : this()
    {
        Title = "編輯設備";

        NameTextBox.Text = camera.Name;
        IpTextBox.Text = camera.IpAddress;
        ModelTextBox.Text = camera.Model;
        LocationTextBox.Text = camera.Location;
        HttpPortTextBox.Text = camera.HttpPort.ToString();
        RtspPortTextBox.Text = camera.RtspPort.ToString();
        SdkPortTextBox.Text = camera.SdkPort.ToString();
        UsernameTextBox.Text = camera.Username;
        PasswordBox.Password = camera.Password;
        RtspTextBox.Text = camera.RtspUrl;

        foreach (ComboBoxItem item in BrandComboBox.Items)
        {
            if ((item.Content?.ToString() ?? "") == camera.Brand.ToString())
            {
                BrandComboBox.SelectedItem = item;
                break;
            }
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DeviceName = NameTextBox.Text.Trim();
        IpAddress = IpTextBox.Text.Trim();
        Brand = ((ComboBoxItem)BrandComboBox.SelectedItem).Content.ToString() ?? "";
        Model = ModelTextBox.Text.Trim();
        Location = LocationTextBox.Text.Trim();
        Username = UsernameTextBox.Text.Trim();
        Password = PasswordBox.Password;
        RtspUrl = RtspTextBox.Text.Trim();

        if (!int.TryParse(HttpPortTextBox.Text.Trim(), out var httpPort))
        {
            MessageBox.Show("HTTP Port 請輸入數字。", "VSP");
            return;
        }

        if (!int.TryParse(RtspPortTextBox.Text.Trim(), out var rtspPort))
        {
            MessageBox.Show("RTSP Port 請輸入數字。", "VSP");
            return;
        }

        if (!int.TryParse(SdkPortTextBox.Text.Trim(), out var sdkPort))
        {
            MessageBox.Show("SDK Port 請輸入數字。", "VSP");
            return;
        }

        HttpPort = httpPort;
        RtspPort = rtspPort;
        SdkPort = sdkPort;

        if (string.IsNullOrWhiteSpace(DeviceName) || string.IsNullOrWhiteSpace(IpAddress))
        {
            MessageBox.Show("請輸入設備名稱與 IP。", "VSP");
            return;
        }

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}