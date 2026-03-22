/* In the name of God, the Merciful, the Compassionate */

using System.Diagnostics;
using System.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using SqlHealthAssessment.Data;
using SqlHealthAssessment.Data.Services;
using System.Windows.Threading;

namespace SqlHealthAssessment
{
    public partial class MainWindow : Window
    {
        private readonly ILogger<MainWindow>? _logger;
        private readonly UserSettingsService? _userSettings;
        private bool _webView2Initialized = false;
        private bool _suppressZoomSync = false;
        private Microsoft.Web.WebView2.Core.CoreWebView2? _coreWebView2;

        public MainWindow()
        {
            InitializeComponent();
            _logger = App.Services?.GetService<ILogger<MainWindow>>();
            _userSettings = App.Services?.GetService<UserSettingsService>();

            var version = App.Services?.GetService<AutoUpdateService>()?.GetCurrentVersion() ?? "0.78.6";
            Title = $"SQL Health Assessment v{version}";

            // Add keyboard shortcut for DevTools (F12)
            KeyDown += OnKeyDown;

            // Add WebView error handling
            BlazorWebView.BlazorWebViewInitialized += OnBlazorWebViewInitialized;
            Loaded += OnWindowLoaded;

            // Listen for zoom changes from settings UI
            if (_userSettings != null)
                _userSettings.OnZoomChanged += OnZoomChanged;
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            _logger?.LogInformation("MainWindow loaded successfully");
        }

