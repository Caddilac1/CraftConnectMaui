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
            // Optional: Implement emoji picker
            DisplayAlert("Info", "Emoji picker coming soon!", "OK");
        }

        private async void OnAttachmentButtonTapped(object sender, EventArgs e)
        {
            Debug.WriteLine("[AI CHAT PAGE] Attachment button tapped");

            try
            {
                var action = await DisplayActionSheet(
                    "Attach File",
                    "Cancel",
                    null,
                    "Invoice",
                    "Document",
                    "Photo"
                );

                if (action == "Cancel" || action == null)
                    return;

                FileResult? file = null;

                if (action == "Photo")
                {
                    // Option 1: Take photo
                    var photoAction = await DisplayActionSheet(
                        "Photo",
                        "Cancel",
                        null,
                        "Take Photo",
                        "Choose from Gallery"
                    );

                    if (photoAction == "Take Photo")
                    {
                        if (MediaPicker.Default.IsCaptureSupported)
                        {
                            file = await MediaPicker.Default.CapturePhotoAsync();
                        }
                        else
                        {
                            await DisplayAlert("Not Supported", "Camera is not available on this device", "OK");
                            return;
                        }
                    }
                    else if (photoAction == "Choose from Gallery")
                    {
                        file = await MediaPicker.Default.PickPhotoAsync();
                    }

                    if (file != null)
                    {
                        await _viewModel.AttachFile(file, "photo");
                    }
                }
                else if (action == "Invoice" || action == "Document")
                {
                    // Pick document file
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
                        PickerTitle = $"Select {action}"
                    };

                    file = await FilePicker.Default.PickAsync(options);

                    if (file != null)
                    {
                        var fileType = action.ToLower();
                        await _viewModel.AttachFile(file, fileType);
                        Debug.WriteLine($"[AI CHAT PAGE] ✅ File attached: {file.FileName}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AI CHAT PAGE] ❌ Error picking file: {ex.Message}");
                await DisplayAlert("Error", "Failed to attach file. Please try again.", "OK");
            }
        }
    }
}