using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Stardrop.ViewModels;
using System;

namespace Stardrop.Views
{
    public partial class MessageWindow : Window
    {
        private readonly MessageWindowViewModel _viewModel;

        public MessageWindow()
        {
            InitializeComponent();

            // Set the main window view
            _viewModel = new MessageWindowViewModel();
            DataContext = _viewModel;

#if DEBUG
            this.AttachDevTools();
#endif
        }

        public MessageWindow(string messageText, string? positiveButtonText = null, string? negativeButtonText = null) : this()
        {
            Program.helper.Log($"Created a message window with the following text: [{positiveButtonText} | {negativeButtonText}] {messageText}");

            _viewModel.MessageText = messageText;
            _viewModel.PositiveButtonText = String.IsNullOrEmpty(positiveButtonText) ? Program.translation.Get("internal.yes") : positiveButtonText;
            _viewModel.NegativeButtonText = String.IsNullOrEmpty(negativeButtonText) ? Program.translation.Get("internal.no") : negativeButtonText;

            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            this.SizeToContent = SizeToContent.Height;
        }

        private void PositiveButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            this.Close(true);
        }

        private void NegativeButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            this.Close(false);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
