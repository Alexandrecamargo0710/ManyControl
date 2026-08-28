namespace ManyControl.Services;

public class DialogService : IDialogService
{
    private static Page? CurrentPage => Application.Current?.Windows.FirstOrDefault()?.Page;

    public async Task ShowAlertAsync(string title, string message, string cancel = "OK")
    {
        if (CurrentPage is not null)
        {
            await CurrentPage.DisplayAlertAsync(title, message, cancel);
        }
    }

    public async Task<bool> ShowConfirmationAsync(string title, string message, string accept, string cancel)
    {
        if (CurrentPage is not null)
        {
            return await CurrentPage.DisplayAlertAsync(title, message, accept, cancel);
        }

        return false;
    }

    public async Task<string> ShowPromptAsync(string title, string message, string accept = "Continuar", string cancel = "Cancelar", string? initialValue = null)
    {
        if (CurrentPage is not null)
        {
            return await CurrentPage.DisplayPromptAsync(title, message, accept, cancel, initialValue: initialValue) ?? string.Empty;
        }

        return string.Empty;
    }

    public async Task<string?> ShowActionSheetAsync(string title, string cancel, string? destruction, params string[] buttons)
    {
        if (CurrentPage is not null)
        {
            return await CurrentPage.DisplayActionSheetAsync(title, cancel, destruction, buttons);
        }

        return null;
    }

    public async Task<FileResult?> PickFileAsync(string pickerTitle)
    {
        return await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = pickerTitle
        });
    }
}
