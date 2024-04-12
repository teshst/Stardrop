using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stardrop.ViewModels
{
    public class ModDownloadViewModel : ViewModelBase
    {
        private Uri _modUri;
        public Uri ModUrl { get => _modUri; set => this.RaiseAndSetIfChanged(ref _modUri, value); }

        private string _name;
        public string Name { get => _name; set => this.RaiseAndSetIfChanged(ref _name, value); }

        private long _sizeBytes;
        public long SizeBytes { get => _sizeBytes; set => this.RaiseAndSetIfChanged(ref _sizeBytes, value); }

        private long _downloadedBytes;
        public long DownloadedBytes { get => _downloadedBytes; set => this.RaiseAndSetIfChanged(ref _downloadedBytes, value); }

        private readonly ObservableAsPropertyHelper<double> _completion;
        public double Completion => _completion.Value;

        public ModDownloadViewModel(Uri modUri, string name, long sizeInBytes)
        {
            _modUri = modUri;
            _name = name;
            _sizeBytes = sizeInBytes;
            _downloadedBytes = 0;

            this.WhenAnyValue(x => x.DownloadedBytes)
                .Select(x => DownloadedBytes / (double)SizeBytes)
                .ToProperty(this, x => x.Completion, out _completion);
        }
    }
}
