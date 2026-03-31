using CraftConnect_Mobile_App.Services;
using CraftConnect_Mobile_App.PageModels;
using System.Diagnostics;
using System.Globalization;

namespace CraftConnect_Mobile_App.Pages
{
    public partial class CreateProposalPage : ContentPage
    {
        private readonly CreateProposalPageModel _pm;

        // Guard flag — prevents OnProjectSelected from overwriting the
        // programmatically-preselected feed while RefreshPicker is running.
        private bool _suppressPickerEvents = false;

        public List<(string Id, string DisplayName)> AvailableProjects { get; set; } = new();
        public string? PreselectedFeedId { get; set; }
        public string? PreselectedFeedTitle { get; set; }

        public CreateProposalPage(ArtisanProposalService proposalService)
        {
            InitializeComponent();
            _pm = new CreateProposalPageModel(proposalService);
            BindingContext = _pm;
        }

        // ══════════════════════════════════════════════════════════════
        // ▌ LIFECYCLE
        // ══════════════════════════════════════════════════════════════

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _pm.Initialize(AvailableProjects, PreselectedFeedId, PreselectedFeedTitle);
            RefreshPicker();
        }

        // ══════════════════════════════════════════════════════════════
        // ▌ PROJECT PICKER
        // ══════════════════════════════════════════════════════════════

        private void RefreshPicker()
        {
            // Suppress OnProjectSelected while we rebuild the picker so that
            // setting SelectedIndex programmatically doesn't accidentally
            // clear or overwrite the feed the PM already has selected.
            _suppressPickerEvents = true;

            try
            {
                ProjectPicker.Items.Clear();
                foreach (var feed in _pm.AvailableFeeds)
                    ProjectPicker.Items.Add(feed.DisplayName);

                // Sync picker index to whatever SelectedFeed the PM already has
                if (_pm.SelectedFeed != null)
                {
                    var idx = _pm.AvailableFeeds.IndexOf(_pm.SelectedFeed);
                    if (idx >= 0)
                    {
                        ProjectPicker.SelectedIndex = idx;
                        Debug.WriteLine($"[PAGE] Picker synced to idx={idx}, feed={_pm.SelectedFeed.DisplayName}");
                    }
                }
            }
            finally
            {
                _suppressPickerEvents = false;
            }
        }

        private void OnProjectSelected(object sender, EventArgs e)
        {
            // Ignore events fired during programmatic picker population
            if (_suppressPickerEvents) return;

            var idx = ProjectPicker.SelectedIndex;
            _pm.SelectedFeed = (idx >= 0 && idx < _pm.AvailableFeeds.Count)
                ? _pm.AvailableFeeds[idx]
                : null;

            Debug.WriteLine($"[PAGE] Project selected idx={idx}, id={_pm.SelectedFeed?.Id ?? "none"}");
        }

        // ══════════════════════════════════════════════════════════════
        // ▌ HELPER — ensure SelectedFeed is in sync with the picker
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// If the PM's SelectedFeed is still null (e.g. because the user
        /// never touched the picker after pre-population), try to recover
        /// it from the picker's current index before we validate or navigate.
        /// </summary>
        private void EnsureSelectedFeedSynced()
        {
            if (_pm.SelectedFeed == null
                && ProjectPicker.SelectedIndex >= 0
                && ProjectPicker.SelectedIndex < _pm.AvailableFeeds.Count)
            {
                _pm.SelectedFeed = _pm.AvailableFeeds[ProjectPicker.SelectedIndex];
                Debug.WriteLine($"[PAGE] SelectedFeed recovered from picker: {_pm.SelectedFeed.DisplayName}");
            }
        }

        // ══════════════════════════════════════════════════════════════
        // ▌ NAVIGATION
        // ══════════════════════════════════════════════════════════════

        private async void OnBackClicked(object sender, EventArgs e)
        {
            if (_pm.HasUnsavedChanges)
            {
                var confirmed = await DisplayAlert(
                    "Discard Changes?",
                    "You have unsaved changes. Are you sure you want to go back?",
                    "Discard", "Keep Editing");

                if (!confirmed) return;
            }

            await Navigation.PopAsync();
        }

