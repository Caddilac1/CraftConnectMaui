using CraftConnect_Mobile_App.PageModels;
using System.Diagnostics;
namespace CraftConnect_Mobile_App.Pages
{
    public partial class ChatPage : ContentPage
    {
        private readonly ChatPageModel _viewModel;
        public ChatPage(ChatPageModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
            Debug.WriteLine("[CHAT PAGE] Constructor - Page initialized");
        }
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            Debug.WriteLine("[CHAT PAGE] OnAppearing");
            Debug.WriteLine($"[CHAT PAGE] GroupId: {_viewModel.GroupId}");
            Debug.WriteLine($"[CHAT PAGE] GroupName: {_viewModel.GroupName}");
            try
            {
                // Small delay to ensure page is fully loaded
                await Task.Delay(100);
                // Initialize the view model (loads messages)
                await _viewModel.InitializeAsync();
                Debug.WriteLine("[CHAT PAGE] ✅ Initialization complete");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE] ❌ Error in OnAppearing: {ex.Message}");
                Debug.WriteLine($"[CHAT PAGE] StackTrace: {ex.StackTrace}");
                await DisplayAlert("Error",
                    $"Failed to load chat: {ex.Message}",
                    "OK");
            }
        }
        // Back button handler
        private async void OnBackButtonTapped(object sender, EventArgs e)
        {
            Debug.WriteLine("[CHAT PAGE] Back button tapped");
            await Shell.Current.GoToAsync("..");
        }

        // Emoji button handler
        private void OnEmojiButtonTapped(object sender, EventArgs e)
        {
            Debug.WriteLine("[CHAT PAGE] Emoji button tapped");
            // TODO: Implement emoji picker functionality
            // You can show an emoji picker or use a third-party library
        }

        // Attachment button handler
        private async void OnAttachmentButtonTapped(object sender, EventArgs e)
        {
            Debug.WriteLine("[CHAT PAGE] Attachment button tapped");
            try
            {
                // TODO: Implement file attachment functionality
                // Example: Use FilePicker to select files
                // var result = await FilePicker.PickAsync();
                // if (result != null)
                // {
                //     await _viewModel.SendAttachmentAsync(result);
                // }

                await DisplayAlert("Attachment", "Attachment feature coming soon!", "OK");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE] ❌ Error picking attachment: {ex.Message}");
                await DisplayAlert("Error", "Failed to pick attachment", "OK");
            }
        }

        // Camera button handler
        private async void OnCameraButtonTapped(object sender, EventArgs e)
        {
            Debug.WriteLine("[CHAT PAGE] Camera button tapped");
            try
            {
                // TODO: Implement camera functionality
                // Example: Use MediaPicker to take a photo
                // var photo = await MediaPicker.CapturePhotoAsync();
                // if (photo != null)
                // {
                //     await _viewModel.SendPhotoAsync(photo);
                // }

                await DisplayAlert("Camera", "Camera feature coming soon!", "OK");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE] ❌ Error opening camera: {ex.Message}");
                await DisplayAlert("Error", "Failed to open camera", "OK");
            }
        }
    }
}