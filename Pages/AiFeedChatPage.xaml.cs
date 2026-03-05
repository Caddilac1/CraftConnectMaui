using CraftConnect_Mobile_App.PageModels;
using Plugin.Maui.Audio;
using System.Diagnostics;

namespace CraftConnect_Mobile_App.Pages
{
    public partial class AiFeedChatPage : ContentPage
    {
        private readonly AiFeedChatPageModel _viewModel;

        // ── Audio recording fields ────────────────────────────────
        private IAudioRecorder? _recorder;
        private string? _recordingFilePath;

        public AiFeedChatPage(AiFeedChatPageModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
            Debug.WriteLine("[AI CHAT PAGE] Constructor - Page initialized");
        }

        // ─────────────────────────────────────────────────────────
        // Page lifecycle
        // ─────────────────────────────────────────────────────────

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

        protected override async void OnDisappearing()
        {
            base.OnDisappearing();
            Debug.WriteLine("[AI CHAT PAGE] OnDisappearing");

            // Safety: stop any in-progress recording if user navigates away
            if (_viewModel.IsRecording)
                await StopRecordingAndDiscard();
        }

        // ─────────────────────────────────────────────────────────
        // Navigation
        // ─────────────────────────────────────────────────────────

        private async void OnBackButtonTapped(object sender, EventArgs e)
        {
            Debug.WriteLine("[AI CHAT PAGE] Back button tapped");

            // Stop recording first if active
            if (_viewModel.IsRecording)
                await StopRecordingAndDiscard();

            var confirm = await DisplayAlert(
                "Exit Chat",
                "Are you sure you want to exit? Your progress will be saved.",
                "Yes",
                "No");

            if (confirm)
                await Shell.Current.GoToAsync("..");
        }

        // ─────────────────────────────────────────────────────────
        // Send / Mic tap — central dispatcher
        // ─────────────────────────────────────────────────────────

        /// <summary>
        /// Single handler for the combined send/mic button.
        /// • Text present  → send the text message
        /// • No text, idle → start recording
        /// • No text, recording → stop + send voice note
        /// </summary>
        private async void OnSendMicTapped(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(_viewModel.MessageText))
            {
                // Normal text send
                await _viewModel.SendMessageCommand.ExecuteAsync(null);
                return;
            }

            if (_viewModel.IsRecording)
                await StopRecordingAndSend();
            else
                await StartRecording();
        }

        // ─────────────────────────────────────────────────────────
        // Recording helpers
        // ─────────────────────────────────────────────────────────

        private async Task StartRecording()
        {
            // Check / request microphone permission
            var status = await Permissions.RequestAsync<Permissions.Microphone>();
            if (status != PermissionStatus.Granted)
            {
                await DisplayAlert("Permission Required",
                    "Microphone access is needed to record voice messages.", "OK");
                return;
            }

            try
            {
                // Build a temp path for the audio file
                var fileName = $"voice_{DateTime.Now:yyyyMMdd_HHmmss}.m4a";
                _recordingFilePath = Path.Combine(FileSystem.CacheDirectory, fileName);

                _recorder = AudioManager.Current.CreateRecorder();
                await _recorder.StartAsync(_recordingFilePath);

                _viewModel.StartRecordingState();
                _ = AnimateRecordingDot();          // pulsing dot (fire-and-forget)

                Debug.WriteLine($"[AI CHAT PAGE] 🎙 Recording started → {_recordingFilePath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AI CHAT PAGE] ❌ StartRecording error: {ex.Message}");
                await DisplayAlert("Error", "Could not start recording. Please try again.", "OK");
            }
        }

        private async Task StopRecordingAndSend()
        {
            try
            {
                if (_recorder == null) return;

                await _recorder.StopAsync();
                _viewModel.StopRecordingState();

                Debug.WriteLine("[AI CHAT PAGE] ⏹ Recording stopped");

                if (string.IsNullOrEmpty(_recordingFilePath) || !File.Exists(_recordingFilePath))
                {
                    await DisplayAlert("Error", "Recording file not found.", "OK");
                    return;
                }

                // Hand off to the view model for upload / transcription
                await _viewModel.SendVoiceMessageAsync(_recordingFilePath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AI CHAT PAGE] ❌ StopRecording error: {ex.Message}");
                _viewModel.StopRecordingState();
                await DisplayAlert("Error", "Failed to process recording. Please try again.", "OK");
            }
            finally
            {
                _recorder = null;
                _recordingFilePath = null;
            }
        }

        private async Task StopRecordingAndDiscard()
        {
            try
            {
                if (_recorder != null)
                {
                    await _recorder.StopAsync();
                    _recorder = null;
                }

                if (!string.IsNullOrEmpty(_recordingFilePath) && File.Exists(_recordingFilePath))
                    File.Delete(_recordingFilePath);

                _recordingFilePath = null;
                _viewModel.StopRecordingState();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AI CHAT PAGE] ❌ StopRecordingAndDiscard error: {ex.Message}");
            }
        }

        /// <summary>Pulsing opacity animation on the red dot while recording.</summary>
        private async Task AnimateRecordingDot()
        {
            while (_viewModel.IsRecording)
            {
                await RecordingDot.FadeTo(0.2, 500);
                if (!_viewModel.IsRecording) break;
                await RecordingDot.FadeTo(1.0, 500);
            }
            RecordingDot.Opacity = 1;
        }

        // ─────────────────────────────────────────────────────────
        // Attachment sheet
        // ─────────────────────────────────────────────────────────

        private async void OnAttachmentButtonTapped(object sender, EventArgs e)
        {
            Debug.WriteLine("[AI CHAT PAGE] Attachment button tapped");

            AttachmentOverlay.IsVisible = true;

            var sheet = AttachmentOverlay.Children[1] as VerticalStackLayout;
            if (sheet != null)
            {
                sheet.TranslationY = 300;
                await sheet.TranslateTo(0, 0, 250, Easing.CubicOut);
            }
        }

        private async void OnDismissAttachmentSheet(object sender, EventArgs e) =>
            await HideAttachmentSheet();

        private async Task HideAttachmentSheet()
        {
            var sheet = AttachmentOverlay.Children[1] as VerticalStackLayout;
            if (sheet != null)
                await sheet.TranslateTo(0, 300, 200, Easing.CubicIn);

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

        private void OnEmojiButtonTapped(object sender, EventArgs e)
        {
            Debug.WriteLine("[AI CHAT PAGE] Emoji button tapped");
            DisplayAlert("Info", "Emoji picker coming soon!", "OK");
        }

        // ─────────────────────────────────────────────────────────
        // File / photo pickers (unchanged)
        // ─────────────────────────────────────────────────────────

        private async Task PickDocument(string fileType)
        {
            try
            {
                var customFileType = new FilePickerFileType(
                    new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.iOS,    new[] { "public.pdf", "public.image", "public.data" } },
                        { DevicePlatform.Android, new[] { "application/pdf", "image/*", "*/*" } },
                        { DevicePlatform.WinUI,  new[] { ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx" } }
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