        // ══════════════════════════════════════════════════════════════
        // ▌ WORKMANSHIP ENTRY
        // ══════════════════════════════════════════════════════════════

        private void OnPriceChanged(object sender, TextChangedEventArgs e)
        {
            var text = e.NewTextValue ?? string.Empty;

            if (!string.IsNullOrEmpty(text) &&
                !decimal.TryParse(text, NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out _))
            {
                PriceEntry.Text = e.OldTextValue;
                return;
            }

            _pm.ProposedPrice = text;
        }

        // ══════════════════════════════════════════════════════════════
        // ▌ DATE PICKER
        // ══════════════════════════════════════════════════════════════

        private async void OnDatePickerTapped(object sender, EventArgs e)
        {
            var initialDate = _pm.EstimatedDuration ?? DateTime.Today;
            var pickerPage = new DatePickerModalPage(initialDate, DateTime.Today);

            pickerPage.DateConfirmed += (_, selectedDate) =>
            {
                _pm.EstimatedDuration = selectedDate;
                DateLabel.Text = _pm.DateDisplayText;
                DateLabel.TextColor = Color.FromArgb("#0D0D0D");
                Debug.WriteLine($"[PAGE] Date confirmed: {selectedDate:dd MMM yyyy}");
            };

            await Navigation.PushModalAsync(pickerPage, animated: true);
        }

        // ══════════════════════════════════════════════════════════════
        // ▌ DOCUMENT PICKER
        // ══════════════════════════════════════════════════════════════

