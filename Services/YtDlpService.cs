using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using TagLibFile = TagLib.File;

namespace BbcSoundz.Services
{
    public class YtDlpService : IDisposable
    {
        private Process? _currentProcess;
        private readonly string _ytDlpPath;
        private readonly HttpClient _httpClient;

        public YtDlpService()
        {
            // Get the path to yt-dlp.exe in the application directory
            var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            _ytDlpPath = Path.Combine(appDirectory, "yt-dlp.exe");
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", 
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        }

        public bool IsYtDlpAvailable => File.Exists(_ytDlpPath);

        public async Task<string?> DownloadAsync(string url, string outputDirectory, string? imageUrl = null, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        {
            if (!IsYtDlpAvailable)
            {
                progress?.Report("ERROR: yt-dlp.exe not found in application directory");
                return null;
            }

            string? downloadedFileName = null;

            try
            {
                // Ensure output directory exists
                Directory.CreateDirectory(outputDirectory);

                // Build yt-dlp command without post-processing to avoid ffmpeg dependency
                var arguments = BuildDownloadCommand(url);
                
                progress?.Report($"Starting download: {url}");
                progress?.Report($"Command: yt-dlp {arguments}");
                progress?.Report("");

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = _ytDlpPath,
                    Arguments = arguments,
                    WorkingDirectory = outputDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                _currentProcess = new Process { StartInfo = processStartInfo };

                // Handle output events
                _currentProcess.OutputDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        var cleanedData = args.Data.Replace("\r", "").Trim();
                        if (!string.IsNullOrEmpty(cleanedData))
                        {
                            progress?.Report(cleanedData);
                            
                            // Try to extract filename from yt-dlp output
                            if (cleanedData.Contains("[download] Destination:"))
                            {
                                var parts = cleanedData.Split(new[] { "[download] Destination:" }, StringSplitOptions.RemoveEmptyEntries);
                                if (parts.Length > 1)
                                {
                                    downloadedFileName = parts[1].Trim();
                                }
                            }
                            else if (cleanedData.Contains("has already been downloaded"))
                            {
                                // Handle case where file already exists
                                var match = System.Text.RegularExpressions.Regex.Match(cleanedData, @"\[download\]\s+(.+?)\s+has already been downloaded");
                                if (match.Success)
                                {
                                    downloadedFileName = match.Groups[1].Value.Trim();
                                }
                            }
                        }
                    }
                };

                _currentProcess.ErrorDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        var cleanedData = args.Data.Replace("\r", "").Trim();
                        if (!string.IsNullOrEmpty(cleanedData))
                        {
                            progress?.Report($"ERROR: {cleanedData}");
                        }
                    }
                };

                _currentProcess.Start();
                _currentProcess.BeginOutputReadLine();
                _currentProcess.BeginErrorReadLine();

                await Task.Run(() =>
                {
                    while (!_currentProcess.HasExited)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            _currentProcess.Kill();
                            progress?.Report("Download cancelled by user");
                            return;
                        }
                        Thread.Sleep(100);
                    }
                }, cancellationToken);

                var exitCode = _currentProcess.ExitCode;
                var success = exitCode == 0;

                if (success)
                {
                    progress?.Report("");
                    progress?.Report("Download completed successfully!");
                    
                    // If we couldn't extract filename from output, try to find the most recent file
                    if (string.IsNullOrEmpty(downloadedFileName))
                    {
                        try
                        {
                            var files = Directory.GetFiles(outputDirectory)
                                .Where(f => !f.EndsWith(".part") && !f.EndsWith(".json"))
                                .OrderByDescending(f => File.GetCreationTime(f))
                                .ToArray();
                            
                            if (files.Length > 0)
                            {
                                downloadedFileName = files[0];
                            }
                        }
                        catch (Exception)
                        {
                            // Ignore file search errors
                        }
                    }
                    else if (!Path.IsPathRooted(downloadedFileName))
                    {
                        // Make path absolute if it's relative
                        downloadedFileName = Path.Combine(outputDirectory, downloadedFileName);
                    }
                }
                else
                {
                    progress?.Report("");
                    progress?.Report($"Download failed with exit code: {exitCode}");
                }

                return success ? downloadedFileName : null;
            }
            catch (Exception ex)
            {
                progress?.Report($"ERROR: {ex.Message}");
                return null;
            }
            finally
            {
                _currentProcess?.Dispose();
                _currentProcess = null;
            }
        }

        public void StopDownload()
        {
            if (_currentProcess != null && !_currentProcess.HasExited)
            {
                try
                {
                    _currentProcess.Kill();
                }
                catch (Exception)
                {
                    // Process might have already exited
                }
            }
        }

        private string BuildDownloadCommand(string url)
        {
            var args = new List<string>
            {
                "--format", "bestaudio",
                "--output", "\"%(title)s.%(ext)s\"",
                "--user-agent", "\"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36\"",
                "--newline"
            };

            args.Add($"\"{url}\"");
            
            return string.Join(" ", args);
        }

        public bool IsDownloading => _currentProcess != null && !_currentProcess.HasExited;

        public void Dispose()
        {
            StopDownload();
            _currentProcess?.Dispose();
            _httpClient?.Dispose();
        }
    }
}
