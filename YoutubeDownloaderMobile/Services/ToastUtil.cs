using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;

namespace YoutubeDownloaderMobile.Services
{
    public class ToastUtil
    {
        public ToastUtil() { }

        public static async void show(string message)
        {
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            ToastDuration duration = ToastDuration.Short;
            double fontSize = 14;

            var toast = Toast.Make(message, duration, fontSize);
            await toast.Show(cancellationTokenSource.Token);
        }

    }
}
