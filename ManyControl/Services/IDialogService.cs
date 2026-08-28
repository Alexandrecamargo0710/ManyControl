namespace ManyControl.Services;

public interface IDialogService
{
    Task ShowAlertAsync(string title, string message, string cancel = "OK");

    Task<bool> ShowConfirmationAsync(string title, string message, string accept, string cancel);

    Task<string> ShowPromptAsync(string title, string message, string accept = "Continuar", string cancel = "Cancelar", string? initialValue = null);

    Task<string?> ShowActionSheetAsync(string title, string cancel, string? destruction, params string[] buttons);

    Task<FileResult?> PickFileAsync(string pickerTitle);
}
