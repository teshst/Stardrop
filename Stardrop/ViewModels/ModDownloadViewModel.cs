using ReactiveUI;
using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;

namespace Stardrop.ViewModels
{
    public enum ModDownloadStatus
    {
        NotStarted,
        InProgress,
        Successful,
        Canceled,
        Failed
    }

    public class ModDownloadViewModel : ViewModelBase
    {
        private readonly DateTimeOffset _startTime;
        private readonly CancellationTokenSource _downloadCancellationSource;

        // Communicates up to the parent panel that the user wants to remove this download from the list.
        public event EventHandler? RemovalRequested = null!;

        // --Set-once properties--
        public Uri ModUri { get; init; }

        public string CancelButtonTooltip { get; init; } = Program.translation.Get("ui.downloads_panel.tooltips.cancel_button");
        public string RemoveButtonTooltip { get; init; } = Program.translation.Get("ui.downloads_panel.tooltips.remove_button");

        // --Bindable properties--

        private string _name;
        public string Name { get => _name; set => this.RaiseAndSetIfChanged(ref _name, value); }

        private long? _sizeBytes;
        public long? SizeBytes { get => _sizeBytes; set => this.RaiseAndSetIfChanged(ref _sizeBytes, value); }

        private long _downloadedBytes;
        public long DownloadedBytes { get => _downloadedBytes; set => this.RaiseAndSetIfChanged(ref _downloadedBytes, value); }

        private ModDownloadStatus _downloadStatus = ModDownloadStatus.NotStarted;
        public ModDownloadStatus DownloadStatus { get => _downloadStatus; set => this.RaiseAndSetIfChanged(ref _downloadStatus, value); }

        // --Composite or dependent properties--

        private ObservableAsPropertyHelper<string> _downloadCompletionStatusText = null!;
        public string DownloadCompletionStatusText => _downloadCompletionStatusText.Value;

        private readonly ObservableAsPropertyHelper<double> _completion = null!;
        public double Completion => _completion.Value;

        private readonly ObservableAsPropertyHelper<bool> _isSizeUnknown = null!;
        public bool IsSizeUnknown => _isSizeUnknown.Value;

        // Ended via success, failure, or cancellation
        private readonly ObservableAsPropertyHelper<bool> _isDownloadEnded = null!;
        public bool IsDownloadEnded => _isDownloadEnded.Value;

        private readonly ObservableAsPropertyHelper<bool> _isDownloadUnsuccessful = null!;
        public bool IsDownloadUnsuccessful => _isDownloadEnded.Value;

        private readonly ObservableAsPropertyHelper<string> _downloadSpeedLabel = null!;
        public string DownloadSpeedLabel => _downloadSpeedLabel.Value;

        private readonly ObservableAsPropertyHelper<string> _downloadProgressLabel = null!;
        public string DownloadProgressLabel => _downloadProgressLabel.Value;

        // --Commands--

        public ReactiveCommand<Unit, Unit> CancelCommand { get; }
        public ReactiveCommand<Unit, Unit> RemoveCommand { get; }

