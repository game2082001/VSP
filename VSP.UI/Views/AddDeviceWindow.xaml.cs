using System.Net;
using System.Text.RegularExpressions;
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

        // ===== 必填欄位 =====

        if (string.IsNullOrWhiteSpace(DeviceName))
        {
            MessageBox.Show("請輸入設備名稱。", "VSP");
            NameTextBox.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(IpAddress))
        {
            MessageBox.Show("請輸入 IP 位址。", "VSP");
            IpTextBox.Focus();
            return;
        }

        // ===== IP 驗證 =====

        if (!IsValidIPv4(IpAddress))
        {
            MessageBox.Show("IP 位址格式錯誤。", "VSP");
            IpTextBox.Focus();
            IpTextBox.SelectAll();
            return;
        }

        // ===== Port 驗證 =====

        if (!int.TryParse(HttpPortTextBox.Text.Trim(), out var httpPort)
            || httpPort < 1 || httpPort > 65535)
        {
            MessageBox.Show("HTTP Port 必須介於 1~65535。", "VSP");
            HttpPortTextBox.Focus();
            HttpPortTextBox.SelectAll();
            return;
        }

        if (!int.TryParse(RtspPortTextBox.Text.Trim(), out var rtspPort)
            || rtspPort < 1 || rtspPort > 65535)
        {
            MessageBox.Show("RTSP Port 必須介於 1~65535。", "VSP");
            RtspPortTextBox.Focus();
            RtspPortTextBox.SelectAll();
            return;
        }

        if (!int.TryParse(SdkPortTextBox.Text.Trim(), out var sdkPort)
            || sdkPort < 1 || sdkPort > 65535)
        {
            MessageBox.Show("SDK Port 必須介於 1~65535。", "VSP");
            SdkPortTextBox.Focus();
            SdkPortTextBox.SelectAll();
            return;
        }

        HttpPort = httpPort;
        RtspPort = rtspPort;
        SdkPort = sdkPort;

        DialogResult = true;
        Close();
    }

    private bool IsValidIPv4(string ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
            return false;

        // 必須是 x.x.x.x
        if (!Regex.IsMatch(ip, @"^(\d{1,3}\.){3}\d{1,3}$"))
            return false;

        string[] parts = ip.Split('.');

        foreach (string part in parts)
        {
            if (!int.TryParse(part, out int value))
                return false;

            if (value < 0 || value > 255)
                return false;
        }

        return IPAddress.TryParse(ip, out _);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}