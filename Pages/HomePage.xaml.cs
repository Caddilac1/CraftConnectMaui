using CraftConnect_Mobile_App.PageModels;   // FIX: was "Agooha.Mobile.PageModels" (CS0234)

namespace CraftConnect_Mobile_App.Pages     // FIX: was "Agooha.Mobile.Pages"
{
    public partial class HomePage : ContentPage
    {
        private readonly HomePageModel _viewModel;

        public HomePage(HomePageModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            try
            {
                await Task.Delay(100); // allow layout to settle
                await _viewModel.InitialiseAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HOME PAGE] OnAppearing error: {ex.Message}");
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            // PageModel handles timer cleanup via its own dispose logic
        }
    }
}