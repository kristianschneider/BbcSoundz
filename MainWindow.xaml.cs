using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using BbcSoundz.Models;
using BbcSoundz.Services;
using BbcSoundz.Helpers;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;

namespace BbcSoundz
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private BbcScheduleScraper? _scraper;
        private YtDlpService? _ytDlpService;
        private DownloadManager? _downloadManager;
        private ObservableCollection<ShowInfo> _searchResults;
        private bool _isSearching = false;
        private bool _isDownloading = false;
        private CancellationTokenSource? _downloadCancellationTokenSource;

        public MainWindow()
        {
            InitializeComponent();
            _searchResults = new ObservableCollection<ShowInfo>();
            ResultsListBox.ItemsSource = _searchResults;
            _ytDlpService = new YtDlpService();
            _downloadManager = new DownloadManager();

            // Check if yt-dlp is available
            if (!_ytDlpService.IsYtDlpAvailable)
            {
                RichTextBoxHelper.AppendColoredText(OutputRichTextBox, 
                    "WARNING: yt-dlp.exe not found in application directory. Download functionality will not work.");
            }
            else
            {
                RichTextBoxHelper.AppendColoredText(OutputRichTextBox, 
                    "yt-dlp.exe found. Ready for downloads.");
            }

            // Scan for existing downloads at startup
            LoadExistingDownloads();
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isSearching)
            {
                MessageBox.Show("Search is already in progress. Please wait...", "Search In Progress", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var filter = FilterTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(filter))
            {
                MessageBox.Show("Please enter a filter term to search for.", "Filter Required", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                FilterTextBox.Focus();
                return;
            }

            try
            {
                _isSearching = true;
                SearchButton.IsEnabled = false;
                ProgressBar.Visibility = Visibility.Visible;
                ProgressBar.IsIndeterminate = true;
                _searchResults.Clear();
                SelectedShowTextBlock.Text = "No show selected";
                SelectedUrlTextBlock.Text = "";
                DownloadUrlTextBox.Text = "";
                DownloadButton.IsEnabled = false;

                _scraper = new BbcScheduleScraper();

                var progress = new Progress<string>(message =>
                {
                    StatusTextBlock.Text = message;
                });

                StatusTextBlock.Text = "Starting search...";

                var results = await _scraper.ScrapeSchedulesAsync(filter, progress);

                foreach (var show in results)
                {
                    // Check if this show has already been downloaded
                    _downloadManager?.CheckDownloadStatus(show);
                    _searchResults.Add(show);
                }

                StatusTextBlock.Text = $"Search completed. Found {results.Count} shows matching '{filter}'.";
                
                if (results.Count == 0)
                {
                    MessageBox.Show($"No shows found matching '{filter}' in the past 2 months.", 
                        "No Results", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred during the search:\n{ex.Message}", 
                    "Search Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusTextBlock.Text = "Search failed.";
            }
            finally
            {
                _isSearching = false;
                SearchButton.IsEnabled = true;
                ProgressBar.Visibility = Visibility.Collapsed;
                ProgressBar.IsIndeterminate = false;
                _scraper?.Dispose();
                _scraper = null;
            }
        }

        private void LoadExistingDownloads()
        {
            try
            {
                var existingDownloads = _downloadManager?.ScanExistingDownloads();
                if (existingDownloads != null)
                {
                    foreach (var download in existingDownloads)
                    {
                        _searchResults.Add(download);
                    }

                    if (existingDownloads.Any())
                    {
                        var count = existingDownloads.Count();
                        StatusTextBlock.Text = $"Found {count} existing download{(count == 1 ? "" : "s")} in Downloads folder.";
                        RichTextBoxHelper.AppendColoredText(OutputRichTextBox, 
                            $"Loaded {count} existing download{(count == 1 ? "" : "s")} from Downloads folder.");
                    }
                    else
                    {
                        StatusTextBlock.Text = "No existing downloads found.";
                    }
                }
            }
            catch (Exception ex)
            {
                RichTextBoxHelper.AppendColoredText(OutputRichTextBox, 
                    $"Warning: Could not scan Downloads folder: {ex.Message}");
                StatusTextBlock.Text = "Ready";
            }
        }

        private void ResultsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ResultsListBox.SelectedItem is ShowInfo selectedShow)
            {
                SelectedShowTextBlock.Text = selectedShow.Title;
                SelectedUrlTextBlock.Text = selectedShow.Url;
                DownloadUrlTextBox.Text = selectedShow.Url;
                
                if (selectedShow.IsDownloaded)
                {
                    DownloadButton.IsEnabled = false;
                    DownloadStatusTextBlock.Text = "Already downloaded - double-click to play";
                }
                else
                {
                    DownloadButton.IsEnabled = !_isDownloading && _ytDlpService!.IsYtDlpAvailable;
                    DownloadStatusTextBlock.Text = "Ready to download";
                }
            }
            else
            {
                SelectedShowTextBlock.Text = "No show selected";
                SelectedUrlTextBlock.Text = "";
                DownloadUrlTextBox.Text = "";
                DownloadButton.IsEnabled = false;
                DownloadStatusTextBlock.Text = "Ready";
            }
        }

        private void ResultsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ResultsListBox.SelectedItem is ShowInfo selectedShow)
            {
                if (selectedShow.IsDownloaded)
                {
                    // Play the already downloaded file
                    RichTextBoxHelper.AppendColoredText(OutputRichTextBox, $"Playing downloaded file: {selectedShow.Title}");
                    
                    var success = _downloadManager?.PlayDownloadedFile(selectedShow) ?? false;
                    if (success)
                    {
                        RichTextBoxHelper.AppendColoredText(OutputRichTextBox, "Media player launched successfully!");
                    }
                    else
                    {
                        RichTextBoxHelper.AppendColoredText(OutputRichTextBox, "ERROR: Could not launch media player for downloaded file");
                        
                        // File might have been deleted, recheck status
                        _downloadManager?.CheckDownloadStatus(selectedShow);
                    }
                }
                else if (_ytDlpService!.IsYtDlpAvailable && !_isDownloading)
                {
                    // Download the file
                    StartDownload(selectedShow.Url);
                }
            }
        }

        private void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            var url = DownloadUrlTextBox.Text?.Trim();
            if (!string.IsNullOrEmpty(url))
            {
                StartDownload(url);
            }
        }

        private async void StartDownload(string url)
        {
            if (_isDownloading || _ytDlpService == null)
                return;

            try
            {
                _isDownloading = true;
                DownloadButton.IsEnabled = false;
                StopDownloadButton.IsEnabled = true;
                DownloadStatusTextBlock.Text = "Downloading...";
                
                _downloadCancellationTokenSource = new CancellationTokenSource();

                // Create downloads directory
                var downloadsPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Downloads");
                Directory.CreateDirectory(downloadsPath);

                var progress = new Progress<string>(message =>
                {
                    RichTextBoxHelper.AppendColoredText(OutputRichTextBox, message);
                });

                var downloadedFile = await _ytDlpService.DownloadAsync(url, downloadsPath, progress, _downloadCancellationTokenSource.Token);

                if (!string.IsNullOrEmpty(downloadedFile))
                {
                    DownloadStatusTextBlock.Text = "Download completed";
                    RichTextBoxHelper.AppendColoredText(OutputRichTextBox, "");
                    RichTextBoxHelper.AppendColoredText(OutputRichTextBox, $"Files saved to: {downloadsPath}");
                    RichTextBoxHelper.AppendColoredText(OutputRichTextBox, $"Downloaded file: {System.IO.Path.GetFileName(downloadedFile)}");
                    
                    // Update download status for any matching shows in the list
                    var matchingShow = _searchResults.FirstOrDefault(s => s.Url == url);
                    if (matchingShow != null)
                    {
                        _downloadManager?.UpdateDownloadStatus(matchingShow, downloadedFile);
                    }
                    
                    // Launch default program with the downloaded file
                    await LaunchDefaultPlayer(downloadedFile);
                }
                else
                {
                    DownloadStatusTextBlock.Text = "Download failed";
                }
            }
            catch (Exception ex)
            {
                RichTextBoxHelper.AppendColoredText(OutputRichTextBox, $"ERROR: {ex.Message}");
                DownloadStatusTextBlock.Text = "Download error";
            }
            finally
            {
                _isDownloading = false;
                DownloadButton.IsEnabled = ResultsListBox.SelectedItem != null && _ytDlpService.IsYtDlpAvailable;
                StopDownloadButton.IsEnabled = false;
                _downloadCancellationTokenSource?.Dispose();
                _downloadCancellationTokenSource = null;
            }
        }

        private Task LaunchDefaultPlayer(string filePath)
        {
            return Task.Run(() =>
            {
                try
                {
                    if (!File.Exists(filePath))
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                            RichTextBoxHelper.AppendColoredText(OutputRichTextBox, $"WARNING: Downloaded file not found: {filePath}"));
                        return;
                    }

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        RichTextBoxHelper.AppendColoredText(OutputRichTextBox, "");
                        RichTextBoxHelper.AppendColoredText(OutputRichTextBox, "Opening file with default program...");
                    });

                    // Use Windows default file association (like double-clicking)
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true
                    };
                    Process.Start(startInfo);
                    Application.Current.Dispatcher.Invoke(() =>
                        RichTextBoxHelper.AppendColoredText(OutputRichTextBox, "File opened with default program successfully!"));
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        RichTextBoxHelper.AppendColoredText(OutputRichTextBox, $"ERROR: Could not open file: {ex.Message}");
                        RichTextBoxHelper.AppendColoredText(OutputRichTextBox, $"File location: {filePath}");
                    });
                }
            });
        }

        private void StopDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            _downloadCancellationTokenSource?.Cancel();
            _ytDlpService?.StopDownload();
            RichTextBoxHelper.AppendColoredText(OutputRichTextBox, "Stopping download...");
            DownloadStatusTextBlock.Text = "Stopping...";
        }

        private void ClearOutputButton_Click(object sender, RoutedEventArgs e)
        {
            RichTextBoxHelper.ClearText(OutputRichTextBox);
        }

        private void SelectedUrlTextBlock_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!string.IsNullOrEmpty(SelectedUrlTextBlock.Text))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = SelectedUrlTextBlock.Text,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to open URL:\n{ex.Message}", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _scraper?.Dispose();
            _ytDlpService?.Dispose();
            _downloadCancellationTokenSource?.Cancel();
            _downloadCancellationTokenSource?.Dispose();
            base.OnClosed(e);
        }
    }
}
