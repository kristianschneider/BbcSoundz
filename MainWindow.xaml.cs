using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using BbcSoundz.Helpers;
using BbcSoundz.Models;
using BbcSoundz.Services;

namespace BbcSoundz
{
    public partial class MainWindow : Window
    {
        private YtDlpService? _ytDlpService;
        private DownloadManager? _downloadManager;
        private BbcProgrammeContentService? _programmeContentService;
        private GenreShowService? _genreShowService;
        private ShowProgrammeService? _programmeListingService;
        private ObservableCollection<ShowInfo> _availableShows = null!;
        private ObservableCollection<ShowInfo> _availableProgrammes = null!;
        private bool _isLoadingShows;
        private bool _isLoadingProgrammes;
        private bool _isDownloading;
        private bool _isLoadingPreview;
        private bool _suppressShowSelection;
        private CancellationTokenSource? _downloadCancellationTokenSource;

        public MainWindow()
        {
            try
            {
                InitializeComponent();
                InitializeCollections();
                InitializeServices();
                ReportYtDlpStatus();
                Loaded += MainWindow_Loaded;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing MainWindow: {ex.Message}\n\nStack trace:\n{ex.StackTrace}",
                    "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        private void InitializeCollections()
        {
            _availableShows = new ObservableCollection<ShowInfo>();
            _availableProgrammes = new ObservableCollection<ShowInfo>();
            ShowComboBox.ItemsSource = _availableShows;
            ProgrammesTreeView.ItemsSource = _availableProgrammes;
        }

        private void InitializeServices()
        {
            _ytDlpService = new YtDlpService();
            _downloadManager = new DownloadManager();
            _programmeContentService = new BbcProgrammeContentService();
            _genreShowService = new GenreShowService();
            _programmeListingService = new ShowProgrammeService();
        }

        private void ReportYtDlpStatus()
        {
            if (_ytDlpService?.IsYtDlpAvailable != true)
            {
                RichTextBoxHelper.AppendColoredText(OutputRichTextBox,
                    "WARNING: yt-dlp.exe not found in application directory. Download functionality will not work.");
            }
            else
            {
                RichTextBoxHelper.AppendColoredText(OutputRichTextBox,
                    "yt-dlp.exe found. Ready for downloads.");
            }
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= MainWindow_Loaded;
            await LoadShowsAsync();
        }

        private async void RefreshShowsButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadShowsAsync();
        }

        private async Task LoadShowsAsync()
        {
            if (_isLoadingShows || _genreShowService is null)
                return;

            try
            {
                _isLoadingShows = true;
                ProgressBar.Visibility = Visibility.Visible;
                ProgressBar.IsIndeterminate = true;
                StatusTextBlock.Text = "Loading Dance & Electronica shows...";

                _availableShows.Clear();
                _availableProgrammes.Clear();
                ClearProgrammeSelection();

                var shows = await _genreShowService.GetShowsAsync();
                var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var show in shows)
                {
                    var key = NormalizeKey(show.Url, show.Title);
                    if (string.IsNullOrEmpty(key) || !seenUrls.Add(key))
                    {
                        continue;
                    }

                    show.Url = key;
                    _availableShows.Add(show);
                }

                StatusTextBlock.Text = _availableShows.Count == 0
                    ? "No Dance & Electronica shows found."
                    : "Select a show in the dropdown to load its programmes.";

                _suppressShowSelection = true;
                ShowComboBox.SelectedIndex = -1;
                _suppressShowSelection = false;
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "Failed to load shows.";
                RichTextBoxHelper.AppendColoredText(OutputRichTextBox, $"ERROR: Could not load show list: {ex.Message}");
            }
            finally
            {
                _isLoadingShows = false;
                ProgressBar.Visibility = Visibility.Collapsed;
                ProgressBar.IsIndeterminate = false;
            }
        }

