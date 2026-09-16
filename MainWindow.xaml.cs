using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using DiscordRPC;
using Microsoft.Web.WebView2.Core;

namespace AppleTVForPC
{
    public partial class MainWindow : Window
    {
        // Register your own app at https://discord.com/developers/applications
        // and paste its Client ID here to enable Rich Presence.
        private const string DiscordClientId = "YOUR_DISCORD_APPLICATION_ID";

        private DiscordRpcClient? _discord;
        private readonly DateTime _sessionStart = DateTime.UtcNow;

        private bool _isFullscreen = true;

        public MainWindow()
        {
            InitializeComponent();

            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;

            PreviewKeyDown += MainWindow_PreviewKeyDown;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Enter proper fullscreen
            EnterFullscreen();

            InitializeDiscord();

            // Keep the Apple TV+ login/profile session on disk between launches.
            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AppleTVForPC",
                "WebView2Data");

            var env = await CoreWebView2Environment.CreateAsync(
                userDataFolder: userDataFolder);

            await Browser.EnsureCoreWebView2Async(env);

            // Everyday desktop Chromium UA so tv.apple.com serves its normal web player.
            Browser.CoreWebView2.Settings.UserAgent =
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                "(KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36";

            Browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            Browser.CoreWebView2.Settings.AreDevToolsEnabled = true;

            Browser.CoreWebView2.DocumentTitleChanged +=
                CoreWebView2_DocumentTitleChanged;

            Browser.CoreWebView2.NewWindowRequested += (s, args) =>
            {
                // Open popups (sign-in, help links) in the same view
                // instead of a new OS window.
                args.Handled = true;
                Browser.CoreWebView2.Navigate(args.Uri);
            };

            Browser.Source = new Uri("https://tv.apple.com/");
        }

        private void EnterFullscreen()
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;

            // Topmost helps ensure the Windows taskbar doesn't appear
            // over the application.
            Topmost = true;

            WindowState = WindowState.Maximized;
        }

        private void ExitFullscreen()
        {
            Topmost = false;

            WindowState = WindowState.Normal;
            WindowStyle = WindowStyle.SingleBorderWindow;
            ResizeMode = ResizeMode.CanResize;

            Width = 1400;
            Height = 900;
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // F11 toggles fullscreen
            if (e.Key == Key.F11)
            {
                if (_isFullscreen)
                {
                    ExitFullscreen();
                    _isFullscreen = false;
                }
                else
                {
                    EnterFullscreen();
                    _isFullscreen = true;
                }

                e.Handled = true;
            }

            // Escape exits fullscreen
            if (e.Key == Key.Escape && _isFullscreen)
            {
                ExitFullscreen();
                _isFullscreen = false;

                e.Handled = true;
            }
        }

        private void InitializeDiscord()
        {
            if (DiscordClientId == "YOUR_DISCORD_APPLICATION_ID")
                return;

            _discord = new DiscordRpcClient(DiscordClientId);
            _discord.Initialize();

            SetPresence(
                "Browsing Apple TV+",
                "On the home screen");
        }

        private void CoreWebView2_DocumentTitleChanged(
            object? sender,
            object e)
        {
            var title = Browser.CoreWebView2.DocumentTitle;

            if (string.IsNullOrWhiteSpace(title))
                return;

            // tv.apple.com titles typically look like:
            // "<Show/Movie name> - Apple TV"
            string[] suffixes =
            {
                " - Apple TV",
                " — Apple TV",
                " | Apple TV"
            };

            string? showName = null;

            foreach (var suffix in suffixes)
            {
                if (title.EndsWith(
                    suffix,
                    StringComparison.OrdinalIgnoreCase))
                {
                    showName = title[..^suffix.Length].Trim();
                    break;
                }
            }

            if (!string.IsNullOrWhiteSpace(showName) &&
                !showName.Equals(
                    "Apple TV",
                    StringComparison.OrdinalIgnoreCase))
            {
                SetPresence(
                    "Watching " + showName,
                    "via Apple TV+ for PC");
            }
            else
            {
                SetPresence(
                    "Browsing Apple TV+",
                    "Looking for something to watch");
            }
        }

        private void SetPresence(
            string details,
            string state)
        {
            _discord?.SetPresence(
                new RichPresence
                {
                    Details = details,
                    State = state,

                    Timestamps = new Timestamps(
                        _sessionStart),

                    Assets = new Assets
                    {
                        LargeImageKey = "appletv_logo",
                        LargeImageText = "Apple TV+ for PC"
                    }
                });
        }

        private void MainWindow_Closed(
            object? sender,
            EventArgs e)
        {
            _discord?.Dispose();
        }
    }
}