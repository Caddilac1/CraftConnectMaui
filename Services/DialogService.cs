namespace CraftConnect_Mobile_App.Services;

public class DialogService : IDialogService
{
    public Task ShowAlertAsync(string title, string message, string cancel = "OK")
    {
        return Application.Current.MainPage.DisplayAlert(title, message, cancel);
    }

    public Task<bool> ShowConfirmAsync(string title, string message, string accept = "Yes", string cancel = "No", bool isDestructive = false)
    {
        return Application.Current.MainPage.DisplayAlert(title, message, accept, cancel);
    }

    public async Task<string> ShowPromptAsync(string title, string message, string accept = "OK", string cancel = "Cancel", string placeholder = null, int maxLength = -1, Keyboard keyboard = null, string initialValue = "", bool isPassword = false)
    {
        return await Application.Current.MainPage.DisplayPromptAsync(
            title,
            message,
            accept,
            cancel,
            placeholder,
            maxLength,
            keyboard ?? Keyboard.Default,
            initialValue);
    }

    public async Task<string> ShowActionSheetAsync(string title, string cancel, string destruction, params string[] buttons)
    {
        return await Application.Current.MainPage.DisplayActionSheet(title, cancel, destruction, buttons);
    }

    public Task<string[]> ShowMultiSelectAsync(string title, string[] options, string[] selected = null)
    {
        // MAUI doesn't have built-in multi-select dialog
        // You'll need to implement a custom page/popup for this
        // For now, return the selected items or empty array
        return Task.FromResult(selected ?? Array.Empty<string>());
    }

    public async Task ShowToastAsync(string message, int duration = 3000)
    {
        // Simple implementation - you might want to use a toast library like CommunityToolkit.Maui
        await Application.Current.MainPage.DisplayAlert("", message, "OK");
    }
}