using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using BbcSoundz.Models;
using BbcSoundz.Configuration;
using HtmlAgilityPack;
using Microsoft.Extensions.Configuration;

namespace BbcSoundz.Services
{
    public class BbcScheduleScraper
    {
        private readonly HttpClient _httpClient;
        private readonly List<BbcScheduleSource> _sources;

        public BbcScheduleScraper()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", 
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept-Charset", "UTF-8");
            
            // Load configuration
            _sources = LoadConfiguration();
        }

        public async Task<List<ShowInfo>> ScrapeSchedulesAsync(string filter, IProgress<string>? progress = null)
        {
            var allShows = new List<ShowInfo>();
            var startDate = DateTime.Now;
            var endDate = startDate.AddMonths(-2); // 2 months back

            progress?.Report("Starting scrape...");

            // Generate all week URLs for all sources for the past 2 months
            var allWeekUrls = new List<string>();
            foreach (var source in _sources)
            {
                var weekUrls = GenerateWeekUrls(source.Url, endDate, startDate);
                allWeekUrls.AddRange(weekUrls);
            }
            
            int totalWeeks = allWeekUrls.Count;
            int processedWeeks = 0;

            progress?.Report($"Processing {totalWeeks} weeks in parallel...");

            // Use concurrent collection for thread-safe operations
            var concurrentShows = new System.Collections.Concurrent.ConcurrentBag<ShowInfo>();
            var lockObject = new object();

            try
            {
                // Process weeks in parallel with limited concurrency to be respectful
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 4) // Limit to 4 concurrent requests
                };

                await Task.Run(() =>
                {
                    Parallel.ForEach(allWeekUrls, parallelOptions, weekUrl =>
                    {
                        try
                        {
                            var weekShows = ScrapeWeekSync(weekUrl, filter);
                            
                            foreach (var show in weekShows)
                            {
                                concurrentShows.Add(show);
                            }
                            
                            // Thread-safe progress reporting
                            lock (lockObject)
                            {
                                processedWeeks++;
                                progress?.Report($"Processed {processedWeeks} of {totalWeeks} weeks...");
                            }
                        }
                        catch (Exception ex)
                        {
                            lock (lockObject)
                            {
                                progress?.Report($"Error processing {weekUrl}: {ex.Message}");
                            }
                        }
                    });
                });

