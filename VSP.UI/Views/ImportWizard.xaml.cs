using System.Windows;
using VSP.UI.ViewModels;

namespace VSP.UI.Views;

public partial class ImportWizard : Window
{
    public ImportWizard()
    {
        InitializeComponent();
        DataContext = new ImportWizardViewModel();
    }
}
