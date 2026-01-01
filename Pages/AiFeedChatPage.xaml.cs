using CraftConnect_Mobile_App.PageModels;
using System.Diagnostics;

namespace CraftConnect_Mobile_App.Pages
{
    public partial class AiFeedChatPage : ContentPage
    {
        private readonly AiFeedChatPageModel _viewModel;

        public AiFeedChatPage(AiFeedChatPageModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
            Debug.WriteLine("[AI CHAT PAGE] Constructor - Page initialized");
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            Debug.WriteLine("[AI CHAT PAGE] OnAppearing");

            try
            {
                await Task.Delay(100);
                await _viewModel.InitializeAsync();
                Debug.WriteLine("[AI CHAT PAGE] ✅ Initialization complete");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AI CHAT PAGE] ❌ Error: {ex.Message}");
                await DisplayAlert("Error", $"Failed to initialize: {ex.Message}", "OK");
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            Debug.WriteLine("[AI CHAT PAGE] OnDisappearing");
        }

        private async void OnBackButtonTapped(object sender, EventArgs e)
        {
            Debug.WriteLine("[AI CHAT PAGE] Back button tapped");

            var confirm = await DisplayAlert(
                "Exit Chat",
                "Are you sure you want to exit? Your progress will be saved.",
                "Yes",
                "No");

            if (confirm)
            {
                await Shell.Current.GoToAsync("..");
            }
        }

        private void OnEmojiButtonTapped(object sender, EventArgs e)
        {
            Debug.WriteLine("[AI CHAT PAGE] Emoji button tapped");
            DisplayAlert("Info", "Emoji picker coming soon!", "OK");
        }

        private async void OnAttachmentButtonTapped(object sender, EventArgs e)
        {
            Debug.WriteLine("[AI CHAT PAGE] Attachment button tapped");

            // Show the WhatsApp-style bottom sheet
            AttachmentOverlay.IsVisible = true;

            // Animate the bottom sheet sliding up
            var sheet = AttachmentOverlay.Children[1] as VerticalStackLayout;
            if (sheet != null)
            {
                sheet.TranslationY = 300;
                await sheet.TranslateTo(0, 0, 250, Easing.CubicOut);
            }
        }

        private async void OnDismissAttachmentSheet(object sender, EventArgs e)
        {
            await HideAttachmentSheet();
        }

        private async Task HideAttachmentSheet()
        {
            var sheet = AttachmentOverlay.Children[1] as VerticalStackLayout;
            if (sheet != null)
            {
                await sheet.TranslateTo(0, 300, 200, Easing.CubicIn);
            }
            AttachmentOverlay.IsVisible = false;
        }

        private async void OnDocumentTapped(object sender, EventArgs e)
        {
            await HideAttachmentSheet();
            await PickDocument("document");
        }

        private async void OnInvoiceTapped(object sender, EventArgs e)
        {
            await HideAttachmentSheet();
            await PickDocument("invoice");
        }

        private async void OnCameraTapped(object sender, EventArgs e)
        {
            await HideAttachmentSheet();
            await TakePhoto();
        }

        private async void OnGalleryTapped(object sender, EventArgs e)
        {
            await HideAttachmentSheet();
            await PickPhoto();
        }

        private async Task PickDocument(string fileType)
        {
            try
            {
                var customFileType = new FilePickerFileType(
                    new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.iOS, new[] { "public.pdf", "public.image", "public.data" } },
                        { DevicePlatform.Android, new[] { "application/pdf", "image/*", "*/*" } },
                        { DevicePlatform.WinUI, new[] { ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx" } }
                    }
                );

                var options = new PickOptions
                {
                    FileTypes = customFileType,
                    PickerTitle = $"Select {fileType}"
                };

                var file = await FilePicker.Default.PickAsync(options);

                if (file != null)
                {
                    await _viewModel.AttachFile(file, fileType);
                    Debug.WriteLine($"[AI CHAT PAGE] ✅ File attached: {file.FileName}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AI CHAT PAGE] ❌ Error picking file: {ex.Message}");
                await DisplayAlert("Error", "Failed to attach file. Please try again.", "OK");
            }
        }

        private async Task TakePhoto()
        {
            try
            {
                if (MediaPicker.Default.IsCaptureSupported)
                {
                    var file = await MediaPicker.Default.CapturePhotoAsync();

                    if (file != null)
                    {
                        await _viewModel.AttachFile(file, "photo");
                        Debug.WriteLine($"[AI CHAT PAGE] ✅ Photo captured: {file.FileName}");
                    }
                }
                else
                {
                    await DisplayAlert("Not Supported", "Camera is not available on this device", "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AI CHAT PAGE] ❌ Error taking photo: {ex.Message}");
                await DisplayAlert("Error", "Failed to capture photo. Please try again.", "OK");
            }
        }

        private async Task PickPhoto()
        {
            try
            {
                var file = await MediaPicker.Default.PickPhotoAsync();

                if (file != null)
                {
                    await _viewModel.AttachFile(file, "photo");
                    Debug.WriteLine($"[AI CHAT PAGE] ✅ Photo selected: {file.FileName}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AI CHAT PAGE] ❌ Error picking photo: {ex.Message}");
                await DisplayAlert("Error", "Failed to select photo. Please try again.", "OK");
            }
        }
    }
}