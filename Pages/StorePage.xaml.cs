using CraftConnect_Mobile_App.PageModels;
using System.Diagnostics;

namespace CraftConnect_Mobile_App.Pages
{
    public partial class StorePage : ContentPage
    {
        private readonly StorePageModel _viewModel;
        private bool _initialized = false;

        public StorePage(StorePageModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
            Debug.WriteLine("[STORE PAGE] Constructor - Page initialized");
        }

        protected override void OnNavigatedTo(NavigatedToEventArgs args)
        {
            base.OnNavigatedTo(args);
            BottomNav.SyncTab("Store");
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            Debug.WriteLine("[STORE PAGE] OnAppearing");

            // SPEED FIX: page renders immediately, data loads in background.
            // _initialized guard prevents redundant API calls on tab re-visits.
            if (!_initialized)
            {
                _initialized = true;
                _ = LoadDataAsync();
            }
        }

        private async Task LoadDataAsync()
        {
            try
            {
                await _viewModel.InitializeAsync();
                Debug.WriteLine("[STORE PAGE] ✅ Data loaded");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[STORE PAGE] ❌ Load error: {ex.Message}");
                // Show error on UI thread
                await MainThread.InvokeOnMainThreadAsync(async () =>
                    await DisplayAlert("Error", $"Failed to load store: {ex.Message}", "OK"));
            }
        }
    }
}