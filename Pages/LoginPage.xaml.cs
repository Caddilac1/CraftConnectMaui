namespace CraftConnect_Mobile_App.Pages;

public partial class LoginPage : ContentPage
{
    public LoginPage(CraftConnect_Mobile_App.PageModels.LoginPageModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        // Hide overlay whenever we land on this page
        this.FindByName<CraftConnect_Mobile_App.Controls.LoadingOverlay>("PageLoadingOverlay")?.Hide();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is CraftConnect_Mobile_App.PageModels.LoginPageModel vm)
            vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (BindingContext is CraftConnect_Mobile_App.PageModels.LoginPageModel vm)
            vm.PropertyChanged -= OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CraftConnect_Mobile_App.PageModels.LoginPageModel.IsBusy))
            return;

        var vm = (CraftConnect_Mobile_App.PageModels.LoginPageModel)sender;
        var overlay = this.FindByName<CraftConnect_Mobile_App.Controls.LoadingOverlay>("PageLoadingOverlay");
        if (overlay == null) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (vm.IsBusy)
                overlay.Show(vm.IsPasswordMode ? "Signing you in..." : "Sending OTP...");
            else
                overlay.Hide();
        });
    }
}