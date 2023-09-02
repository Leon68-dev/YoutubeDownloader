using System.Diagnostics;
using System.Net;
using System.Web;
using System.Xml;
using YoutubeExplode;

namespace YoutubeDownloader
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Youtube downloader");

            if (args != null && args.Length != 2)
            {
                Console.WriteLine("For download video from You need use 2 params");
                Console.WriteLine("\tParam 1 - Path for save on local PC");
                Console.WriteLine("\tParam 2 - URL Youtube video");
                return;
            }

            try
            {
                string outputDirectory = args[0];
                string videoUrl = args[1];
                await DownloadYouTubeVideo(videoUrl, outputDirectory);
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred while downloading the videos: " + ex.Message);
            }
        }
        static async Task DownloadYouTubeVideo(string videoUrl, string outputDirectory)
        {
            var youtube = new YoutubeClient();
            var video = await youtube.Videos.GetAsync(videoUrl);

            string sanitizedTitle = string.Join("_", video.Title.Split(Path.GetInvalidFileNameChars()));
            var streamManifest = await youtube.Videos.Streams.GetManifestAsync(video.Id);
            var muxedStreams = streamManifest.GetMuxedStreams().OrderByDescending(s => s.VideoQuality).ToList();

            if (muxedStreams.Any())
            {
                Console.WriteLine("Download started");

                var streamInfo = muxedStreams.First();
                using var httpClient = new HttpClient();
                var stream = await httpClient.GetStreamAsync(streamInfo.Url);
                var datetime = DateTime.Now;

                string outputFilePath = Path.Combine(outputDirectory, $"{sanitizedTitle}.{streamInfo.Container}");
                using var outputStream = File.Create(outputFilePath);
                await stream.CopyToAsync(outputStream);

                Console.WriteLine("Download completed!");
                Console.WriteLine($"Video saved as: {outputFilePath}{datetime}");
            }
            else
            {
                Console.WriteLine($"No suitable video stream found for {video.Title}.");
            }
        }
    }

}