using CraftConnect_Mobile_App.PageModels;
using System.Diagnostics;

namespace CraftConnect_Mobile_App.Pages
{
    public partial class CartPage : ContentPage
    {
        private readonly CartPageModel _viewModel;

        public CartPage(CartPageModel viewModel)
        {
            InitializeComponent();
            _viewModel     = viewModel;
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
