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
        // Register your own app at:
        // https://discord.com/developers/applications
        //
        // Paste your Client ID here to enable Discord Rich Presence.
        private const string DiscordClientId =
            "YOUR_DISCORD_APPLICATION_ID";

        private DiscordRpcClient? _discord;
        private readonly DateTime _sessionStart = DateTime.UtcNow;

        private bool _isFullscreen = false;

        // Store the normal window size/position so we can restore it.
        private double _normalWidth = 1400;
        private double _normalHeight = 900;

        public MainWindow()
        {
            InitializeComponent();

            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;

            // Keyboard controls
            PreviewKeyDown += MainWindow_PreviewKeyDown;
        }

        private async void MainWindow_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            InitializeDiscord();

            // Keep Apple TV login/profile/session data between launches.
            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "AppleTVForPC",
                "WebView2Data");

            var env =
                await CoreWebView2Environment.CreateAsync(
                    userDataFolder: userDataFolder);

            await Browser.EnsureCoreWebView2Async(env);

            /*
             * Use a current desktop Chromium user agent.
             *
             * Apple officially supports Chrome, Firefox and Edge
             * on Windows for tv.apple.com, with compatible content
             * available up to 1080p.
             */
            Browser.CoreWebView2.Settings.UserAgent =
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                "AppleWebKit/537.36 (KHTML, like Gecko) " +
                "Chrome/151.0.0.0 Safari/537.36";

            /*
             * Keep normal Chromium/WebView2 rendering features enabled.
             */
            Browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled =
                true;

            Browser.CoreWebView2.Settings.AreDevToolsEnabled =
                true;

            /*
             * Make the WebView background black.
             * This prevents a white flash around the page/video.
             */
            Browser.CoreWebView2.Profile.PreferredColorScheme =
                CoreWebView2PreferredColorScheme.Dark;

            /*
             * Detect page title changes for Discord Rich Presence.
             */
            Browser.CoreWebView2.DocumentTitleChanged +=
                CoreWebView2_DocumentTitleChanged;

            /*
             * Keep Apple TV links/popups inside our window.
             */
            Browser.CoreWebView2.NewWindowRequested +=
                (s, args) =>
                {
                    args.Handled = true;

                    if (!string.IsNullOrWhiteSpace(args.Uri))
                    {
                        Browser.CoreWebView2.Navigate(args.Uri);
                    }
                };

            /*
             * Navigate to Apple TV.
             */
            Browser.Source =
                new Uri("https://tv.apple.com/");
        }

        // ============================================================
        // FULLSCREEN
        // ============================================================

        private void EnterFullscreen()
        {
            if (_isFullscreen)
                return;

            // Save current window dimensions.
            _normalWidth = Width;
            _normalHeight = Height;

            /*
             * Remove the Windows title bar/borders.
             */
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;

            /*
             * Make the window cover the screen.
             */
            WindowState = WindowState.Maximized;

            /*
             * Keep it above the taskbar.
             */
            Topmost = true;

            _isFullscreen = true;
        }

        private void ExitFullscreen()
        {
            if (!_isFullscreen)
                return;

            /*
             * Remove Topmost first.
             */
            Topmost = false;

            /*
             * Restore normal window mode.
             */
            WindowState = WindowState.Normal;

            WindowStyle = WindowStyle.SingleBorderWindow;
            ResizeMode = ResizeMode.CanResize;

            /*
             * Restore the original size.
             */
            Width = _normalWidth;
            Height = _normalHeight;

            _isFullscreen = false;
        }

        private void ToggleFullscreen()
        {
            if (_isFullscreen)
            {
                ExitFullscreen();
            }
            else
            {
                EnterFullscreen();
            }
        }

        private void MainWindow_PreviewKeyDown(
            object sender,
            KeyEventArgs e)
        {
            /*
             * F11 = toggle fullscreen.
             */
            if (e.Key == Key.F11)
            {
                ToggleFullscreen();

                e.Handled = true;
                return;
            }

            /*
             * Escape = leave fullscreen.
             */
            if (e.Key == Key.Escape && _isFullscreen)
            {
                ExitFullscreen();

                e.Handled = true;
                return;
            }
        }

        // ============================================================
        // DISCORD
        // ============================================================

        private void InitializeDiscord()
        {
            if (DiscordClientId ==
                "YOUR_DISCORD_APPLICATION_ID")
            {
                return;
            }

            _discord =
                new DiscordRpcClient(DiscordClientId);

            _discord.Initialize();

            SetPresence(
                "Browsing Apple TV+",
                "On the home screen");
        }

        private void CoreWebView2_DocumentTitleChanged(
            object? sender,
            object e)
        {
            var title =
                Browser.CoreWebView2.DocumentTitle;

            if (string.IsNullOrWhiteSpace(title))
                return;

            /*
             * Apple TV page titles commonly end in
             * one of these strings.
             */
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
                    showName =
                        title[..^suffix.Length].Trim();

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

                    Timestamps =
                        new Timestamps(_sessionStart),

                    Assets = new Assets
                    {
                        LargeImageKey =
                            "appletv_logo",

                        LargeImageText =
                            "Apple TV+ for PC"
                    }
                });
        }

        // ============================================================
        // CLOSE
        // ============================================================

        private void MainWindow_Closed(
            object? sender,
            EventArgs e)
        {
            _discord?.Dispose();
        }
    }
}