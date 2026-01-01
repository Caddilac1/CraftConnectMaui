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
                OnPropertyChanged(nameof(MessageButtonIcon)); // Update button icon
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
            Debug.WriteLine($"[CHAT DETAILS VM] InitializeAsync called for group: {GroupId}");

            var userInfo = await _authService.GetCurrentUserAsync();
            CurrentUserId = userInfo.UserId;
            Debug.WriteLine($"[CHAT DETAILS VM] Current user ID: {CurrentUserId}");

            await LoadMessages();
        }

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

                Messages.Clear();
                foreach (var message in messages.OrderBy(m => m.SentAt))
                {
                    var viewModel = new GroupMessageItemViewModel(message, CurrentUserId);
                    Messages.Add(viewModel);
                }

                Debug.WriteLine($"[CHAT DETAILS VM] ✅ Messages loaded successfully. Total: {Messages.Count}");
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

                    var sentMessage = MessageText;
                    MessageText = string.Empty;

                    // Optimistic update
                    var newMessage = new GroupMessageItem
                    {
                        Id = Guid.NewGuid(),
                        Message = sentMessage,
                        SenderId = Guid.Parse(CurrentUserId),
                        SenderName = "You",
                        SenderFullName = "You",
                        SentAt = DateTime.Now
                    };

                    var viewModel = new GroupMessageItemViewModel(newMessage, CurrentUserId);
                    Messages.Add(viewModel);

                    await Task.Delay(500);
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

        private async Task RefreshMessages()
        {
            Debug.WriteLine($"[CHAT DETAILS VM] Refreshing messages...");
            await LoadMessages();
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

        // Attachment properties (for future use when backend supports it)
        public bool HasAttachment => !string.IsNullOrEmpty(AttachmentName);

        public string AttachmentName { get; set; } // Will come from backend

        public string AttachmentSize { get; set; } // Will come from backend

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