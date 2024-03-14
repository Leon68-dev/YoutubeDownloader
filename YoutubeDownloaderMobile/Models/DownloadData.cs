using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.VisualBasic;


namespace YoutubeDownloaderMobile.Models
{
    public class DownloadData : ObservableObject
    {
        public int index { get; set; } = 0;
        public string label { get; set; } = string.Empty;
        public string name { get; set; } = string.Empty;
        public double size { get; set; } = 0.0;
        public string sanitizedTitle { get; set; } = string.Empty;
        public override string ToString()
        {
            string tmp = sanitizedTitle;
            if (tmp.Length > 25)
            {
                tmp = $"{tmp.Substring(0, 21)}...";
            }
            string res = $"{tmp} - {label} ({name}) {(int)size}MB";
            return res;
        }
        public string buttonLabel { get { return ToString(); } }

    }
}
