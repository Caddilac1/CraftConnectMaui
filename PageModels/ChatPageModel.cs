using System.Collections.ObjectModel;
using System.Diagnostics;
using CraftConnect_Mobile_App.Models;
using CraftConnect_Mobile_App.Services;

namespace CraftConnect_Mobile_App.PageModels
{
    [QueryProperty(nameof(GroupIdString), "GroupId")]  // ✅ Changed to receive as string
    [QueryProperty(nameof(GroupName), nameof(GroupName))]
    public class ChatPageModel : BasePageModel
    {
        private readonly IChatService _chatService;
        private readonly AuthService _authService;

        private Guid _groupId;
        private string _groupName = string.Empty;
        private string _messageText = string.Empty;
        private string _currentUserId = string.Empty;

        public ObservableCollection<GroupMessageItemViewModel> Messages { get; } = new();

        public Command LoadMessagesCommand { get; }
        public Command SendMessageCommand { get; }
        public Command RefreshCommand { get; }

        public ChatPageModel(IChatService chatService, AuthService authService)
        {
            _chatService = chatService;
            _authService = authService;

            LoadMessagesCommand = new Command(async () => await LoadMessages());
            SendMessageCommand = new Command(async () => await SendMessage(), () => !string.IsNullOrWhiteSpace(MessageText) && !IsBusy);
            RefreshCommand = new Command(async () => await RefreshMessages());

            Debug.WriteLine("[CHAT DETAILS VM] Initialized");
        }

        // ✅ NEW: String property for QueryProperty to handle string-to-Guid conversion
        public string GroupIdString
        {
            set
            {
                if (Guid.TryParse(value, out var guidValue))
                {
                    GroupId = guidValue;
                    Debug.WriteLine($"[CHAT DETAILS VM] GroupIdString parsed: {value} → {guidValue}");
                }
                else
                {
                    Debug.WriteLine($"[CHAT DETAILS VM] ❌ Failed to parse GroupId: {value}");
                }
            }
        }

        public Guid GroupId
        {
            get => _groupId;
            set
            {
                _groupId = value;
                OnPropertyChanged();
                Debug.WriteLine($"[CHAT DETAILS VM] GroupId set to: {value}");
            }
        }

        public string GroupName
        {
            get => _groupName;
            set
            {
                _groupName = value;
                OnPropertyChanged();
                Debug.WriteLine($"[CHAT DETAILS VM] GroupName set to: {value}");
            }
        }

        public string MessageText
        {
            get => _messageText;
            set
            {
                _messageText = value;
                OnPropertyChanged();
                ((Command)SendMessageCommand).ChangeCanExecute();
            }
        }

