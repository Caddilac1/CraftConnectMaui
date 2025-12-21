namespace CraftConnect_Mobile_App.Services;

public interface IDialogService
{
    Task ShowAlertAsync(string title, string message, string cancel = "OK");
    Task<bool> ShowConfirmAsync(string title, string message, string accept = "Yes", string cancel = "No", bool isDestructive = false);
    Task<string> ShowPromptAsync(string title, string message, string accept = "OK", string cancel = "Cancel", string placeholder = null, int maxLength = -1, Keyboard keyboard = null, string initialValue = "", bool isPassword = false);
    Task<string> ShowActionSheetAsync(string title, string cancel, string destruction, params string[] buttons);
    Task<string[]> ShowMultiSelectAsync(string title, string[] options, string[] selected = null);
    Task ShowToastAsync(string message, int duration = 3000);
}