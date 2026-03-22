using Avalonia.Controls;
using System;
using System.Diagnostics;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Input;
using Avalonia.Media;

namespace AvyscanLab.Views
{
    public partial class AboutWindow : Window
    {
        private string? _websiteUrl;

        public AboutWindow()
        {
            InitializeComponent();
            CloseButton.Click += (_, _) => Close();
            CloseXButton.Click += (_, _) => Close();
            TitleBar.PointerPressed += (_, e) => BeginMoveDrag(e);
            WebsiteTextBlock.PointerPressed += WebsiteTextBlock_OnPointerPressed;
        }

        public void Configure(
            string title,
            string company,
            string rights,
            string website,
            string version,
            string closeLabel,
            string? imageUri)
        {
            Title = title;
            TitleBarLabel.Text = title.ToUpperInvariant();
            CompanyTextBlock.Text = company;
            RightsTextBlock.Text = rights;

            _websiteUrl = NormalizeWebsiteUrl(website);
            WebsiteTextBlock.Text = website;
            WebsiteTextBlock.Cursor = _websiteUrl is null ? new Cursor(StandardCursorType.Arrow) : new Cursor(StandardCursorType.Hand);
            WebsiteTextBlock.Foreground = _websiteUrl is null ? new SolidColorBrush(Color.Parse("#7984A5")) : new SolidColorBrush(Color.Parse("#4A9EDB"));
            WebsiteTextBlock.TextDecorations = _websiteUrl is null ? null : TextDecorations.Underline;

            VersionTextBlock.Text = version;
            CloseButton.Content = closeLabel;

            if (!string.IsNullOrWhiteSpace(imageUri))
            {
                var uri = new Uri(imageUri, UriKind.RelativeOrAbsolute);
                AboutImage.Source = uri.IsAbsoluteUri
                    ? new Bitmap(AssetLoader.Open(uri))
                    : new Bitmap(imageUri);
            }
        }

        private static string? NormalizeWebsiteUrl(string website)
        {
            if (string.IsNullOrWhiteSpace(website))
            {
                return null;
            }

            var trimmed = website.Trim();
            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var absoluteUri)
                && (absoluteUri.Scheme == Uri.UriSchemeHttp || absoluteUri.Scheme == Uri.UriSchemeHttps))
            {
                return absoluteUri.ToString();
            }

            var withProtocol = $"https://{trimmed}";
            return Uri.TryCreate(withProtocol, UriKind.Absolute, out var httpsUri)
                ? httpsUri.ToString()
                : null;
        }

        private void WebsiteTextBlock_OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (_websiteUrl is null)
            {
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = _websiteUrl,
                UseShellExecute = true
            });
        }
    }
}
