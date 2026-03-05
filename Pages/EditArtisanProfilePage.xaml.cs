using CraftConnect_Mobile_App.PageModels;
using CraftConnect_Mobile_App.Services;
using Microsoft.Maui.Controls;

namespace CraftConnect_Mobile_App.Pages
{
    public partial class EditArtisanProfilePage : ContentPage
    {
        private readonly EditArtisanProfilePageModel _vm;

        public string? ReturnFeedId { get; set; }
        public string? ReturnFeedTitle { get; set; }
        public List<(string Id, string Title)> AllFeedsSnapshot { get; set; } = new();

        // FIX: single-init guard
        private bool _initialized;

        public EditArtisanProfilePage(IProfileApiService profileService)
        {
            InitializeComponent();
            _vm = new EditArtisanProfilePageModel(profileService);
            BindingContext = _vm;
            _vm.ShowToastRequested += OnShowToast;
            _vm.NavigateBackRequested += OnNavigateBack;
            _vm.NavigateToProposalRequested += OnNavigateToProposal;
        }

        // FIX: removed the old public Initialise() method entirely.
        // OnAppearing is now the one and only init entry-point.
        protected override void OnAppearing()
        {
            base.OnAppearing();
            if (_initialized) return;
            _initialized = true;
            _vm.ReturnFeedId = ReturnFeedId;
            _vm.ReturnFeedTitle = ReturnFeedTitle;
            _ = _vm.InitialiseAsync(!string.IsNullOrWhiteSpace(ReturnFeedId));
        }

        private async void OnShowToast(string message)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                ToastLabel.Text = message;
                ToastOverlay.IsVisible = true;
                await ToastOverlay.FadeTo(1, 200);
                await Task.Delay(2400);
                await ToastOverlay.FadeTo(0, 300);
                ToastOverlay.IsVisible = false;
            });
        }

        private void OnNavigateBack()
        {
            MainThread.BeginInvokeOnMainThread(async () => await Navigation.PopAsync());
        }

        private async void OnNavigateToProposal(string feedId)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    var createPage = Handler?.MauiContext?.Services.GetService<CreateProposalPage>();
                    if (createPage == null)
                    {
                        await DisplayAlert("Error", "Could not open the proposal page. Please try again.", "OK");
                        await Navigation.PopAsync();
                        return;
                    }
                    createPage.AvailableProjects = AllFeedsSnapshot.Select(f => (f.Id, f.Title)).ToList();
                    createPage.PreselectedFeedId = feedId;
                    var navStack = Navigation.NavigationStack.ToList();
                    await Navigation.PushAsync(createPage);
                    if (navStack.Contains(this))
                        Navigation.RemovePage(this);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[EditArtisanProfilePage] {ex.Message}");
                    await Navigation.PopAsync();
                }
            });
        }
    }
}