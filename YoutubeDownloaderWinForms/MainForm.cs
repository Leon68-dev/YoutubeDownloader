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
        private List<MuxedStreamInfo> _muxedStreams; // Для зберігання доступних потоків
        private CancellationTokenSource _cancellationTokenSource; // Для скасування завантаження

        public MainForm()
        {
            InitializeComponent();
            _youtubeClient = new YoutubeClient();
            InitializeUI();
        }

        private void InitializeUI()
        {
            // Встановіть початкові значення або налаштування для елементів UI
            txtOutputDirectory.Text = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos); // Шлях за замовчуванням
            progressBar.Minimum = 0;
            progressBar.Maximum = 100;
            progressBar.Value = 0;
            txtLog.ReadOnly = true;
            btnDownload.Enabled = false; // Деактивуємо кнопку завантаження, доки не буде обрано потік
        }

        private void Log(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => Log(message)));
                return;
            }
            txtLog.AppendText($"{message}{Environment.NewLine}");
        }

        private void UpdateProgress(int percentage)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateProgress(percentage)));
                return;
            }
            progressBar.Value = percentage;
        }

        private async void btnBrowseDirectory_Click(object sender, EventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                fbd.SelectedPath = txtOutputDirectory.Text;
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    txtOutputDirectory.Text = fbd.SelectedPath;
                }
            }
        }

        private async void btnGetInfo_Click(object sender, EventArgs e) // Нова кнопка для отримання інформації про відео
        {
            string videoUrl = txtVideoUrl.Text.Trim();
            if (string.IsNullOrWhiteSpace(videoUrl))
            {
                MessageBox.Show("Будь ласка, введіть URL відео YouTube.", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Log("Отримання інформації про відео...");
            btnGetInfo.Enabled = false;
            btnDownload.Enabled = false;
            lbxQualities.Items.Clear();
            _muxedStreams = null;

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
                    Log("Доступні муксовані потоки (відео + аудіо):");
                    int index = 0;
                    foreach (var streamInfo in _muxedStreams)
                    {
                        double fileSizeMb = streamInfo.Size.Bytes / (1024.0 * 1024.0);
                        lbxQualities.Items.Add($"[{index}] {streamInfo.VideoQuality.Label} ({streamInfo.Container.Name}) {fileSizeMb:F2} MB");
                        index++;
                    }
                    lbxQualities.SelectedIndex = 0; // Вибираємо найвищу якість за замовчуванням
                    btnDownload.Enabled = true;
                }
                else
                {
                    Log("Муксовані потоки не знайдено. Буде завантажено окремі потоки відео та аудіо.");
                    // У цьому випадку ми не надаємо вибір, а просто дозволяємо завантаження
                    btnDownload.Enabled = true;
                }
            }
            catch (Exception ex)
            {
                Log($"Помилка при отриманні інформації про відео: {ex.Message}");
                MessageBox.Show($"Помилка: {ex.Message}", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                MessageBox.Show("Будь ласка, вкажіть дійсну директорію для збереження.", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(videoUrl))
            {
                MessageBox.Show("Будь ласка, введіть URL відео YouTube.", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Деактивуємо кнопки під час завантаження
            btnDownload.Enabled = false;
            btnGetInfo.Enabled = false;
            btnBrowseDirectory.Enabled = false;
            txtVideoUrl.Enabled = false;
            lbxQualities.Enabled = false;
            UpdateProgress(0);
            txtLog.Clear();
            Log("Завантаження розпочато...");

            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                await DownloadYouTubeVideo(videoUrl, outputDirectory, _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                Log("Завантаження скасовано.");
            }
            catch (Exception ex)
            {
                Log($"Сталася помилка під час завантаження відео: {ex.Message}");
                MessageBox.Show($"Помилка: {ex.Message}", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                // Активуємо кнопки після завершення/скасування
                btnDownload.Enabled = true;
                btnGetInfo.Enabled = true;
                btnBrowseDirectory.Enabled = true;
                txtVideoUrl.Enabled = true;
                lbxQualities.Enabled = true;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        // Можна додати кнопку "Скасувати"
        private void btnCancel_Click(object sender, EventArgs e)
        {
            _cancellationTokenSource?.Cancel();
        }


        private async Task DownloadYouTubeVideo(string videoUrl, string outputDirectory, CancellationToken cancellationToken)
        {
            var video = await _youtubeClient.Videos.GetAsync(videoUrl);
            string sanitizedTitle = string.Join("_", video.Title.Split(Path.GetInvalidFileNameChars()));
            var streamManifest = await _youtubeClient.Videos.Streams.GetManifestAsync(video.Id);

            if (_muxedStreams != null && _muxedStreams.Any()) // Якщо ми вже отримали муксовані потоки
            {
                MuxedStreamInfo streamToDownload;
                if (lbxQualities.SelectedIndex != -1)
                {
                    streamToDownload = _muxedStreams[lbxQualities.SelectedIndex];
                }
                else
                {
                    streamToDownload = _muxedStreams.First(); // За замовчуванням найвища якість
                    Log($"Не вибрано якість. Завантажуємо відео з найвищою якістю ({streamToDownload.VideoQuality.Label}).");
                }

                var progress = new Progress<double>(p =>
                {
                    // Оновлення ProgressBar
                    UpdateProgress((int)(p * 100));
                });

                string outputFilePath = Path.Combine(outputDirectory, $"{sanitizedTitle}_{streamToDownload.VideoQuality.Label}.{streamToDownload.Container}");

                Log("Завантаження розпочато...");
                await _youtubeClient.Videos.Streams.DownloadAsync(streamToDownload, outputFilePath, progress, cancellationToken);

                Log("Завантаження успішно завершено!");
                Log($"Відео збережено як: {outputFilePath}");
            }
            else // Окремі потоки відео та аудіо
            {
                Log("Муксовані потоки не знайдено. Завантаження окремих потоків відео та аудіо...");
                Log("Це відео має окремі потоки відео та аудіо. Автоматичне об'єднання за допомогою FFmpeg.");

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
                        // Оновлення ProgressBar
                        UpdateProgress((int)(p * 100));
                    });

                    string tempVideoFilePath = Path.Combine(outputDirectory, $"video_temp.{videoStreamInfo.Container}");
                    string tempAudioFilePath = Path.Combine(outputDirectory, $"audio_temp.{audioStreamInfo.Container}");
                    string combinedFilePath = Path.Combine(outputDirectory, $"output_video.mp4");
                    string finalFilePath = Path.Combine(outputDirectory, $"{sanitizedTitle}.mp4");

                    Log($"Завантаження відео потоку до: {tempVideoFilePath}");
                    await _youtubeClient.Videos.Streams.DownloadAsync(videoStreamInfo, tempVideoFilePath, progress, cancellationToken);

                    Log($"Завантаження аудіо потоку до: {tempAudioFilePath}");
                    await _youtubeClient.Videos.Streams.DownloadAsync(audioStreamInfo, tempAudioFilePath, progress, cancellationToken);

                    Log("Обидва потоки завантажено. Об'єднання їх за допомогою FFmpeg...");

                    var ffmpegArguments = $"-i \"{tempVideoFilePath}\" -i \"{tempAudioFilePath}\" -c copy \"{combinedFilePath}\"";
                    await RunFFmpegProcess(ffmpegArguments, cancellationToken);

                    File.Delete(tempVideoFilePath);
                    File.Delete(tempAudioFilePath);

                    if (File.Exists(combinedFilePath))
                    {
                        Log("Об'єднання завершено. Перейменування файлу...");
                        File.Move(combinedFilePath, finalFilePath);
                        Log($"Фінальне відео збережено як: {finalFilePath}");
                    }
                    else
                    {
                        Log("Об'єднання не вдалося. Фінальний файл не було створено.");
                    }
                }
                else
                {
                    Log($"Не знайдено відповідних окремих потоків відео або аудіо для {video.Title}.");
                }
            }
        }

        private async Task RunFFmpegProcess(string arguments, CancellationToken cancellationToken)
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
                    Log("Не вдалося запустити FFmpeg. Переконайтеся, що він встановлений і знаходиться у вашому системному PATH.");
                    return;
                }

                // Асинхронне читання виводу FFmpeg
                var errorReaderTask = Task.Run(async () =>
                {
                    var errorReader = process.StandardError;
                    while (!errorReader.EndOfStream)
                    {
                        string? line = await errorReader.ReadLineAsync();
                        if (line != null)
                        {
                            Log($"FFmpeg: {line}"); // Виводимо логи FFmpeg у текстове поле
                        }
                    }
                }, cancellationToken);

                await process.WaitForExitAsync(cancellationToken);
                await errorReaderTask; // Дочекатися завершення читання виводу

                if (process.ExitCode != 0)
                {
                    Log($"FFmpeg завершився з помилкою. Код виходу: {process.ExitCode}");
                }
            }
        }

        private void txtLog_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
