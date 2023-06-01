using CommunityToolkit.Mvvm.Input;
using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Prediction.Models;
using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Prediction;
using Microsoft.Maui.Graphics.Platform;
using System.Diagnostics;
using System.Windows.Input;
using System.ComponentModel;

namespace Trashify
{
    internal class MainPageViewModel : INotifyPropertyChanged
    {
        private const int MaxBiteSizeImage = 4194304;
        private const int MaxResImage = 1024;

       
        public event PropertyChangedEventHandler PropertyChanged;


        public ICommand ChoosePhoto { get; }
        public ICommand TakePhoto { get; }
        private Task ChoosePhotoAction() => Analysis(false);
        private Task TakePhotoAction() => Analysis(true);


        /* Percentage : Label to display the percentage */
        private string percentage;
        public string Percentage
        {
            get { return percentage; }
            private set
            {
                if (percentage != value)
                {
                    percentage = value;
                    OnChanged(nameof(Percentage));
                }
            }
        }

        /* FirstPageVisible : false -> the page 2 is displaying*/
        private bool firstPageVisible;
        public bool FirstPageVisible
        {
            get { return firstPageVisible; }
            private set
            {
                if (firstPageVisible != value)
                {
                    firstPageVisible = value;
                    OnChanged(nameof(FirstPageVisible));
                }
            }
        }

        /* SecondPageVisible : false -> the page 1 is displaying*/
        private bool secondPageVisible;
        public bool SecondPageVisible
        {
            get { return secondPageVisible; }
            private set
            {
                if (secondPageVisible != value)
                {
                    secondPageVisible = value;
                    OnChanged(nameof(SecondPageVisible));
                }
            }
        }

        /* BinLabel : Label to display the name of the bin*/
        private string binLabel;
        public string BinLabel
        {
            get { return binLabel; }
            private set
            {
                if (binLabel != value)
                {
                    binLabel = value;
                    OnChanged(nameof(BinLabel));
                }
            }
        }

        /* BinColor : Label to display the color of the bin*/
        private Color binColor;
        public Color BinColor
        {
            get { return binColor; }
            private set
            {
                if (binColor != value)
                {
                    binColor = value;
                    OnChanged(nameof(BinColor));
                }
            }
        }

        /* Photo : the photo of the waste*/
        private ImageSource photo;
        public ImageSource Photo
        {
            get { return photo; }
            set
            {
                if (photo != value)
                {
                    photo = value;
                    OnChanged(nameof(Photo));
                }
            }
        }

        /* BinLogo : the logo of the bin*/
        private ImageSource binLogo;
        public ImageSource BinLogo
        {
            get { return binLogo; }
            set
            {
                if (binLogo != value)
                {
                    binLogo = value;
                    OnChanged(nameof(BinLogo));
                }
            }
        }

        /* when he analyze the photo*/ 
        private bool isRunning;
        public bool IsRunning
        {
            get { return isRunning; }
            set
            {
                if (isRunning != value)
                {
                    isRunning = value;
                    OnChanged(nameof(IsRunning));
                }
            }
        }

        /* Constructor : intitialize*/
        public MainPageViewModel()
        {
            ChoosePhoto = new AsyncRelayCommand(ChoosePhotoAction);
            TakePhoto = new AsyncRelayCommand(TakePhotoAction);
            Photo = null;
            Percentage = string.Empty;
            IsRunning = false;
            BinLabel = string.Empty;
            BinLogo = null;
            BinColor = null;
            FirstPageVisible = true;
            SecondPageVisible = false;
        }

        
        private void OnChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }


        private async Task Analysis(bool useCamera)
        {
            // recover photo either from files or directly from camera
            var photo = useCamera
              ? await MediaPicker.Default.CapturePhotoAsync()
              : await MediaPicker.Default.PickPhotoAsync();


            if (photo is { })
            {
                // change the visibility of the pages
                SecondPageVisible = true;
                FirstPageVisible = false;

                // Resize the photo to 4MB
                var resizedPhoto = await StreamResize(photo);

                // Call the Custom Vision API
                var result = await Classification(new MemoryStream(resizedPhoto));

                // Percentage 0.10 to 10% for example
                var percent = result.Probability.ToString("P1");

                // add the photo to the variable, to add on the screen
                Photo = ImageSource.FromStream(() => new MemoryStream(resizedPhoto));

                
                if (result.TagName == null)
                {
                    Percentage = "The API call did not work. Try again later.";
                }
                else
                {
                    Percentage = result.TagName.Equals("Negative")
                      ? "The object you scanned is not present in the database."
                      : $"It looks {percent} a {result.TagName}.";

                    if(result.TagName == "carton" || result.TagName == "newspaper" || result.TagName == "magazine")
                    {
                        BinLogo = "papir_karton.png";
                        BinColor = Color.FromHex("#FE4D93");
                        BinLabel = "PAPIR \n KARTON";
                    }
                    else if(result.TagName == "plastic bottle")
                    {
                        BinLogo = "metal_hard_plast_glas.png";
                        BinColor = Color.FromHex("#00B3BO");
                        BinLabel = "METAL \n HARD PLAST \n GLAS";
                    }
                    else if (result.TagName == "eggs")
                    {
                        BinLogo = "mad_affald.png";
                        BinColor = Color.FromHex("#A28F01");
                        BinLabel = "MAD-\nAFFALD";
                    }
                    else
                    {
                        BinLogo = "restaffald_til_for_braending.png";
                        BinColor = Color.FromHex("#FE7228");
                        BinLabel = "RESTAFFALD \n TIL FOR-\n BRAENDING";
                    }

                }
            }
        }

        /* resize the photo */
        private async Task<byte[]> StreamResize(FileResult photo)
        {
            byte[] result = null;

            using (var stream = await photo.OpenReadAsync())
            {
                if (stream.Length > MaxBiteSizeImage)
                {
                    var image = PlatformImage.FromStream(stream);
                    if (image != null)
                    {
                        var newImage = image.Downsize(MaxResImage, true);
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

        /* send the photo to custom vision and return the result */
        private async Task<PredictionModel> Classification(Stream photoStream)
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