        public ModDownloadViewModel(Uri modUri, string name, long? sizeInBytes, CancellationTokenSource downloadCancellationSource)
        {
            _startTime = DateTimeOffset.UtcNow;

            ModUri = modUri;
            _name = name;
            _sizeBytes = sizeInBytes;
            _downloadedBytes = 0;
            _downloadCancellationSource = downloadCancellationSource;

            CancelCommand = ReactiveCommand.Create(Cancel);
            RemoveCommand = ReactiveCommand.Create(Remove);

            // SizeBytes null-ness to IsSizeUnknown converison
            this.WhenAnyValue(x => x.SizeBytes)
                .Select(x => x.HasValue is false)
                .ToProperty(this, x => x.IsSizeUnknown, out _isSizeUnknown);

            // DownloadStaus to IsDownloadEnded conversion
            this.WhenAnyValue(x => x.DownloadStatus)
                .Select(x => x == ModDownloadStatus.Successful
                    || x == ModDownloadStatus.Canceled
                    || x == ModDownloadStatus.Failed)
                .ToProperty(this, x => x.IsDownloadEnded, out _isDownloadEnded);

            // DownloadStatus to DownloadCompletionStatusText conversion
            this.WhenAnyValue(x => x.DownloadStatus)
                .Select(x =>
                {
                    return x switch
                    {
                        ModDownloadStatus.Canceled => Program.translation.Get("ui.downloads_panel.download_canceled"),
                        ModDownloadStatus.Failed => Program.translation.Get("ui.downloads_panel.download_failed"),
                        ModDownloadStatus.Successful => Program.translation.Get("ui_downloads_panel.download_success"),
                        // The label isn't visible in these states, so the value we return doesn't matter
                        _ => ""
                    };
                }).ToProperty(this, x => x.DownloadCompletionStatusText, out _downloadCompletionStatusText);

            // DownloadStatus to IsDownloadUnsuccessful conversion
            this.WhenAnyValue(x => x.DownloadStatus)
                .Select(x => x == ModDownloadStatus.Canceled
                    || x == ModDownloadStatus.Failed)
                .ToProperty(this, x => x.IsDownloadUnsuccessful, out _isDownloadUnsuccessful);

            if (SizeBytes.HasValue)
            {
                // DownloadedBytes to Completion conversion
                this.WhenAnyValue(x => x.DownloadedBytes)
                    .Sample(TimeSpan.FromMilliseconds(500), RxApp.MainThreadScheduler)
                    .Select(x => (DownloadedBytes / (double)SizeBytes) * 100)
                    .ToProperty(this, x => x.Completion, out _completion);

                // DownloadedBytes to DownloadSpeedLabel conversion
                this.WhenAnyValue(x => x.DownloadedBytes)
                    .Sample(TimeSpan.FromMilliseconds(500), RxApp.MainThreadScheduler)
                    .Select(bytes =>
                    {
                        double elapsedSeconds = (DateTimeOffset.UtcNow - _startTime).TotalSeconds;
                        double bytesPerSecond = bytes / elapsedSeconds;
                        if (bytesPerSecond > 1024 * 1024)  // MB 
                        {
                            return $"{(bytesPerSecond / (1024 * 1024)):N2} MB/s";
                        }
                        else if (bytesPerSecond > 1024) // KB
                        {
                            return $"{(bytesPerSecond / 1024):N2} KB/s";
                        }
                        else // Bytes
                        {
                            return $"{bytesPerSecond:N0} B/s";
                        }
                    }).ToProperty(this, x => x.DownloadSpeedLabel, out _downloadSpeedLabel);

                // DownloadedBytes and SizeBytes to DownloadProgressLabel conversion
                this.WhenAnyValue(x => x.DownloadedBytes, x => x.SizeBytes)
                    .Sample(TimeSpan.FromMilliseconds(500), RxApp.MainThreadScheduler)
                    .Select(((long Bytes, long? Total) x) =>
                    {
                        string bytesString = ToHumanReadable(x.Bytes);
                        string totalString = ToHumanReadable(x.Total!.Value);
                        return $"{bytesString} / {totalString}";

                        static string ToHumanReadable(long bytes)
                        {
                            if (bytes > 1024 * 1024) // MB
                            {
                                return $"{(bytes / (1024.0 * 1024.0)):N2} MB";
                            }
                            else if (bytes > 1024) // KB
                            {
                                return $"{(bytes / 1024.0):N2} KB";
                            }
                            else
                            {
                                return $"{bytes:N0} B";
                            }
                        }
                    }).ToProperty(this, x => x.DownloadProgressLabel, out _downloadProgressLabel);
            }
        }

        private void Cancel()
        {
            _downloadCancellationSource.Cancel();
            DownloadStatus = ModDownloadStatus.Canceled;
        }

        private void Remove()
        {
            RemovalRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
