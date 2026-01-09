using System.Diagnostics;

namespace CraftConnect_Mobile_App.Pages
{
    [QueryProperty(nameof(ImagePath), nameof(ImagePath))]
    [QueryProperty(nameof(FileName), nameof(FileName))]
    public partial class ImageViewerPage : ContentPage
    {
        private string _imagePath;
        private string _fileName;
        private double _currentScale = 1;
        private double _startScale = 1;

        public string ImagePath
        {
            get => _imagePath;
            set
            {
                _imagePath = value;
                LoadImage();
            }
        }

        public string FileName
        {
            get => _fileName;
            set
            {
                _fileName = value;
                if (FileNameLabel != null)
                {
                    FileNameLabel.Text = value ?? "Image";
                }
            }
        }

        public ImageViewerPage()
        {
            InitializeComponent();
        }

        private void LoadImage()
        {
            try
            {
                Debug.WriteLine($"[IMAGE VIEWER] Loading image from: {ImagePath}");

                if (string.IsNullOrEmpty(ImagePath) || !File.Exists(ImagePath))
                {
                    Debug.WriteLine("[IMAGE VIEWER] ❌ File not found");
                    DisplayAlert("Error", "Image file not found", "OK");
                    return;
                }

                LoadingIndicator.IsRunning = true;
                LoadingIndicator.IsVisible = true;

                // Load from local file
                ImageDisplay.Source = ImageSource.FromFile(ImagePath);

                LoadingIndicator.IsRunning = false;
                LoadingIndicator.IsVisible = false;

                Debug.WriteLine("[IMAGE VIEWER] ✅ Image loaded successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[IMAGE VIEWER] ❌ Error loading image: {ex.Message}");
                LoadingIndicator.IsRunning = false;
                LoadingIndicator.IsVisible = false;

                DisplayAlert("Error", "Failed to load image", "OK");
            }
        }

        private async void OnCloseClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        private async void OnMenuClicked(object sender, EventArgs e)
        {
            var action = await DisplayActionSheet(
                "Options",
                "Cancel",
                null,
                "Share");

            if (action == "Share")
            {
                await ShareImage();
            }
        }

        private void OnImageTapped(object sender, EventArgs e)
        {
            // Toggle top bar visibility
            TopBar.IsVisible = !TopBar.IsVisible;
        }

        private void OnPinchUpdated(object sender, PinchGestureUpdatedEventArgs e)
        {
            if (e.Status == GestureStatus.Started)
            {
                _startScale = _currentScale;
            }

            if (e.Status == GestureStatus.Running)
            {
                _currentScale = Math.Max(1, _startScale * e.Scale);
                ImageDisplay.Scale = _currentScale;
            }

            if (e.Status == GestureStatus.Completed)
            {
                // Reset if zoomed out too much
                if (_currentScale < 1)
                {
                    _currentScale = 1;
                    ImageDisplay.Scale = 1;
                }
            }
        }

        private async Task ShareImage()
        {
            try
            {
                Debug.WriteLine("[IMAGE VIEWER] Sharing image...");

                if (string.IsNullOrEmpty(ImagePath) || !File.Exists(ImagePath))
                {
                    await DisplayAlert("Error", "Image file not found", "OK");
                    return;
                }

                await Share.RequestAsync(new ShareFileRequest
                {
                    Title = "Share Image",
                    File = new ShareFile(ImagePath)
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[IMAGE VIEWER] ❌ Share error: {ex.Message}");
                await DisplayAlert("Error", "Failed to share image", "OK");
            }
        }
    }
}