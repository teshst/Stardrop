using Avalonia.Collections;
using Avalonia.Controls;
using ReactiveUI;
using Stardrop.Models.Data;
using Stardrop.Utilities.External;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stardrop.ViewModels
{
    public class DownloadPanelViewModel : ViewModelBase
    {
        //private AvaloniaDictionary<Uri, ModDownloadViewModel> _downloads = new AvaloniaDictionary<Uri, ModDownloadViewModel>();
        //public AvaloniaDictionary<Uri, ModDownloadViewModel> Downloads { get => _downloads; set => this.RaiseAndSetIfChanged(ref _downloads, value); }
        private ObservableCollection<ModDownloadViewModel> _downloads = new();
        public ObservableCollection<ModDownloadViewModel> Downloads { get => _downloads; set => this.RaiseAndSetIfChanged(ref _downloads, value); }

        public DownloadPanelViewModel(NexusClient? nexusClient)
        {
            Nexus.ClientChanged += NexusClientChanged;
            if (nexusClient is not null)
            {
                RegisterEventHandlers(nexusClient);
            }
        }

        private void NexusClientChanged(NexusClient? oldClient, NexusClient? newClient)
        {
            if (oldClient is not null)
            {
                // TODO: Cancel all in-flight downloads
                // Unsubscribe from all removal event handlers
                // Clear the Downloads dict
                ClearEventHandlers(oldClient);
            }
            if (newClient is not null)
            {
                RegisterEventHandlers(newClient);
            }
        }

        private void RegisterEventHandlers(NexusClient nexusClient)
        {
            nexusClient.DownloadStarted += DownloadStarted;
            nexusClient.DownloadProgressChanged += DownloadProgressChanged;
            nexusClient.DownloadCompleted += DownloadCompleted;
            nexusClient.DownloadFailed += DownloadFailed;
        }

        private void ClearEventHandlers(NexusClient nexusClient)
        {
            nexusClient.DownloadStarted -= DownloadStarted;
            nexusClient.DownloadProgressChanged -= DownloadProgressChanged;
            nexusClient.DownloadCompleted -= DownloadCompleted;
            nexusClient.DownloadFailed -= DownloadFailed;
        }

        private void DownloadStarted(object? sender, ModDownloadStartedEventArgs e)
        {
            var downloadVM = new ModDownloadViewModel(e.Uri, e.Name, e.Size, e.DownloadCancellationSource);
            downloadVM.RemovalRequested += DownloadRemovalRequested;
            Downloads.Add(downloadVM);
        }

        private void DownloadProgressChanged(object? sender, ModDownloadProgressEventArgs e)
        {
            var download = Downloads.SingleOrDefault(x => x.ModUrl == e.Uri);
            if (download is not null)
            {
                download.DownloadStatus = ModDownloadStatus.InProgress;
                download.DownloadedBytes = e.TotalBytes;
            }            
        }

        private void DownloadCompleted(object? sender, ModDownloadCompletedEventArgs e)
        {
            var download = Downloads.SingleOrDefault(x => x.ModUrl == e.Uri);
            if (download is not null)
            {
                download.DownloadStatus = ModDownloadStatus.Successful;
            }
        }

        private void DownloadFailed(object? sender, ModDownloadFailedEventArgs e)
        {
            var download = Downloads.SingleOrDefault(x => x.ModUrl == e.Uri);
            if (download is not null)
            {
                download.DownloadStatus = ModDownloadStatus.Failed;
            }
        }

        private void DownloadRemovalRequested(object? sender, EventArgs _)
        {
            if (sender is not ModDownloadViewModel downloadVM)
            {
                return;
            }

            downloadVM.RemovalRequested -= DownloadRemovalRequested;
            Downloads.Remove(downloadVM);
        }
    }
}
