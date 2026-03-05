using CraftConnect_Mobile_App.PageModels;

namespace CraftConnect_Mobile_App.Pages;

public partial class RegisterPage : ContentPage
{
    public RegisterPage(RegisterPageModel model)
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
        if (BindingContext is RegisterPageModel vm)
            vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (BindingContext is RegisterPageModel vm)
            vm.PropertyChanged -= OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(RegisterPageModel.IsBusy))
            return;

        var vm = (RegisterPageModel)sender;
        var overlay = this.FindByName<CraftConnect_Mobile_App.Controls.LoadingOverlay>("PageLoadingOverlay");
        if (overlay == null) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (vm.IsBusy)
                overlay.Show("Creating your account...");
            else
                overlay.Hide();
        });
    }
}