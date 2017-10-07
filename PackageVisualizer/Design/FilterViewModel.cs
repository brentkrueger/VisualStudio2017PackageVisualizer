
using System.Windows;
using System.Windows.Input;
using Microsoft.VisualStudio.PlatformUI;

namespace PackageVisualizer.Design
{
    public class FilterViewModel : ObservableObject
    {
        private string _packageFilter;

        public string PackageFilter
        {
            get => _packageFilter;
            set
            {
                _packageFilter = value;
                OnPropertyChanged();
            }
        }

        public ICommand ApplyCommand { get; set; }

        public FilterViewModel()
        {
            ApplyCommand = new DelegateCommand(s => ((DialogWindow)s).Close());
        }
    }
}
