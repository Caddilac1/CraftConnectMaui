using CraftConnect_Mobile_App.PageModels;
using System.Diagnostics;

namespace CraftConnect_Mobile_App.Pages
{
    public partial class CheckoutPage : ContentPage
    {
        private readonly CheckoutPageModel _viewModel;

        public CheckoutPage(CheckoutPageModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            Debug.WriteLine("[CART PAGE] OnAppearing");
            _ = _viewModel.InitializeAsync();
        }
    }
}
