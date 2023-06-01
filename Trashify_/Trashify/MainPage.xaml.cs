using Microsoft.Maui;
using Plugin.Maui.Audio;
using System.ComponentModel;
using System.Diagnostics;

namespace Trashify;

public partial class MainPage : ContentPage
{
    private readonly IAudioManager audioManager;
    private MainPageViewModel viewModel;
    public MainPage(IAudioManager audioManager)
	{
		InitializeComponent();
        this.audioManager = audioManager;
        viewModel = new MainPageViewModel();
        BindingContext = viewModel;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private async void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainPageViewModel.BinLabel))
        {
            string soundName;

            Debug.WriteLine("*********************************************************************" + viewModel.BinLabel);
            switch (viewModel.BinLabel)
            {
                case "PAPIR \n KARTON":
                    soundName = "A.mp3";
                    break;
                case "METAL \n HARD PLAST \n GLAS":
                    soundName = "B.mp3";
                    break;
                case "MAD-\nAFFALD":
                    soundName = "C.mp3";
                    break;
                case "RESTAFFALD \n TIL FOR-\n BRAENDING":
                    soundName = "D.mp3";
                    break;
                default:
                    soundName = "test.mp3";
                    break;
            }

            var player = audioManager.CreatePlayer(await FileSystem.OpenAppPackageFileAsync("A.mp3"));
            player.Play();


        }
    }
}

