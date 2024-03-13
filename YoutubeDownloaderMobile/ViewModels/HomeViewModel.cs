using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using YoutubeDownloaderMobile.Models;
using YoutubeDownloaderMobile.Services;
using YoutubeDownloaderMobile.Views;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using YoutubeExplode;
using System;
using YoutubeExplode.Videos.Streams;
using CommunityToolkit.Maui.Storage;


namespace YoutubeDownloaderMobile.ViewModels 
{
    public class HomeViewModel : ObservableObject, INotifyPropertyChanged
    {
        public ICommand clickPasteCommand { get; }
        public ICommand clickGetDataCommand { get; }

        public new event PropertyChangedEventHandler? PropertyChanged;
        protected new virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private List<MuxedStreamInfo> _muxedStreams = new List<MuxedStreamInfo>();
        private string _sanitizedTitle = string.Empty;

        private string _downloadURL = string.Empty;
        public string downloadURL
        {
            get => _downloadURL;
            set
            {
                if (_downloadURL != value)
                {
                    _downloadURL = value;
                    OnPropertyChanged("downloadURL");
                    isEnableGetDataButton = _downloadURL.Length > 0;

                    if(string.IsNullOrEmpty(_downloadURL))
                        isVisibleDownloadDataCollection = false;
                }
            }
        }

        private bool _isVisibleDownloadDataCollection = false;
        public bool isVisibleDownloadDataCollection
        {
            get => _isVisibleDownloadDataCollection;
            set
            {
                if (_isVisibleDownloadDataCollection != value)
                {
                    _isVisibleDownloadDataCollection = value;
                    OnPropertyChanged("isVisibleDownloadDataCollection");
                }
            }
        }

        private bool _isEnableUrlEditor = true;
        public bool isEnableUrlEditor
        {
            get => _isEnableUrlEditor;
            set
            {
                if (_isEnableUrlEditor != value)
                {
                    _isEnableUrlEditor = value;
                    OnPropertyChanged("isEnableUrlEditor");
                }
            }
        }

        private bool _isEnablePasteButton = true;
        public bool isEnablePasteButton
        {
            get => _isEnablePasteButton;
            set
            {
                if (_isEnablePasteButton != value)
                {
                    _isEnablePasteButton = value;
                    OnPropertyChanged("isEnablePasteButton");
                }
            }
        }

        private bool _isEnableGetDataButton = false;
        public bool isEnableGetDataButton
        {
            get => _isEnableGetDataButton;
            set
            {
                if (_isEnableGetDataButton != value)
                {
                    _isEnableGetDataButton = value;
                    OnPropertyChanged("isEnableGetDataButton");
                }
            }
        }
        
        private bool _isShowLoading = false;
        public bool isShowLoading
        {
            get => _isShowLoading;
            set
            {
                if (_isShowLoading != value)
                {
                    _isShowLoading = value;
                    OnPropertyChanged("isShowLoading");
                }
            }
        }

        private ObservableCollection<DownloadData> _downloadDataCollection = new ObservableCollection<DownloadData>();
        public ObservableCollection<DownloadData> downloadDataCollection
        {
            get => _downloadDataCollection;
            set
            {
                _downloadDataCollection = value;
                OnPropertyChanged("downloadDataCollection");
            }
        }

        public HomeViewModel()
        {
            clickPasteCommand = new Command(runPasteCommand);
            clickGetDataCommand = new Command(runGetDataCommand);
        }

        private void runPasteCommand() 
        {
            _ = runPasteCommandAsync();
        }

        private async Task runPasteCommandAsync()
        {
            _downloadDataCollection.Clear();
            downloadURL = await Clipboard.Default.GetTextAsync() ?? "";
        }

        private void runGetDataCommand() 
        {
            _ = runGetDataCommandAsync();
        }

        private async Task runGetDataCommandAsync()
        {
            isEnableUrlEditor = false;
            isEnablePasteButton = false;
            isEnableGetDataButton = false;
            isVisibleDownloadDataCollection = false;
            isShowLoading = true;

            var youtube = new YoutubeClient();
            var video = await youtube.Videos.GetAsync(downloadURL);
            var streamManifest = await youtube.Videos.Streams.GetManifestAsync(video.Id);
            _muxedStreams = streamManifest.GetMuxedStreams().OrderByDescending(s => s.VideoQuality).ToList();
            _sanitizedTitle = video.Title;

            downloadDataCollection.Clear();

            int index = 0;
            foreach (var item in _muxedStreams)
            {
                DownloadData downloadData = new DownloadData()
                {
                    index = index,
                    label = item.VideoQuality.Label,
                    name = item.Container.Name,
                    sanitizedTitle = _sanitizedTitle,
                    size = item.Size.MegaBytes
                };
                this.downloadDataCollection.Add(downloadData);
                index++;
            }

            isEnableUrlEditor = true;
            isEnablePasteButton = true;
            isEnableGetDataButton = true;
            isShowLoading = false;
            isVisibleDownloadDataCollection = true;

            return;
        }

        public async Task downloadFile(int index)
        {
            var streamInfo = _muxedStreams[index];
            string fileName = $"{_sanitizedTitle}_{streamInfo.VideoQuality.Label}.{streamInfo.Container}";
            fileName = fileName.Replace("/", "-");

            using (var stream = new MemoryStream())
            {
                var fileSaverResult = await FileSaver.Default.SaveAsync($"{fileName}", stream);
                if (fileSaverResult.IsSuccessful)
                {
                    isEnablePasteButton = false;
                    isEnableGetDataButton = false;
                    isVisibleDownloadDataCollection = false;
                    isShowLoading = true;
                    isEnableUrlEditor= false;

                    fileSaverResult.EnsureSuccess();

                    using (var client = new HttpClient())
                    {
                        client.Timeout = TimeSpan.FromMinutes(5);
                        using (var file = new FileStream(fileSaverResult.FilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                            await client.DownloadAsync(streamInfo.Url, file);
                    }
                    
                    ToastUtil.show("File was downloaded");
                }
            }

            isEnableUrlEditor = true;
            isEnablePasteButton = true;
            isEnableGetDataButton = true;
            isShowLoading = false;
            isVisibleDownloadDataCollection = true;
        }

        public void runOnPropertyChanged()
        {
            OnPropertyChanged("downloadURL");
            OnPropertyChanged("downloadDataCollection");
            OnPropertyChanged("isVisibleUpButton");
        }

    }
    
}