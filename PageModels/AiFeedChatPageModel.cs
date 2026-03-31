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

        // ── Basic chat state ──────────────────────────────────────
        [ObservableProperty]
        private string _messageText = string.Empty;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _statusText = "Online";

        [ObservableProperty]
        private bool _hasAttachedFiles;

        // ── Typing indicator state ────────────────────────────────

        /// <summary>
        /// True while the AI is composing a reply.
        /// Drives the page-level TypingIndicatorOverlay in XAML and the
        /// bouncing-wave animation in AiFeedChatPage.xaml.cs.
        /// </summary>
        [ObservableProperty]
        private bool _isTyping;

        // ── Recording state ───────────────────────────────────────

        /// <summary>True while the microphone is actively recording.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowMicIcon))]
        [NotifyPropertyChangedFor(nameof(SendButtonColor))]
        private bool _isRecording;

        /// <summary>Elapsed time string shown in the recording banner, e.g. "0:12".</summary>
        [ObservableProperty]
        private string _recordingDuration = "0:00";

        private IDispatcherTimer? _recordingTimer;
        private int _recordingSeconds;

        // ── Derived properties for XAML bindings ─────────────────

        /// <summary>Show the mic icon when no text AND not recording.</summary>
        public bool ShowMicIcon => string.IsNullOrWhiteSpace(MessageText) && !IsRecording;

        /// <summary>Button is red while recording, otherwise WhatsApp teal.</summary>
        public Color SendButtonColor => IsRecording ? Color.FromArgb("#D32F2F") : Color.FromArgb("#075E54");

        // ── Collections ───────────────────────────────────────────

        public ObservableCollection<AiChatMessageViewModel> Messages { get; } = new();
        public ObservableCollection<AttachedFileViewModel> AttachedFiles { get; } = new();

        // ─────────────────────────────────────────────────────────
        // Constructor
        // ─────────────────────────────────────────────────────────

        public AiFeedChatPageModel(AiFeedChatService aiFeedService)
        {
            _aiFeedService = aiFeedService;
            _sessionId = Guid.NewGuid();
            Debug.WriteLine($"[AI CHAT MODEL] Session ID: {_sessionId}");
        }

        // ─────────────────────────────────────────────────────────
        // Initialization
        // ─────────────────────────────────────────────────────────

        public async Task InitializeAsync()
        {
            Debug.WriteLine("[AI CHAT MODEL] Initializing...");
            IsBusy = true;

            try
            {
                var token = Preferences.Get("auth_token", string.Empty);
                if (!string.IsNullOrEmpty(token))
                    _aiFeedService.SetAuthToken(token);

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
                    await AddAiMessage(response.Message, false);
                else
                    await AddAiMessage("Hello! 👋 I'm your AI assistant. I can help you create a professional feed post. Would you like to get started?", false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AI CHAT MODEL] ❌ Greeting error: {ex.Message}");
                throw;
            }
        }

        // ─────────────────────────────────────────────────────────
        // Send text message
        // ─────────────────────────────────────────────────────────

        [RelayCommand]
        public async Task SendMessage()
        {
            if (string.IsNullOrWhiteSpace(MessageText))
                return;

            var userMessage = MessageText.Trim();
            MessageText = string.Empty;

            await AddUserMessage(userMessage);
            await DispatchAiResponse(userMessage);
        }

        // ─────────────────────────────────────────────────────────
        // Recording state management (called from code-behind)
        // ─────────────────────────────────────────────────────────

        /// <summary>Called by code-behind when recording starts.</summary>
        public void StartRecordingState()
        {
            _recordingSeconds = 0;
            RecordingDuration = "0:00";
            IsRecording = true;

            _recordingTimer = Application.Current!.Dispatcher.CreateTimer();
            _recordingTimer.Interval = TimeSpan.FromSeconds(1);
            _recordingTimer.Tick += OnRecordingTimerTick;
            _recordingTimer.Start();

            StatusText = "🎙 Recording...";
            Debug.WriteLine("[AI CHAT MODEL] Recording state started");
        }

        /// <summary>Called by code-behind when recording stops (send or discard).</summary>
        public void StopRecordingState()
        {
            IsRecording = false;
            StatusText = "Online";

            _recordingTimer?.Stop();
            _recordingTimer = null;

            RecordingDuration = "0:00";
            _recordingSeconds = 0;
            Debug.WriteLine("[AI CHAT MODEL] Recording state stopped");
        }

        private void OnRecordingTimerTick(object? sender, EventArgs e)
        {
            _recordingSeconds++;
            var minutes = _recordingSeconds / 60;
            var seconds = _recordingSeconds % 60;
            RecordingDuration = $"{minutes}:{seconds:D2}";
        }

        // ─────────────────────────────────────────────────────────
        // Send voice message
        // ─────────────────────────────────────────────────────────

        public async Task SendVoiceMessageAsync(string filePath)
        {
            try
            {
                IsBusy = true;
                StatusText = "Sending voice note...";

                var durationLabel = RecordingDuration == "0:00" ? "" : $" ({RecordingDuration})";
                await AddUserMessage($"🎙 Voice note{durationLabel}");

                var fileName = Path.GetFileName(filePath);

                using var stream = File.OpenRead(filePath);
                var response = await _aiFeedService.UploadFileAsync(_sessionId, stream, fileName, "voice");

                if (response?.Success == true && !string.IsNullOrEmpty(response.Message))
                    await DispatchAiResponse(response.Message, alreadyFromServer: true);
                else
                    await DispatchAiResponse("[voice note attached]");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AI CHAT MODEL] ❌ Voice upload error: {ex.Message}");
                await AddAiMessage("Sorry, I couldn't process your voice note. Please try typing instead.", false);
                StatusText = "Online";
            }
            finally
            {
                IsBusy = false;

                try { if (File.Exists(filePath)) File.Delete(filePath); }
                catch { /* non-critical */ }
            }
        }

        // ─────────────────────────────────────────────────────────
        // Shared AI response dispatcher
        // ─────────────────────────────────────────────────────────

        /// <param name="userInput">Text sent to the AI (or already-received server text).</param>
        /// <param name="alreadyFromServer">When true, skip the API call and display directly.</param>
        private async Task DispatchAiResponse(string userInput, bool alreadyFromServer = false)
        {
            // ── Show typing indicator ─────────────────────────────
            // 1. Flip the page-level flag → triggers the wave animation in code-behind
            //    and shows the TypingIndicatorOverlay in XAML.
            // 2. Also add a per-message placeholder so the CollectionView scrolls correctly.
            IsTyping = true;

            var typingMessage = new AiChatMessageViewModel
            {
                IsFromAi = true,
                IsTyping = true,
                Timestamp = DateTime.Now
            };
            Messages.Add(typingMessage);

            try
            {
                StatusText = "typing...";

                string replyText;

                if (alreadyFromServer)
                {
                    replyText = userInput;
                }
                else
                {
                    var response = await _aiFeedService.SendMessageAsync(_sessionId, userInput);
                    replyText = response?.Message ?? "Sorry, I didn't receive a response. Please try again.";
                }

                // ── Hide typing indicator ─────────────────────────
                IsTyping = false;
                Messages.Remove(typingMessage);

                await AddAiMessage(replyText, false);
                StatusText = "Online";

                var apiResponse = alreadyFromServer ? null : await _aiFeedService.SendMessageAsync(_sessionId, userInput);
                if (apiResponse?.ReadyToCreate == true)
                {
                    await Task.Delay(1000);
                    await CreateFeed();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AI CHAT MODEL] ❌ DispatchAiResponse error: {ex.Message}");

                // Always clear the typing state on error
                IsTyping = false;
                Messages.Remove(typingMessage);

                var errorMessage = ex.Message.Contains("Network") ? "Network error. Please check your connection."
                                 : ex.Message.Contains("timed out") ? "Request timed out. Please try again."
                                                                     : "Sorry, something went wrong. Please try again.";
                await AddAiMessage(errorMessage, false);
                StatusText = "Error";
            }
        }

        // ─────────────────────────────────────────────────────────
        // Message helpers
        // ─────────────────────────────────────────────────────────

        private async Task AddUserMessage(string message)
        {
            Messages.Add(new AiChatMessageViewModel
            {
                Message = message,
                IsFromUser = true,
                IsFromAi = false,
                Timestamp = DateTime.Now
            });
            await Task.Delay(100);
        }

        private async Task AddAiMessage(string message, bool isTyping)
        {
            if (isTyping) await Task.Delay(800);

            Messages.Add(new AiChatMessageViewModel
            {
                Message = message,
                IsFromAi = true,
                IsFromUser = false,
                Timestamp = DateTime.Now
            });
            await Task.Delay(100);
        }

        // ─────────────────────────────────────────────────────────
        // File attachment
        // ─────────────────────────────────────────────────────────

        public async Task AttachFile(FileResult file, string fileType)
        {
            if (file == null) return;

            try
            {
                IsBusy = true;
                StatusText = "Uploading file...";

                Debug.WriteLine($"[AI CHAT MODEL] Attaching file: {file.FileName}");

                using var stream = await file.OpenReadAsync();
                var response = await _aiFeedService.UploadFileAsync(_sessionId, stream, file.FileName, fileType);

                if (response?.Success == true)
                {
                    AttachedFiles.Add(new AttachedFileViewModel
                    {
                        FileName = file.FileName,
                        FileType = fileType == "invoice" ? "Invoice" : "Document",
                        FilePath = file.FullPath ?? ""
                    });
                    HasAttachedFiles = AttachedFiles.Count > 0;

                    var fileEmoji = fileType == "invoice" ? "🧾" : "📎";
                    await AddUserMessage($"{fileEmoji} Attached: {file.FileName}");
                    await Task.Delay(500);

                    var confirmMessage = fileType == "invoice"
                        ? "Great! I've received your invoice. You can attach more documents or type 'done' to continue."
                        : "Perfect! I've got your document. Feel free to attach more or type 'done' when ready.";
                    await AddAiMessage(confirmMessage, false);
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
                await AddAiMessage("Failed to upload file. Please check your connection and try again.", false);
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

        // ─────────────────────────────────────────────────────────
        // Feed creation
        // ─────────────────────────────────────────────────────────

        private async Task CreateFeed()
        {
            try
            {
                StatusText = "Creating your feed...";
                IsBusy = true;

                var response = await _aiFeedService.CreateFeedAsync(_sessionId);

                if (response?.Success == true)
                {
                    await AddAiMessage("🎉 Success! Your feed has been created and is now live.", false);
                    await Task.Delay(2500);
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    var errorMsg = response?.Message ?? "Unknown error";
                    await AddAiMessage($"There was an error creating your feed: {errorMsg}. Please try again.", false);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AI CHAT MODEL] ❌ Create error: {ex.Message}");
                await AddAiMessage("Failed to create your feed. Please try again or contact support.", false);
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

    // ─────────────────────────────────────────────────────────────
    // View Models
    // ─────────────────────────────────────────────────────────────

    public partial class AiChatMessageViewModel : ObservableObject
    {
        [ObservableProperty] private string _message = string.Empty;
        [ObservableProperty] private bool _isFromUser;
        [ObservableProperty] private bool _isFromAi;
        [ObservableProperty] private bool _isTyping;
        [ObservableProperty] private DateTime _timestamp;
        [ObservableProperty] private bool _hasAttachment;
        [ObservableProperty] private string _attachmentName = string.Empty;
        [ObservableProperty] private string _attachmentType = string.Empty;

        public string DisplayTime => Timestamp.ToString("HH:mm");
        public LayoutOptions AttachmentAlignment => IsFromUser ? LayoutOptions.End : LayoutOptions.Start;
    }

    public partial class AttachedFileViewModel : ObservableObject
    {
        [ObservableProperty] private string _fileName = string.Empty;
        [ObservableProperty] private string _fileType = string.Empty;
        [ObservableProperty] private string _filePath = string.Empty;
    }
}