/* In the name of God, the Merciful, the Compassionate */

using System.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace SqlHealthAssessment
{
    public partial class MainWindow : Window
    {
        private readonly ILogger<MainWindow>? _logger;

        public MainWindow()
        {
            InitializeComponent();
            _logger = App.Services?.GetService<ILogger<MainWindow>>();
            
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
    }
}
