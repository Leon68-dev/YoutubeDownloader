using YoutubeDownloaderMobile.Models;
using YoutubeDownloaderMobile.ViewModels;

namespace YoutubeDownloaderMobile.Views;

public partial class Home : ContentPage
{
    private readonly HomeViewModel _viewModel;

	public Home()
	{
		InitializeComponent();
        _viewModel = (HomeViewModel)BindingContext;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
    }

    private void downloadButton_Clicked(object sender, EventArgs e)
    {
        Button? button = sender as Button;
        if (button != null)
        {
            var v = button.BindingContext as DownloadData;
            if (v != null)
                _ = _viewModel.downloadFile(v.index);
        }
    }

}
