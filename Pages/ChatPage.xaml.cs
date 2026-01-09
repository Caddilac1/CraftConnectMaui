using CraftConnect_Mobile_App.PageModels;
using System.Diagnostics;

namespace CraftConnect_Mobile_App.Pages
{
    public partial class ChatPage : ContentPage
    {
        private ChatPageModel _viewModel;

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
                await _viewModel.InitializeAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE] ❌ Error in OnAppearing: {ex.Message}");
                Debug.WriteLine($"[CHAT PAGE] StackTrace: {ex.StackTrace}");

                await DisplayAlert(
                    "Error",
                    "Failed to initialize chat. Please try again.",
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

        // ═══════════════════════════════════════════════════════════════
        // DOWNLOAD & VIEW ATTACHMENT (WhatsApp style)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Handle download/cancel button click - Single button that toggles
        /// </summary>
        private async void OnDownloadAttachment(object sender, EventArgs e)
        {
            GroupMessageItemViewModel message = null;

            try
            {
                // Try to get message from TapGestureRecognizer CommandParameter first
                if (e is TappedEventArgs tappedArgs && tappedArgs.Parameter != null)
                {
                    message = tappedArgs.Parameter as GroupMessageItemViewModel;
                    Debug.WriteLine($"[CHAT PAGE] Got message from TappedEventArgs.Parameter");
                }

                // Fallback: Get from BindingContext
                if (message == null && sender is Element element)
                {
                    message = element.BindingContext as GroupMessageItemViewModel;
                    Debug.WriteLine($"[CHAT PAGE] Got message from BindingContext");
                }

                if (message == null)
                {
                    Debug.WriteLine("[CHAT PAGE] ❌ Invalid attachment - message is null");
                    await DisplayAlert("Error", "Invalid attachment - could not find message", "OK");
                    return;
                }

                if (string.IsNullOrEmpty(message.AttachmentUrl))
                {
                    Debug.WriteLine($"[CHAT PAGE] ❌ Invalid attachment - URL is empty");
                    Debug.WriteLine($"[CHAT PAGE] Message ID: {message.Id}");
                    Debug.WriteLine($"[CHAT PAGE] Has Attachment: {message.HasAttachment}");
                    Debug.WriteLine($"[CHAT PAGE] Is Image: {message.IsImageAttachment}");
                    Debug.WriteLine($"[CHAT PAGE] Is Document: {message.IsDocumentAttachment}");
                    await DisplayAlert("Error", "Invalid attachment - no URL", "OK");
                    return;
                }

                // ✅ If already downloading, cancel it
                if (message.IsDownloading)
                {
                    Debug.WriteLine($"[CHAT PAGE] ❌ Cancelling download for: {message.AttachmentName}");
                    message.CancelDownload();
                    return;
                }

                Debug.WriteLine($"[CHAT PAGE] 📥 Starting download: {message.AttachmentName}");
                Debug.WriteLine($"[CHAT PAGE] Media Type: {message.MediaType}");
                Debug.WriteLine($"[CHAT PAGE] Is Image: {message.IsImageAttachment}");
                Debug.WriteLine($"[CHAT PAGE] Is Document: {message.IsDocumentAttachment}");
                Debug.WriteLine($"[CHAT PAGE] Attachment URL: {message.AttachmentUrl}");

                // Build absolute URL
                string downloadUrl = GetAbsoluteUrl(message.AttachmentUrl);
                Debug.WriteLine($"[CHAT PAGE] 🌐 Download URL: {downloadUrl}");

                // Mark as downloading (button changes to red X with spinner)
                message.MarkAsDownloading();

                try
                {
                    // Create downloads directory
                    var downloadsDir = Path.Combine(FileSystem.CacheDirectory, "downloads");
                    Directory.CreateDirectory(downloadsDir);

                    // Download file
                    var fileName = message.AttachmentName ?? $"file_{message.Id}{message.AttachmentType}";
                    var filePath = Path.Combine(downloadsDir, fileName);

                    Debug.WriteLine($"[CHAT PAGE] 💾 Will save to: {filePath}");

                    using var httpClient = CreateHttpClient();
                    var fileBytes = await httpClient.GetByteArrayAsync(downloadUrl);

                    Debug.WriteLine($"[CHAT PAGE] ✅ Downloaded {fileBytes.Length} bytes");

                    // Check if download was cancelled
                    if (!message.IsDownloading)
                    {
                        Debug.WriteLine($"[CHAT PAGE] Download was cancelled");
                        return;
                    }

                    await File.WriteAllBytesAsync(filePath, fileBytes);

                    Debug.WriteLine($"[CHAT PAGE] ✅ Saved to: {filePath}");

                    // Mark as downloaded - button disappears, clear image shows
                    message.MarkAsDownloaded(filePath);

                    // ✅ NO ALERT FOR IMAGES - just silently download
                    // User can tap the image to view it in full screen
                    if (message.IsImageAttachment)
                    {
                        Debug.WriteLine($"[CHAT PAGE] ✅ Image downloaded silently");
                        // No alert - user can just tap to view
                    }
                    // For documents, try to open
                    else if (message.IsDocumentAttachment)
                    {
                        try
                        {
                            await Launcher.OpenAsync(new OpenFileRequest
                            {
                                File = new ReadOnlyFile(filePath),
                                Title = "Open with"
                            });
                            Debug.WriteLine($"[CHAT PAGE] ✅ Opened document successfully");
                        }
                        catch (Exception openEx)
                        {
                            Debug.WriteLine($"[CHAT PAGE] ⚠️ Could not open: {openEx.Message}");

                            var share = await DisplayAlert(
                                "Downloaded",
                                "File downloaded successfully. Would you like to share it?",
                                "Share",
                                "OK");

                            if (share)
                            {
                                await Share.RequestAsync(new ShareFileRequest
                                {
                                    Title = "Share file",
                                    File = new ShareFile(filePath)
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CHAT PAGE] ❌ Download error: {ex.Message}");
                    Debug.WriteLine($"[CHAT PAGE] ❌ Stack trace: {ex.StackTrace}");
                    message?.CancelDownload();
                    await DisplayAlert("Error", $"Failed to download: {ex.Message}", "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE] ❌ Error: {ex.Message}");
                Debug.WriteLine($"[CHAT PAGE] ❌ Stack trace: {ex.StackTrace}");
                message?.CancelDownload();
                await DisplayAlert("Error", "Failed to process attachment", "OK");
            }
        }

        /// <summary>
        /// Handle image tap - Opens downloaded image in full-screen viewer
        /// </summary>
        private async void OnImageTapped(object sender, EventArgs e)
        {
            try
            {
                var image = sender as Image;
                var message = image?.BindingContext as GroupMessageItemViewModel;

                if (message == null)
                {
                    Debug.WriteLine("[CHAT PAGE] ❌ Invalid message");
                    return;
                }

                // Only open if downloaded
                if (!message.IsDownloaded)
                {
                    Debug.WriteLine("[CHAT PAGE] Image not downloaded yet");
                    await DisplayAlert("Not Downloaded", "Please download the image first", "OK");
                    return;
                }

                Debug.WriteLine($"[CHAT PAGE] 🖼️ Opening downloaded image: {message.LocalFilePath}");

                // Navigate to full-screen viewer
                var parameters = new Dictionary<string, object>
                {
                    { "ImagePath", message.LocalFilePath },
                    { "FileName", message.AttachmentName }
                };

                await Shell.Current.GoToAsync("ImageViewerPage", parameters);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE] ❌ Error opening image: {ex.Message}");
                await DisplayAlert("Error", "Failed to open image", "OK");
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // HELPER METHODS
        // ═══════════════════════════════════════════════════════════════

        private string GetAbsoluteUrl(string url)
        {
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return url;
            }

            var baseUrl = Preferences.Get("api_base_url", "https://192.168.188.1127023");
            baseUrl = baseUrl.TrimEnd('/');
            var relativePath = url.TrimStart('/');
            return $"{baseUrl}/{relativePath}";
        }

        private HttpClient CreateHttpClient()
        {
#if DEBUG
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };
            return new HttpClient(handler);
#else
            return new HttpClient();
#endif
        }

        // ═══════════════════════════════════════════════════════════════
        // EXISTING EVENT HANDLERS
        // ═══════════════════════════════════════════════════════════════

        private async void OnBackButtonTapped(object sender, EventArgs e)
        {
            Debug.WriteLine("[CHAT PAGE] Back button tapped");
            await Shell.Current.GoToAsync("..");
        }

        private void OnEmojiButtonTapped(object sender, EventArgs e)
        {
            Debug.WriteLine("[CHAT PAGE] Emoji button tapped");
            // TODO: Implement emoji picker
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
                    // TODO: Upload file and send message with attachment
                    await DisplayAlert("Coming Soon", "File upload feature will be available soon!", "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE] ❌ Error picking file: {ex.Message}");
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
                        // TODO: Upload photo and send message
                        await DisplayAlert("Coming Soon", "Photo upload feature will be available soon!", "OK");
                    }
                }
                else
                {
                    await DisplayAlert("Not Supported", "Camera is not available on this device", "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE] ❌ Error capturing photo: {ex.Message}");
            }
        }

        private void OnMessageEditorFocused(object sender, FocusEventArgs e)
        {
            Debug.WriteLine("[CHAT PAGE] Message editor focused");
        }

        private void OnMessageEditorUnfocused(object sender, FocusEventArgs e)
        {
            Debug.WriteLine("[CHAT PAGE] Message editor unfocused");
        }
    }
}