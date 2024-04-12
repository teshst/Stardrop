using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Stardrop.ViewModels;

namespace Stardrop.Views;

public partial class DownloadPanel : UserControl
{
    private DownloadPanelViewModel _viewModel;

    public DownloadPanel()
    {
        AvaloniaXamlLoader.Load(this);

        _viewModel = new DownloadPanelViewModel();
        DataContext = _viewModel;
    }
}