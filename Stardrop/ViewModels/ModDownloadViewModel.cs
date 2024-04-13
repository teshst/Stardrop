using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
        private DateTimeOffset _startTime;
        private CancellationTokenSource _downloadCancellationSource;

        public event EventHandler? RemovalRequested = null!;

        private Uri _modUri;
        public Uri ModUrl { get => _modUri; set => this.RaiseAndSetIfChanged(ref _modUri, value); }

        private string _name;
        public string Name { get => _name; set => this.RaiseAndSetIfChanged(ref _name, value); }

        private long? _sizeBytes;
        public long? SizeBytes { get => _sizeBytes; set => this.RaiseAndSetIfChanged(ref _sizeBytes, value); }

        private long _downloadedBytes;
        public long DownloadedBytes { get => _downloadedBytes; set => this.RaiseAndSetIfChanged(ref _downloadedBytes, value); }

        private ModDownloadStatus _downloadStatus = ModDownloadStatus.NotStarted;
        public ModDownloadStatus DownloadStatus { get => _downloadStatus; set => this.RaiseAndSetIfChanged(ref _downloadStatus, value); }

        private readonly ObservableAsPropertyHelper<double> _completion = null!;
        public double Completion => _completion.Value;

        private readonly ObservableAsPropertyHelper<bool> _isSizeUnknown = null!;
        public bool IsSizeUnknown => _isSizeUnknown.Value;

        // Ended via success, failure, or cancellation
        private readonly ObservableAsPropertyHelper<bool> _isDownloadEnded = null!;
        public bool IsDownloadEnded => _isDownloadEnded.Value;

        private readonly ObservableAsPropertyHelper<string> _downloadSpeedLabel = null!;
        public string DownloadSpeedLabel => _downloadSpeedLabel.Value;

        private readonly ObservableAsPropertyHelper<string> _downloadProgressLabel = null!;
        public string DownloadProgressLabel => _downloadProgressLabel.Value;

        public ReactiveCommand<Unit, Unit> CancelCommand { get; }
        public ReactiveCommand<Unit, Unit> RemoveCommand { get; }

        public ModDownloadViewModel(Uri modUri, string name, long? sizeInBytes, CancellationTokenSource downloadCancellationSource)
        {
            _startTime = DateTimeOffset.UtcNow;

            _modUri = modUri;
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

            if (SizeBytes.HasValue)
            {
                // DownloadedBytes to Completion conversion
                this.WhenAnyValue(x => x.DownloadedBytes)
                    .Select(x => (DownloadedBytes / (double)SizeBytes) * 100)
                    .ToProperty(this, x => x.Completion, out _completion);

                // DownloadedBytes to DownloadSpeedLabel conversion
                this.WhenAnyValue(x => x.DownloadedBytes)
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
                    .Select( ((long Bytes, long? Total) x) =>
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
