using CraftConnect_Mobile_App.Services;
using CraftConnect_Mobile_App.PageModels;
using System.Diagnostics;
using System.Globalization;

namespace CraftConnect_Mobile_App.Pages
{
    public partial class CreateProposalPage : ContentPage
    {
        private readonly CreateProposalPageModel _pm;

        public List<(string Id, string DisplayName)> AvailableProjects { get; set; } = new();
        public string? PreselectedFeedId { get; set; }

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
            _pm.Initialize(AvailableProjects, PreselectedFeedId);
            RefreshPicker();
        }

        // ══════════════════════════════════════════════════════════════
        // ▌ PROJECT PICKER
        // ══════════════════════════════════════════════════════════════

        private void RefreshPicker()
        {
            ProjectPicker.Items.Clear();
            foreach (var feed in _pm.AvailableFeeds)
                ProjectPicker.Items.Add(feed.DisplayName);

            if (_pm.SelectedFeed != null)
            {
                var idx = _pm.AvailableFeeds.IndexOf(_pm.SelectedFeed);
                if (idx >= 0) ProjectPicker.SelectedIndex = idx;
            }
        }

        private void OnProjectSelected(object sender, EventArgs e)
        {
            var idx = ProjectPicker.SelectedIndex;
            _pm.SelectedFeed = (idx >= 0 && idx < _pm.AvailableFeeds.Count)
                ? _pm.AvailableFeeds[idx]
                : null;

            Debug.WriteLine($"[PAGE] Project selected idx={idx}, id={_pm.SelectedFeed?.Id ?? "none"}");
        }

        // ══════════════════════════════════════════════════════════════
        // ▌ NAVIGATION
        // ══════════════════════════════════════════════════════════════

        private async void OnBackClicked(object sender, EventArgs e) =>
            await Navigation.PopAsync();

        private async void OnBackToListClicked(object sender, EventArgs e)
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
        // ▌ PRICE ENTRY
        // ══════════════════════════════════════════════════════════════

        private void OnPriceChanged(object sender, TextChangedEventArgs e)
        {
            var text = e.NewTextValue ?? string.Empty;

            if (!string.IsNullOrEmpty(text) &&
                !decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out _))
            {
                PriceEntry.Text = e.OldTextValue;
                return;
            }

            _pm.ProposedPrice = text;
        }

        // ══════════════════════════════════════════════════════════════
        // ▌ DATE PICKER
        //
        // WHY THE OLD APPROACH FAILED:
        // A DatePicker with HeightRequest="0" or "1" + IsVisible="False"
        // inside a ScrollView is never fully laid out by Android's view
        // hierarchy. When Focus() is called the renderer exists but the
        // native View has zero size and is not attached to the window
        // input system, so the OS ignores the focus request entirely.
        //
        // NEW APPROACH — Modal page:
        // Push a lightweight modal that contains a properly rendered
        // DatePicker as its primary content. OnAppearing() calls Focus()
        // when the view IS fully laid out and attached, so the native
        // date dialog opens immediately and reliably on both platforms.
        // ══════════════════════════════════════════════════════════════

        private async void OnDatePickerTapped(object sender, EventArgs e)
        {
            var initialDate = _pm.EstimatedDuration ?? DateTime.Today;
            var pickerPage = new DatePickerModalPage(initialDate, DateTime.Today);

            pickerPage.DateConfirmed += (_, selectedDate) =>
            {
                _pm.EstimatedDuration = selectedDate;
                DateLabel.Text = _pm.DateDisplayText;
                DateLabel.TextColor = Color.FromArgb("#1B2B3A");
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
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
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
        // ▌ SUBMIT
        // ══════════════════════════════════════════════════════════════

        private async void OnSubmitProposalClicked(object sender, EventArgs e)
        {
            _pm.Message = CoverLetterEditor.Text ?? string.Empty;
            _pm.TermsConditions = TermsEditor.Text;
            _pm.PaymentTerms = PaymentTermsEditor.Text;

            var validationError = _pm.Validate();
            if (validationError != null)
            {
                await DisplayAlert("Validation", validationError, "OK");
                return;
            }

            _pm.SubmitCommand.Execute(null);
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // ▌ MODAL DATE PICKER PAGE
    //
    // A minimal full-screen modal that hosts a real, visible DatePicker.
    // Because the DatePicker is fully laid out when OnAppearing fires,
    // Focus() reliably opens the native date dialog on Android & iOS.
    //
    // Usage:
    //   var page = new DatePickerModalPage(currentDate, DateTime.Today);
    //   page.DateConfirmed += (_, date) => { /* use date */ };
    //   await Navigation.PushModalAsync(page);
    // ══════════════════════════════════════════════════════════════════

    public class DatePickerModalPage : ContentPage
    {
        public event EventHandler<DateTime>? DateConfirmed;

        private readonly DatePicker _datePicker;

        public DatePickerModalPage(DateTime initialDate, DateTime minimumDate)
        {
            Shell.SetNavBarIsVisible(this, false);
            BackgroundColor = Color.FromArgb("#80000000"); // dim overlay

            _datePicker = new DatePicker
            {
                MinimumDate = minimumDate,
                Date = initialDate,
                TextColor = Color.FromArgb("#1B2B3A"),
                BackgroundColor = Colors.White,
                FontSize = 16,
                HorizontalOptions = LayoutOptions.Fill,
            };

            var titleLabel = new Label
            {
                Text = "Select Completion Date",
                FontSize = 17,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#1B2B3A"),
                HorizontalOptions = LayoutOptions.Center,
            };

            var confirmButton = new Button
            {
                Text = "Confirm",
                BackgroundColor = Color.FromArgb("#F5A623"),
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
                TextColor = Color.FromArgb("#6B7D8D"),
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

            // Tapping the dim background dismisses without confirming
            var bgTap = new TapGestureRecognizer();
            bgTap.Tapped += async (_, _) => await Navigation.PopModalAsync();

            var root = new Grid();

            var dimBackground = new BoxView
            {
                Color = Color.FromArgb("#80000000"),
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
            };
            dimBackground.GestureRecognizers.Add(bgTap);

            root.Children.Add(dimBackground);
            root.Children.Add(card);

            Content = root;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            // The DatePicker is now fully laid out — Focus() reliably
            // opens the native date dialog on Android and iOS.
            _datePicker.Focus();
        }

        private async void OnConfirmClicked(object sender, EventArgs e)
        {
            DateConfirmed?.Invoke(this, _datePicker.Date);
            await Navigation.PopModalAsync();
        }
    }
}