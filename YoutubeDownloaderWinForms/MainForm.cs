using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
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
        private List<MuxedStreamInfo> _muxedStreams; // To store available streams
        private CancellationTokenSource _cancellationTokenSource; // For download cancellation

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
                txtLog.AppendText($"{message}{Environment.NewLine}");
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
            _muxedStreams = null;
            UpdateProgress(0);

            try
            {
                var video = await _youtubeClient.Videos.GetAsync(videoUrl);
                var streamManifest = await _youtubeClient.Videos.Streams.GetManifestAsync(video.Id);

                _muxedStreams = streamManifest.GetMuxedStreams()
                                             .Where(s => s.Container == YoutubeExplode.Videos.Streams.Container.Mp4)
                                             .OrderByDescending(s => s.VideoQuality)
                                             .ToList();

                if (_muxedStreams.Any())
                {
                    Log($"Video found: {video.Title}");
                    Log("Available muxed streams (video + audio):");
                    int index = 0;
                    foreach (var streamInfo in _muxedStreams)
                    {
                        double fileSizeMb = streamInfo.Size.Bytes / (1024.0 * 1024.0);
                        lbxQualities.Items.Add($"[{index}] {streamInfo.VideoQuality.Label} ({streamInfo.Container.Name}) {fileSizeMb:F2} MB");
                        index++;
                    }
                    lbxQualities.SelectedIndex = 0; // Select highest quality by default
                    btnDownload.Enabled = true;
                }
                else
                {
                    Log($"Video found: {video.Title}");
                    Log("No muxed streams found. Separate video and audio streams will be downloaded.");
                    // In this case, we don't provide a choice, just allow download
                    btnDownload.Enabled = true;
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

            // Disable buttons during download
            btnDownload.Enabled = false;
            btnGetInfo.Enabled = false;
            btnBrowseDirectory.Enabled = false;
            txtVideoUrl.Enabled = false;
            lbxQualities.Enabled = false;
            UpdateProgress(0);
            txtLog.Clear();
            Log("Download started...");

            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                await DownloadYouTubeVideo(videoUrl, outputDirectory, _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                Log("Download canceled.");
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
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        // You can add a "Cancel" button
        private void btnCancel_Click(object sender, EventArgs e)
        {
            _cancellationTokenSource?.Cancel();
        }

        private async Task DownloadYouTubeVideo(string videoUrl, string outputDirectory, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var video = await _youtubeClient.Videos.GetAsync(videoUrl);
            string sanitizedTitle = video.Title.GetSafeFileName();
            var streamManifest = await _youtubeClient.Videos.Streams.GetManifestAsync(video.Id);

            if (_muxedStreams != null && _muxedStreams.Any()) // If we have already retrieved muxed streams
            {
                MuxedStreamInfo streamToDownload;
                if (lbxQualities.SelectedIndex != -1)
                {
                    streamToDownload = _muxedStreams[lbxQualities.SelectedIndex];
                }
                else
                {
                    streamToDownload = _muxedStreams.First(); // Highest quality by default
                    Log($"No quality selected. Downloading video with the highest quality ({streamToDownload.VideoQuality.Label}).");
                }

                var progress = new Progress<double>(p =>
                {
                    // Update ProgressBar
                    UpdateProgress((int)(p * 100));
                });

                string outputFilePath = Path.Combine(outputDirectory, $"{sanitizedTitle}_{streamToDownload.VideoQuality.Label}.{streamToDownload.Container}");

                Log($"Download of muxed stream ({streamToDownload.VideoQuality.Label}) started to: {outputFilePath}");
                await _youtubeClient.Videos.Streams.DownloadAsync(streamToDownload, outputFilePath, progress, cancellationToken);

                Log("Download successfully completed!");
                Log($"Video saved as: {outputFilePath}");
            }
            else // Separate video and audio streams (requires FFmpeg)
            {
                Log("No muxed streams found. Downloading separate video and audio streams...");
                Log("This video has separate video and audio streams. Automatic merging with FFmpeg.");

                var videoStreamInfo = streamManifest.GetVideoStreams()
                    .Where(s => s.Container == YoutubeExplode.Videos.Streams.Container.Mp4)
                    .OrderByDescending(s => s.VideoQuality)
                    .FirstOrDefault();

                var audioStreamInfo = streamManifest.GetAudioStreams()
                    .Where(s => s.Container == YoutubeExplode.Videos.Streams.Container.Mp4)
                    .OrderByDescending(s => s.Bitrate)
                    .FirstOrDefault();

                if (videoStreamInfo != null && audioStreamInfo != null)
                {
                    var progress = new Progress<double>(p =>
                    {
                        // Update ProgressBar
                        UpdateProgress((int)(p * 100));
                    });

                    // Use unique temp names to prevent conflicts
                    string tempVideoFilePath = Path.Combine(outputDirectory, $"{Guid.NewGuid():N}_video_temp.mp4");
                    string tempAudioFilePath = Path.Combine(outputDirectory, $"{Guid.NewGuid():N}_audio_temp.mp4");
                    string combinedFilePath = Path.Combine(outputDirectory, $"{Guid.NewGuid():N}_combined_temp.mp4");
                    string finalFilePath = Path.Combine(outputDirectory, $"{sanitizedTitle}.mp4");

                    try
                    {
                        Log($"Downloading video stream to: {tempVideoFilePath}");
                        await _youtubeClient.Videos.Streams.DownloadAsync(videoStreamInfo, tempVideoFilePath, progress, cancellationToken);

                        Log($"Downloading audio stream to: {tempAudioFilePath}");
                        await _youtubeClient.Videos.Streams.DownloadAsync(audioStreamInfo, tempAudioFilePath, progress, cancellationToken);

                        Log("Both streams downloaded. Merging them using FFmpeg...");
                        UpdateProgress(50); // Progress placeholder for merging

                        var ffmpegArguments = $"-i \"{tempVideoFilePath}\" -i \"{tempAudioFilePath}\" -c copy \"{combinedFilePath}\"";
                        await RunFFmpegProcess(ffmpegArguments, cancellationToken);

                        if (File.Exists(combinedFilePath))
                        {
                            Log("Merging complete. Renaming file...");
                            if (File.Exists(finalFilePath))
                            {
                                File.Delete(finalFilePath);
                            }
                            File.Move(combinedFilePath, finalFilePath);
                            Log($"Final video saved as: {finalFilePath}");
                            UpdateProgress(100);
                        }
                        else
                        {
                            Log("Merging failed. Final file was not created.");
                        }
                    }
                    finally
                    {
                        // Cleanup temporary files
                        try 
                        { 
                            if (File.Exists(tempVideoFilePath)) 
                                File.Delete(tempVideoFilePath); 
                        } catch (Exception ex) 
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
                        
                        try 
                        { 
                            if (File.Exists(combinedFilePath)) 
                                File.Delete(combinedFilePath); 
                        } 
                        catch (Exception ex) 
                        { 
                            Log($"Error deleting {combinedFilePath}: {ex.Message}"); 
                        }
                    }
                }
                else
                {
                    Log($"No suitable separate video or audio streams found for {video.Title}.");
                }
            }
        }

        private async Task RunFFmpegProcess(string arguments, CancellationToken cancellationToken)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = FFmpegFileName,
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
                        if (line != null)
                        {
                            Log($"FFmpeg: {line}"); // Log FFmpeg output to the text field
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