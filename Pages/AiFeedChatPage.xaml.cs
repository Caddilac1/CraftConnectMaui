using CraftConnect_Mobile_App.PageModels;
using Microsoft.Maui.Controls.Shapes;
using Plugin.Maui.Audio;
using System.Diagnostics;

// Alias to avoid ambiguity between
// Microsoft.Maui.Controls.Shapes.Path and System.IO.Path
using IOPath = System.IO.Path;

namespace CraftConnect_Mobile_App.Pages
{
    public partial class AiFeedChatPage : ContentPage
    {
        private readonly AiFeedChatPageModel _viewModel;

        // ── Audio recording fields ────────────────────────────────
        private IAudioRecorder? _recorder;
        private string? _recordingFilePath;

        // ── Audio playback fields ─────────────────────────────────
        private IAudioPlayer? _audioPlayer;

        public AiFeedChatPage(AiFeedChatPageModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;

            // Subscribe to playback requests raised by the ViewModel relay command
            _viewModel.PlayVoiceRequested += OnPlayVoiceRequested;

            Debug.WriteLine("[SUPPORT CHAT] Page initialized");
        }

        // ─────────────────────────────────────────────────────────
        // Page lifecycle
        // ─────────────────────────────────────────────────────────

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            Debug.WriteLine("[SUPPORT CHAT] OnAppearing");