        public string CurrentUserId
        {
            get => _currentUserId;
            set
            {
                _currentUserId = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Initialize the page - called when page appears
        /// </summary>
        public async Task InitializeAsync()
        {
            Debug.WriteLine($"[CHAT DETAILS VM] InitializeAsync called for group: {GroupId}");

            // Get current user ID
            var userInfo = await _authService.GetCurrentUserAsync();
            CurrentUserId = userInfo.UserId;
            Debug.WriteLine($"[CHAT DETAILS VM] Current user ID: {CurrentUserId}");

            // Load messages
            await LoadMessages();
        }

        /// <summary>
        /// Load messages for the current group
        /// </summary>
        private async Task LoadMessages()
        {
            if (IsBusy || GroupId == Guid.Empty)
            {
                Debug.WriteLine($"[CHAT DETAILS VM] LoadMessages skipped - IsBusy: {IsBusy}, GroupId: {GroupId}");
                return;
            }

            try
            {
                Debug.WriteLine($"[CHAT DETAILS VM] Loading messages for group: {GroupId}");
                IsBusy = true;

                var messages = await _chatService.GetGroupMessagesAsync(GroupId);

                Debug.WriteLine($"[CHAT DETAILS VM] Received {messages.Count} messages");

                // Clear and reload
                Messages.Clear();
                foreach (var message in messages.OrderBy(m => m.SentAt))
                {
                    // Wrap in ViewModel to add UI properties
                    var viewModel = new GroupMessageItemViewModel(message, CurrentUserId);
                    Messages.Add(viewModel);
                }

                Debug.WriteLine($"[CHAT DETAILS VM] ✅ Messages loaded successfully. Total in collection: {Messages.Count}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.WriteLine($"[CHAT DETAILS VM] ❌ Unauthorized: {ex.Message}");
                await Application.Current.MainPage.DisplayAlert(
                    "Session Expired",
                    "Your session has expired. Please login again.",
                    "OK");
                await Shell.Current.GoToAsync("//auth/LoginPage");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT DETAILS VM] ❌ Error loading messages: {ex.Message}");
                await Application.Current.MainPage.DisplayAlert(
                    "Error",
                    $"Failed to load messages: {ex.Message}",
                    "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Send a new message to the group
        /// </summary>
        private async Task SendMessage()
        {
            if (string.IsNullOrWhiteSpace(MessageText) || IsBusy)
            {
                Debug.WriteLine($"[CHAT DETAILS VM] SendMessage skipped - Empty message or busy");
                return;
            }

            try
            {
                Debug.WriteLine($"[CHAT DETAILS VM] Sending message to group: {GroupId}");
                Debug.WriteLine($"[CHAT DETAILS VM] Message: {MessageText}");

                IsBusy = true;

                var success = await _chatService.SendMessageAsync(GroupId, MessageText);

                if (success)
                {
                    Debug.WriteLine($"[CHAT DETAILS VM] ✅ Message sent successfully");

                    // Clear the input
                    var sentMessage = MessageText;
                    MessageText = string.Empty;

                    // Add message to UI immediately (optimistic update)
                    var newMessage = new GroupMessageItem
                    {
                        Id = Guid.NewGuid(), // Temporary ID
                        Message = sentMessage,
                        SenderId = Guid.Parse(CurrentUserId),
                        SenderName = "You",
                        SenderFullName = "You",
                        SentAt = DateTime.Now
                    };

                    var viewModel = new GroupMessageItemViewModel(newMessage, CurrentUserId);
                    Messages.Add(viewModel);

                    // Reload messages to get the actual message from server
                    await Task.Delay(500); // Small delay
                    await LoadMessages();
                }
                else
                {
                    Debug.WriteLine($"[CHAT DETAILS VM] ❌ Failed to send message");
                    await Application.Current.MainPage.DisplayAlert(
                        "Error",
                        "Failed to send message. Please try again.",
                        "OK");
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.WriteLine($"[CHAT DETAILS VM] ❌ Unauthorized: {ex.Message}");
                await Application.Current.MainPage.DisplayAlert(
                    "Session Expired",
                    "Your session has expired. Please login again.",
                    "OK");
                await Shell.Current.GoToAsync("//auth/LoginPage");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT DETAILS VM] ❌ Error sending message: {ex.Message}");
                await Application.Current.MainPage.DisplayAlert(
                    "Error",
                    $"Failed to send message: {ex.Message}",
                    "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Refresh messages (pull to refresh)
        /// </summary>
        private async Task RefreshMessages()
        {
            Debug.WriteLine($"[CHAT DETAILS VM] Refreshing messages...");
            await LoadMessages();
        }
    }

    /// <summary>
    /// ViewModel wrapper for GroupMessageItem that adds UI-specific properties
    /// </summary>
    public class GroupMessageItemViewModel
    {
        private readonly GroupMessageItem _message;
        private readonly string _currentUserId;

        public GroupMessageItemViewModel(GroupMessageItem message, string currentUserId)
        {
            _message = message;
            _currentUserId = currentUserId;
        }

        // Original properties from GroupMessageItem
        public Guid Id => _message.Id;
        public string Message => _message.Message;
        public DateTime SentAt => _message.SentAt;
        public Guid SenderId => _message.SenderId;
        public string SenderName => _message.SenderName;
        public string SenderFullName => _message.SenderFullName;

        // UI-specific properties
        public bool IsFromCurrentUser => SenderId.ToString() == _currentUserId;

        public string DisplayName => IsFromCurrentUser ? "You" : (SenderFullName ?? SenderName ?? "Unknown");

        public string DisplayTime
        {
            get
            {
                var now = DateTime.Now;
                var diff = now - SentAt;

                if (diff.TotalMinutes < 1)
                    return "Just now";
                else if (diff.TotalHours < 1)
                    return $"{(int)diff.TotalMinutes}m ago";
                else if (diff.TotalDays < 1)
                    return SentAt.ToString("h:mm tt");
                else if (diff.TotalDays < 2)
                    return $"Yesterday {SentAt:h:mm tt}";
                else if (diff.TotalDays < 7)
                    return SentAt.ToString("ddd h:mm tt");
                else
                    return SentAt.ToString("MMM d, h:mm tt");
            }
        }
    }
}