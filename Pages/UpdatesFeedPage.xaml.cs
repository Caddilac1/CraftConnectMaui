using CraftConnect_Mobile_App.PageModels;

namespace CraftConnect_Mobile_App.Pages
{
    public partial class UpdatesFeedPage : ContentPage
    {
        private readonly UpdatesFeedPageModel _viewModel;

        public UpdatesFeedPage()
        {
            InitializeComponent();

            // Initialize and set the BindingContext
            _viewModel = new UpdatesFeedPageModel();
            BindingContext = _viewModel;

            System.Diagnostics.Debug.WriteLine("[UpdatesFeedPage] Page initialized");
        }

        // Event handlers for XAML buttons (using emoji icons instead of images)
        private void OnSearchClicked(object sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[UpdatesFeedPage] Search clicked");
            _viewModel.SearchCommand.Execute(null);
        }

        private void OnFilterClicked(object sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[UpdatesFeedPage] Filter clicked");
            _viewModel.FilterCommand.Execute(null);
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            System.Diagnostics.Debug.WriteLine("[UpdatesFeedPage] OnAppearing - Initializing...");

            // Initialize data when page appears (only if not already loaded)
            if (_viewModel.AllFeeds.Count == 0)
            {
                await _viewModel.InitializeAsync();
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            System.Diagnostics.Debug.WriteLine("[UpdatesFeedPage] OnDisappearing");
        }
    }
}