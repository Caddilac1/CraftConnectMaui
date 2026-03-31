using CraftConnect_Mobile_App.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace CraftConnect_Mobile_App.PageModels
{
    public class CreateProposalPageModel : INotifyPropertyChanged
    {
        private readonly ArtisanProposalService _proposalService;

        // ── Backing fields ─────────────────────────────────────────────
        private FeedPickerItem? _selectedFeed;
        private string _proposedPrice = string.Empty;
        private DateTime? _estimatedDuration;
        private string _message = string.Empty;
        private string? _termsConditions;
        private string? _paymentTerms;

        private byte[]? _quoteDocumentBytes;
        private string? _quoteDocumentFileName;
        private string? _quoteDocumentSizeText;

        private bool _isBusy = false;
        private bool _isLoadingFeeds = false;

        private string _errorMessage = string.Empty;
        private string _successMessage = string.Empty;

        // ── Constructor ────────────────────────────────────────────────
        public CreateProposalPageModel(ArtisanProposalService proposalService)
        {
            _proposalService = proposalService;

            SubmitCommand = new Command(async () => await SubmitProposalAsync(), () => !IsBusy);
            RemoveDocumentCommand = new Command(OnRemoveDocument);
        }

        // ══════════════════════════════════════════════════════════════
        // ▌ COMMANDS
        // ══════════════════════════════════════════════════════════════

        public ICommand SubmitCommand { get; }
        public ICommand RemoveDocumentCommand { get; }

        // ══════════════════════════════════════════════════════════════
        // ▌ FEED / PROJECT LIST
        // ══════════════════════════════════════════════════════════════

        public ObservableCollection<FeedPickerItem> AvailableFeeds { get; } = new();

        public FeedPickerItem? SelectedFeed
        {
            get => _selectedFeed;
            set { _selectedFeed = value; OnPropertyChanged(); ClearMessages(); }
        }

        // ══════════════════════════════════════════════════════════════
        // ▌ FORM FIELDS
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// The workmanship (labour/craft cost) amount entered by the artisan.
        /// Exposed as ProposedPrice to keep the service layer contract unchanged.
        /// </summary>
        public string ProposedPrice
        {
            get => _proposedPrice;
            set { _proposedPrice = value; OnPropertyChanged(); ClearMessages(); }
        }

        public DateTime? EstimatedDuration
        {
            get => _estimatedDuration;
            set
            {
                _estimatedDuration = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DateDisplayText));
                OnPropertyChanged(nameof(HasDateSelected));
                ClearMessages();
            }
        }

        public string DateDisplayText =>
            EstimatedDuration.HasValue
                ? EstimatedDuration.Value.ToString("dd MMM yyyy")
                : "Select date...";

        public bool HasDateSelected => EstimatedDuration.HasValue;

        public string Message
        {
            get => _message;
            set { _message = value; OnPropertyChanged(); ClearMessages(); }
        }

        public string? TermsConditions
        {
            get => _termsConditions;
            set { _termsConditions = value; OnPropertyChanged(); }
        }

        public string? PaymentTerms
        {
            get => _paymentTerms;
            set { _paymentTerms = value; OnPropertyChanged(); }
        }

        // ══════════════════════════════════════════════════════════════
        // ▌ DOCUMENT
        // ══════════════════════════════════════════════════════════════

        public byte[]? QuoteDocumentBytes
        {
            get => _quoteDocumentBytes;
            set { _quoteDocumentBytes = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasDocument)); }
        }

        public string? QuoteDocumentFileName
        {
            get => _quoteDocumentFileName;
            set { _quoteDocumentFileName = value; OnPropertyChanged(); }
        }

        public string? QuoteDocumentSizeText
        {
            get => _quoteDocumentSizeText;
            set { _quoteDocumentSizeText = value; OnPropertyChanged(); }
        }

        public bool HasDocument => QuoteDocumentBytes != null && QuoteDocumentBytes.Length > 0;

        // ══════════════════════════════════════════════════════════════
        // ▌ UI STATE
        // ══════════════════════════════════════════════════════════════

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); ((Command)SubmitCommand).ChangeCanExecute(); }
        }

        public bool IsLoadingFeeds
        {
            get => _isLoadingFeeds;
            set { _isLoadingFeeds = value; OnPropertyChanged(); }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); }
        }

        public string SuccessMessage
        {
            get => _successMessage;
            set { _successMessage = value; OnPropertyChanged(); }
        }

        // ══════════════════════════════════════════════════════════════
        // ▌ PUBLIC METHODS — called from page code-behind
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Call from OnAppearing. Pass the feed list from your navigation/service layer.
        /// Optionally pre-select a feed when arriving from a feed detail page.
        /// </summary>
        // Replace just the Initialize method:

        public void Initialize(
            List<(string Id, string DisplayName)> feeds,
            string? preselectedFeedId = null,
            string? preselectedFeedTitle = null)   // ← NEW param
        {
            AvailableFeeds.Clear();

            // If the preselected feed isn't in the list, add it so it's always present
            if (!string.IsNullOrEmpty(preselectedFeedId))
            {
                var exists = feeds.Any(f => f.Id == preselectedFeedId);
                if (!exists && !string.IsNullOrEmpty(preselectedFeedTitle))
                {
                    feeds = new List<(string, string)>(feeds)
            {
                (preselectedFeedId, preselectedFeedTitle)
            };
                    Debug.WriteLine($"[PROPOSAL PM] Injected preselected feed into list: {preselectedFeedTitle}");
                }
            }

            foreach (var f in feeds)
                AvailableFeeds.Add(new FeedPickerItem { Id = f.Id, DisplayName = f.DisplayName });

            if (!string.IsNullOrEmpty(preselectedFeedId))
            {
                SelectedFeed = AvailableFeeds.FirstOrDefault(f => f.Id == preselectedFeedId);
                Debug.WriteLine($"[PROPOSAL PM] SelectedFeed = {SelectedFeed?.DisplayName ?? "NOT FOUND"}");
            }

            Debug.WriteLine($"[PROPOSAL PM] Initialized with {AvailableFeeds.Count} feeds.");
        }

        /// <summary>
        /// Reads the picked file into bytes and stores display info.
        /// Call this after FilePicker.Default.PickAsync() returns a result.
        /// Returns false if the file could not be read.
        /// </summary>
        public async Task<bool> SetDocumentAsync(FileResult fileResult)
        {
            try
            {
                await using var stream = await fileResult.OpenReadAsync();
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);

                QuoteDocumentBytes = ms.ToArray();
                QuoteDocumentFileName = fileResult.FileName;

                var sizeKb = QuoteDocumentBytes.Length / 1024.0;
                QuoteDocumentSizeText = sizeKb >= 1024
                    ? $"{sizeKb / 1024:F1} MB"
                    : $"{sizeKb:F0} KB";

                Debug.WriteLine($"[PROPOSAL PM] Document set: {QuoteDocumentFileName} ({QuoteDocumentSizeText})");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PROPOSAL PM] ❌ SetDocument error: {ex.Message}");
                ErrorMessage = "Could not read the selected file. Please try again.";
                return false;
            }
        }

        /// <summary>
        /// Returns true when there are unsaved changes worth confirming before navigating away.
        /// </summary>
        public bool HasUnsavedChanges =>
            !string.IsNullOrWhiteSpace(Message) ||
            !string.IsNullOrWhiteSpace(ProposedPrice);

        /// <summary>
        /// Validates the form and returns a user-facing error string, or null if valid.
        /// </summary>
        public string? Validate()
        {
            if (SelectedFeed == null)
                return "Please select a project.";

            if (string.IsNullOrWhiteSpace(ProposedPrice)
                || !decimal.TryParse(ProposedPrice, out var workmanship)
                || workmanship <= 0)
                return "Please enter a valid workmanship amount.";

            if (!EstimatedDuration.HasValue)
                return "Please select an estimated completion date.";

            if (EstimatedDuration.Value.Date < DateTime.Today)
                return "Estimated completion date must be today or a future date.";

            if (string.IsNullOrWhiteSpace(Message))
                return "Please write a cover letter / message.";

            return null;
        }

        // ══════════════════════════════════════════════════════════════
        // ▌ SUBMIT
        // ══════════════════════════════════════════════════════════════

        private async Task SubmitProposalAsync()
        {
            if (IsBusy) return;
            ClearMessages();

            var validationError = Validate();
            if (validationError != null)
            {
                ErrorMessage = validationError;
                return;
            }

            IsBusy = true;

            try
            {
                var request = new CreateProposalServiceRequest
                {
                    UserFeedId = SelectedFeed!.Id,
                    ProposedPrice = decimal.Parse(ProposedPrice),
                    EstimatedDuration = EstimatedDuration!.Value,
                    Message = Message.Trim(),
                    TermsConditions = string.IsNullOrWhiteSpace(TermsConditions) ? null : TermsConditions!.Trim(),
                    PaymentTerms = string.IsNullOrWhiteSpace(PaymentTerms) ? null : PaymentTerms!.Trim(),
                    QuoteDocumentBytes = QuoteDocumentBytes,
                    QuoteDocumentFileName = QuoteDocumentFileName
                };

                Debug.WriteLine($"[PROPOSAL PM] Submitting — FeedId: {request.UserFeedId}, Workmanship: {request.ProposedPrice}");

                var result = await _proposalService.CreateProposalAsync(request);

                if (result.Success)
                {
                    SuccessMessage = result.Message ?? "Proposal submitted successfully!";
                    Debug.WriteLine($"[PROPOSAL PM] ✅ Created: {result.Proposal?.Id}");

                    await Task.Delay(500); // Let the success message show briefly
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    ErrorMessage = result.Error ?? "Failed to submit proposal. Please try again.";
                    Debug.WriteLine($"[PROPOSAL PM] ❌ API error: {ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Unexpected error: {ex.Message}";
                Debug.WriteLine($"[PROPOSAL PM] ❌ Exception: {ex.GetType().FullName}: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Remove document ────────────────────────────────────────────

        private void OnRemoveDocument()
        {
            QuoteDocumentBytes = null;
            QuoteDocumentFileName = null;
            QuoteDocumentSizeText = null;
            Debug.WriteLine("[PROPOSAL PM] Document removed.");
        }

        // ── Clear messages ─────────────────────────────────────────────

        private void ClearMessages()
        {
            ErrorMessage = string.Empty;
            SuccessMessage = string.Empty;
        }

        // ══════════════════════════════════════════════════════════════
        // ▌ INotifyPropertyChanged
        // ══════════════════════════════════════════════════════════════

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }

    // ══════════════════════════════════════════════════════════════════
    // ▌ PICKER ITEM
    // ══════════════════════════════════════════════════════════════════

    public class FeedPickerItem
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;

        public override string ToString() => DisplayName;
    }
}
