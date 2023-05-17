using CommunityToolkit.Mvvm.Input;
using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Prediction.Models;
using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Prediction;
using Microsoft.Maui.Graphics.Platform;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.ComponentModel;

namespace Trashify
{
    internal class MainPageViewModel : INotifyPropertyChanged
    {
        private const int ImageMaxSizeBytes = 4194304;
        private const int ImageMaxResolution = 1024;

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public MainPageViewModel()
        {
            PickPhotoCommand = new AsyncRelayCommand(ExecutePickPhoto);
            TakePhotoCommand = new AsyncRelayCommand(ExecuteTakePhoto);
            Photo = null;
            OutputLabel = string.Empty;
            IsRunning = false;
        }

        public ICommand PickPhotoCommand { get; }

        public ICommand TakePhotoCommand { get; }

        private Task ExecutePickPhoto() => ProcessPhotoAsync(false);

        private Task ExecuteTakePhoto() => ProcessPhotoAsync(true);

        private string outputLabel;
        public string OutputLabel
        {
            get { return outputLabel; }
            private set
            {
                if (outputLabel != value)
                {
                    outputLabel = value;
                    OnPropertyChanged(nameof(OutputLabel));
                }
            }
        }

        private ImageSource photo;
        public ImageSource Photo
        {
            get { return photo; }
            set
            {
                if (photo != value)
                {
                    photo = value;
                    OnPropertyChanged(nameof(Photo));
                }
            }
        }

        private bool isRunning;
        public bool IsRunning
        {
            get { return isRunning; }
            set
            {
                if (isRunning != value)
                {
                    isRunning = value;
                    OnPropertyChanged(nameof(IsRunning));
                }
            }
        }

        private async Task ProcessPhotoAsync(bool useCamera)
        {
            var photo = useCamera
              ? await MediaPicker.Default.CapturePhotoAsync()
              : await MediaPicker.Default.PickPhotoAsync();

            if (photo is { })
            {
                // Resize to allowed size - 4MB
                var resizedPhoto = await ResizePhotoStream(photo);

                // Custom Vision API call
                var result = await ClassifyImage(new MemoryStream(resizedPhoto));

                // result = Microsoft.Azure.CognitiveServices.Vision.CustomVision.Prediction.Models.PredictionModel
                Debug.WriteLine("************************************************    result :   " + result + "    *****************************\n");

                // Change the percentage notation from 0.9 to display 90.0%
                var percent = result.Probability.ToString("P1");

                Photo = ImageSource.FromStream(() => new MemoryStream(resizedPhoto));

                Debug.WriteLine("************************************************    result.TagType :   " + result.TagType + "   *****************************\n");
                Debug.WriteLine("************************************************    result.TagName :   " + result.TagName + "   *****************************\n");
                Debug.WriteLine("************************************************    result.TagId :   " + result.TagId + "   *****************************\n");

                if (result.TagName == null)
                {
                    OutputLabel = "For some reason it returns null";
                }
                else
                {
                    OutputLabel = result.TagName.Equals("Negative")
                      ? "This is not in the Database."
                      : $"It looks {percent} a {result.TagName}.";
                }
            }
        }

        private async Task<byte[]> ResizePhotoStream(FileResult photo)
        {
            byte[] result = null;

            using (var stream = await photo.OpenReadAsync())
            {
                if (stream.Length > ImageMaxSizeBytes)
                {
                    var image = PlatformImage.FromStream(stream);
                    if (image != null)
                    {
                        var newImage = image.Downsize(ImageMaxResolution, true);
                        result = newImage.AsBytes();
                    }
                }
                else
                {
                    using (var binaryReader = new BinaryReader(stream))
                    {
                        result = binaryReader.ReadBytes((int)stream.Length);
                    }
                }
            }

            return result;
        }

        private async Task<PredictionModel> ClassifyImage(Stream photoStream)
        {

            try
            {
                IsRunning = true;

                var endpoint = new CustomVisionPredictionClient(new ApiKeyServiceClientCredentials(ApiKeys.PredictionKey))
                {
                    Endpoint = ApiKeys.CustomVisionEndPoint
                };

                // Send image to the Custom Vision API
                var results = await endpoint.ClassifyImageAsync(Guid.Parse(ApiKeys.ProjectId), ApiKeys.PublishedName, photoStream);

                // Return the most likely prediction
                return results.Predictions?.OrderByDescending(x => x.Probability).FirstOrDefault();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                Debug.WriteLine("--------------------------------------------- message : " +  ex.Message + "  ----------------------");
                return new PredictionModel();
            }
            finally
            {
                IsRunning = false;
            }
        }

    }
}