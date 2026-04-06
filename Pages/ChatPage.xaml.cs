using CraftConnect_Mobile_App.PageModels;
using CraftConnect_Mobile_App.Services;
using Microsoft.Maui.Controls.Shapes;
using Plugin.Maui.Audio;
using System.Diagnostics;
using Path = System.IO.Path;

namespace CraftConnect_Mobile_App.Pages
{
    public partial class ChatPage : ContentPage
    {
        private readonly ChatPageModel _viewModel;
        private readonly string _baseUrl;

        // ── Audio recording ────────────────────────────────────────
        private IAudioRecorder? _recorder;
        private string? _recordingFilePath;

        // ── Audio playback ────────────────────────────────────────
        private IAudioPlayer? _audioPlayer;
        private GroupMessageItemViewModel? _playingMessage;

        // ── Wave animation ────────────────────────────────────────
        private CancellationTokenSource? _waveCts;

        // ── Long-press via PanGestureRecognizer ───────────────────
        // PanGestureRecognizer.PanUpdated fires on touch-DOWN (Running state)
        // immediately, before the user lifts — giving us reliable 500 ms timing
        // without waiting for touch-UP like TapGestureRecognizer does on some devices.
        private CancellationTokenSource? _longPressCts;
        private GroupMessageItemViewModel? _pendingLongPress;
        private const int LongPressMs = 500;

        // Cooldown prevents the menu re-opening right after dismissal
        private DateTime _lastContextOpen = DateTime.MinValue;
        private const int ContextCooldownMs = 800;

        // ── Sender-name popup tracking ────────────────────────────
        private GroupMessageItemViewModel? _senderMenuTarget;

#if ANDROID
        private bool _insetsApplied;
#endif

        public ChatPage(ChatPageModel viewModel, ApiConfig apiConfig)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _baseUrl = apiConfig.BaseUrl.TrimEnd('/');
            BindingContext = _viewModel;
        }

        // ══════════════════════════════════════════════════════════════
        // LIFECYCLE
        // ══════════════════════════════════════════════════════════════

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            try { await _viewModel.InitializeAsync(); }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE] OnAppearing: {ex.Message}");
                await DisplayAlert("Error", "Failed to initialize chat.", "OK");
            }
#if ANDROID
            ApplyAndroidInsets();