                // Convert concurrent collection to list
                allShows.AddRange(concurrentShows);
            }
            catch (Exception ex)
            {
                progress?.Report($"Parallel processing error: {ex.Message}");
                throw;
            }

            progress?.Report($"Scraping complete. Found {allShows.Count} matching shows.");
            
            // Sort by date descending (newest first)
            return allShows.OrderByDescending(s => s.Date).ToList();
        }

        private async Task<List<ShowInfo>> ScrapeWeekAsync(string weekUrl, string filter)
        {
            var shows = new List<ShowInfo>();

            try
            {
                // Get the response as bytes first to handle encoding properly
                var response = await _httpClient.GetAsync(weekUrl);
                response.EnsureSuccessStatusCode();
                
                var bytes = await response.Content.ReadAsByteArrayAsync();
                var html = System.Text.Encoding.UTF8.GetString(bytes);
                
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // Look for programme links and titles
                // BBC schedule pages typically have programme listings in specific containers
                var programmeNodes = doc.DocumentNode
                    .SelectNodes("//a[contains(@href, '/programmes/') or contains(@href, '/sounds/')]")
                    ?.Where(node => 
                    {
                        var text = System.Net.WebUtility.HtmlDecode(node.InnerText?.Trim() ?? "");
                        var href = node.GetAttributeValue("href", "");
                        return !string.IsNullOrEmpty(text) && 
                               !string.IsNullOrEmpty(href) &&
                               text.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
                    });

                if (programmeNodes != null)
                {
                    foreach (var node in programmeNodes)
                    {
                        var title = System.Net.WebUtility.HtmlDecode(node.InnerText?.Trim() ?? "");
                        var href = node.GetAttributeValue("href", "");
                        
                        if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(href))
                        {
                            // Make sure URL is absolute
                            if (href.StartsWith("/"))
                            {
                                href = "https://www.bbc.co.uk" + href;
                            }

                            // Try to extract date from the week URL
                            var weekDate = ExtractDateFromWeekUrl(weekUrl);

                            shows.Add(new ShowInfo
                            {
                                Title = title,
                                DisplayName = $"{title} ({weekDate:MMM dd, yyyy})",
                                Url = href,
                                Date = weekDate,
                                Description = $"Found in week of {weekDate:MMMM dd, yyyy}"
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to scrape {weekUrl}: {ex.Message}", ex);
            }

            return shows;
        }

        private List<ShowInfo> ScrapeWeekSync(string weekUrl, string filter)
        {
            var shows = new List<ShowInfo>();

            try
            {
                // Get the response as bytes first to handle encoding properly
                var response = _httpClient.GetAsync(weekUrl).Result;
                response.EnsureSuccessStatusCode();
                
                var bytes = response.Content.ReadAsByteArrayAsync().Result;
                var html = System.Text.Encoding.UTF8.GetString(bytes);
                
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // Look for programme links and titles
                // BBC schedule pages typically have programme listings in specific containers
                var programmeNodes = doc.DocumentNode
                    .SelectNodes("//a[contains(@href, '/programmes/') or contains(@href, '/sounds/')]")
                    ?.Where(node => 
                    {
                        var text = System.Net.WebUtility.HtmlDecode(node.InnerText?.Trim() ?? "");
                        var href = node.GetAttributeValue("href", "");
                        return !string.IsNullOrEmpty(text) && 
                               !string.IsNullOrEmpty(href) &&
                               text.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
                    });

                if (programmeNodes != null)
                {
                    foreach (var node in programmeNodes)
                    {
                        var title = System.Net.WebUtility.HtmlDecode(node.InnerText?.Trim() ?? "");
                        var href = node.GetAttributeValue("href", "");
                        
                        if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(href))
                        {
                            // Make sure URL is absolute
                            if (href.StartsWith("/"))
                            {
                                href = "https://www.bbc.co.uk" + href;
                            }

                            // Try to extract date from the week URL
                            var weekDate = ExtractDateFromWeekUrl(weekUrl);

                            var show = new ShowInfo
                            {
                                Title = title,
                                DisplayName = $"{title} ({weekDate:MMM dd, yyyy})",
                                Url = href,
                                Date = weekDate,
                                Description = $"Found in week of {weekDate:MMMM dd, yyyy}"
                            };

                            // Try to extract image URL (async, but we'll do it synchronously for now)
                            try
                            {
                                show.ImageUrl = ExtractImageUrlFromProgrammePage(href).Result;
                            }
                            catch
                            {
                                // Continue without image if extraction fails
                            }

                            shows.Add(show);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to scrape {weekUrl}: {ex.Message}", ex);
            }

            return shows;
        }

        private List<BbcScheduleSource> LoadConfiguration()
        {
            try
            {
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .Build();

                var appSettings = new AppSettings();
                configuration.Bind(appSettings);
                
                return appSettings.BbcScheduleSources;
            }
            catch (Exception)
            {
                // Fallback to default sources if configuration fails
                return new List<BbcScheduleSource>
                {
                    new BbcScheduleSource 
                    { 
                        Name = "BBC Radio 1", 
                        Url = "https://www.bbc.co.uk/schedules/p00fzl86",
                        Description = "BBC Radio 1 - Default fallback"
                    }
                };
            }
        }

        private List<string> GenerateWeekUrls(string baseUrl, DateTime startDate, DateTime endDate)
        {
            var urls = new List<string>();
            
            var current = startDate;
            while (current <= endDate)
            {
                // Get the ISO week number
                var calendar = CultureInfo.InvariantCulture.Calendar;
                var year = current.Year;
                var week = calendar.GetWeekOfYear(current, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
                
                // Handle year transition for ISO weeks
                if (week >= 52 && current.Month == 1)
                {
                    year = current.Year - 1;
                }
                else if (week == 1 && current.Month == 12)
                {
                    year = current.Year + 1;
                }

                var url = $"{baseUrl}/{year}/w{week:D2}";
                if (!urls.Contains(url))
                {
                    urls.Add(url);
                }
                
                current = current.AddDays(7);
            }

            return urls;
        }

        private DateTime ExtractDateFromWeekUrl(string weekUrl)
        {
            try
            {
                // Extract year and week from URL like .../2025/w30
                var parts = weekUrl.Split('/');
                var yearPart = parts[^2]; // Second to last part
                var weekPart = parts[^1]; // Last part

                if (int.TryParse(yearPart, out int year) && weekPart.StartsWith("w"))
                {
                    var weekStr = weekPart.Substring(1);
                    if (int.TryParse(weekStr, out int week))
                    {
                        // Calculate the date of the first day of that week
                        var jan1 = new DateTime(year, 1, 1);
                        var daysOffset = (int)CultureInfo.InvariantCulture.DateTimeFormat.FirstDayOfWeek - (int)jan1.DayOfWeek;
                        var firstWeek = jan1.AddDays(daysOffset);
                        var weekDate = firstWeek.AddDays((week - 1) * 7);
                        return weekDate;
                    }
                }
            }
            catch
            {
                // Fallback to current date if parsing fails
            }

            return DateTime.Now;
        }

        public static async Task<string?> ExtractImageUrlFromProgrammePage(string programmeUrl)
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", 
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

                var html = await httpClient.GetStringAsync(programmeUrl);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // Look for the playout image in the episode-playout section
                var playoutImage = doc.DocumentNode
                    .SelectSingleNode("//div[contains(@class, 'episode-playout')]//img[contains(@class, 'image')]");

                if (playoutImage != null)
                {
                    // Get the highest resolution from srcset
                    var srcSet = playoutImage.GetAttributeValue("srcset", "");
                    if (!string.IsNullOrEmpty(srcSet))
                    {
                        var bestImageUrl = ParseBestImageFromSrcSet(srcSet);
                        if (!string.IsNullOrEmpty(bestImageUrl))
                        {
                            return bestImageUrl.StartsWith("http") ? bestImageUrl : $"https:{bestImageUrl}";
                        }
                    }

                    // Fallback to src attribute
                    var src = playoutImage.GetAttributeValue("src", "");
                    if (!string.IsNullOrEmpty(src))
                    {
                        return src.StartsWith("http") ? src : $"https:{src}";
                    }
                }

                // Alternative: Look for programme images in other sections
                var alternativeImage = doc.DocumentNode
                    .SelectSingleNode("//div[contains(@class, 'programme-image')]//img | //img[contains(@alt, 'programme') or contains(@alt, 'episode')]");

                if (alternativeImage != null)
                {
                    var srcSet = alternativeImage.GetAttributeValue("srcset", "");
                    if (!string.IsNullOrEmpty(srcSet))
                    {
                        var bestImageUrl = ParseBestImageFromSrcSet(srcSet);
                        if (!string.IsNullOrEmpty(bestImageUrl))
                        {
                            return bestImageUrl.StartsWith("http") ? bestImageUrl : $"https:{bestImageUrl}";
                        }
                    }

                    var src = alternativeImage.GetAttributeValue("src", "");
                    if (!string.IsNullOrEmpty(src))
                    {
                        return src.StartsWith("http") ? src : $"https:{src}";
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                // Log error but don't fail the entire operation
                Console.WriteLine($"Failed to extract image from {programmeUrl}: {ex.Message}");
                return null;
            }
        }

        private static string? ParseBestImageFromSrcSet(string srcSet)
        {
            try
            {
                // Parse srcset: "url1 width1, url2 width2, ..."
                // Example: "https://ichef.bbci.co.uk/images/ic/80x45/p0bvp6bh.jpg 80w, https://ichef.bbci.co.uk/images/ic/640x360/p0bvp6bh.jpg 640w"
                
                var sources = srcSet.Split(',')
                    .Select(part => part.Trim())
                    .Where(part => !string.IsNullOrEmpty(part))
                    .Select(part =>
                    {
                        var segments = part.Split(' ');
                        if (segments.Length >= 2)
                        {
                            var url = segments[0].Trim();
                            var widthStr = segments[1].Replace("w", "").Trim();
                            if (int.TryParse(widthStr, out int width))
                            {
                                return new { Url = url, Width = width };
                            }
                        }
                        return null;
                    })
                    .Where(item => item != null)
                    .OrderByDescending(item => item!.Width)
                    .ToList();

                // Return the highest resolution image (usually 640x360 for BBC)
                return sources.FirstOrDefault()?.Url;
            }
            catch
            {
                return null;
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
