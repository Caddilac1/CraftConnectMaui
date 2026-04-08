using CraftConnect_Mobile_App.PageModels;
using CraftConnect_Mobile_App.Services;
using Microsoft.Maui.Controls.Shapes;
using System.Diagnostics;

namespace CraftConnect_Mobile_App.Pages
{
    public partial class PrivateChatPage : ContentPage
    {
        private readonly PrivateChatPageModel _viewModel;

        // ── Long-press via PanGestureRecognizer ───────────────────────────
        private CancellationTokenSource? _longPressCts;
        private PrivateMessageItemViewModel? _pendingLongPress;
        private const int LongPressMs = 500;

        private DateTime _lastContextOpen = DateTime.MinValue;
        private const int ContextCooldownMs = 800;

        public PrivateChatPage(PrivateChatPageModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        // ── Lifecycle ─────────────────────────────────────────────────────

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            try { await _viewModel.InitializeAsync(); }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DM PAGE] Init error: {ex.Message}");
                await DisplayAlert("Error", "Failed to load chat.", "OK");
            }
        }

        protected override async void OnDisappearing()
        {
            base.OnDisappearing();
            _longPressCts?.Cancel();
            _viewModel.CancelSelectionMode();
            try { await _viewModel.CleanupAsync(); }
            catch { }
        }

        // ── Long-press via PanGestureRecognizer ───────────────────────────

        private void OnMessagePanUpdated(object sender, PanUpdatedEventArgs e)
        {
            // In selection mode, tapping toggles selection instead of long-press menu
            if (_viewModel.IsSelectionMode)
            {
                if (e.StatusType == GestureStatus.Started)
                {
                    var msg = (sender as Element)?.BindingContext as PrivateMessageItemViewModel;
                    if (msg != null) _viewModel.ToggleMessageSelection(msg);
                }
                return;
            }

            var message = (sender as Element)?.BindingContext as PrivateMessageItemViewModel;
            if (message == null) return;

            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    if ((DateTime.UtcNow - _lastContextOpen).TotalMilliseconds < ContextCooldownMs)
                        return;

                    if (_viewModel.IsContextMenuVisible && _viewModel.SelectedMessage == message)
                    {
                        _viewModel.CloseContextMenu();
                        return;
                    }

                    _longPressCts?.Cancel();
                    _longPressCts = new CancellationTokenSource();
                    _pendingLongPress = message;
                    var token = _longPressCts.Token;

                    Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(LongPressMs, token);
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                if (!token.IsCancellationRequested && _pendingLongPress == message)
                                {
                                    _lastContextOpen = DateTime.UtcNow;
                                    _pendingLongPress = null;
                                    _viewModel.OpenContextMenu(message);
                                }
                            });
                        }
                        catch (TaskCanceledException) { }
                    }, token);
                    break;

                case GestureStatus.Running:
                    if (Math.Abs(e.TotalX) > 8 || Math.Abs(e.TotalY) > 8)
                    {
                        _longPressCts?.Cancel();
                        _pendingLongPress = null;
                    }
                    break;

                case GestureStatus.Completed:
                case GestureStatus.Canceled:
                    _longPressCts?.Cancel();
                    _pendingLongPress = null;
                    break;
            }
        }

        // ── Reply preview tap → scroll to original message ────────────────

        private void OnReplyPreviewTapped(object sender, TappedEventArgs e)
        {
            PrivateMessageItemViewModel? msg = null;
            if (e.Parameter is PrivateMessageItemViewModel pvm) msg = pvm;
            else if (sender is Element el) msg = el.BindingContext as PrivateMessageItemViewModel;

            if (msg?.ReplyToId == null) return;

            var original = _viewModel.Messages.FirstOrDefault(m => m.Id == msg.ReplyToId);
            if (original == null) return;

            MessagesCollectionView.ScrollTo(original, position: ScrollToPosition.Center, animate: true);
            _viewModel.HighlightMessage(original);
        }

        // ── Input ─────────────────────────────────────────────────────────

        private void OnSendTapped(object sender, EventArgs e)
        {
            if (_viewModel.SendMessageCommand.CanExecute(null))
                _viewModel.SendMessageCommand.Execute(null);
        }

        private void OnCancelReplyTapped(object sender, EventArgs e) =>
            _viewModel.CancelReply();

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
            catch (Exception ex) { Debug.WriteLine($"[DM PAGE] FilePick: {ex.Message}"); }
        }

        // ── Context menu ──────────────────────────────────────────────────

        private void OnContextMenuDismissed(object sender, EventArgs e) =>
            _viewModel.CloseContextMenu();

        private async void OnContextReplyTapped(object sender, EventArgs e)
        {
            _viewModel.ReplyToSelected();
            _viewModel.CloseContextMenu();
            await Task.Delay(80);
            MessageEditor?.Focus();
        }

        private async void OnContextForwardTapped(object sender, EventArgs e)
        {
            var msg = _viewModel.SelectedMessage;
            _viewModel.CloseContextMenu();
            if (msg == null) return;
            // Forward stub — wire to real forwarding logic when ready
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

        private async void OnContextPinTapped(object sender, EventArgs e)
        {
            var msg = _viewModel.SelectedMessage;
            _viewModel.CloseContextMenu();
            if (msg == null) return;

            _viewModel.TogglePin(msg);
            _ = ShowToastAsync(msg.IsPinned ? "Message pinned 📌" : "Message unpinned");
        }

        private async void OnContextStarTapped(object sender, EventArgs e)
        {
            var msg = _viewModel.SelectedMessage;
            _viewModel.CloseContextMenu();
            if (msg == null) return;
            msg.ToggleStar();
            _ = ShowToastAsync(msg.IsStarred ? "Message starred ⭐" : "Star removed");
        }

        private void OnContextSelectTapped(object sender, EventArgs e)
        {
            var msg = _viewModel.SelectedMessage;
            _viewModel.CloseContextMenu();
            if (msg == null) return;
            _viewModel.EnterSelectionMode(msg);
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
                    await _viewModel.DeleteForSelfAsync(msg);
                    _ = ShowToastAsync("Message deleted");
                    break;
                case "Delete for Everyone":
                    await _viewModel.DeleteForEveryoneAsync(msg);
                    _ = ShowToastAsync("Deleted for everyone");
                    break;
            }
        }

        // ── Selection mode toolbar ────────────────────────────────────────

        private void OnCancelSelectionTapped(object sender, EventArgs e) =>
            _viewModel.CancelSelectionMode();

        private async void OnForwardSelectedTapped(object sender, EventArgs e)
        {
            var count = _viewModel.SelectedMessages.Count;
            _viewModel.CancelSelectionMode();
            await DisplayAlert("Forward", $"{count} message(s) — forward coming soon!", "OK");
        }

        private async void OnStarSelectedTapped(object sender, EventArgs e)
        {
            _viewModel.StarSelectedMessages();
            var count = _viewModel.SelectedMessages.Count;
            _viewModel.CancelSelectionMode();
            _ = ShowToastAsync($"{count} message(s) starred ⭐");
        }

        private async void OnDeleteSelectedTapped(object sender, EventArgs e)
        {
            var count = _viewModel.SelectedMessages.Count;
            if (count == 0) return;

            var action = await DisplayActionSheet(
                $"Delete {count} message(s)?", "Cancel", null,
                "Delete for Everyone", "Delete for Me");

            var messages = _viewModel.SelectedMessages.ToList();
            _viewModel.CancelSelectionMode();

            switch (action)
            {
                case "Delete for Me":
                    foreach (var m in messages)
                        await _viewModel.DeleteForSelfAsync(m);
                    _ = ShowToastAsync($"{count} message(s) deleted");
                    break;
                case "Delete for Everyone":
                    foreach (var m in messages)
                        await _viewModel.DeleteForEveryoneAsync(m);
                    _ = ShowToastAsync($"{count} message(s) deleted for everyone");
                    break;
            }
        }

        // ── Navigation ────────────────────────────────────────────────────

        private async void OnBackTapped(object sender, EventArgs e)
        {
            if (_viewModel.IsSelectionMode)
            {
                _viewModel.CancelSelectionMode();
                return;
            }
            await Shell.Current.GoToAsync("..");
        }

        // ── Toast ─────────────────────────────────────────────────────────

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
                    Content = new Label
                    {
                        Text = message,
                        TextColor = Colors.White,
                        FontSize = 14
                    }
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
    }
}