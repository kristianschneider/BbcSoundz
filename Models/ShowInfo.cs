using System.ComponentModel;

namespace BbcSoundz.Models
{
    public class ShowInfo : INotifyPropertyChanged
    {
        private bool _isDownloaded = false;
        private string? _downloadedFilePath;

        public string DisplayName { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string Description { get; set; } = string.Empty;
        
        public bool IsDownloaded 
        { 
            get => _isDownloaded; 
            set 
            { 
                if (_isDownloaded != value) 
                { 
                    _isDownloaded = value; 
                    OnPropertyChanged(nameof(IsDownloaded)); 
                } 
            } 
        }
        
        public string? DownloadedFilePath 
        { 
            get => _downloadedFilePath; 
            set 
            { 
                if (_downloadedFilePath != value) 
                { 
                    _downloadedFilePath = value; 
                    OnPropertyChanged(nameof(DownloadedFilePath)); 
                } 
            } 
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public override string ToString()
        {
            return DisplayName;
        }
    }
}
