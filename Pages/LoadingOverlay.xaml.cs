using Microsoft.Maui.Controls.Shapes;

namespace CraftConnect_Mobile_App.Controls
{
    public partial class LoadingOverlay : ContentView
    {
        private CancellationTokenSource _waveCts;
        private bool _isAnimating = false;

        public LoadingOverlay()
        {
            InitializeComponent();
        }

        // ── Public API ────────────────────────────────────────────────

        public void Show(string message = "Loading...")
        {
            if (_isAnimating) return;
            _isAnimating = true;

            LoadingMessageLabel.Text = message;
            IsVisible = true;

            ResetToStartState();

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await PlayEntranceSequenceAsync();
                StartWaveLoop();
            });
        }

        public void Hide()
        {
            StopWaveLoop();
            _isAnimating = false;

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Task.WhenAll(
                    IconFrame.ScaleTo(0, 220, Easing.CubicIn),
                    IconFrame.FadeTo(0, 180),
                    AppNameLabel.FadeTo(0, 140),
                    LoadingMessageLabel.FadeTo(0, 140)
                );
                IsVisible = false;
                ResetToStartState();
            });
        }

        // ── Entrance animation ─────────────────────────────────────────

        private async Task PlayEntranceSequenceAsync()
        {
            IconFrame.Scale = 0;
            IconFrame.Rotation = -180 + 45; // account for the 45° diamond rotation
            IconFrame.Opacity = 0;

            await Task.WhenAll(
                IconFrame.FadeTo(1, 280),
                IconFrame.ScaleTo(1.1, 380, Easing.CubicOut),
                IconFrame.RotateTo(45, 460, Easing.CubicOut)   // settle at 45° (diamond)
            );

            await IconFrame.ScaleTo(1.0, 140, Easing.CubicIn);

            await Task.WhenAll(
                AppNameLabel.FadeTo(1, 280),
                AppNameLabel.TranslateTo(0, 0, 280, Easing.CubicOut)
            );

            await Dot1.FadeTo(0.8, 110);
            await Dot2.FadeTo(0.8, 110);
            await Dot3.FadeTo(0.8, 110);

            await LoadingMessageLabel.FadeTo(1, 200);
        }

        // ── Wave dot loop — runs entirely on the main thread ──────────

        private void StartWaveLoop()
        {
            _waveCts = new CancellationTokenSource();
            var token = _waveCts.Token;

            // Run the whole wave loop on the main thread using a recursive async call
            MainThread.BeginInvokeOnMainThread(() => RunWaveAsync(token));
        }

        private async Task RunWaveAsync(CancellationToken token)
        {
            var dots = new[] { Dot1, Dot2, Dot3 };

            while (!token.IsCancellationRequested)
            {
                // Animate each dot one after another with 120ms stagger
                foreach (var dot in dots)
                {
                    if (token.IsCancellationRequested) return;

                    // Fire dot bounce without awaiting so next dot starts after stagger
                    _ = BounceDotAsync(dot, token);

                    try { await Task.Delay(160, token); }
                    catch (TaskCanceledException) { return; }
                }

                // Pause between full wave cycles
                try { await Task.Delay(400, token); }
                catch (TaskCanceledException) { return; }
            }
        }

        private async Task BounceDotAsync(Ellipse dot, CancellationToken token)
        {
            if (token.IsCancellationRequested) return;
            await Task.WhenAll(
                dot.TranslateTo(0, -10, 200, Easing.CubicOut),
                dot.FadeTo(1.0, 200)
            );
            if (token.IsCancellationRequested) return;
            await Task.WhenAll(
                dot.TranslateTo(0, 0, 200, Easing.CubicIn),
                dot.FadeTo(0.35, 200)
            );
        }

        private void StopWaveLoop()
        {
            _waveCts?.Cancel();
            _waveCts?.Dispose();
            _waveCts = null;
        }

        // ── Reset ─────────────────────────────────────────────────────

        private void ResetToStartState()
        {
            IconFrame.Scale = 0;
            IconFrame.Opacity = 0;
            IconFrame.Rotation = -135; // start position: -180 + 45

            AppNameLabel.Opacity = 0;
            AppNameLabel.TranslationY = 10;

            Dot1.Opacity = 0.35; Dot1.TranslationY = 0;
            Dot2.Opacity = 0.35; Dot2.TranslationY = 0;
            Dot3.Opacity = 0.35; Dot3.TranslationY = 0;

            LoadingMessageLabel.Opacity = 0;
        }
    }
}