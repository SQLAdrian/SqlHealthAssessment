/* In the name of God, the Merciful, the Compassionate */

using System.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using SqlHealthAssessment.Data;
using System.Windows.Threading;

namespace SqlHealthAssessment
{
    public partial class MainWindow : Window
    {
        private readonly ILogger<MainWindow>? _logger;
        private bool _webView2Initialized = false;

        public MainWindow()
        {
            InitializeComponent();
            _logger = App.Services?.GetService<ILogger<MainWindow>>();

            var version = App.Services?.GetService<AutoUpdateService>()?.GetCurrentVersion() ?? "0.78.6";
            Title = $"SQL Health Assessment v{version}";
            
            // Add keyboard shortcut for DevTools (F12)
            KeyDown += OnKeyDown;
            
            // Add WebView error handling
            BlazorWebView.BlazorWebViewInitialized += OnBlazorWebViewInitialized;
            Loaded += OnWindowLoaded;
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

                // Wire up PrintService with the live CoreWebView2 instance
                var printService = App.Services?.GetService<Data.Services.PrintService>();
                printService?.SetWebView(e.WebView.CoreWebView2);

                // Add JavaScript error handling
                e.WebView.NavigationCompleted += (s, args) =>
                {
                    if (args.IsSuccess)
                    {
                        _logger?.LogDebug("WebView navigation completed successfully");
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
