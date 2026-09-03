namespace ManyControl.Services;

public class DialogService : IDialogService
{
    private static Page? CurrentPage
    {
        get
        {
            if (Shell.Current?.CurrentPage is Page shellPage)
            {
                return shellPage;
            }
            return Application.Current?.Windows.FirstOrDefault()?.Page;
        }
    }

    public async Task ShowAlertAsync(string title, string message, string cancel = "OK")
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var page = CurrentPage;
            if (page is not null)
            {
                await page.DisplayAlertAsync(title, message, cancel);
            }
        });
    }

    public async Task<bool> ShowConfirmationAsync(string title, string message, string accept, string cancel)
    {
        return await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var page = CurrentPage;
            if (page is not null)
            {
                return await page.DisplayAlertAsync(title, message, accept, cancel);
            }

            return false;
        });
    }

    public async Task<string> ShowPromptAsync(string title, string message, string accept = "Continuar", string cancel = "Cancelar", string? initialValue = null)
    {
        return await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var page = CurrentPage;
            if (page is not null)
            {
                return await page.DisplayPromptAsync(title, message, accept, cancel, initialValue: initialValue) ?? string.Empty;
            }

            return string.Empty;
        });
    }

    public async Task<string?> ShowActionSheetAsync(string title, string cancel, string? destruction, params string[] buttons)
    {
        return await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var page = CurrentPage;
            if (page is not null)
            {
                return await page.DisplayActionSheetAsync(title, cancel, destruction, buttons);
            }

            return null;
        });
    }

    public async Task<FileResult?> PickFileAsync(string pickerTitle)
    {
        return await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var jsonFileType = new FilePickerFileType(
                new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".json" } },
                    { DevicePlatform.Android, new[] { "application/json", "text/json" } },
                    { DevicePlatform.iOS, new[] { "public.json" } },
                    { DevicePlatform.MacCatalyst, new[] { "public.json" } }
                });

            return await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = pickerTitle,
                FileTypes = jsonFileType
            });
        });
    }
}
