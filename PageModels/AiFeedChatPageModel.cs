using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CraftConnect_Mobile_App.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace CraftConnect_Mobile_App.PageModels
{
    public partial class AiFeedChatPageModel : ObservableObject
    {
        private readonly AiFeedChatService _aiFeedService;
        private Guid _sessionId;

        // ── Basic chat state ──────────────────────────────────────
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowMicIcon))]
        [NotifyPropertyChangedFor(nameof(ShowSendIcon))]
        [NotifyPropertyChangedFor(nameof(SendButtonColor))]
        private string _messageText = string.Empty;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _statusText = "Online";

        [ObservableProperty]
        private bool _hasAttachedFiles;

        // ── Typing indicator state ────────────────────────────────
        [ObservableProperty]
        private bool _isTyping;

        // ── Recording state ───────────────────────────────────────
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowMicIcon))]
        [NotifyPropertyChangedFor(nameof(ShowSendIcon))]
        [NotifyPropertyChangedFor(nameof(SendButtonColor))]
        private bool _isRecording;

        [ObservableProperty]
        private string _recordingDuration = "0:00";

        private IDispatcherTimer? _recordingTimer;
        private int _recordingSeconds;

        // ── Derived properties for XAML bindings ─────────────────

        /// <summary>Show mic when: no text typed AND not currently recording.</summary>
        public bool ShowMicIcon => string.IsNullOrWhiteSpace(MessageText) && !IsRecording;

        /// <summary>Show send arrow when: text is present AND not recording.</summary>
        public bool ShowSendIcon => !string.IsNullOrWhiteSpace(MessageText) && !IsRecording;

        /// <summary>Button is red while recording, teal otherwise.</summary>
        public Color SendButtonColor => IsRecording ? Color.FromArgb("#D32F2F") : Color.FromArgb("#075E54");

        // ── Collections ───────────────────────────────────────────
        public ObservableCollection<AiChatMessageViewModel> Messages { get; } = new();
        public ObservableCollection<AttachedFileViewModel> AttachedFiles { get; } = new();

        // ── Events ────────────────────────────────────────────────

        /// <summary>Raised when the user taps the play button on a voice message bubble.</summary>
        public event EventHandler<string>? PlayVoiceRequested;

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
        // duration is captured by the code-behind BEFORE StopRecordingState()
        // resets RecordingDuration, so we receive it as a parameter here.
        // ─────────────────────────────────────────────────────────

        public async Task SendVoiceMessageAsync(string filePath, string duration)
        {
            // Use the passed-in duration; fall back to a minimum of "0:01"
            var capturedDuration = string.IsNullOrEmpty(duration) || duration == "0:00"
                ? "0:01"
                : duration;

            try
            {
                IsBusy = true;
                StatusText = "Sending voice note...";

                // Add the voice message bubble immediately, storing the file path
                // so the user can tap play before the file is cleaned up.
                Messages.Add(new AiChatMessageViewModel
                {
                    IsFromUser = true,
                    IsFromAi = false,
                    IsVoiceMessage = true,
                    VoiceDuration = capturedDuration,
                    AudioFilePath = filePath,
                    Timestamp = DateTime.Now
                });

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

                // NOTE: we intentionally do NOT delete the file here so the user
                // can still play it back from the bubble. The code-behind cleans
                // up the _recordingFilePath reference; cached copies in the app's
                // cache directory will be swept by the OS in due course.
            }
        }

        // ─────────────────────────────────────────────────────────
        // Play voice message (relay command — wired via event to Page)
        // ─────────────────────────────────────────────────────────

        [RelayCommand]
        private void PlayVoice(string filePath)
        {
            Debug.WriteLine($"[AI CHAT MODEL] PlayVoice requested: {filePath}");
            PlayVoiceRequested?.Invoke(this, filePath ?? string.Empty);
        }

        // ─────────────────────────────────────────────────────────
        // Shared AI response dispatcher
        // ─────────────────────────────────────────────────────────

        private async Task DispatchAiResponse(string userInput, bool alreadyFromServer = false)
        {
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
                Message = StripMarkdown(message),
                IsFromAi = true,
                IsFromUser = false,
                Timestamp = DateTime.Now
            });
            await Task.Delay(100);
        }

        // ─────────────────────────────────────────────────────────
        // Markdown stripper
        // Removes common markdown syntax so AI replies render cleanly
        // in plain Label controls.
        // ─────────────────────────────────────────────────────────

        private static string StripMarkdown(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // Bold: **text** or __text__
            text = Regex.Replace(text, @"\*\*(.+?)\*\*", "$1", RegexOptions.Singleline);
            text = Regex.Replace(text, @"__(.+?)__", "$1", RegexOptions.Singleline);

            // Italic: *text* or _text_  (single asterisk / underscore)
            text = Regex.Replace(text, @"\*(.+?)\*", "$1", RegexOptions.Singleline);
            text = Regex.Replace(text, @"_(.+?)_", "$1", RegexOptions.Singleline);

            // Strikethrough: ~~text~~
            text = Regex.Replace(text, @"~~(.+?)~~", "$1", RegexOptions.Singleline);

            // Inline code: `text`
            text = Regex.Replace(text, @"`(.+?)`", "$1", RegexOptions.Singleline);

            // Fenced code blocks: ```...```
            text = Regex.Replace(text, @"```[\s\S]*?```", string.Empty);

            // ATX headings: ## Heading → Heading
            text = Regex.Replace(text, @"^#{1,6}\s+", string.Empty, RegexOptions.Multiline);

            // Horizontal rules: --- / *** / ___
            text = Regex.Replace(text, @"^(\s*[-*_]){3,}\s*$", string.Empty, RegexOptions.Multiline);

            // Bullet list markers: "- item" or "* item" → "• item"
            text = Regex.Replace(text, @"^\s*[-*]\s+", "• ", RegexOptions.Multiline);

            // Numbered list: "1. item" → keep as-is (already readable)

            // Blockquotes: "> text" → "text"
            text = Regex.Replace(text, @"^\s*>\s?", string.Empty, RegexOptions.Multiline);

            // Links: [text](url) → text
            text = Regex.Replace(text, @"\[(.+?)\]\(.+?\)", "$1");

            // Images: ![alt](url) → alt
            text = Regex.Replace(text, @"!\[(.+?)\]\(.+?\)", "$1");

            // Collapse 3+ consecutive blank lines to 2
            text = Regex.Replace(text, @"\n{3,}", "\n\n");

            return text.Trim();
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
        [ObservableProperty] private bool _isVoiceMessage;
        [ObservableProperty] private string _voiceDuration = string.Empty;

        /// <summary>
        /// Local file path for the recorded audio so the play button can
        /// stream it back. Populated when the voice bubble is first added.
        /// </summary>
        [ObservableProperty] private string _audioFilePath = string.Empty;

        [ObservableProperty] private DateTime _timestamp;
        [ObservableProperty] private bool _hasAttachment;
        [ObservableProperty] private string _attachmentName = string.Empty;
        [ObservableProperty] private string _attachmentType = string.Empty;

        public string DisplayTime => Timestamp.ToString("HH:mm");
        public LayoutOptions AttachmentAlignment => IsFromUser ? LayoutOptions.End : LayoutOptions.Start;

        /// <summary>
        /// True for AI messages that are real text (not the typing placeholder).
        /// Used in XAML to hide/show the regular text bubble vs typing dots.
        /// </summary>
        public bool IsRegularAiMessage => IsFromAi && !IsTyping && !IsVoiceMessage;
    }

    public partial class AttachedFileViewModel : ObservableObject
    {
        [ObservableProperty] private string _fileName = string.Empty;
        [ObservableProperty] private string _fileType = string.Empty;
        [ObservableProperty] private string _filePath = string.Empty;
    }
}