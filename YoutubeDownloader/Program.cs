using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

class Program
{
    private static readonly object _locker = new object();

    static async Task Main(string[] args)
    {
        Console.Clear();
        Console.WriteLine("YouTube Downloader");

        if (args == null || args.Length != 2)
        {
            Console.WriteLine("To download a YouTube video, you need to use 2 parameters:");
            Console.WriteLine("\tParameter 1: Path to save the file on your local PC");
            Console.WriteLine("\tParameter 2: YouTube video URL");
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
            Console.WriteLine("An error occurred while downloading the video: " + ex.Message);
        }
    }

    static async Task DownloadYouTubeVideo(string videoUrl, string outputDirectory)
    {
        var youtube = new YoutubeClient();
        var video = await youtube.Videos.GetAsync(videoUrl);
        string sanitizedTitle = string.Join("_", video.Title.Split(Path.GetInvalidFileNameChars()));
        var streamManifest = await youtube.Videos.Streams.GetManifestAsync(video.Id);
        var muxedStreams = streamManifest.GetMuxedStreams()
                                         .Where(s => s.Container == Container.Mp4)
                                         .OrderByDescending(s => s.VideoQuality)
                                         .ToList();

        if (muxedStreams.Any())
        {
            Console.WriteLine("Available muxed streams (video + audio):");
            Console.WriteLine("----------------------------------------------------------------");
            int index = 0;
            foreach (var streamInfo in muxedStreams)
            {
                double fileSizeMb = streamInfo.Size.Bytes / (1024.0 * 1024.0);
                Console.WriteLine($"Index: {index} - {streamInfo.VideoQuality.Label} ({streamInfo.Container.Name}) {fileSizeMb:F2} MB");
                index++;
            }
            Console.WriteLine("----------------------------------------------------------------");

            Console.Write("Please select an index to download: ");
            string? indexStr = Console.ReadLine();

            if (!int.TryParse(indexStr, out int selectedIndex) || selectedIndex < 0 || selectedIndex >= muxedStreams.Count)
            {
                selectedIndex = 0;
                Console.WriteLine($"Invalid selection. Downloading video with the highest quality ({muxedStreams[selectedIndex].VideoQuality.Label}).");
            }

            var streamToDownload = muxedStreams[selectedIndex];

            var progress = new Progress<double>(p =>
            {
                lock (_locker)
                {
                    Console.CursorLeft = 0;
                    Console.Write($"Downloading... {p:P0}");
                }
            });

            string outputFilePath = Path.Combine(outputDirectory, $"{sanitizedTitle}_{streamToDownload.VideoQuality.Label}.{streamToDownload.Container}");

            Console.WriteLine("\nDownload started...");

            await youtube.Videos.Streams.DownloadAsync(streamToDownload, outputFilePath, progress);

            Console.WriteLine("\nDownload completed successfully!");
            Console.WriteLine($"Video saved as: {outputFilePath}");
        }
        else
        {
            Console.WriteLine("No muxed streams found. Downloading separate video and audio streams...");
            Console.WriteLine("This video has separate video and audio streams. Combining them automatically with FFmpeg.");

            var videoStreamInfo = streamManifest.GetVideoStreams()
                .Where(s => s.Container == Container.Mp4)
                .OrderByDescending(s => s.VideoQuality)
                .FirstOrDefault();

            var audioStreamInfo = streamManifest.GetAudioStreams()
                .Where(s => s.Container == Container.Mp4)
                .OrderByDescending(s => s.Bitrate)
                .FirstOrDefault();

            if (videoStreamInfo != null && audioStreamInfo != null)
            {
                var progress = new Progress<double>(p =>
                {
                    lock (_locker)
                    {
                        Console.CursorLeft = 0;
                        Console.Write($"Downloading video and audio... {p:P0}");
                    }
                });

                string tempVideoFilePath = Path.Combine(outputDirectory, $"video_temp.{videoStreamInfo.Container}");
                string tempAudioFilePath = Path.Combine(outputDirectory, $"audio_temp.{audioStreamInfo.Container}");
                string combinedFilePath = Path.Combine(outputDirectory, $"output_video.mp4");
                string finalFilePath = Path.Combine(outputDirectory, $"{sanitizedTitle}.mp4");

                Console.WriteLine($"\nDownloading video stream to: {tempVideoFilePath}");
                await youtube.Videos.Streams.DownloadAsync(videoStreamInfo, tempVideoFilePath, progress);

                Console.WriteLine($"\nDownloading audio stream to: {tempAudioFilePath}");
                await youtube.Videos.Streams.DownloadAsync(audioStreamInfo, tempAudioFilePath, progress);

                Console.WriteLine("\nBoth streams downloaded. Combining them with FFmpeg...");

                var ffmpegArguments = $"-i \"{tempVideoFilePath}\" -i \"{tempAudioFilePath}\" -c copy \"{combinedFilePath}\"";
                await RunFFmpegProcess(ffmpegArguments);

                File.Delete(tempVideoFilePath);
                File.Delete(tempAudioFilePath);

                if (File.Exists(combinedFilePath))
                {
                    Console.WriteLine("\nCombining completed. Renaming file...");
                    File.Move(combinedFilePath, finalFilePath);
                    Console.WriteLine($"Final video saved as: {finalFilePath}");
                }
                else
                {
                    Console.WriteLine("\nCombining failed. Final file was not created.");
                }
            }
            else
            {
                Console.WriteLine($"No suitable separate video or audio streams found for {video.Title}.");
            }
        }
    }

    private static async Task RunFFmpegProcess(string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var process = Process.Start(startInfo))
        {
            if (process == null)
            {
                Console.WriteLine("Failed to start FFmpeg. Make sure it is installed and in your system PATH.");
                return;
            }

            var errorReader = process.StandardError;
            while (!errorReader.EndOfStream)
            {
                string? line = await errorReader.ReadLineAsync();
                if (line != null)
                {
                    Console.WriteLine(line);
                }
            }

            await process.WaitForExitAsync();
        }
    }
}
