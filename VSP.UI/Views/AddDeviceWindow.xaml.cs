using System.Windows;
using System.Windows.Controls;

namespace VSP.UI.Views;

public partial class AddDeviceWindow : Window
{
    public string DeviceName { get; private set; } = "";
    public string IpAddress { get; private set; } = "";
    public string Brand { get; private set; } = "";
    public string Username { get; private set; } = "";
    public string Password { get; private set; } = "";
    public string RtspUrl { get; private set; } = "";

    public AddDeviceWindow()
    {
        InitializeComponent();
        BrandComboBox.SelectedIndex = 0;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DeviceName = NameTextBox.Text.Trim();
        IpAddress = IpTextBox.Text.Trim();
        Brand = ((ComboBoxItem)BrandComboBox.SelectedItem).Content.ToString() ?? "";
        Username = UsernameTextBox.Text.Trim();
        Password = PasswordBox.Password;
        RtspUrl = RtspTextBox.Text.Trim();

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