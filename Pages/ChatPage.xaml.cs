using CraftConnect_Mobile_App.PageModels;
using CraftConnect_Mobile_App.Services;
using Plugin.Maui.Audio;
using System.Diagnostics;
using Microsoft.Maui.Controls;

namespace CraftConnect_Mobile_App.Pages
{
    public partial class ChatPage : ContentPage
    {
        private readonly ChatPageModel _viewModel;
        private readonly string _baseUrl;

        // ── Audio recording fields ────────────────────────────────
        private IAudioRecorder? _recorder;
        private string? _recordingFilePath;

#if ANDROID
        private bool _insetsApplied;
#endif

        public ChatPage(ChatPageModel viewModel, ApiConfig apiConfig)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _baseUrl = apiConfig.BaseUrl.TrimEnd('/');
            BindingContext = _viewModel;

            Debug.WriteLine($"[CHAT PAGE] Constructor — BaseUrl: {_baseUrl}");
        }

        // ══════════════════════════════════════════════════════════════
        // ▌ LIFECYCLE
        // ══════════════════════════════════════════════════════════════

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            Debug.WriteLine("[CHAT PAGE] OnAppearing");

            try
            {
                await _viewModel.InitializeAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE] ❌ Error in OnAppearing: {ex.Message}");
                await DisplayAlert("Error", "Failed to initialize chat. Please try again.", "OK");
            }

#if ANDROID
            ApplyAndroidInsets();
#endif
        }

        protected override async void OnDisappearing()
        {
            base.OnDisappearing();
            Debug.WriteLine("[CHAT PAGE] OnDisappearing");

            // Safety: stop any in-progress recording silently
            if (_viewModel.IsRecording)
                await StopRecordingAndDiscard();

            try
            {
                await _viewModel.CleanupAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE] ❌ Error in OnDisappearing: {ex.Message}");
            }

#if ANDROID
            if (_insetsApplied)
            {
                var inputAreaVe = this.FindByName<VisualElement>("MessageInputArea");
                if (inputAreaVe is Layout inputAreaLayout)
                    PageInsetManager.RestoreElementPadding(inputAreaLayout);

                PageInsetManager.RestorePagePadding(this);
                _insetsApplied = false;
            }
