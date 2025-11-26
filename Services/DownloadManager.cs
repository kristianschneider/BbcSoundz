using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BbcSoundz.Models;

namespace BbcSoundz.Services
{
    public class DownloadManager
    {
        private readonly string _downloadsDirectory;

        public DownloadManager()
        {
            _downloadsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Downloads");
            Directory.CreateDirectory(_downloadsDirectory);
        }

        public string DownloadsDirectory => _downloadsDirectory;

        public void CheckDownloadStatus(ShowInfo showInfo)
        {
            try
            {
                // Try to find downloaded file for this show
                var possibleFiles = GetPossibleFilenames(showInfo);
                
                foreach (var filename in possibleFiles)
                {
                    var fullPath = Path.Combine(_downloadsDirectory, filename);
                    if (File.Exists(fullPath))
                    {
                        showInfo.IsDownloaded = true;
                        showInfo.DownloadedFilePath = fullPath;
                        return;
                    }
                }

                // Disable broad matching for now - only use exact filename patterns
                // This prevents false positives where any file might match a short title
                
                showInfo.IsDownloaded = false;
                showInfo.DownloadedFilePath = null;
            }
            catch (Exception)
            {
                // If there's any error, assume not downloaded
                showInfo.IsDownloaded = false;
                showInfo.DownloadedFilePath = null;
            }
        }

        private IEnumerable<string> GetPossibleFilenames(ShowInfo showInfo)
        {
            var sanitizedTitle = SanitizeFilename(showInfo.Title);
            var extensions = new[] { ".mp4", ".m4a", ".mp3", ".webm", ".ogg", ".wav" };

            // Generate possible filenames that yt-dlp might create
            foreach (var ext in extensions)
            {
                yield return $"{sanitizedTitle}{ext}";
                yield return $"{sanitizedTitle.Replace(" ", "_")}{ext}";
                yield return $"{sanitizedTitle.Replace(" ", "-")}{ext}";
                
                // Include date if available
                if (showInfo.Date != default)
                {
                    yield return $"{showInfo.Date:yyyy-MM-dd} - {sanitizedTitle}{ext}";
                    yield return $"{sanitizedTitle} - {showInfo.Date:yyyy-MM-dd}{ext}";
                }

                // BBC specific patterns
                yield return $"BBC - {sanitizedTitle}{ext}";
                yield return $"{sanitizedTitle} - BBC{ext}";
            }
        }

        private string SanitizeFilename(string filename)
        {
            if (string.IsNullOrEmpty(filename))
                return string.Empty;

            // Remove invalid characters for filenames
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string(filename.Where(c => !invalidChars.Contains(c)).ToArray());
            
            // Replace common problematic characters
            sanitized = sanitized.Replace(":", " -");
            sanitized = sanitized.Replace("?", "");
            sanitized = sanitized.Replace("*", "");
            sanitized = sanitized.Replace("\"", "'");
            
            // Remove extra spaces
            sanitized = Regex.Replace(sanitized, @"\s+", " ").Trim();
            
            return sanitized;
        }

        public void UpdateDownloadStatus(ShowInfo showInfo, string downloadedFilePath)
        {
            showInfo.IsDownloaded = true;
            showInfo.DownloadedFilePath = downloadedFilePath;
        }

        public IEnumerable<ShowInfo> ScanExistingDownloads()
        {
            var existingDownloads = new List<ShowInfo>();
            
            try
            {
                if (!Directory.Exists(_downloadsDirectory))
                    return existingDownloads;

                var mediaFiles = Directory.GetFiles(_downloadsDirectory)
                    .Where(f => !f.EndsWith(".part", StringComparison.OrdinalIgnoreCase) && 
                               !f.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && 
                               !f.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                    .Where(f => IsMediaFile(f))
                    .ToArray();

                foreach (var filePath in mediaFiles)
                {
                    var fileName = Path.GetFileNameWithoutExtension(filePath);
                    var cleanedTitle = CleanupFileNameForDisplay(fileName);
                    var showInfo = new ShowInfo
                    {
                        Title = cleanedTitle,
                        DisplayName = cleanedTitle, // This was missing!
                        Url = "", // No URL for existing files
                        Date = TryExtractDateFromFilename(fileName),
                        IsDownloaded = true,
                        DownloadedFilePath = filePath
                    };
                    
                    existingDownloads.Add(showInfo);
                }
            }
            catch (Exception)
            {
                // If there's any error scanning, return empty list
            }

            return existingDownloads;
        }

        private bool IsMediaFile(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            var mediaExtensions = new[] { ".mp3", ".m4a", ".mp4", ".webm", ".ogg", ".wav", ".flac", ".aac" };
            return mediaExtensions.Contains(extension);
        }

        private string CleanupFileNameForDisplay(string fileName)
        {
            // Remove common yt-dlp patterns and clean up for display
            var cleaned = fileName;
            
            // Remove BBC prefix if present
            if (cleaned.StartsWith("BBC - ", StringComparison.OrdinalIgnoreCase))
                cleaned = cleaned.Substring(6);
            
            // Remove trailing BBC suffix if present
            if (cleaned.EndsWith(" - BBC", StringComparison.OrdinalIgnoreCase))
                cleaned = cleaned.Substring(0, cleaned.Length - 6);
            
            // Replace underscores and hyphens with spaces for better readability
            cleaned = cleaned.Replace("_", " ").Replace("-", " ");
            
            // Remove multiple spaces
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+", " ").Trim();
            
            return cleaned;
        }

        private DateTime TryExtractDateFromFilename(string fileName)
        {
            // Try to extract date patterns like "2025-01-15" or "15-01-2025"
            var datePatterns = new[]
            {
                @"(\d{4}-\d{2}-\d{2})",  // YYYY-MM-DD
                @"(\d{2}-\d{2}-\d{4})",  // DD-MM-YYYY
                @"(\d{4}\d{2}\d{2})",    // YYYYMMDD
            };

            foreach (var pattern in datePatterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(fileName, pattern);
                if (match.Success)
                {
                    var dateString = match.Groups[1].Value;
                    if (DateTime.TryParseExact(dateString, new[] { "yyyy-MM-dd", "dd-MM-yyyy", "yyyyMMdd" }, 
                        null, System.Globalization.DateTimeStyles.None, out DateTime parsedDate))
                    {
                        return parsedDate;
                    }
                }
            }

            // If no date found, try to get file creation date
            try
            {
                return File.GetCreationTime(Path.Combine(_downloadsDirectory, fileName));
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        public bool PlayDownloadedFile(ShowInfo showInfo)
        {
            if (!showInfo.IsDownloaded || string.IsNullOrEmpty(showInfo.DownloadedFilePath))
                return false;

            if (!File.Exists(showInfo.DownloadedFilePath))
            {
                // File was deleted, update status
                showInfo.IsDownloaded = false;
                showInfo.DownloadedFilePath = null;
                return false;
            }

            try
            {
                // Use Windows default file association (like double-clicking)
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = showInfo.DownloadedFilePath,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(startInfo);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