#endif
        }

        protected override async void OnDisappearing()
        {
            base.OnDisappearing();
            _waveCts?.Cancel();
            _longPressCts?.Cancel();

            if (_viewModel.IsRecording) await StopRecordingAndDiscard();
            StopAudioPlayback();

            try { await _viewModel.CleanupAsync(); }
            catch (Exception ex) { Debug.WriteLine($"[CHAT PAGE] OnDisappearing: {ex.Message}"); }

#if ANDROID
            if (_insetsApplied)
            {
                var ve = this.FindByName<VisualElement>("MessageInputArea");
                if (ve is Layout l) PageInsetManager.RestoreElementPadding(l);
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
                var insets = CraftConnect_Mobile_App.Platforms.Android.AndroidInsetService.GetInsets();
                var ve = this.FindByName<VisualElement>("MessageInputArea");
                if (ve is Layout l && insets.IsImeVisible && insets.ImeHeight > 0)
                { PageInsetManager.ApplyInsetToElement(l, insets.ImeHeight); _insetsApplied = true; }
                else if (insets.NavigationBarHeight > 0)
                { PageInsetManager.ApplyInsetToPage(this, insets.NavigationBarHeight); _insetsApplied = true; }
            }
            catch (Exception ex) { Debug.WriteLine($"[CHAT PAGE] Insets: {ex.Message}"); }
#endif
        }

        // ══════════════════════════════════════════════════════════════
        // LONG-PRESS VIA PanGestureRecognizer
        //
        // PanUpdated fires with StatusType.Started on the very first touch
        // contact — before any movement or lift. We start the 500 ms timer
        // there. If the finger moves (Running/Completed/Cancelled), we cancel
        // the timer so a scroll doesn't accidentally open the menu.
        // ══════════════════════════════════════════════════════════════

        private void OnMessagePanUpdated(object sender, PanUpdatedEventArgs e)
        {
            var msg = (sender as Element)?.BindingContext as GroupMessageItemViewModel;
            if (msg == null) return;

            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    // Touch contact made — start timer
                    if ((DateTime.UtcNow - _lastContextOpen).TotalMilliseconds < ContextCooldownMs)
                        return;

                    // If menu already open for this message, dismiss on next touch
                    if (_viewModel.IsContextMenuVisible && _viewModel.SelectedMessage == msg)
                    {
                        _viewModel.CloseContextMenu();
                        return;
                    }

                    _longPressCts?.Cancel();
                    _longPressCts = new CancellationTokenSource();
                    _pendingLongPress = msg;
                    var token = _longPressCts.Token;

                    Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(LongPressMs, token);
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                if (!token.IsCancellationRequested && _pendingLongPress == msg)
                                {
                                    _lastContextOpen = DateTime.UtcNow;
                                    _pendingLongPress = null;
                                    _viewModel.OpenContextMenu(msg);
                                }
                            });
                        }
                        catch (TaskCanceledException) { }
                    }, token);
                    break;

                case GestureStatus.Running:
                    // Finger moved — treat as a scroll gesture, cancel long-press
                    // Only cancel if movement exceeds a small threshold (avoid cancelling on jitter)
                    if (Math.Abs(e.TotalX) > 8 || Math.Abs(e.TotalY) > 8)
                    {
                        _longPressCts?.Cancel();
                        _pendingLongPress = null;
                    }
                    break;

                case GestureStatus.Completed:
                case GestureStatus.Canceled:
                    // Finger lifted cleanly — cancel timer (it's just a tap, not a hold)
                    _longPressCts?.Cancel();
                    _pendingLongPress = null;
                    break;
            }
        }

        // ══════════════════════════════════════════════════════════════
        // SENDER NAME TAP → show "Message" popup
        // ══════════════════════════════════════════════════════════════

        private void OnSenderNameTapped(object sender, TappedEventArgs e)
        {
            GroupMessageItemViewModel? msg = null;
            if (e.Parameter is GroupMessageItemViewModel pvm) msg = pvm;
            else if (sender is Element el) msg = el.BindingContext as GroupMessageItemViewModel;

            if (msg == null || msg.IsFromCurrentUser) return;

            _senderMenuTarget = msg;
            _viewModel.ShowSenderMenu(msg.DisplayName);
        }

        private void OnSenderMenuDismissed(object sender, EventArgs e)
        {
            _senderMenuTarget = null;
            _viewModel.HideSenderMenu();
        }

        private async void OnSenderMenuMessageTapped(object sender, EventArgs e)
        {
            var msg = _senderMenuTarget;
            _viewModel.HideSenderMenu();
            _senderMenuTarget = null;

            if (msg == null) return;

            // Open fresh private chat — NO prequoted message
            try
            {
                await Shell.Current.GoToAsync(
                    $"PrivateChatPage" +
                    $"?ConversationId=PENDING_{msg.SenderId}" +
                    $"&OtherUserId={Uri.EscapeDataString(msg.SenderId.ToString())}" +
                    $"&OtherUserName={Uri.EscapeDataString(msg.DisplayName)}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE] SenderMenuMessage: {ex.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════════
        // REPLY PREVIEW TAP → scroll to original message in group chat
        // ══════════════════════════════════════════════════════════════

        private void OnReplyPreviewTapped(object sender, TappedEventArgs e)
        {
            GroupMessageItemViewModel? msg = null;
            if (e.Parameter is GroupMessageItemViewModel pvm) msg = pvm;
            else if (sender is Element el) msg = el.BindingContext as GroupMessageItemViewModel;

            if (msg?.ReplyToId == null) return;

            // Find the original message in the collection and scroll to it
            var original = _viewModel.Messages.FirstOrDefault(m => m.Id == msg.ReplyToId);
            if (original == null) return;

            MessagesCollectionView.ScrollTo(original, position: ScrollToPosition.Center, animate: true);

            // Flash highlight — tell the viewmodel to briefly highlight this message
            _viewModel.HighlightMessage(original);
        }

        // ══════════════════════════════════════════════════════════════
        // SEND / MIC
        // ══════════════════════════════════════════════════════════════

        private async void OnSendMicTapped(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(_viewModel.MessageText))
            {
                if (_viewModel.SendMessageCommand.CanExecute(null))
                    _viewModel.SendMessageCommand.Execute(null);
                return;
            }
            if (_viewModel.IsRecording) await StopRecordingAndSend();
            else await StartRecording();
        }

        // ══════════════════════════════════════════════════════════════
        // RECORDING
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
                _ = AnimateRecordingDot();
                _ = AnimateRecordingWave();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE] StartRecording: {ex.Message}");
                await DisplayAlert("Error", "Could not start recording.", "OK");
            }
        }

        private async Task StopRecordingAndSend()
        {
            try
            {
                if (_recorder == null) return;
                _waveCts?.Cancel();
                await _recorder.StopAsync();
                _viewModel.StopRecordingState();

                if (string.IsNullOrEmpty(_recordingFilePath) || !File.Exists(_recordingFilePath))
                {
                    await DisplayAlert("Error", "Recording file not found.", "OK");
                    return;
                }
                await _viewModel.SendVoiceMessageAsync(_recordingFilePath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE] StopRecordingAndSend: {ex.Message}");
                _viewModel.StopRecordingState();
                await DisplayAlert("Error", "Failed to process recording.", "OK");
            }
            finally { _recorder = null; _recordingFilePath = null; }
        }

        private async Task StopRecordingAndDiscard()
        {
            try
            {
                _waveCts?.Cancel();
                if (_recorder != null) { await _recorder.StopAsync(); _recorder = null; }
                if (!string.IsNullOrEmpty(_recordingFilePath) && File.Exists(_recordingFilePath))
                    File.Delete(_recordingFilePath);
                _recordingFilePath = null;
                _viewModel.StopRecordingState();
            }
            catch (Exception ex) { Debug.WriteLine($"[CHAT PAGE] Discard: {ex.Message}"); }
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

        private async Task AnimateRecordingWave()
        {
            _waveCts?.Cancel();
            _waveCts = new CancellationTokenSource();
            var token = _waveCts.Token;
            var bars = new[] { Wave1, Wave2, Wave3, Wave4, Wave5, Wave6, Wave7, Wave8 };
            var rnd = new Random();
            try
            {
                while (!token.IsCancellationRequested && _viewModel.IsRecording)
                {
                    var tasks = bars.Select(b =>
                        b.ScaleYTo(0.3 + rnd.NextDouble() * 1.4, 150, Easing.SinInOut));
                    await Task.WhenAll(tasks);
                    await Task.Delay(100, token);
                }
            }
            catch (TaskCanceledException) { }
            finally { foreach (var b in bars) b.ScaleY = 1; }
        }

        // ══════════════════════════════════════════════════════════════
        // VOICE PLAYBACK
        // ══════════════════════════════════════════════════════════════

        private async void OnPlayVoiceTapped(object sender, EventArgs e)
        {
            try
            {
                GroupMessageItemViewModel? msg = null;
                if (e is TappedEventArgs ta && ta.Parameter is GroupMessageItemViewModel vm1) msg = vm1;
                if (msg == null && sender is Element el) msg = el.BindingContext as GroupMessageItemViewModel;
                if (msg == null) return;

                if (_playingMessage == msg && (_audioPlayer?.IsPlaying ?? false))
                {
                    StopAudioPlayback();
                    return;
                }

                StopAudioPlayback();

                if (string.IsNullOrEmpty(msg.LocalFilePath) || !File.Exists(msg.LocalFilePath))
                {
                    await DisplayAlert("Not Downloaded", "Please download the voice note first.", "OK");
                    return;
                }

                _audioPlayer = AudioManager.Current.CreatePlayer(File.OpenRead(msg.LocalFilePath));
                _playingMessage = msg;
                msg.SetPlaying(true);
                _audioPlayer.PlaybackEnded += OnPlaybackEnded;
                _audioPlayer.Play();
            }
            catch (Exception ex) { Debug.WriteLine($"[CHAT PAGE] PlayVoice: {ex.Message}"); StopAudioPlayback(); }
        }

        private void OnPlaybackEnded(object? sender, EventArgs e) =>
            MainThread.BeginInvokeOnMainThread(StopAudioPlayback);

        private void StopAudioPlayback()
        {
            try
            {
                if (_audioPlayer != null)
                {
                    _audioPlayer.PlaybackEnded -= OnPlaybackEnded;
                    if (_audioPlayer.IsPlaying) _audioPlayer.Stop();
                    _audioPlayer.Dispose();
                    _audioPlayer = null;
                }
                _playingMessage?.SetPlaying(false);
                _playingMessage = null;
            }
            catch (Exception ex) { Debug.WriteLine($"[CHAT PAGE] StopPlayback: {ex.Message}"); }
        }

        // ══════════════════════════════════════════════════════════════
        // CONTEXT MENU ACTIONS
        // ══════════════════════════════════════════════════════════════

        private void OnContextMenuDismissed(object sender, EventArgs e) =>
            _viewModel.CloseContextMenu();

        private async void OnContextReplyTapped(object sender, EventArgs e)
        {
            _viewModel.ReplyToSelected();
            _viewModel.CloseContextMenu();
            await Task.Delay(80);
            MessageEditor?.Focus();
        }

        private async void OnContextReplyPrivatelyTapped(object sender, EventArgs e)
        {
            var msg = _viewModel.SelectedMessage;
            _viewModel.CloseContextMenu();
            if (msg == null || msg.IsFromCurrentUser) return;

            try
            {
                var quotedText = msg.IsVoiceMessage
                    ? "🎙 Voice note"
                    : Truncate(msg.Message, 120);

                // Pass GroupId so the private chat can navigate back to us
                await Shell.Current.GoToAsync(
                    $"PrivateChatPage" +
                    $"?ConversationId=PENDING_{msg.SenderId}" +
                    $"&OtherUserId={Uri.EscapeDataString(msg.SenderId.ToString())}" +
                    $"&OtherUserName={Uri.EscapeDataString(msg.DisplayName)}" +
                    $"&QuotedGroupSender={Uri.EscapeDataString(msg.DisplayName)}" +
                    $"&QuotedGroupMessage={Uri.EscapeDataString(quotedText ?? string.Empty)}" +
                    $"&QuotedGroupMessageId={Uri.EscapeDataString(msg.Id.ToString())}" +
                    $"&SourceGroupId={Uri.EscapeDataString(_viewModel.GroupId.ToString())}" +
                    $"&SourceGroupName={Uri.EscapeDataString(_viewModel.GroupName)}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE] ReplyPrivately: {ex.Message}");
                await DisplayAlert("Error", "Could not open private chat.", "OK");
            }
        }

        private async void OnContextForwardTapped(object sender, EventArgs e)
        {
            var msg = _viewModel.SelectedMessage;
            _viewModel.CloseContextMenu();
            if (msg == null) return;
            await DisplayAlert("Forward Message",
                $"Forward to another chat — coming soon!\n\n\"{Truncate(msg.Message, 60)}\"", "OK");
        }

        private async void OnContextCopyTapped(object sender, EventArgs e)
        {
            var text = _viewModel.SelectedMessage?.Message;
            _viewModel.CloseContextMenu();
            if (string.IsNullOrEmpty(text))
            {
                await DisplayAlert("Nothing to Copy", "This message has no text content.", "OK");
                return;
            }
            await Clipboard.SetTextAsync(text);
            _ = ShowToastAsync("Message copied");
        }

        private async void OnContextStarTapped(object sender, EventArgs e)
        {
            var msg = _viewModel.SelectedMessage;
            _viewModel.CloseContextMenu();
            if (msg == null) return;
            msg.ToggleStar();
            await _viewModel.PersistMessagesPublicAsync();
            _ = ShowToastAsync(msg.IsStarred ? "Message starred ⭐" : "Star removed");
        }

        private async void OnContextDeleteTapped(object sender, EventArgs e)
        {
            var msg = _viewModel.SelectedMessage;
            _viewModel.CloseContextMenu();
            if (msg == null) return;

            string? action;
            if (msg.IsFromCurrentUser)
            {
                action = await DisplayActionSheet(
                    "Delete message?", "Cancel", null,
                    "Delete for Everyone", "Delete for Me");
            }
            else
            {
                action = await DisplayActionSheet(
                    "Delete message?", "Cancel", null, "Delete for Me");
            }

            switch (action)
            {
                case "Delete for Me":
                    await _viewModel.DeleteMessageForSelfAsync(msg);
                    _ = ShowToastAsync("Message deleted");
                    break;
                case "Delete for Everyone":
                    await _viewModel.DeleteMessageForEveryoneAsync(msg);
                    _ = ShowToastAsync("Message deleted for everyone");
                    break;
            }
        }

        private void OnCancelReplyTapped(object sender, EventArgs e) =>
            _viewModel.CancelReply();

        // ══════════════════════════════════════════════════════════════
        // SCROLL TO MESSAGE (called by PrivateChatPage via navigation param)
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Called from OnAppearing when the page receives a ScrollToMessageId query param.
        /// Scrolls the CollectionView to the target message and briefly highlights it.
        /// </summary>
        public void ScrollToMessage(Guid messageId)
        {
            var target = _viewModel.Messages.FirstOrDefault(m => m.Id == messageId);
            if (target == null) return;

            MessagesCollectionView.ScrollTo(target, position: ScrollToPosition.Center, animate: true);
            _viewModel.HighlightMessage(target);
        }

        // ══════════════════════════════════════════════════════════════
        // DOWNLOAD & VIEW ATTACHMENT
        // ══════════════════════════════════════════════════════════════

        private async void OnDownloadAttachment(object sender, EventArgs e)
        {
            GroupMessageItemViewModel? message = null;
            try
            {
                if (e is TappedEventArgs ta && ta.Parameter is GroupMessageItemViewModel vm1)
                    message = vm1;
                if (message == null && sender is Element el)
                    message = el.BindingContext as GroupMessageItemViewModel;

                if (message == null) { await DisplayAlert("Error", "Invalid attachment", "OK"); return; }
                if (string.IsNullOrEmpty(message.AttachmentUrl))
                { await DisplayAlert("Error", "No attachment URL", "OK"); return; }

                if (message.IsDownloaded) { await OpenDownloadedFile(message); return; }
                if (message.IsDownloading) { message.CancelDownload(); return; }

                var downloadUrl = BuildAbsoluteUrl(message.AttachmentUrl);
                message.MarkAsDownloading();

                try
                {
                    var dir = Path.Combine(FileSystem.CacheDirectory, "downloads");
                    Directory.CreateDirectory(dir);
                    var fileName = message.AttachmentName ?? $"file_{message.Id}{message.AttachmentType}";
                    var filePath = Path.Combine(dir, fileName);

                    using var http = CreateHttpClient();
                    var bytes = await http.GetByteArrayAsync(downloadUrl);
                    if (!message.IsDownloading) return;

                    await File.WriteAllBytesAsync(filePath, bytes);
                    message.MarkAsDownloaded(filePath);

                    if (message.IsDocumentAttachment || message.IsVoiceMessage)
                        await OpenDownloadedFile(message);
                }
                catch (Exception ex)
                {
                    message?.CancelDownload();
                    await DisplayAlert("Error", $"Failed to download: {ex.Message}", "OK");
                }
            }
            catch (Exception ex)
            {
                message?.CancelDownload();
                Debug.WriteLine($"[CHAT PAGE] Download: {ex.Message}");
                await DisplayAlert("Error", "Failed to process attachment", "OK");
            }
        }

        private async Task OpenDownloadedFile(GroupMessageItemViewModel message)
        {
            try
            {
                await Launcher.OpenAsync(new OpenFileRequest
                {
                    File = new ReadOnlyFile(message.LocalFilePath),
                    Title = "Open with"
                });
            }
            catch
            {
                var share = await DisplayAlert("Downloaded",
                    "File ready. Would you like to share it?", "Share", "OK");
                if (share)
                    await Share.RequestAsync(new ShareFileRequest
                    {
                        Title = "Share file",
                        File = new ShareFile(message.LocalFilePath)
                    });
            }
        }

        private async void OnImageTapped(object sender, EventArgs e)
        {
            try
            {
                var msg = (sender as Image)?.BindingContext as GroupMessageItemViewModel;
                if (msg == null) return;
                if (!msg.IsDownloaded)
                { await DisplayAlert("Not Downloaded", "Download the image first.", "OK"); return; }

                await Shell.Current.GoToAsync("ImageViewerPage", new Dictionary<string, object>
                {
                    { "ImagePath", msg.LocalFilePath },
                    { "FileName",  msg.AttachmentName }
                });
            }
            catch (Exception ex) { Debug.WriteLine($"[CHAT PAGE] ImageTap: {ex.Message}"); }
        }

        // ══════════════════════════════════════════════════════════════
        // HELPERS
        // ══════════════════════════════════════════════════════════════

        private string BuildAbsoluteUrl(string url)
        {
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return url;
            return $"{_baseUrl}/{url.TrimStart('/')}";
        }

        private static HttpClient CreateHttpClient()
        {
#if ANDROID
            var handler = new Xamarin.Android.Net.AndroidMessageHandler
            {
                ServerCertificateCustomValidationCallback = (m, c, ch, e) => true
            };
#else
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (m, c, ch, e) => true
            };
#endif
            return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        }

        private async Task ShowToastAsync(string message)
        {
            try
            {
                var toast = new Border
                {
                    BackgroundColor = Color.FromArgb("#CC111B21"),
                    StrokeThickness = 0,
                    Padding = new Thickness(18, 8),
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.End,
                    Margin = new Thickness(0, 0, 0, 80),
                    ZIndex = 200,
                    StrokeShape = new RoundRectangle { CornerRadius = 20 },
                    Content = new Label { Text = message, TextColor = Colors.White, FontSize = 14 }
                };

                if (Content is Grid rootGrid)
                {
                    rootGrid.Children.Add(toast);
                    toast.Opacity = 0;
                    await toast.FadeTo(1, 200);
                    await Task.Delay(1500);
                    await toast.FadeTo(0, 300);
                    rootGrid.Children.Remove(toast);
                }
            }
            catch { }
        }

        private static string Truncate(string? s, int max) =>
            string.IsNullOrEmpty(s) ? string.Empty
            : s.Length <= max ? s
            : s[..max] + "…";

        // ══════════════════════════════════════════════════════════════
        // UI EVENT HANDLERS
        // ══════════════════════════════════════════════════════════════

        private async void OnBackButtonTapped(object sender, EventArgs e)
        {
            if (_viewModel.IsRecording) await StopRecordingAndDiscard();
            StopAudioPlayback();
            await Shell.Current.GoToAsync("..");
        }

        private void OnEmojiButtonTapped(object sender, EventArgs e) =>
            Debug.WriteLine("[CHAT PAGE] Emoji — TODO");

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
                    await DisplayAlert("Coming Soon", "File upload coming soon!", "OK");
            }
            catch (Exception ex) { Debug.WriteLine($"[CHAT PAGE] FilePick: {ex.Message}"); }
        }

        private async void OnCameraButtonTapped(object sender, EventArgs e)
        {
            try
            {
                if (MediaPicker.Default.IsCaptureSupported)
                {
                    var photo = await MediaPicker.Default.CapturePhotoAsync();
                    if (photo != null)
                        await DisplayAlert("Coming Soon", "Photo upload coming soon!", "OK");
                }
                else await DisplayAlert("Not Supported", "Camera not available.", "OK");
            }
            catch (Exception ex) { Debug.WriteLine($"[CHAT PAGE] Camera: {ex.Message}"); }
        }

        private void OnMessageEditorFocused(object sender, FocusEventArgs e) { }
        private void OnMessageEditorUnfocused(object sender, FocusEventArgs e) { }
    }
}