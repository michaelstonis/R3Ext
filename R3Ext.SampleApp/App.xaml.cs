namespace R3Ext.SampleApp;

public partial class App : Application
{
    public App()
    {
        this.InitializeComponent();

        // Force the app to use light mode
        this.UserAppTheme = AppTheme.Light;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }
}
