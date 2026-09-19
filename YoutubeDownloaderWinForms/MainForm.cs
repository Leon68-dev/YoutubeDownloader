using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO; // Added for file system operations
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

namespace YoutubeDownloaderWinForms
{
    public partial class MainForm : Form
    {
        private static readonly object _locker = new object();
        private YoutubeClient _youtubeClient;
        private List<MuxedStreamInfo>? _muxedStreams; // To store available streams
        private List<VideoQualityOption> _qualityOptions = new List<VideoQualityOption>(); // Available quality options
        private CancellationTokenSource? _cancellationTokenSource; // For download cancellation

        private const string FFmpegFileName = "ffmpeg";

        public MainForm()
        {
            InitializeComponent();
            _youtubeClient = new YoutubeClient();
            InitializeUI();
        }

        private void InitializeUI()
        {
            // Set initial values or settings for UI elements
            txtOutputDirectory.Text = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos); // Default path
            progressBar.Minimum = 0;
            progressBar.Maximum = 100;
            progressBar.Value = 0;
            txtLog.ReadOnly = true;
            btnDownload.Enabled = false; // Disable download button until a stream is selected
            btnOpenDir.Enabled = Directory.Exists(txtOutputDirectory.Text); // Enable if default dir exists
        }

        private void Log(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => Log(message)));
                return;
            }
            lock (_locker)
            {
                txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            }
        }

        private void UpdateProgress(int percentage)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateProgress(percentage)));
                return;
            }
            progressBar.Value = Math.Max(progressBar.Minimum, Math.Min(progressBar.Maximum, percentage));
        }

        private async void btnBrowseDirectory_Click(object sender, EventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                if (Directory.Exists(txtOutputDirectory.Text))
                {
                    fbd.SelectedPath = txtOutputDirectory.Text;
                }

                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    txtOutputDirectory.Text = fbd.SelectedPath;
                    btnOpenDir.Enabled = true; // Enable Open Directory button after selection
                }
            }
        }

        private async void btnGetInfo_Click(object sender, EventArgs e) // New button for getting video information
        {
            string videoUrl = txtVideoUrl.Text.Trim();
            if (string.IsNullOrWhiteSpace(videoUrl))
            {
                MessageBox.Show("Please enter a YouTube video URL.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Log("Getting video information...");
            btnGetInfo.Enabled = false;
            btnDownload.Enabled = false;
            lbxQualities.Items.Clear();
            _qualityOptions.Clear();
            _muxedStreams = null;
            UpdateProgress(0);

            try
            {
                var video = await _youtubeClient.Videos.GetAsync(videoUrl);
                var streamManifest = await _youtubeClient.Videos.Streams.GetManifestAsync(video.Id);

                Log($"Video found: \"{video.Title}\" ({video.Duration})");

                // Get highest quality audio stream for merging or audio-only download
                var bestAudio = streamManifest.GetAudioOnlyStreams()
                    .OrderByDescending(s => s.Bitrate)
                    .FirstOrDefault();

                // 1. Muxed streams (video + audio combined, usually 360p / 720p)
                _muxedStreams = streamManifest.GetMuxedStreams()
                    .OrderByDescending(s => s.VideoQuality.MaxHeight)
                    .ThenByDescending(s => s.VideoQuality.Framerate)
                    .ToList();

                foreach (var stream in _muxedStreams)
                {
                    double sizeMb = stream.Size.Bytes / (1024.0 * 1024.0);
                    _qualityOptions.Add(new VideoQualityOption
                    {
                        DisplayText = $"[Video + Audio] {stream.VideoQuality.Label} ({stream.Container.Name}) ~ {sizeMb:F1} MB",
                        MuxedStream = stream,
                        RequiresMuxing = false
                    });
                }

                // 2. Video-only streams for higher qualities (1080p, 1440p, 4K, 60fps)
                var videoOnlyStreams = streamManifest.GetVideoOnlyStreams()
                    .OrderByDescending(s => s.VideoQuality.MaxHeight)
                    .ThenByDescending(s => s.VideoQuality.Framerate)
                    .ThenByDescending(s => s.Bitrate)
                    .ToList();

                // Group by label and container to avoid duplicate resolution entries
                var distinctVideoStreams = videoOnlyStreams
                    .GroupBy(s => new { s.VideoQuality.Label, s.Container.Name })
                    .Select(g => g.First())
                    .ToList();

                foreach (var vStream in distinctVideoStreams)
                {
                    var matchedAudio = streamManifest.GetAudioOnlyStreams()
                        .Where(a => a.Container == vStream.Container)
                        .OrderByDescending(a => a.Bitrate)
                        .FirstOrDefault() ?? bestAudio;

                    double vSizeMb = vStream.Size.Bytes / (1024.0 * 1024.0);
                    double aSizeMb = matchedAudio != null ? (matchedAudio.Size.Bytes / (1024.0 * 1024.0)) : 0;
                    double totalSizeMb = vSizeMb + aSizeMb;

                    _qualityOptions.Add(new VideoQualityOption
                    {
                        DisplayText = $"[HD/FFmpeg] {vStream.VideoQuality.Label} ({vStream.Container.Name}) ~ {totalSizeMb:F1} MB",
                        VideoStream = vStream,
                        AudioStream = matchedAudio,
                        RequiresMuxing = true
                    });
                }

                // 3. Audio-only stream option
                if (bestAudio != null)
                {
                    double aSizeMb = bestAudio.Size.Bytes / (1024.0 * 1024.0);
                    _qualityOptions.Add(new VideoQualityOption
                    {
                        DisplayText = $"[Audio Only] Best quality ({bestAudio.Bitrate.KiloBitsPerSecond:F0} kbps, {bestAudio.Container.Name}) ~ {aSizeMb:F1} MB",
                        AudioStream = bestAudio,
                        IsAudioOnly = true,
                        RequiresMuxing = false
                    });
                }

                foreach (var option in _qualityOptions)
                {
                    lbxQualities.Items.Add(option.DisplayText);
                }

                if (_qualityOptions.Any())
                {
                    lbxQualities.SelectedIndex = 0;
                    btnDownload.Enabled = true;
                    Log($"Found {_qualityOptions.Count} quality options.");
                }
                else
                {
                    Log("No downloadable streams found for this video.");
                }
            }
            catch (Exception ex)
            {
                Log($"Error getting video information: {ex.Message}");
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnGetInfo.Enabled = true;
            }
        }

        private async void btnDownload_Click(object sender, EventArgs e)
        {
            string outputDirectory = txtOutputDirectory.Text;
            string videoUrl = txtVideoUrl.Text.Trim();

            if (string.IsNullOrWhiteSpace(outputDirectory) || !Directory.Exists(outputDirectory))
            {
                MessageBox.Show("Please specify a valid save directory.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(videoUrl))
            {
                MessageBox.Show("Please enter a YouTube video URL.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (lbxQualities.SelectedIndex < 0 || lbxQualities.SelectedIndex >= _qualityOptions.Count)
            {
                MessageBox.Show("Please select a video quality from the list.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selectedOption = _qualityOptions[lbxQualities.SelectedIndex];

            // Verify FFmpeg presence prior to downloading if muxing is needed
            if (selectedOption.RequiresMuxing && !IsFFmpegInPath())
            {
                string msg = $"The selected quality \"{selectedOption.DisplayText}\" requires FFmpeg to merge video and audio.\n\n" +
                             $"Please place 'ffmpeg.exe' in the application folder or add it to system PATH.";
                MessageBox.Show(msg, "FFmpeg Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Log("Error: FFmpeg is not found in the system.");
                return;
            }

            // Disable buttons during download
            btnDownload.Enabled = false;
            btnGetInfo.Enabled = false;
            btnBrowseDirectory.Enabled = false;
            txtVideoUrl.Enabled = false;
            lbxQualities.Enabled = false;
            btnOpenDir.Enabled = false; // Disable Open Dir during download
            UpdateProgress(0);
            txtLog.Clear();
            Log($"Starting download: {selectedOption.DisplayText}");

            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                await DownloadYouTubeVideo(videoUrl, selectedOption, outputDirectory, _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                Log("Download canceled by user.");
                UpdateProgress(0);
            }
            catch (Exception ex)
            {
                Log($"An error occurred during video download: {ex.Message}");
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                // Enable buttons after completion/cancellation
                btnDownload.Enabled = true;
                btnGetInfo.Enabled = true;
                btnBrowseDirectory.Enabled = true;
                txtVideoUrl.Enabled = true;
                lbxQualities.Enabled = true;
                btnOpenDir.Enabled = Directory.Exists(txtOutputDirectory.Text); // Re-enable if directory exists
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        // You can add a "Cancel" button
        private void btnCancel_Click(object sender, EventArgs e)
        {
            _cancellationTokenSource?.Cancel();
        }

        private async Task DownloadYouTubeVideo(string videoUrl, VideoQualityOption option, string outputDirectory, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var video = await _youtubeClient.Videos.GetAsync(videoUrl, cancellationToken);
            string sanitizedTitle = video.Title.GetSafeFileName();

            // Case 1: Audio-only download
            if (option.IsAudioOnly && option.AudioStream != null)
            {
                string audioFilePath = Path.Combine(outputDirectory, $"{sanitizedTitle}.{option.AudioStream.Container.Name}");
                Log($"Downloading audio to: {audioFilePath}");

                var progress = new Progress<double>(p => UpdateProgress((int)(p * 100)));
                await _youtubeClient.Videos.Streams.DownloadAsync(option.AudioStream, audioFilePath, progress, cancellationToken);

                Log($"Audio successfully saved: {audioFilePath}");
                UpdateProgress(100);
                return;
            }

            // Case 2: Muxed stream download (video + audio combined, no FFmpeg required)
            if (!option.RequiresMuxing && option.MuxedStream != null)
            {
                string outputFilePath = Path.Combine(outputDirectory, $"{sanitizedTitle}_{option.MuxedStream.VideoQuality.Label}.{option.MuxedStream.Container.Name}");
                Log($"Downloading muxed stream ({option.MuxedStream.VideoQuality.Label}) to: {outputFilePath}");

                var progress = new Progress<double>(p => UpdateProgress((int)(p * 100)));
                await _youtubeClient.Videos.Streams.DownloadAsync(option.MuxedStream, outputFilePath, progress, cancellationToken);

                Log("Download successfully completed!");
                Log($"Video saved as: {outputFilePath}");
                UpdateProgress(100);
                return;
            }

            // Case 3: Separate video and audio streams (requires FFmpeg merging)
            if (option.VideoStream != null && option.AudioStream != null)
            {
                string ext = option.VideoStream.Container.Name;
                string tempVideoFilePath = Path.Combine(outputDirectory, $"{Guid.NewGuid():N}_video_temp.{ext}");
                string tempAudioFilePath = Path.Combine(outputDirectory, $"{Guid.NewGuid():N}_audio_temp.{option.AudioStream.Container.Name}");
                string finalFilePath = Path.Combine(outputDirectory, $"{sanitizedTitle}_{option.VideoStream.VideoQuality.Label}.mp4");

                try
                {
                    // Video download: 0% -> 50%
                    Log($"Downloading video stream ({option.VideoStream.VideoQuality.Label})...");
                    var videoProgress = new Progress<double>(p => UpdateProgress((int)(p * 50)));
                    await _youtubeClient.Videos.Streams.DownloadAsync(option.VideoStream, tempVideoFilePath, videoProgress, cancellationToken);

                    // Audio download: 50% -> 90%
                    Log("Downloading audio stream...");
                    var audioProgress = new Progress<double>(p => UpdateProgress(50 + (int)(p * 40)));
                    await _youtubeClient.Videos.Streams.DownloadAsync(option.AudioStream, tempAudioFilePath, audioProgress, cancellationToken);

                    // FFmpeg merging: 90% -> 100%
                    Log("Merging video and audio using FFmpeg...");
                    UpdateProgress(90);

                    if (File.Exists(finalFilePath))
                    {
                        File.Delete(finalFilePath);
                    }

                    var ffmpegArguments = $"-i \"{tempVideoFilePath}\" -i \"{tempAudioFilePath}\" -c copy -y \"{finalFilePath}\"";
                    await RunFFmpegProcess(ffmpegArguments, cancellationToken);

                    Log($"Download successfully completed!");
                    Log($"Final video saved as: {finalFilePath}");
                    UpdateProgress(100);
                }
                finally
                {
                    // Cleanup temporary files
                    try
                    {
                        if (File.Exists(tempVideoFilePath))
                            File.Delete(tempVideoFilePath);
                    }
                    catch (Exception ex)
                    {
                        Log($"Error deleting {tempVideoFilePath}: {ex.Message}");
                    }

                    try
                    {
                        if (File.Exists(tempAudioFilePath))
                            File.Delete(tempAudioFilePath);
                    }
                    catch (Exception ex)
                    {
                        Log($"Error deleting {tempAudioFilePath}: {ex.Message}");
                    }
                }
            }
        }

        private bool IsFFmpegInPath()
        {
            try
            {
                // Check if ffmpeg is directly in the application directory
                if (File.Exists(Path.Combine(Application.StartupPath, FFmpegFileName + ".exe")) ||
                    File.Exists(Path.Combine(Application.StartupPath, FFmpegFileName)))
                {
                    return true;
                }

                // Check if ffmpeg is in the system PATH
                var values = Environment.GetEnvironmentVariable("PATH");
                if (values != null)
                {
                    foreach (var path in values.Split(';'))
                    {
                        var fullPath = Path.Combine(path.Trim(), FFmpegFileName + ".exe");
                        if (File.Exists(fullPath))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Log($"Error checking FFmpeg path: {ex.Message}");
                return false;
            }
        }

        private async Task RunFFmpegProcess(string arguments, CancellationToken cancellationToken)
        {
            string ffmpegExecutable = File.Exists(Path.Combine(Application.StartupPath, FFmpegFileName + ".exe"))
                ? Path.Combine(Application.StartupPath, FFmpegFileName + ".exe")
                : FFmpegFileName;

            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpegExecutable,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process? process = null;

            try
            {
                process = Process.Start(startInfo);

                if (process == null)
                {
                    Log($"Failed to start {FFmpegFileName}. Make sure it is installed and in your system PATH.");
                    throw new InvalidOperationException("FFmpeg process could not be started.");
                }

                // Asynchronously read FFmpeg error output (where it logs progress)
                var errorReaderTask = Task.Run(async () =>
                {
                    var errorReader = process.StandardError;
                    while (!errorReader.EndOfStream)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        string? line = await errorReader.ReadLineAsync();
                        if (line != null && line.StartsWith("frame="))
                        {
                            // FFmpeg writes progress and errors to StandardError
                        }
                    }
                }, cancellationToken);

                // Wait for the process to exit, respecting the cancellation token
                await process.WaitForExitAsync(cancellationToken);
                await errorReaderTask; // Wait for output reading to finish

                if (process.ExitCode != 0)
                {
                    Log($"FFmpeg finished with an error. Exit code: {process.ExitCode}");
                    throw new Exception($"FFmpeg failed with exit code {process.ExitCode}. See log for details.");
                }
            }
            catch (OperationCanceledException)
            {
                if (process != null && !process.HasExited)
                {
                    try { process.Kill(); } catch { /* Ignore */ }
                }
                throw;
            }
            catch (Exception)
            {
                if (process != null && !process.HasExited)
                {
                    try { process.Kill(); } catch { /* Ignore */ }
                }
                throw;
            }
        }

        private void btnOpenDir_Click(object sender, EventArgs e)
        {
            string outputDirectory = txtOutputDirectory.Text;

            if (Directory.Exists(outputDirectory))
            {
                try
                {
                    Process.Start(new ProcessStartInfo()
                    {
                        FileName = outputDirectory,
                        UseShellExecute = true,
                        Verb = "open"
                    });
                }
                catch (Exception ex)
                {
                    Log($"Error opening directory: {ex.Message}");
                    MessageBox.Show($"Could not open directory: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                MessageBox.Show("Output directory does not exist.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Log($"Attempted to open non-existent directory: {outputDirectory}");
            }
        }
    }

    // Data model for storing stream options in the UI
    public class VideoQualityOption
    {
        public string DisplayText { get; set; } = string.Empty;
        public MuxedStreamInfo? MuxedStream { get; set; }
        public IVideoStreamInfo? VideoStream { get; set; }
        public IAudioStreamInfo? AudioStream { get; set; }
        public bool RequiresMuxing { get; set; }
        public bool IsAudioOnly { get; set; }
    }

    // Helper extension class for robust file name creation
    public static class StringExtensions
    {
        public static string GetSafeFileName(this string text)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = string.Join("_", text.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries)).Trim();
            return sanitized.Length > 100 ? sanitized.Substring(0, 100) : sanitized;
        }
    }
}