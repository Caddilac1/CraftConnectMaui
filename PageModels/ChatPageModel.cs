using System.Collections.ObjectModel;
using System.Diagnostics;
using CraftConnect_Mobile_App.Models;
using CraftConnect_Mobile_App.Services;

namespace CraftConnect_Mobile_App.PageModels
{
    [QueryProperty(nameof(GroupIdString), "GroupId")]
    [QueryProperty(nameof(GroupName), nameof(GroupName))]
    public class ChatPageModel : BasePageModel
    {
        private readonly IChatService _chatService;
        private readonly AuthService _authService;
        private readonly IChatSignalRService _signalRService;

        private Guid _groupId;
        private string _groupName = string.Empty;
        private string _messageText = string.Empty;
        private string _currentUserId = string.Empty;
        private string _currentUserName = string.Empty;
        private string _currentUserFullName = string.Empty;

        public ObservableCollection<GroupMessageItemViewModel> Messages { get; } = new();

        public Command LoadMessagesCommand { get; }
        public Command SendMessageCommand { get; }
        public Command RefreshCommand { get; }

        public ChatPageModel(IChatService chatService, AuthService authService, IChatSignalRService signalRService)
        {
            _chatService = chatService;
            _authService = authService;
            _signalRService = signalRService;

            LoadMessagesCommand = new Command(async () => await LoadMessages());
            SendMessageCommand = new Command(async () => await SendMessage(), () => !string.IsNullOrWhiteSpace(MessageText) && !IsBusy);
            RefreshCommand = new Command(async () => await RefreshMessages());

            // Subscribe to SignalR events
            _signalRService.MessageReceived += OnMessageReceived;

            Debug.WriteLine("[CHAT PAGE MODEL] Initialized");
        }

        public string GroupIdString
        {
            set
            {
                if (Guid.TryParse(value, out var guidValue))
                {
                    GroupId = guidValue;
                    Debug.WriteLine($"[CHAT PAGE MODEL] GroupIdString parsed: {value} → {guidValue}");
                }
                else
                {
                    Debug.WriteLine($"[CHAT PAGE MODEL] ❌ Failed to parse GroupId: {value}");
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
                Debug.WriteLine($"[CHAT PAGE MODEL] GroupId set to: {value}");
            }
        }

        public string GroupName
        {
            get => _groupName;
            set
            {
                _groupName = value;
                OnPropertyChanged();
                Debug.WriteLine($"[CHAT PAGE MODEL] GroupName set to: {value}");
            }
        }

        public string MessageText
        {
            get => _messageText;
            set
            {
                _messageText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MessageButtonIcon));
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
        /// Icon for the send/microphone button - changes based on message text
        /// </summary>
        public string MessageButtonIcon => string.IsNullOrWhiteSpace(MessageText) ? "\ue029" : "\ue163";

        public async Task InitializeAsync()
        {
            Debug.WriteLine($"[CHAT PAGE MODEL] InitializeAsync called for group: {GroupId}");

            try
            {
                // Get current user info
                var userInfo = await _authService.GetCurrentUserAsync();
                CurrentUserId = userInfo.UserId;
                _currentUserName = userInfo.FullName ?? "User";
                _currentUserFullName = userInfo.FullName ?? userInfo.FullName ?? "User";
                Debug.WriteLine($"[CHAT PAGE MODEL] Current user: {CurrentUserId} - {_currentUserFullName}");

                // Connect to SignalR if not connected
                if (!_signalRService.IsConnected)
                {
                    Debug.WriteLine("[CHAT PAGE MODEL] Connecting to SignalR...");
                    await _signalRService.ConnectAsync();
                }

                // Join the group chat
                Debug.WriteLine($"[CHAT PAGE MODEL] Joining group: {GroupId}");
                await _signalRService.JoinGroupAsync(GroupId.ToString());

                // Load existing messages
                await LoadMessages();

                Debug.WriteLine("[CHAT PAGE MODEL] ✅ Initialization complete");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE MODEL] ❌ Initialization error: {ex.Message}");
                throw;
            }
        }

        public async Task CleanupAsync()
        {
            Debug.WriteLine("[CHAT PAGE MODEL] Cleanup called");

            try
            {
                // Leave the group
                await _signalRService.LeaveGroupAsync(GroupId.ToString());
                Debug.WriteLine("[CHAT PAGE MODEL] ✅ Left group");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE MODEL] ❌ Cleanup error: {ex.Message}");
            }
        }

