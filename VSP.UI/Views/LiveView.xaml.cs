using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using VSP.Domain.Entities;
using VSP.UI.ViewModels;

namespace VSP.UI.Views;

public partial class LiveView : UserControl
{
    private readonly LiveViewViewModel _viewModel;

    public LiveView()
        : this(new LiveViewViewModel(Dispatcher.CurrentDispatcher))
    {
    }

    internal LiveView(LiveViewViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    public void LoadCamera(Camera camera)
    {
        _viewModel.LoadCamera(camera);
    }
}
