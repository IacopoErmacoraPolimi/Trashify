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

namespace Trashify
{
    internal class MainPageViewModel
    {
        private const int ImageMaxSizeBytes = 4194304;
        private const int ImageMaxResolution = 1024;

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
        public ImageSource Photo { get; private set; }
        public string OutputLabel { get; private set; }
        public bool IsRunning { get; private set; }

        private Task ExecutePickPhoto() => ProcessPhotoAsync(false);

        private Task ExecuteTakePhoto() => ProcessPhotoAsync(true);

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

                // Change the percentage notation from 0.9 to display 90.0%
                var percent = result.Probability.ToString("P1");

                Photo = ImageSource.FromStream(() => new MemoryStream(resizedPhoto));

                OutputLabel = result.TagName.Equals("Negative")
                  ? "This is not in the Database."
                  : $"It looks {percent} a {result.TagName}.";
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
                return new PredictionModel();
            }
            finally
            {
                IsRunning = false;
            }
        }

    }
}
