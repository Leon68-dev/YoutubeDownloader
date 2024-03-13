using CommunityToolkit.Mvvm.ComponentModel;


namespace YoutubeDownloaderMobile.Models
{
    public class DownloadData : ObservableObject
    {
        public int index { get; set; } = 0;
        public string label { get; set; } = string.Empty;
        public string name { get; set; } = string.Empty;
        public double size { get; set; } = 0.0;
        public string sanitizedTitle { get; set; } = string.Empty;
        public string buttonLabel { get { return ToString(); } }
        public override string ToString()
        {
            string res = string.Empty;
            res = $"{sanitizedTitle} - {label} ({name}) {(int)size}MB";
            return res;
        }
    }
}
