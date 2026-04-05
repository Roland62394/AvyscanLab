using System;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Threading;

namespace AvyScanLab.Views
{
    public partial class SplashWindow : Window
    {
        private DispatcherTimer? _sparkleTimer;
        private double _sparklePhase;

        public SplashWindow()
        {
            InitializeComponent();
            Opened += OnOpened;
        }

        private void OnOpened(object? sender, EventArgs e)
        {
            var sparkle = this.FindControl<Ellipse>("Sparkle");
            if (sparkle == null) return;

            _sparklePhase = 0;
            _sparkleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
            _sparkleTimer.Tick += (_, _) =>
            {
                // ~2.4s full cycle
                _sparklePhase += 0.03;

                // sin curve: 0→1→0 mapped to opacity 0.15..0.95
                var sin = Math.Sin(_sparklePhase);
                var opacity = 0.15 + 0.80 * sin * sin;
                sparkle.Opacity = opacity;
            };
            _sparkleTimer.Start();
        }

        protected override void OnClosed(EventArgs e)
        {
            _sparkleTimer?.Stop();
            _sparkleTimer = null;
            base.OnClosed(e);
        }
    }
}