        private async Task LoadMessages()
        {
            if (GroupId == Guid.Empty)
            {
                Debug.WriteLine($"[CHAT PAGE MODEL] LoadMessages skipped - Invalid GroupId");
                return;
            }

            try
            {
                Debug.WriteLine($"[CHAT PAGE MODEL] Loading messages for group: {GroupId}");
                IsBusy = true;

                var messages = await _chatService.GetGroupMessagesAsync(GroupId);

                Debug.WriteLine($"[CHAT PAGE MODEL] Received {messages.Count} messages from API");

                // Clear and reload messages
                Messages.Clear();
                foreach (var message in messages.OrderBy(m => m.SentAt))
                {
                    var viewModel = new GroupMessageItemViewModel(message, CurrentUserId);
                    Messages.Add(viewModel);
                }

                Debug.WriteLine($"[CHAT PAGE MODEL] ✅ Messages loaded. Total: {Messages.Count}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.WriteLine($"[CHAT PAGE MODEL] ❌ Unauthorized: {ex.Message}");
                await Application.Current.MainPage.DisplayAlert(
                    "Session Expired",
                    "Your session has expired. Please login again.",
                    "OK");
                await Shell.Current.GoToAsync("//auth/LoginPage");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE MODEL] ❌ Error loading messages: {ex.Message}");
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

        private async Task SendMessage()
        {
            if (string.IsNullOrWhiteSpace(MessageText))
            {
                Debug.WriteLine($"[CHAT PAGE MODEL] SendMessage skipped - Empty message");
                return;
            }

            if (IsBusy)
            {
                Debug.WriteLine($"[CHAT PAGE MODEL] SendMessage skipped - Already busy");
                return;
            }

            var messageToSend = MessageText.Trim();

            try
            {
                Debug.WriteLine($"[CHAT PAGE MODEL] Sending message to group: {GroupId}");
                Debug.WriteLine($"[CHAT PAGE MODEL] Message: {messageToSend}");

                // Clear message text immediately for better UX
                MessageText = string.Empty;

                // Send via SignalR for real-time delivery
                await _signalRService.SendMessageAsync(
                    GroupId.ToString(),
                    messageToSend,
                    _currentUserName,
                    _currentUserFullName
                );

                Debug.WriteLine($"[CHAT PAGE MODEL] ✅ Message sent via SignalR");

                // Also save to backend via API
                var success = await _chatService.SendMessageAsync(GroupId, messageToSend);

                if (!success)
                {
                    Debug.WriteLine($"[CHAT PAGE MODEL] ⚠️ API save failed, but message sent via SignalR");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE MODEL] ❌ Error sending message: {ex.Message}");

                // Restore message text if send failed
                MessageText = messageToSend;

                await Application.Current.MainPage.DisplayAlert(
                    "Error",
                    $"Failed to send message: {ex.Message}",
                    "OK");
            }
        }

        private async Task RefreshMessages()
        {
            Debug.WriteLine($"[CHAT PAGE MODEL] Refreshing messages...");
            await LoadMessages();
        }

        /// <summary>
        /// Handle incoming SignalR messages
        /// </summary>
        private void OnMessageReceived(object sender, MessageReceivedEventArgs e)
        {
            try
            {
                Debug.WriteLine($"[CHAT PAGE MODEL] 📨 SignalR message received");
                Debug.WriteLine($"[CHAT PAGE MODEL]    Group: {e.GroupChatId}");
                Debug.WriteLine($"[CHAT PAGE MODEL]    Sender: {e.SenderFullName}");
                Debug.WriteLine($"[CHAT PAGE MODEL]    Message: {e.Message}");

                // Only add message if it's for this group
                if (e.GroupChatId != GroupId.ToString())
                {
                    Debug.WriteLine($"[CHAT PAGE MODEL] Message not for this group, ignoring");
                    return;
                }

                // Check if message already exists (to avoid duplicates)
                var existingMessage = Messages.FirstOrDefault(m => m.Id.ToString() == e.Id);
                if (existingMessage != null)
                {
                    Debug.WriteLine($"[CHAT PAGE MODEL] Message already exists, ignoring");
                    return;
                }

                // Convert to domain model
                var message = new GroupMessageItem
                {
                    Id = Guid.Parse(e.Id),
                    GroupChatId = Guid.Parse(e.GroupChatId),
                    SenderId = Guid.Parse(e.SenderId),
                    SenderName = e.SenderName,
                    SenderFullName = e.SenderFullName,
                    Message = e.Message,
                    SentAt = e.SentAt,
                    HasAttachment = e.HasAttachment,
                    AttachmentUrl = e.AttachmentUrl,
                    AttachmentName = e.AttachmentName,
                    AttachmentSize = e.AttachmentSize,
                    AttachmentType = e.AttachmentType
                };

                // Add to UI on main thread
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var viewModel = new GroupMessageItemViewModel(message, CurrentUserId);
                    Messages.Add(viewModel);
                    Debug.WriteLine($"[CHAT PAGE MODEL] ✅ Message added to UI. Total: {Messages.Count}");
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE MODEL] ❌ Error handling received message: {ex.Message}");
            }
        }

        ~ChatPageModel()
        {
            // Unsubscribe from events
            _signalRService.MessageReceived -= OnMessageReceived;
        }
    }

    /// <summary>
    /// ViewModel wrapper for GroupMessageItem with UI-specific properties
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

        public Guid Id => _message.Id;
        public string Message => _message.Message;
        public DateTime SentAt => _message.SentAt;
        public Guid SenderId => _message.SenderId;
        public string SenderName => _message.SenderName;
        public string SenderFullName => _message.SenderFullName;

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

        // Attachment properties
        public bool HasAttachment => _message.HasAttachment && !string.IsNullOrEmpty(_message.AttachmentName);
        public string AttachmentName => _message.AttachmentName;
        public string AttachmentSize => _message.AttachmentSize;
        public string AttachmentUrl => _message.AttachmentUrl;
        public string AttachmentType => _message.AttachmentType;

        public string AttachmentIcon
        {
            get
            {
                if (string.IsNullOrEmpty(AttachmentName))
                    return string.Empty;

                var extension = Path.GetExtension(AttachmentName)?.ToLower();
                return extension switch
                {
                    ".pdf" => "\ue415", // picture_as_pdf
                    ".doc" or ".docx" => "\ue873", // description
                    ".xls" or ".xlsx" => "\ue873", // description
                    ".jpg" or ".jpeg" or ".png" or ".gif" => "\ue3f4", // image
                    ".mp4" or ".mov" or ".avi" => "\ue04b", // videocam
                    ".mp3" or ".wav" => "\ue310", // audiotrack
                    _ => "\ue24d" // insert_drive_file
                };
            }
        }
    }
}