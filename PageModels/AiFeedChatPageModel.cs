using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CraftConnect_Mobile_App.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace CraftConnect_Mobile_App.PageModels
{
    public partial class AiFeedChatPageModel : ObservableObject
    {
        private readonly AiFeedChatService _aiFeedService;
        private Guid _sessionId;

        [ObservableProperty]
        private string _messageText = string.Empty;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _statusText = "Online";

        [ObservableProperty]
        private bool _hasAttachedFiles;

        public ObservableCollection<AiChatMessageViewModel> Messages { get; } = new();
        public ObservableCollection<AttachedFileViewModel> AttachedFiles { get; } = new();

        public AiFeedChatPageModel(AiFeedChatService aiFeedService)
        {
            _aiFeedService = aiFeedService;
            _sessionId = Guid.NewGuid();

            Debug.WriteLine($"[AI CHAT MODEL] Session ID: {_sessionId}");
        }

        public async Task InitializeAsync()
        {
            Debug.WriteLine("[AI CHAT MODEL] Initializing...");
            IsBusy = true;

            try
            {
                // Load auth token if available
                var token = Preferences.Get("auth_token", string.Empty);
                if (!string.IsNullOrEmpty(token))
                {
                    _aiFeedService.SetAuthToken(token);
                }

                // Send initial greeting request (empty message triggers greeting)
                await SendInitialGreeting();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AI CHAT MODEL] ❌ Init error: {ex.Message}");
                await AddAiMessage("Sorry, I'm having trouble connecting. Please check your internet connection and try again.", false);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task SendInitialGreeting()
        {
            try
            {
                var response = await _aiFeedService.SendMessageAsync(_sessionId, "");

                if (response != null && !string.IsNullOrEmpty(response.Message))
                {
                    await AddAiMessage(response.Message, false);
                }
                else
                {
                    await AddAiMessage("Hello! 👋 I'm your AI assistant. I can help you create a professional feed post. Would you like to get started?", false);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AI CHAT MODEL] ❌ Greeting error: {ex.Message}");
                throw;
            }
        }

        [RelayCommand]
        private async Task SendMessage()
        {
            if (string.IsNullOrWhiteSpace(MessageText))
                return;

            var userMessage = MessageText.Trim();
            MessageText = string.Empty;

            // Add user message immediately
            await AddUserMessage(userMessage);

            // Show typing indicator
            var typingMessage = new AiChatMessageViewModel
            {
                IsFromAi = true,
                IsTyping = true,
                Timestamp = DateTime.Now
            };
            Messages.Add(typingMessage);

            try
            {
                StatusText = "AI is thinking...";

                // Send to API via service
                var response = await _aiFeedService.SendMessageAsync(_sessionId, userMessage);

                // Remove typing indicator
                Messages.Remove(typingMessage);

                if (response != null)
                {
                    await AddAiMessage(response.Message, false);
                    StatusText = "Online";

                    // Check if ready to create feed
                    if (response.ReadyToCreate)
                    {
                        await Task.Delay(1000); // Brief pause before creating
                        await CreateFeed();
                    }
                }
                else
                {
                    await AddAiMessage("Sorry, I didn't receive a response. Please try again.", false);
                    StatusText = "Error";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AI CHAT MODEL] ❌ Send error: {ex.Message}");
                Messages.Remove(typingMessage);

                var errorMessage = ex.Message.Contains("Network")
                    ? "Network error. Please check your connection."
                    : ex.Message.Contains("timed out")
                    ? "Request timed out. Please try again."
                    : "Sorry, something went wrong. Please try again.";

                await AddAiMessage(errorMessage, false);
                StatusText = "Error";
            }
        }

        private async Task AddUserMessage(string message)
        {
            var msg = new AiChatMessageViewModel
            {
                Message = message,
                IsFromUser = true,
                IsFromAi = false,
                Timestamp = DateTime.Now
            };

            Messages.Add(msg);
            await Task.Delay(100);
        }

        private async Task AddAiMessage(string message, bool isTyping)
        {
            if (isTyping)
            {
                await Task.Delay(800);
            }

            var msg = new AiChatMessageViewModel
            {
                Message = message,
                IsFromAi = true,
                IsFromUser = false,
                Timestamp = DateTime.Now
            };

            Messages.Add(msg);
            await Task.Delay(100);
        }

        /// <summary>
        /// Handles file attachment from the UI
        /// </summary>
        public async Task AttachFile(FileResult file, string fileType)
        {
            if (file == null) return;

            try
            {
                IsBusy = true;
                StatusText = "Uploading file...";

                Debug.WriteLine($"[AI CHAT MODEL] Attaching file: {file.FileName}");

                // Open file stream
                using var stream = await file.OpenReadAsync();

                // Upload via service
                var response = await _aiFeedService.UploadFileAsync(
                    _sessionId,
                    stream,
                    file.FileName,
                    fileType
                );

                if (response != null && response.Success)
                {
                    // Add to attached files list
                    var attachedFile = new AttachedFileViewModel
                    {
                        FileName = file.FileName,
                        FileType = fileType == "invoice" ? "Invoice" : "Document",
                        FilePath = file.FullPath ?? ""
                    };

                    AttachedFiles.Add(attachedFile);
                    HasAttachedFiles = AttachedFiles.Count > 0;

                    // Show confirmation in chat
                    var fileEmoji = fileType == "invoice" ? "🧾" : "📎";
                    await AddUserMessage($"{fileEmoji} Attached: {file.FileName}");

                    await Task.Delay(500);

                    var confirmMessage = fileType == "invoice"
                        ? "Great! I've received your invoice. You can attach more documents or type 'done' to continue."
                        : "Perfect! I've got your document. Feel free to attach more or type 'done' when ready.";

                    await AddAiMessage(confirmMessage, false);

                    Debug.WriteLine($"[AI CHAT MODEL] ✅ File uploaded successfully");
                }
                else
                {
                    await AddAiMessage("Sorry, I couldn't upload that file. Please try again.", false);
                }

                StatusText = "Online";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AI CHAT MODEL] ❌ Upload error: {ex.Message}");

                var errorMessage = ex.Message.Contains("upload")
                    ? ex.Message
                    : "Failed to upload file. Please check your connection and try again.";

                await AddAiMessage(errorMessage, false);
                StatusText = "Online";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void RemoveFile(AttachedFileViewModel file)
        {
            Debug.WriteLine($"[AI CHAT MODEL] Removing file: {file.FileName}");
            AttachedFiles.Remove(file);
            HasAttachedFiles = AttachedFiles.Count > 0;
        }

        private async Task CreateFeed()
        {
            try
            {
                StatusText = "Creating your feed...";
                IsBusy = true;

                Debug.WriteLine("[AI CHAT MODEL] Creating feed...");

                var response = await _aiFeedService.CreateFeedAsync(_sessionId);

                if (response != null && response.Success)
                {
                    await AddAiMessage("🎉 Success! Your feed has been created and is now live. You can view it in your feed list.", false);

                    Debug.WriteLine("[AI CHAT MODEL] ✅ Feed created successfully");

                    // Navigate back after a delay
                    await Task.Delay(2500);
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    var errorMsg = response?.Message ?? "Unknown error occurred";
                    await AddAiMessage($"There was an error creating your feed: {errorMsg}. Please try again or contact support.", false);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AI CHAT MODEL] ❌ Create error: {ex.Message}");
                await AddAiMessage("Failed to create your feed. Please try again or contact support if the problem persists.", false);
            }
            finally
            {
                IsBusy = false;
                StatusText = "Online";
            }
        }

        [RelayCommand]
        private async Task Refresh()
        {
            IsBusy = true;
            await Task.Delay(1000);
            IsBusy = false;
        }
    }

    #region View Models

    public partial class AiChatMessageViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _message = string.Empty;

        [ObservableProperty]
        private bool _isFromUser;

        [ObservableProperty]
        private bool _isFromAi;

        [ObservableProperty]
        private bool _isTyping;

        [ObservableProperty]
        private DateTime _timestamp;

        [ObservableProperty]
        private bool _hasAttachment;

        [ObservableProperty]
        private string _attachmentName = string.Empty;

        [ObservableProperty]
        private string _attachmentType = string.Empty;

        public string DisplayTime => Timestamp.ToString("HH:mm");

        public LayoutOptions AttachmentAlignment => IsFromUser
            ? LayoutOptions.End
            : LayoutOptions.Start;
    }

    public partial class AttachedFileViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _fileName = string.Empty;

        [ObservableProperty]
        private string _fileType = string.Empty;

        [ObservableProperty]
        private string _filePath = string.Empty;
    }

    #endregion
}