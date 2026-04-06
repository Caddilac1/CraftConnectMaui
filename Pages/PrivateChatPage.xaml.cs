using CraftConnect_Mobile_App.PageModels;
using CraftConnect_Mobile_App.Services;
using System.Diagnostics;

namespace CraftConnect_Mobile_App.Pages
{
    public partial class PrivateChatPage : ContentPage
    {
        private readonly PrivateChatPageModel _viewModel;

        // ── Long-press via PanGestureRecognizer ───────────────────────────
        // PanUpdated fires on touch-DOWN (Started) immediately — giving us
        // reliable 500 ms long-press without waiting for touch-UP.
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
            try { await _viewModel.CleanupAsync(); }
            catch { }
        }

        // ── Long-press via PanGestureRecognizer ───────────────────────────
        //
        // GestureStatus.Started fires on the very first touch contact — before
        // any movement. Timer starts there. If finger moves significantly
        // (scroll intent) the timer is cancelled. If held for 500 ms, context
        // menu opens while finger is still down.

        private void OnMessagePanUpdated(object sender, PanUpdatedEventArgs e)
        {
            var msg = (sender as Element)?.BindingContext as PrivateMessageItemViewModel;
            if (msg == null) return;

            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    if ((DateTime.UtcNow - _lastContextOpen).TotalMilliseconds < ContextCooldownMs)
                        return;

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
                    // Cancel if the user is actually scrolling
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

        // ── Group quote banner tap → navigate back to group chat ──────────

        private async void OnGroupQuoteTapped(object sender, TappedEventArgs e)
        {
            PrivateMessageItemViewModel? msg = null;
            if (e.Parameter is PrivateMessageItemViewModel pvm) msg = pvm;
            else if (sender is Element el) msg = el.BindingContext as PrivateMessageItemViewModel;

            if (msg == null || !msg.HasGroupQuote) return;

            // If we don't have a source group to navigate back to, just go back
            if (string.IsNullOrEmpty(_viewModel.SourceGroupId))
            {
                await Shell.Current.GoToAsync("..");
                return;
            }

            try
            {
                // Build navigation query. ScrollToMessageId tells ChatPage which
                // message to scroll to and highlight.
                var queryParams = $"?GroupId={Uri.EscapeDataString(_viewModel.SourceGroupId)}" +
                                  $"&GroupName={Uri.EscapeDataString(_viewModel.SourceGroupName ?? string.Empty)}";

                if (msg.QuotedGroupMessageId.HasValue)
                    queryParams += $"&ScrollToMessageId={Uri.EscapeDataString(msg.QuotedGroupMessageId.Value.ToString())}";

                // Go back to the group chat page. Using ".." navigates back in the
                // shell stack; if the group chat is still in the stack it will reuse it.
                // We pass the scroll param via GoToAsync so OnAppearing can pick it up.
                await Shell.Current.GoToAsync($"..{queryParams}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DM PAGE] GroupQuoteTapped: {ex.Message}");
                await Shell.Current.GoToAsync("..");
            }
        }

        // ── Input actions ─────────────────────────────────────────────────

        private void OnSendTapped(object sender, EventArgs e)
        {
            if (_viewModel.SendMessageCommand.CanExecute(null))
                _viewModel.SendMessageCommand.Execute(null);
        }

        private void OnCancelReplyTapped(object sender, EventArgs e) =>
            _viewModel.CancelReply();

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

        private async void OnContextCopyTapped(object sender, EventArgs e)
        {
            var text = _viewModel.SelectedMessage?.Message;
            _viewModel.CloseContextMenu();
            if (string.IsNullOrEmpty(text))
            {
                await DisplayAlert("Nothing to Copy", "No text to copy.", "OK");
                return;
            }
            await Clipboard.SetTextAsync(text);
            await ShowToastAsync("Copied");
        }

        private async void OnContextStarTapped(object sender, EventArgs e)
        {
            var msg = _viewModel.SelectedMessage;
            _viewModel.CloseContextMenu();
            if (msg == null) return;
            msg.ToggleStar();
            await ShowToastAsync(msg.IsStarred ? "Starred ⭐" : "Star removed");
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
                    await ShowToastAsync("Message deleted");
                    break;
                case "Delete for Everyone":
                    await _viewModel.DeleteForEveryoneAsync(msg);
                    await ShowToastAsync("Deleted for everyone");
                    break;
            }
        }

        // ── Navigation ────────────────────────────────────────────────────

        private async void OnBackTapped(object sender, EventArgs e)
        {
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
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 20 },
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
    }
}