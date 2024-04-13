using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Stardrop.Utilities.External;
using Stardrop.ViewModels;
using System.Diagnostics;

namespace Stardrop.Views;

public partial class DownloadPanel : UserControl
{
    private DownloadPanelViewModel _viewModel;
    private ItemsRepeater _downloadItems = null;

    public DownloadPanel()
    {
        AvaloniaXamlLoader.Load(this);

        _viewModel = new DownloadPanelViewModel(Nexus.Client);
        DataContext = _viewModel;

        _downloadItems = this.FindControl<ItemsRepeater>("DownloadItems");
    }

    private void Debug_Clicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Debugger.Break();
    }
}