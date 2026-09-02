namespace ManyControl;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
        var savedTheme = Preferences.Get("app_theme", "Dark");
        UserAppTheme = savedTheme == "Light" ? AppTheme.Light : AppTheme.Dark;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var appShell = _services.GetRequiredService<AppShell>();
        return new Window(appShell);
    }
}
