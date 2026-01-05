using CraftConnect_Mobile_App.PageModels;
using System.Diagnostics;

namespace CraftConnect_Mobile_App.Pages
{
    public partial class ChatPage : ContentPage
    {
        private readonly ChatPageModel _viewModel;
        private bool _isInitialized = false;

        public ChatPage(ChatPageModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
            Debug.WriteLine("[CHAT PAGE] Constructor - Page initialized");

            // Subscribe to collection changes to auto-scroll
            _viewModel.Messages.CollectionChanged += OnMessagesCollectionChanged;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            Debug.WriteLine("[CHAT PAGE] OnAppearing");
            Debug.WriteLine($"[CHAT PAGE] GroupId: {_viewModel.GroupId}");
            Debug.WriteLine($"[CHAT PAGE] GroupName: {_viewModel.GroupName}");

            if (_isInitialized)
            {
                Debug.WriteLine("[CHAT PAGE] Already initialized, skipping");
                return;
            }

            try
            {
                await _viewModel.InitializeAsync();
                _isInitialized = true;

                // Scroll to bottom after loading messages
                await Task.Delay(300);
                ScrollToBottom(false);

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

        protected override async void OnDisappearing()
        {
            base.OnDisappearing();
            Debug.WriteLine("[CHAT PAGE] OnDisappearing");

            try
            {
                await _viewModel.CleanupAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE] ❌ Error in OnDisappearing: {ex.Message}");
            }
        }

        private void OnMessagesCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Auto-scroll when new messages are added
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await Task.Delay(100);
                    ScrollToBottom(true);
                });
            }
        }

        private async void OnBackButtonTapped(object sender, EventArgs e)
        {
            Debug.WriteLine("[CHAT PAGE] Back button tapped");
            await Shell.Current.GoToAsync("..");
        }

        private void OnEmojiButtonTapped(object sender, EventArgs e)
        {
            Debug.WriteLine("[CHAT PAGE] Emoji button tapped");
            // TODO: Implement emoji picker functionality
        }

        private async void OnAttachmentButtonTapped(object sender, EventArgs e)
        {
            Debug.WriteLine("[CHAT PAGE] Attachment button tapped");

            try
            {
                var result = await FilePicker.PickAsync(new PickOptions
                {
                    PickerTitle = "Select a file",
                    FileTypes = FilePickerFileType.Images
                });

                if (result != null)
                {
                    Debug.WriteLine($"[CHAT PAGE] File selected: {result.FileName}");
                    await DisplayAlert("File Selected",
                        $"{result.FileName} - Upload functionality coming soon!",
                        "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE] ❌ Error picking attachment: {ex.Message}");
                await DisplayAlert("Error", "Failed to pick attachment", "OK");
            }
        }

        private async void OnCameraButtonTapped(object sender, EventArgs e)
        {
            Debug.WriteLine("[CHAT PAGE] Camera button tapped");

            try
            {
                if (MediaPicker.Default.IsCaptureSupported)
                {
                    var photo = await MediaPicker.Default.CapturePhotoAsync();

                    if (photo != null)
                    {
                        Debug.WriteLine($"[CHAT PAGE] Photo captured: {photo.FileName}");
                        await DisplayAlert("Photo Captured",
                            $"{photo.FileName} - Upload functionality coming soon!",
                            "OK");
                    }
                }
                else
                {
                    await DisplayAlert("Not Supported",
                        "Camera is not supported on this device",
                        "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE] ❌ Error opening camera: {ex.Message}");
                await DisplayAlert("Error", "Failed to open camera", "OK");
            }
        }

        private async void OnMessageEditorFocused(object sender, FocusEventArgs e)
        {
            Debug.WriteLine("[CHAT PAGE] Editor focused - keyboard appearing");

            // Wait for keyboard animation
            await Task.Delay(300);

            // Scroll to bottom
            ScrollToBottom(true);
        }

        private void OnMessageEditorUnfocused(object sender, FocusEventArgs e)
        {
            Debug.WriteLine("[CHAT PAGE] Editor unfocused - keyboard hiding");
        }

        /// <summary>
        /// Scroll to the last message in the collection
        /// </summary>
        private void ScrollToBottom(bool animate)
        {
            try
            {
                if (_viewModel.Messages.Count > 0)
                {
                    var lastMessage = _viewModel.Messages[_viewModel.Messages.Count - 1];
                    MessagesCollectionView.ScrollTo(lastMessage,
                        position: ScrollToPosition.End,
                        animate: animate);

                    Debug.WriteLine($"[CHAT PAGE] Scrolled to bottom (animate: {animate})");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE] Error scrolling: {ex.Message}");
            }
        }
    }
}