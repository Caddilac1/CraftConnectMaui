using CraftConnect_Mobile_App.PageModels;
using System.Diagnostics;

namespace CraftConnect_Mobile_App.Pages
{
    public partial class StorePage : ContentPage
    {
        private readonly StorePageModel _viewModel;

        public StorePage(StorePageModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
            Debug.WriteLine("[STORE PAGE] Constructor - Page initialized");
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            Debug.WriteLine("[STORE PAGE] OnAppearing");

            try
            {
                await _viewModel.InitializeAsync();
                Debug.WriteLine("[STORE PAGE] ✅ Initialization complete");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[STORE PAGE] ❌ Error in OnAppearing: {ex.Message}");
                await DisplayAlert("Error", $"Failed to load store: {ex.Message}", "OK");
            }
        }
    }
}