        private static string NormalizeKey(string? url, string? title = null)
        {
            var normalizedUrl = ShowUrlNormalizer.Normalize(url);
            if (!string.IsNullOrEmpty(normalizedUrl))
            {
                return normalizedUrl;
            }

            return (title ?? string.Empty).Trim();
        }

        private async Task LoadProgrammesForShowAsync(ShowInfo show)
        {
            if (_programmeListingService is null || _isLoadingProgrammes)
                return;

            try
            {
                _isLoadingProgrammes = true;
                _availableProgrammes.Clear();
                ClearProgrammeSelection();

                if (string.IsNullOrEmpty(show?.Url))
                {
                    StatusTextBlock.Text = "Show URL missing. Cannot load programmes.";
                    return;
                }

                StatusTextBlock.Text = $"Loading programmes for {show.Title}...";
                var programmes = await _programmeListingService.GetProgrammesAsync($"{show.Url}/episodes/player");

                foreach (var programme in programmes)
                {
                    _downloadManager?.CheckDownloadStatus(programme);
                    _availableProgrammes.Add(programme);
                }

                StatusTextBlock.Text = programmes.Count == 0
                    ? $"No programmes found for {show.Title}."
                    : $"Loaded {programmes.Count} programme{(programmes.Count == 1 ? string.Empty : "s")} for {show.Title}.";
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Failed to load programmes for {show.Title}.";
                RichTextBoxHelper.AppendColoredText(OutputRichTextBox, $"ERROR: Could not load programmes: {ex.Message}");
            }
            finally
            {
                _isLoadingProgrammes = false;
            }
        }

        private void ClearProgrammeSelection()
        {
            SelectedShowTextBlock.Text = "No programme selected";
            SelectedUrlTextBlock.Text = string.Empty;
            DownloadUrlTextBox.Text = string.Empty;
            DownloadButton.IsEnabled = false;
            DownloadStatusTextBlock.Text = "Ready";
            ClearPreview();
        }

        private async void ShowComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressShowSelection)
                return;

