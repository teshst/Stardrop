using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Stardrop.Views;

public partial class DownloadPanel : UserControl
{
    public DownloadPanel()
    {
        AvaloniaXamlLoader.Load(this);
    }
}