#endif
        }

        private void ApplyAndroidInsets()
        {
#if ANDROID
            try
            {
                var insets      = CraftConnect_Mobile_App.Platforms.Android.AndroidInsetService.GetInsets();
                var inputAreaVe = this.FindByName<VisualElement>("MessageInputArea");

                if (inputAreaVe is Layout inputAreaLayout && insets.IsImeVisible && insets.ImeHeight > 0)
                {
                    PageInsetManager.ApplyInsetToElement(inputAreaLayout, insets.ImeHeight);
                    _insetsApplied = true;
                }
                else if (insets.NavigationBarHeight > 0)
                {
                    PageInsetManager.ApplyInsetToPage(this, insets.NavigationBarHeight);
                    _insetsApplied = true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE] Insets error: {ex.Message}");
            }
#endif
        }

        // ══════════════════════════════════════════════════════════════
        // ▌ SEND / MIC — central dispatcher
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Single handler for the combined send/mic button.
        /// • Text present       → send the text message (via ViewModel command)
        /// • No text, idle      → start recording
        /// • No text, recording → stop + send voice note
        /// </summary>
        private async void OnSendMicTapped(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(_viewModel.MessageText))
            {
                // Normal text send — delegate to existing command
                if (_viewModel.SendMessageCommand.CanExecute(null))
                    _viewModel.SendMessageCommand.Execute(null);
                return;
            }

            if (_viewModel.IsRecording)
                await StopRecordingAndSend();
            else
                await StartRecording();
        }

        // ══════════════════════════════════════════════════════════════
        // ▌ RECORDING HELPERS
        // ══════════════════════════════════════════════════════════════

        private async Task StartRecording()
        {
            var status = await Permissions.RequestAsync<Permissions.Microphone>();
            if (status != PermissionStatus.Granted)
            {
                await DisplayAlert("Permission Required",
                    "Microphone access is needed to record voice messages.", "OK");
                return;
            }

            try
            {
                var fileName = $"voice_{DateTime.Now:yyyyMMdd_HHmmss}.m4a";
                _recordingFilePath = Path.Combine(FileSystem.CacheDirectory, fileName);

                _recorder = AudioManager.Current.CreateRecorder();
                await _recorder.StartAsync(_recordingFilePath);

                _viewModel.StartRecordingState();
                _ = AnimateRecordingDot();   // fire-and-forget pulsing dot

                Debug.WriteLine($"[CHAT PAGE] 🎙 Recording started → {_recordingFilePath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE] ❌ StartRecording error: {ex.Message}");
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

                Debug.WriteLine("[CHAT PAGE] ⏹ Recording stopped");

                if (string.IsNullOrEmpty(_recordingFilePath) || !File.Exists(_recordingFilePath))
                {
                    await DisplayAlert("Error", "Recording file not found.", "OK");
                    return;
                }

                await _viewModel.SendVoiceMessageAsync(_recordingFilePath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE] ❌ StopRecording error: {ex.Message}");
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
                Debug.WriteLine($"[CHAT PAGE] ❌ StopRecordingAndDiscard error: {ex.Message}");
            }
        }

        /// <summary>Pulses the red dot opacity while recording is active.</summary>
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

        // ══════════════════════════════════════════════════════════════
        // ▌ DOWNLOAD & VIEW ATTACHMENT
        // ══════════════════════════════════════════════════════════════

        private async void OnDownloadAttachment(object sender, EventArgs e)
        {
            GroupMessageItemViewModel message = null;

            try
            {
                if (e is TappedEventArgs tappedArgs && tappedArgs.Parameter != null)
                {
                    message = tappedArgs.Parameter as GroupMessageItemViewModel;
                    Debug.WriteLine("[CHAT PAGE] Got message from TappedEventArgs.Parameter");
                }

                if (message == null && sender is Element element)
                {
                    message = element.BindingContext as GroupMessageItemViewModel;
                    Debug.WriteLine("[CHAT PAGE] Got message from BindingContext");
                }

                if (message == null)
                {
                    await DisplayAlert("Error", "Invalid attachment - could not find message", "OK");
                    return;
                }

                if (string.IsNullOrEmpty(message.AttachmentUrl))
                {
                    await DisplayAlert("Error", "Invalid attachment - no URL", "OK");
                    return;
                }

                if (message.IsDownloading)
                {
                    message.CancelDownload();
                    return;
                }

                var downloadUrl = BuildAbsoluteUrl(message.AttachmentUrl);
                Debug.WriteLine($"[CHAT PAGE] 📥 Downloading: {message.AttachmentName} → {downloadUrl}");

                message.MarkAsDownloading();

                try
                {
                    var downloadsDir = Path.Combine(FileSystem.CacheDirectory, "downloads");
                    Directory.CreateDirectory(downloadsDir);

                    var fileName = message.AttachmentName ?? $"file_{message.Id}{message.AttachmentType}";
                    var filePath = Path.Combine(downloadsDir, fileName);

                    using var httpClient = CreateHttpClient();
                    var fileBytes = await httpClient.GetByteArrayAsync(downloadUrl);

                    if (!message.IsDownloading)
                    {
                        Debug.WriteLine("[CHAT PAGE] Download cancelled — discarding bytes");
                        return;
                    }

                    await File.WriteAllBytesAsync(filePath, fileBytes);
                    message.MarkAsDownloaded(filePath);

                    if (message.IsDocumentAttachment)
                    {
                        try
                        {
                            await Launcher.OpenAsync(new OpenFileRequest
                            {
                                File = new ReadOnlyFile(filePath),
                                Title = "Open with"
                            });
                        }
                        catch
                        {
                            var share = await DisplayAlert("Downloaded",
                                "File downloaded. Would you like to share it?", "Share", "OK");

                            if (share)
                                await Share.RequestAsync(new ShareFileRequest
                                {
                                    Title = "Share file",
                                    File = new ShareFile(filePath)
                                });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CHAT PAGE] ❌ Download error: {ex.Message}");
                    message?.CancelDownload();
                    await DisplayAlert("Error", $"Failed to download: {ex.Message}", "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE] ❌ Outer error: {ex.Message}");
                message?.CancelDownload();
                await DisplayAlert("Error", "Failed to process attachment", "OK");
            }
        }

        private async void OnImageTapped(object sender, EventArgs e)
        {
            try
            {
                var message = (sender as Image)?.BindingContext as GroupMessageItemViewModel;

                if (message == null) return;

                if (!message.IsDownloaded)
                {
                    await DisplayAlert("Not Downloaded", "Please download the image first", "OK");
                    return;
                }

                await Shell.Current.GoToAsync("ImageViewerPage", new Dictionary<string, object>
                {
                    { "ImagePath", message.LocalFilePath },
                    { "FileName",  message.AttachmentName }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE] ❌ Image tap error: {ex.Message}");
                await DisplayAlert("Error", "Failed to open image", "OK");
            }
        }

        // ══════════════════════════════════════════════════════════════
        // ▌ HELPERS
        // ══════════════════════════════════════════════════════════════

        private string BuildAbsoluteUrl(string url)
        {
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return url;

            var absolute = $"{_baseUrl}/{url.TrimStart('/')}";
            Debug.WriteLine($"[CHAT PAGE] Built absolute URL: {absolute}");
            return absolute;
        }

        private static HttpClient CreateHttpClient()
        {
#if ANDROID
            var handler = new Xamarin.Android.Net.AndroidMessageHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                {
                    Debug.WriteLine($"[CHAT PAGE SSL] Host: {message.RequestUri.Host}, Errors: {errors}");
                    return true;
                }
            };
#else
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                {
                    Debug.WriteLine($"[CHAT PAGE SSL] Host: {message.RequestUri.Host}, Errors: {errors}");
                    return true;
                }
            };
#endif
            return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        }

        // ══════════════════════════════════════════════════════════════
        // ▌ UI EVENT HANDLERS
        // ══════════════════════════════════════════════════════════════

        private async void OnBackButtonTapped(object sender, EventArgs e)
        {
            if (_viewModel.IsRecording)
                await StopRecordingAndDiscard();

            await Shell.Current.GoToAsync("..");
        }

        private void OnEmojiButtonTapped(object sender, EventArgs e) =>
            Debug.WriteLine("[CHAT PAGE] Emoji tapped — TODO");

        private async void OnAttachmentButtonTapped(object sender, EventArgs e)
        {
            try
            {
                var result = await FilePicker.PickAsync(new PickOptions
                {
                    PickerTitle = "Select a file",
                    FileTypes = FilePickerFileType.Images
                });

                if (result != null)
                    await DisplayAlert("Coming Soon", "File upload will be available soon!", "OK");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE] ❌ File pick error: {ex.Message}");
            }
        }

        private async void OnCameraButtonTapped(object sender, EventArgs e)
        {
            try
            {
                if (MediaPicker.Default.IsCaptureSupported)
                {
                    var photo = await MediaPicker.Default.CapturePhotoAsync();
                    if (photo != null)
                        await DisplayAlert("Coming Soon", "Photo upload will be available soon!", "OK");
                }
                else
                {
                    await DisplayAlert("Not Supported", "Camera is not available on this device", "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE] ❌ Camera error: {ex.Message}");
            }
        }

        private void OnMessageEditorFocused(object sender, FocusEventArgs e) =>
            Debug.WriteLine("[CHAT PAGE] Editor focused");

        private void OnMessageEditorUnfocused(object sender, FocusEventArgs e) =>
            Debug.WriteLine("[CHAT PAGE] Editor unfocused");
    }
}