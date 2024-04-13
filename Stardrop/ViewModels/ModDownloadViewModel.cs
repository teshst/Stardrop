using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
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
        Failed
    }

    public class ModDownloadViewModel : ViewModelBase
    {
        private DateTimeOffset _startTime;
        private CancellationToken _downloadCancelToken;

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

        private readonly ObservableAsPropertyHelper<string> _downloadSpeedLabel = null!;
        public string DownloadSpeedLabel => _downloadSpeedLabel.Value;

        public ModDownloadViewModel(Uri modUri, string name, long? sizeInBytes, CancellationToken downloadCancelToken)
        {
            _startTime = DateTimeOffset.UtcNow;

            _modUri = modUri;
            _name = name;
            _sizeBytes = sizeInBytes;
            _downloadedBytes = 0;
            _downloadCancelToken = downloadCancelToken;

            // SizeBytes null-ness to IsSizeUnknown converison
            this.WhenAnyValue(x => x.SizeBytes)
                .Select(x => x.HasValue is false)
                .ToProperty(this, x => x.IsSizeUnknown, out _isSizeUnknown);

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
            }
        }
    }
}