        private void OnBlazorWebViewInitialized(object? sender, Microsoft.AspNetCore.Components.WebView.BlazorWebViewInitializedEventArgs e)
        {
            try
            {
                _logger?.LogInformation("BlazorWebView initialized");
                _webView2Initialized = true;
                _coreWebView2 = e.WebView.CoreWebView2;

                // Wire up PrintService with the live CoreWebView2 instance
                var printService = App.Services?.GetService<Data.Services.PrintService>();
                printService?.SetWebView(_coreWebView2);

                // Enable Ctrl+scroll zoom and sync back to settings
                _coreWebView2.Settings.IsZoomControlEnabled = true;
                _coreWebView2.Settings.IsPinchZoomEnabled = true;
                e.WebView.ZoomFactorChanged += OnWebViewZoomFactorChanged;

                // Apply saved zoom level after navigation completes (so it sticks)
                e.WebView.NavigationCompleted += (s, args) =>
                {
                    if (args.IsSuccess)
                    {
                        _logger?.LogDebug("WebView navigation completed successfully");
                        ApplyZoom(_userSettings?.GetZoomLevel() ?? 150);
                    }
                    else
                    {
                        _logger?.LogError("WebView navigation failed");
                    }
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during BlazorWebView initialization");
                ShowWebView2Error(ex.Message);
            }
        }

        private void ApplyZoom(int zoomPercent)
        {
            try
            {
                _suppressZoomSync = true;
                BlazorWebView.WebView.ZoomFactor = zoomPercent / 100.0;
                _suppressZoomSync = false;
                _logger?.LogInformation("Zoom set to {ZoomPercent}%", zoomPercent);
            }
            catch (Exception ex)
            {
                _suppressZoomSync = false;
                _logger?.LogWarning(ex, "Failed to set zoom to {ZoomPercent}%", zoomPercent);
            }
        }

        private void OnZoomChanged(int zoomPercent)
        {
            Dispatcher.Invoke(() => ApplyZoom(zoomPercent));
        }

        private void OnWebViewZoomFactorChanged(object? sender, object e)
        {
            if (_suppressZoomSync || _userSettings == null) return;
            try
            {
                var newPercent = (int)Math.Round(BlazorWebView.WebView.ZoomFactor * 100);
                // Only save if it actually changed (avoids infinite loop with OnZoomChanged)
                if (newPercent != _userSettings.GetZoomLevel())
                {
                    _suppressZoomSync = true;
                    _userSettings.SetZoomLevel(newPercent);
                    _suppressZoomSync = false;
                }
            }
            catch { /* non-critical */ }
        }

        private void ShowWebView2Error(string? message = null)
        {
            Dispatcher.Invoke(() =>
            {
                var errorMsg = message ?? App.WebView2ErrorMessage ?? "WebView2 runtime is not available";
                WebView2ErrorMessage.Text = $"Error: {errorMsg}\n\nWindows Version: {WebView2Helper.GetWindowsVersion()}\n\n" +
                    "The application requires Microsoft Edge WebView2 Runtime to function. " +
                    "Please install the runtime and restart the application.";
                WebView2ErrorOverlay.Visibility = Visibility.Visible;
            });
        }

        private async void OnInstallWebView2Click(object sender, RoutedEventArgs e)
        {
            try
            {
                InstallWebView2Button.IsEnabled = false;
                InstallWebView2Button.Content = "Installing...";

                _logger?.LogInformation("Starting WebView2 installation...");
                
                var helper = App.WebView2Helper ?? new WebView2Helper(null);
                var result = await helper.TryInstallWebView2Async();

                if (result.Success)
                {
                    _logger?.LogInformation("WebView2 installed successfully. Version: {Version}", result.Version);
                    MessageBox.Show($"WebView2 Runtime installed successfully!\nVersion: {result.Version}\n\nPlease restart the application.",
                        "Installation Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                    Application.Current.Shutdown();
                }
                else
                {
                    _logger?.LogError("WebView2 installation failed: {Error}", result.ErrorMessage);
                    MessageBox.Show($"Failed to install WebView2:\n{result.ErrorMessage}\n\n" +
                        "Please install manually from:\nhttps://developer.microsoft.com/en-us/microsoft-edge/webview2/",
                        "Installation Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during WebView2 installation");
                MessageBox.Show($"Error during installation:\n{ex.Message}",
                    "Installation Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                InstallWebView2Button.IsEnabled = true;
                InstallWebView2Button.Content = "Auto-Install WebView2";
            }
        }

        private void OnRetryClick(object sender, RoutedEventArgs e)
        {
            // Check if WebView2 is now available
            var helper = App.WebView2Helper ?? new WebView2Helper(null);
            var status = helper.CheckWebView2StatusAsync().GetAwaiter().GetResult();

            if (status.IsInstalled && status.IsCompatible)
            {
                _logger?.LogInformation("WebView2 is now available. Version: {Version}", status.Version);
                WebView2ErrorOverlay.Visibility = Visibility.Collapsed;
                // The WebView will automatically retry initialization
            }
            else
            {
                _logger?.LogWarning("WebView2 still not available");
                MessageBox.Show("WebView2 runtime is still not available.\n\n" +
                    "Please install the runtime and click Retry.",
                    "WebView2 Not Available", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void OnServerModeClick(object sender, RoutedEventArgs e)
        {
            try
            {
                ServerModeButton.IsEnabled = false;
                ServerModeButton.Content = "Starting server...";

                var serverMode = App.Services?.GetService<ServerModeService>();
                if (serverMode == null)
                {
                    MessageBox.Show("Server mode service not available.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                await serverMode.StartAsync();

                _logger?.LogInformation("Server mode started at {Url}", serverMode.Url);

                // Open in default browser
                if (serverMode.Url != null)
                {
                    Process.Start(new ProcessStartInfo(serverMode.Url) { UseShellExecute = true });
                }

                // Update the error overlay to show success
                WebView2ErrorTitle.Text = "Server Mode Active";
                WebView2ErrorTitle.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4488ff"));
                var diagUrl = $"http://localhost:{serverMode.Port}/_server/diag";
                var healthUrl = $"http://localhost:{serverMode.Port}/_server/health";
                WebView2ErrorMessage.Text = $"Access the application in your browser:\n\n" +
                    $"  App:      {serverMode.Url}\n" +
                    $"  Diag:     {diagUrl}\n" +
                    $"  Health:   {healthUrl}\n\n" +
                    "Share the App URL with other users on your network.\n" +
                    "Keep this window open to maintain the server.";

                ServerModeButton.Content = $"Running at {serverMode.Url}";
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to start server mode");
                MessageBox.Show($"Failed to start server mode:\n{ex.Message}",
                    "Server Mode Error", MessageBoxButton.OK, MessageBoxImage.Error);
                ServerModeButton.IsEnabled = true;
                ServerModeButton.Content = "Start Server Mode (no WebView2 needed)";
            }
        }

        private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.F12)
            {
                try
                {
                    BlazorWebView.WebView.CoreWebView2.OpenDevToolsWindow();
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to open DevTools");
                }
            }
        }

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Show the closing dialog
            ClosingOverlay.Visibility = Visibility.Visible;
            
            // Cancel the close to allow dialog to show
            e.Cancel = true;
            
            // Wait 0.8 seconds then close
            await Task.Delay(800);
            
            // Actually close the window
            Application.Current.Shutdown();
        }
    }
}
