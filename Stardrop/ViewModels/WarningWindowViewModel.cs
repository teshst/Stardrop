using ReactiveUI;

namespace Stardrop.ViewModels
{
    public class WarningWindowViewModel : ViewModelBase
    {
        private string _warningText;
        public string WarningText { get { return _warningText; } set { this.RaiseAndSetIfChanged(ref _warningText, value); } }
        private string _buttonText;
        public string ButtonText { get { return _buttonText; } set { this.RaiseAndSetIfChanged(ref _buttonText, value); } }
        private bool _isButtonVisible;
        public bool IsButtonVisible { get { return _isButtonVisible; } set { this.RaiseAndSetIfChanged(ref _isButtonVisible, value); } }
        private bool _isProgressBarVisible;
        public bool IsProgressBarVisible { get { return _isProgressBarVisible; } set { this.RaiseAndSetIfChanged(ref _isProgressBarVisible, value); } }
        private double _progressBarValue;
        public double ProgressBarValue { get { return _progressBarValue; } set { this.RaiseAndSetIfChanged(ref _progressBarValue, value); } }

        public WarningWindowViewModel()
        {

        }
    }
}
