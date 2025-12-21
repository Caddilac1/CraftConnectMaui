using CraftConnect_Mobile_App.PageModels;


namespace CraftConnect_Mobile_App.Pages
{
    public partial class LoginPage : ContentPage
    {
        public LoginPage(LoginPageModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}