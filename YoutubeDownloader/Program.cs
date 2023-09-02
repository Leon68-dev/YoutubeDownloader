using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Web;
using System.Xml;
using YoutubeExplode;
using YoutubeExplode.Videos;

namespace YoutubeDownloader
{
    class Program
    {
        private static object locker = new object();

        static async Task Main(string[] args)
        {
            Console.Clear();
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

            Console.WriteLine("Video quality");
            Console.WriteLine("----------------------------------------------------------------");
            int index = 0;
            foreach(var item in muxedStreams)
            {
                Console.WriteLine($"index: {index} - {item.VideoQuality.Label} ({item.Container.Name}) {(int)item.Size.MegaBytes}MB");
                Console.WriteLine("----------------------------------------------------------------");
                index++;
            }

            Console.Write("Please select index: ");
            string indexStr = Console.ReadLine() ?? "0";

            int indexParse = 0;
            if (int.TryParse(indexStr, out indexParse))
            {
                index = indexParse;
            }
            else
            {
                index = 0;
            }

            if (index >= muxedStreams.Count)
                index = 0;

            var prog = new Progress<KeyValuePair<long, long>>();

            if (muxedStreams.Any())
            {
                Console.WriteLine("Download started");
                
                moveTop += (muxedStreams.Count * 2) + 2;
                var streamInfo = muxedStreams[index];
                
                //using var httpClient = new HttpClient();
                //var stream = await httpClient.GetStreamAsync(streamInfo.Url);
                var datetime = DateTime.Now;
                string outputFilePath = Path.Combine(outputDirectory, $"{sanitizedTitle}_{streamInfo.VideoQuality.Label}.{streamInfo.Container}");
                //using var outputStream = File.Create(outputFilePath);
                //await stream.CopyToAsync(outputStream);

                var progress = new Progress<float>();
                progress.ProgressChanged += Progress_ProgressChanged;    

                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(5);
                    using (var file = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                        await client.DownloadAsync(streamInfo.Url, file, progress);
                }

                Console.WriteLine("Download completed!");
                Console.WriteLine($"Video saved as: {outputFilePath}");
                Console.WriteLine($"{datetime}");
            }
            else
            {
                Console.WriteLine($"No suitable video stream found for {video.Title}.");
            }

        }

        static int clc = 0;
        static int moveTop = 2;
        private static void Progress_ProgressChanged(object? sender, float e)
        {
            int prc = (int)(e * 100);
            if (clc != prc) 
            {
                lock(locker)
                {
                    Console.SetCursorPosition(0, moveTop);
                    Console.WriteLine($"                               ");
                    Console.SetCursorPosition(0, moveTop);
                    Console.WriteLine($"Downloading...{prc}%");
                    Console.SetCursorPosition(0, moveTop);
                }
            }
            clc = prc;
        }
    }

    public static class HttpClientExtensions
    {
        public static async Task DownloadAsync(this HttpClient client, 
            string requestUri, 
            Stream destination, 
            IProgress<float>? progress = null, 
            CancellationToken cancellationToken = default)
        {
            using (var response = await client.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead))
            {
                var contentLength = response.Content.Headers.ContentLength;
                using (var download = await response.Content.ReadAsStreamAsync())
                {
                    if (progress == null || !contentLength.HasValue)
                    {
                        await download.CopyToAsync(destination);
                        return;
                    }

                    var relativeProgress = new Progress<long>(totalBytes => progress.Report((float)totalBytes / contentLength.Value));
                    await download.CopyToAsync(destination, 81920, relativeProgress, cancellationToken);
                    progress.Report(1);
                }
            }
        }
    }

    public static class StreamExtensions
    {
        public static async Task CopyToAsync(this Stream source, 
            Stream destination, 
            int bufferSize, 
            IProgress<long>? progress = null, 
            CancellationToken cancellationToken = default)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (!source.CanRead)
                throw new ArgumentException("Has to be readable", nameof(source));
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            if (!destination.CanWrite)
                throw new ArgumentException("Has to be writable", nameof(destination));
            if (bufferSize < 0)
                throw new ArgumentOutOfRangeException(nameof(bufferSize));

            var buffer = new byte[bufferSize];
            long totalBytesRead = 0;
            int bytesRead;
            while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) != 0)
            {
                await destination.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
                totalBytesRead += bytesRead;
                progress?.Report(totalBytesRead);
            }
        }
    }
}