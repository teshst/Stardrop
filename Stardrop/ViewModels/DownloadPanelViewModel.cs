using ReactiveUI;
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
        private ObservableCollection<ModDownloadViewModel> _downloads = new ObservableCollection<ModDownloadViewModel>();
        public ObservableCollection<ModDownloadViewModel> Downloads { get => _downloads; set => this.RaiseAndSetIfChanged(ref _downloads, value); }

        public DownloadPanelViewModel(NexusClient nexusClient)
        {
            
        }
    }
}
