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
            // Ensure UTF-8 encoding is used throughout the application
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            base.OnStartup(e);
        }
    }
}
