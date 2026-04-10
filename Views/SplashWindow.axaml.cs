using Avalonia.Controls;
using Avalonia.Media;
using AvyScanLab.Services;

namespace AvyScanLab.Views
{
    public partial class SplashWindow : Window
    {
        public SplashWindow() : this(languageCode: null) { }

        /// <summary>
        /// Builds the splash and adapts its status line to the current license state.
        /// The two-letter <paramref name="languageCode"/> selects the localized strings
        /// (en/fr/de/es); unknown codes fall back to English.
        /// </summary>
        public SplashWindow(string? languageCode)
        {
            InitializeComponent();
            ApplyStatusText(languageCode);
        }

        private void ApplyStatusText(string? languageCode)
        {
            var lang = (languageCode ?? "en").ToLowerInvariant();
            var licensed = LicenseService.IsLicensed;

            (string main, string sub, string colorHex) = (lang, licensed) switch
            {
                // ── Licensed (full) ────────────────────────────────────────────
                ("fr", true) => ("Version complète — Activée",
                                 "Toutes les fonctionnalités débloquées",
                                 "#7BC67E"),
                ("de", true) => ("Vollversion — Aktiviert",
                                 "Alle Funktionen freigeschaltet",
                                 "#7BC67E"),
                ("es", true) => ("Versión completa — Activada",
                                 "Todas las funciones desbloqueadas",
                                 "#7BC67E"),
                (_,    true) => ("Full Version — Activated",
                                 "All features unlocked",
                                 "#7BC67E"),

                // ── Trial ──────────────────────────────────────────────────────
                ("fr", false) => ("Version d'essai — Pleinement fonctionnelle",
                                  "Un seul clip — pas d'encodage par lot, pas de filtres personnalisés",
                                  "#CEB35C"),
                ("de", false) => ("Testversion — Voll funktionsfähig",
                                  "Ein Clip — keine Stapelkodierung, keine benutzerdefinierten Filter",
                                  "#CEB35C"),
                ("es", false) => ("Versión de prueba — Totalmente funcional",
                                  "Un solo clip — sin codificación por lotes, sin filtros personalizados",
                                  "#CEB35C"),
                (_,    false) => ("Trial Version — Fully functional",
                                  "Single clip — no batch encoding, no custom filters",
                                  "#CEB35C"),
            };

            StatusLine.Text = main;
            StatusLine.Foreground = new SolidColorBrush(Color.Parse(colorHex));
            StatusSubLine.Text = sub;
        }
    }
}