            try
            {
                await Task.Delay(100);
                await _viewModel.InitializeAsync();
                Debug.WriteLine("[SUPPORT CHAT] ✅ Ready");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SUPPORT CHAT] ❌ Error: {ex.Message}");
                await DisplayAlert("Connection Error",
                    "We couldn't connect to CraftConnect Support right now. Please try again.", "OK");
            }
        }

        protected override async void OnDisappearing()
        {
            base.OnDisappearing();
            Debug.WriteLine("[SUPPORT CHAT] OnDisappearing");

            if (_viewModel.IsRecording)
                await StopRecordingAndDiscard();

            // Stop any active playback when leaving the page
            StopPlayback();

            // Unsubscribe to avoid memory leaks
            _viewModel.PlayVoiceRequested -= OnPlayVoiceRequested;
        }

        // ─────────────────────────────────────────────────────────
        // Navigation
        // ─────────────────────────────────────────────────────────

        private async void OnBackButtonTapped(object sender, EventArgs e)
        {
            Debug.WriteLine("[SUPPORT CHAT] Back tapped");

            if (_viewModel.IsRecording)
                await StopRecordingAndDiscard();

            var confirm = await DisplayAlert(
                "Leave Chat",
                "Are you sure you want to leave? Your progress will be saved.",
                "Leave", "Stay");

            if (confirm)
                await Shell.Current.GoToAsync("..");
        }

        // ─────────────────────────────────────────────────────────
        // Send / Mic — central dispatcher
        // ─────────────────────────────────────────────────────────

        private async void OnSendMicTapped(object sender, EventArgs e)
        {
            // If text is present → send text message
            if (!string.IsNullOrWhiteSpace(_viewModel.MessageText))
            {
                await _viewModel.SendMessageCommand.ExecuteAsync(null);
                return;
            }

            // If currently recording → stop and send
            if (_viewModel.IsRecording)
            {
                await StopRecordingAndSend();
                return;
            }

            // Otherwise → start recording
            await StartRecording();
        }

        // ─────────────────────────────────────────────────────────
        // Recording helpers
        // ─────────────────────────────────────────────────────────

        private async Task StartRecording()
        {
            var status = await Permissions.RequestAsync<Permissions.Microphone>();
            if (status != PermissionStatus.Granted)
            {
                await DisplayAlert("Permission Required",
                    "Microphone access is needed to send voice messages.", "OK");
                return;
            }

            try
            {
                var fileName = $"voice_{DateTime.Now:yyyyMMdd_HHmmss}.m4a";
                _recordingFilePath = IOPath.Combine(FileSystem.CacheDirectory, fileName);

                _recorder = AudioManager.Current.CreateRecorder();
                await _recorder.StartAsync(_recordingFilePath);

                _viewModel.StartRecordingState();
                _ = AnimateRecordingDot();

                Debug.WriteLine($"[SUPPORT CHAT] 🎙 Recording → {_recordingFilePath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SUPPORT CHAT] ❌ StartRecording error: {ex.Message}");
                await DisplayAlert("Error", "Could not start recording. Please try again.", "OK");
            }
        }

        private async Task StopRecordingAndSend()
        {
            try
            {
                if (_recorder == null) return;

                // ⚠️ Capture duration NOW, before StopRecordingState() resets it to "0:00"
                var capturedDuration = _viewModel.RecordingDuration;

                await _recorder.StopAsync();
                _viewModel.StopRecordingState();

                Debug.WriteLine($"[SUPPORT CHAT] ⏹ Recording stopped — duration: {capturedDuration}");

                if (string.IsNullOrEmpty(_recordingFilePath) || !File.Exists(_recordingFilePath))
                {
                    await DisplayAlert("Error", "Recording file not found.", "OK");
                    return;
                }

                // Pass the captured duration so the bubble shows the correct time
                await _viewModel.SendVoiceMessageAsync(_recordingFilePath, capturedDuration);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SUPPORT CHAT] ❌ StopRecording error: {ex.Message}");
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
                Debug.WriteLine($"[SUPPORT CHAT] ❌ StopRecordingAndDiscard error: {ex.Message}");
            }
        }

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
        // Audio playback
        // ─────────────────────────────────────────────────────────

        /// <summary>
        /// Called when the ViewModel fires PlayVoiceRequested.
        /// Runs on whatever thread the event fires on; marshals to UI thread
        /// so DisplayAlert (if needed) is safe.
        /// </summary>
        private void OnPlayVoiceRequested(object? sender, string filePath)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
                await PlayVoiceMessageAsync(filePath));
        }

        private async Task PlayVoiceMessageAsync(string filePath)
        {
            try
            {
                // Stop any currently playing audio first
                StopPlayback();

                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    await DisplayAlert("Playback unavailable",
                        "The audio file is no longer available on this device.", "OK");
                    return;
                }

                var stream = File.OpenRead(filePath);
                _audioPlayer = AudioManager.Current.CreatePlayer(stream);
                _audioPlayer.PlaybackEnded += (s, e) =>
                {
                    // Clean up when playback finishes naturally
                    MainThread.BeginInvokeOnMainThread(StopPlayback);
                };
                _audioPlayer.Play();

                Debug.WriteLine($"[SUPPORT CHAT] ▶️ Playing: {filePath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SUPPORT CHAT] ❌ Playback error: {ex.Message}");
                await DisplayAlert("Error", "Could not play the voice message. Please try again.", "OK");
            }
        }

        private void StopPlayback()
        {
            try
            {
                if (_audioPlayer != null)
                {
                    if (_audioPlayer.IsPlaying)
                        _audioPlayer.Stop();

                    _audioPlayer.Dispose();
                    _audioPlayer = null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SUPPORT CHAT] ❌ StopPlayback error: {ex.Message}");
            }
        }

        // ─────────────────────────────────────────────────────────
        // Attachment sheet
        // ─────────────────────────────────────────────────────────

        private async void OnAttachmentButtonTapped(object sender, EventArgs e)
        {
            Debug.WriteLine("[SUPPORT CHAT] Attachment tapped");

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
            Debug.WriteLine("[SUPPORT CHAT] Emoji tapped");
            DisplayAlert("Coming Soon", "Emoji picker will be available soon!", "OK");
        }

        // ─────────────────────────────────────────────────────────
        // File / photo pickers
        // ─────────────────────────────────────────────────────────

        private async Task PickDocument(string fileType)
        {
            try
            {
                var customFileType = new FilePickerFileType(
                    new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.iOS,     new[] { "public.pdf", "public.image", "public.data" } },
                        { DevicePlatform.Android, new[] { "application/pdf", "image/*", "*/*" } },
                        { DevicePlatform.WinUI,   new[] { ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx" } }
                    });

                var options = new PickOptions
                {
                    FileTypes = customFileType,
                    PickerTitle = $"Select {fileType}"
                };

                var file = await FilePicker.Default.PickAsync(options);
                if (file != null)
                {
                    await _viewModel.AttachFile(file, fileType);
                    Debug.WriteLine($"[SUPPORT CHAT] ✅ File attached: {file.FileName}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SUPPORT CHAT] ❌ PickDocument error: {ex.Message}");
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
                        Debug.WriteLine($"[SUPPORT CHAT] ✅ Photo captured: {file.FileName}");
                    }
                }
                else
                {
                    await DisplayAlert("Not Supported", "Camera is not available on this device.", "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SUPPORT CHAT] ❌ TakePhoto error: {ex.Message}");
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
                    Debug.WriteLine($"[SUPPORT CHAT] ✅ Photo selected: {file.FileName}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SUPPORT CHAT] ❌ PickPhoto error: {ex.Message}");
                await DisplayAlert("Error", "Failed to select photo. Please try again.", "OK");
            }
        }
    }
}