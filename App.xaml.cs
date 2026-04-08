using CraftConnect_Mobile_App.Services;

namespace CraftConnect_Mobile_App
{
    public partial class App : Application
    {
        private readonly AuthService _authService;

        public App(AuthService authService)
        {
            InitializeComponent();
            _authService = authService;

            // Fire immediately in background — warms the TCP+TLS connection
            // so it's ready before the user reaches the login page.
            _ = Task.Run(() => _authService.PreWarmConnectionAsync());
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}