            if (ShowComboBox.SelectedItem is ShowInfo selectedShow)
            {
                await LoadProgrammesForShowAsync(selectedShow);
            }
            else
            {
                _availableProgrammes.Clear();
                ClearProgrammeSelection();
            }
        }

        private async void ProgrammesTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (ProgrammesTreeView.SelectedItem is ShowInfo programme)
            {
                SelectedShowTextBlock.Text = programme.DisplayName ?? programme.Title;
                SelectedUrlTextBlock.Text = programme.Url;
                DownloadUrlTextBox.Text = programme.Url;

                if (programme.IsDownloaded)
                {
                    DownloadButton.IsEnabled = false;
                    DownloadStatusTextBlock.Text = "Already downloaded - double-click to play";
                }
                else if (string.IsNullOrEmpty(programme.Url))
                {
                    DownloadButton.IsEnabled = false;
                    DownloadStatusTextBlock.Text = "No URL available";
                }
                else
                {
                    DownloadButton.IsEnabled = !_isDownloading && _ytDlpService?.IsYtDlpAvailable == true;
                    DownloadStatusTextBlock.Text = "Ready to download";
                }

                if (!string.IsNullOrEmpty(programme.Url))
                {
                    await LoadProgrammePreview(programme.Url);
                }
                else
                {
                    ClearPreview();
                }
            }
            else
            {
                ClearProgrammeSelection();
            }
        }

        private void ProgrammesTreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ProgrammesTreeView.SelectedItem is not ShowInfo programme)
                return;

            if (programme.IsDownloaded)
            {
                RichTextBoxHelper.AppendColoredText(OutputRichTextBox, $"Playing downloaded file: {programme.Title}");
                var success = _downloadManager?.PlayDownloadedFile(programme) ?? false;
                if (success)
                {
                    RichTextBoxHelper.AppendColoredText(OutputRichTextBox, "Media player launched successfully!");
                }
                else
                {
                    RichTextBoxHelper.AppendColoredText(OutputRichTextBox,
                        "ERROR: Could not launch media player for downloaded file");
                    _downloadManager?.CheckDownloadStatus(programme);
                }
            }
            else if (_ytDlpService?.IsYtDlpAvailable == true && !_isDownloading)
            {
                StartDownload(programme);
            }
        }

        private void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (ProgrammesTreeView.SelectedItem is ShowInfo programme)
            {
                StartDownload(programme);
            }
        }

        private async void StartDownload(ShowInfo programme)
        {
            if (programme == null || string.IsNullOrEmpty(programme.Url))
                return;

            await StartDownloadInternal(programme.Url, programme.ImageUrl);
        }

        private async Task StartDownloadInternal(string url, string? imageUrl)
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

                var downloadsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Downloads");
                Directory.CreateDirectory(downloadsPath);

                var progress = new Progress<string>(message =>
                {
                    RichTextBoxHelper.AppendColoredText(OutputRichTextBox, message);
                });

                var downloadedFile = await _ytDlpService.DownloadAsync(url, downloadsPath, imageUrl, progress,
                    _downloadCancellationTokenSource.Token);

                if (!string.IsNullOrEmpty(downloadedFile))
                {
                    DownloadStatusTextBlock.Text = "Download completed";
                    RichTextBoxHelper.AppendColoredText(OutputRichTextBox, string.Empty);
                    RichTextBoxHelper.AppendColoredText(OutputRichTextBox, $"Files saved to: {downloadsPath}");
                    RichTextBoxHelper.AppendColoredText(OutputRichTextBox,
                        $"Downloaded file: {Path.GetFileName(downloadedFile)}");

                    var matchingProgramme = _availableProgrammes.FirstOrDefault(s => s.Url == url);
                    if (matchingProgramme != null)
                    {
                        _downloadManager?.UpdateDownloadStatus(matchingProgramme, downloadedFile);
                    }

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
                DownloadButton.IsEnabled = ProgrammesTreeView.SelectedItem is ShowInfo item
                    && _ytDlpService?.IsYtDlpAvailable == true
                    && !item.IsDownloaded
                    && !string.IsNullOrEmpty(item.Url);
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
                            RichTextBoxHelper.AppendColoredText(OutputRichTextBox,
                                $"WARNING: Downloaded file not found: {filePath}"));
                        return;
                    }

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        RichTextBoxHelper.AppendColoredText(OutputRichTextBox, string.Empty);
                        RichTextBoxHelper.AppendColoredText(OutputRichTextBox,
                            "Opening file with default program...");
                    });

                    var startInfo = new ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true
                    };
                    Process.Start(startInfo);
                    Application.Current.Dispatcher.Invoke(() =>
                        RichTextBoxHelper.AppendColoredText(OutputRichTextBox,
                            "File opened with default program successfully!"));
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        RichTextBoxHelper.AppendColoredText(OutputRichTextBox,
                            $"ERROR: Could not open file: {ex.Message}");
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
            if (string.IsNullOrEmpty(SelectedUrlTextBlock.Text))
                return;

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

        protected override void OnClosed(EventArgs e)
        {
            _ytDlpService?.Dispose();
            _programmeContentService?.Dispose();
            _downloadCancellationTokenSource?.Cancel();
            _downloadCancellationTokenSource?.Dispose();
            base.OnClosed(e);
        }

        private async Task LoadProgrammePreview(string programmeUrl)
        {
            if (_isLoadingPreview || string.IsNullOrEmpty(programmeUrl) || _programmeContentService is null)
                return;

            try
            {
                _isLoadingPreview = true;
                ShowPreviewLoading();

                var content = await _programmeContentService.GetProgrammeContentAsync(programmeUrl);

                if (content != null)
                {
                    await DisplayProgrammeContent(content);
                }
                else
                {
                    ShowPreviewError("Failed to load programme content");
                }
            }
            catch (Exception ex)
            {
                ShowPreviewError($"Error loading preview: {ex.Message}");
            }
            finally
            {
                _isLoadingPreview = false;
                HidePreviewLoading();
            }
        }

        private void ClearPreview()
        {
            PreviewMessageTextBlock.Visibility = Visibility.Visible;
            PreviewMessageTextBlock.Text = "Select a programme to see preview";

            PreviewImageBorder.Visibility = Visibility.Collapsed;
            PreviewTitleTextBlock.Visibility = Visibility.Collapsed;
            PreviewSubtitleTextBlock.Visibility = Visibility.Collapsed;
            PreviewDescriptionTextBlock.Visibility = Visibility.Collapsed;
            PreviewDetailsPanel.Visibility = Visibility.Collapsed;
            PreviewErrorTextBlock.Visibility = Visibility.Collapsed;
            PreviewLoadingPanel.Visibility = Visibility.Collapsed;
        }

        private void ShowPreviewLoading()
        {
            PreviewMessageTextBlock.Visibility = Visibility.Collapsed;
            PreviewLoadingPanel.Visibility = Visibility.Visible;
            PreviewErrorTextBlock.Visibility = Visibility.Collapsed;
        }

        private void HidePreviewLoading()
        {
            PreviewLoadingPanel.Visibility = Visibility.Collapsed;
        }

        private void ShowPreviewError(string message)
        {
            PreviewMessageTextBlock.Visibility = Visibility.Collapsed;
            PreviewErrorTextBlock.Text = message;
            PreviewErrorTextBlock.Visibility = Visibility.Visible;

            PreviewImageBorder.Visibility = Visibility.Collapsed;
            PreviewTitleTextBlock.Visibility = Visibility.Collapsed;
            PreviewSubtitleTextBlock.Visibility = Visibility.Collapsed;
            PreviewDescriptionTextBlock.Visibility = Visibility.Collapsed;
            PreviewDetailsPanel.Visibility = Visibility.Collapsed;
        }

        private async Task DisplayProgrammeContent(ProgrammeContent content)
        {
            if (content.HasError)
            {
                ShowPreviewError(content.ErrorMessage);
                return;
            }

            PreviewMessageTextBlock.Visibility = Visibility.Collapsed;
            PreviewErrorTextBlock.Visibility = Visibility.Collapsed;

            if (!string.IsNullOrEmpty(content.Title))
            {
                PreviewTitleTextBlock.Text = content.Title;
                PreviewTitleTextBlock.Visibility = Visibility.Visible;
            }

            if (content.HasSubtitle)
            {
                PreviewSubtitleTextBlock.Text = content.Subtitle;
                PreviewSubtitleTextBlock.Visibility = Visibility.Visible;
            }

            if (content.HasDescription)
            {
                PreviewDescriptionTextBlock.Text = content.Description;
                PreviewDescriptionTextBlock.Visibility = Visibility.Visible;
            }

            var hasDetails = false;
            if (content.HasBrand)
            {
                PreviewBrandTextBlock.Text = $"Programme: {content.Brand}";
                hasDetails = true;
            }

            if (content.HasDuration)
            {
                PreviewDurationTextBlock.Text = $"Duration: {content.Duration}";
                hasDetails = true;
            }

            if (content.HasBroadcastDate)
            {
                PreviewDateTextBlock.Text = $"Broadcast: {content.BroadcastDate}";
                hasDetails = true;
            }

            if (hasDetails)
            {
                PreviewDetailsPanel.Visibility = Visibility.Visible;
            }

            if (content.HasImage)
            {
                await LoadPreviewImage(content.ImageUrl);
            }
        }

        private async Task LoadPreviewImage(string imageUrl)
        {
            try
            {
                using var httpClient = new HttpClient();
                var imageBytes = await httpClient.GetByteArrayAsync(imageUrl);

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = new MemoryStream(imageBytes);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                PreviewImage.Source = bitmap;
                PreviewImageBorder.Visibility = Visibility.Visible;
            }
            catch
            {
                PreviewImageBorder.Visibility = Visibility.Collapsed;
            }
        }
    }
}

