using System;
using System.Configuration;
using System.Data;
using System.Text;
using System.Windows;

namespace BbcSoundz
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                // Handle unhandled exceptions
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
                DispatcherUnhandledException += App_DispatcherUnhandledException;
                
                // Ensure UTF-8 encoding is used throughout the application
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                base.OnStartup(e);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Startup error: {ex.Message}\n\nStack trace:\n{ex.StackTrace}", 
                    "Application Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show($"Unhandled UI exception: {e.Exception.Message}\n\nStack trace:\n{e.Exception.StackTrace}", 
                "Unhandled Exception", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            MessageBox.Show($"Unhandled domain exception: {ex?.Message}\n\nStack trace:\n{ex?.StackTrace}", 
                "Unhandled Exception", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