        private async void OnPickDocumentClicked(object sender, EventArgs e)
        {
            try
            {
                var options = new PickOptions
                {
                    PickerTitle = "Select a document",
                    FileTypes = new FilePickerFileType(
                        new Dictionary<DevicePlatform, IEnumerable<string>>
                        {
                            { DevicePlatform.Android, new[] {
                                "application/pdf",
                                "application/msword",
                                "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                            }},
                            { DevicePlatform.iOS, new[] {
                                "com.adobe.pdf",
                                "org.openxmlformats.wordprocessingml.document"
                            }},
                            { DevicePlatform.WinUI,       new[] { ".pdf", ".doc", ".docx" } },
                            { DevicePlatform.MacCatalyst, new[] { "com.adobe.pdf" } }
                        })
                };

                var result = await FilePicker.Default.PickAsync(options);
                if (result == null) return;

                var success = await _pm.SetDocumentAsync(result);

                if (success)
                {
                    FileNameLabel.Text = _pm.QuoteDocumentFileName!;
                    FileSizeLabel.Text = _pm.QuoteDocumentSizeText!;
                    UploadBorder.IsVisible = false;
                    FilePicked.IsVisible = true;
                }
                else
                {
                    await DisplayAlert("Error", _pm.ErrorMessage, "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PAGE] File pick error: {ex.Message}");
                await DisplayAlert("Error", "Could not pick file. Please try again.", "OK");
            }
        }

        private void OnRemoveDocumentClicked(object sender, EventArgs e)
        {
            _pm.RemoveDocumentCommand.Execute(null);
            FilePicked.IsVisible = false;
            UploadBorder.IsVisible = true;
        }

        // ══════════════════════════════════════════════════════════════
        // ▌ FOOTER — SUBMIT PROPOSAL
        // ══════════════════════════════════════════════════════════════

        private async void OnSubmitProposalClicked_Tap(object sender, EventArgs e)
        {
            _pm.Message = CoverLetterEditor.Text ?? string.Empty;
            _pm.TermsConditions = TermsEditor.Text;
            _pm.PaymentTerms = PaymentTermsEditor.Text;

            // Recover SelectedFeed from picker BEFORE validating
            EnsureSelectedFeedSynced();

            var validationError = _pm.Validate();
            if (validationError != null)
            {
                await DisplayAlert("Validation", validationError, "OK");
                return;
            }

            _pm.SubmitCommand.Execute(null);
        }

        // ══════════════════════════════════════════════════════════════
        // ▌ FOOTER — ADD INVOICE
        // ══════════════════════════════════════════════════════════════

        private async void OnAddInvoiceClicked_Tap(object sender, EventArgs e)
        {
            _pm.Message = CoverLetterEditor.Text ?? string.Empty;
            _pm.TermsConditions = TermsEditor.Text;
            _pm.PaymentTerms = PaymentTermsEditor.Text;

            // Recover SelectedFeed from picker BEFORE validating —
            // this is the key fix: previously this came after Validate(),
            // so the "please select a project" error fired even when the
            // picker was visually showing the pre-selected feed.
            EnsureSelectedFeedSynced();

            var validationError = _pm.Validate();
            if (validationError != null)
            {
                await DisplayAlert("Validation", validationError, "OK");
                return;
            }

            decimal.TryParse(_pm.ProposedPrice, out var workmanship);

            var navParams = new Dictionary<string, object>
            {
                { "userFeedId",           _pm.SelectedFeed!.Id },
                { "feedTitle",            _pm.SelectedFeed.DisplayName },
                { "proposalId",           string.Empty },
                { "prefilledWorkmanship", workmanship.ToString("F2") },
            };

            Debug.WriteLine($"[PAGE] → CreateInvoicePage. FeedId={_pm.SelectedFeed.Id}, Workmanship={workmanship}");
            await Shell.Current.GoToAsync("CreateInvoicePage", navParams);
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // ▌ MODAL DATE PICKER PAGE  (unchanged)
    // ══════════════════════════════════════════════════════════════════

    public class DatePickerModalPage : ContentPage
    {
        public event EventHandler<DateTime>? DateConfirmed;

        private readonly DatePicker _datePicker;

        public DatePickerModalPage(DateTime initialDate, DateTime minimumDate)
        {
            Shell.SetNavBarIsVisible(this, false);
            BackgroundColor = Colors.Transparent;

            _datePicker = new DatePicker
            {
                MinimumDate = minimumDate,
                Date = initialDate,
                TextColor = Color.FromArgb("#0D0D0D"),
                BackgroundColor = Colors.White,
                FontSize = 16,
                HorizontalOptions = LayoutOptions.Fill,
            };

            var titleLabel = new Label
            {
                Text = "Select Completion Date",
                FontSize = 17,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#0D0D0D"),
                HorizontalOptions = LayoutOptions.Center,
            };

            var confirmButton = new Button
            {
                Text = "Confirm",
                BackgroundColor = Color.FromArgb("#2563EB"),
                TextColor = Colors.White,
                FontAttributes = FontAttributes.Bold,
                FontSize = 15,
                CornerRadius = 12,
                HeightRequest = 50,
            };
            confirmButton.Clicked += OnConfirmClicked;

            var cancelButton = new Button
            {
                Text = "Cancel",
                BackgroundColor = Colors.Transparent,
                TextColor = Color.FromArgb("#6B7280"),
                FontSize = 15,
                HeightRequest = 44,
            };
            cancelButton.Clicked += async (_, _) => await Navigation.PopModalAsync();

            var card = new Border
            {
                BackgroundColor = Colors.White,
                StrokeThickness = 0,
                Padding = new Thickness(24, 28),
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Fill,
                Margin = new Thickness(28, 0),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 20 },
                Content = new VerticalStackLayout
                {
                    Spacing = 20,
                    Children = { titleLabel, _datePicker, confirmButton, cancelButton }
                }
            };

            var bgTap = new TapGestureRecognizer();
            bgTap.Tapped += async (_, _) => await Navigation.PopModalAsync();

            var dimBackground = new BoxView
            {
                Color = Color.FromArgb("#80000000"),
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
            };
            dimBackground.GestureRecognizers.Add(bgTap);

            var root = new Grid();
            root.Children.Add(dimBackground);
            root.Children.Add(card);

            Content = root;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _datePicker.Focus();
        }

        private async void OnConfirmClicked(object sender, EventArgs e)
        {
            DateConfirmed?.Invoke(this, _datePicker.Date);
            await Navigation.PopModalAsync();
        }
    }
}