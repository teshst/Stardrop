using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Stardrop.Utilities.External;
using Stardrop.ViewModels;

namespace Stardrop.Views;

public partial class DownloadPanel : UserControl
{
    private DownloadPanelViewModel _viewModel = null!;

    public DownloadPanel()
    {
        AvaloniaXamlLoader.Load(this);
        if (Design.IsDesignMode)
        {
            Background = new SolidColorBrush(new Color(0xFF, 0x03, 0x13, 0x32));
            return;
        }

        _viewModel = new DownloadPanelViewModel(Nexus.Client);
        DataContext = _viewModel;
    }
}