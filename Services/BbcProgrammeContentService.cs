using System;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using BbcSoundz.Models;

namespace BbcSoundz.Services
{
    public class BbcProgrammeContentService : IDisposable
    {
        private readonly HttpClient _httpClient;

        public BbcProgrammeContentService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", 
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept-Charset", "UTF-8");
        }

        public async Task<ProgrammeContent?> GetProgrammeContentAsync(string programmeUrl)
        {
            if (string.IsNullOrEmpty(programmeUrl))
                return null;

            try
            {
                // Get the response as bytes first to handle encoding properly
                var response = await _httpClient.GetAsync(programmeUrl);
                response.EnsureSuccessStatusCode();
                
                var bytes = await response.Content.ReadAsByteArrayAsync();
                var html = System.Text.Encoding.UTF8.GetString(bytes);
                
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var content = new ProgrammeContent
                {
                    Url = programmeUrl
                };

                // Extract programme title
                var titleNode = doc.DocumentNode.SelectSingleNode("//h1[contains(@class, 'hero__title')] | //h1[@class='no-margin'] | //h1");
                if (titleNode != null)
                {
                    content.Title = System.Net.WebUtility.HtmlDecode(titleNode.InnerText?.Trim() ?? "");
                }

                // Extract programme subtitle/episode title
                var subtitleNode = doc.DocumentNode.SelectSingleNode("//p[contains(@class, 'hero__subtitle')] | //h2[contains(@class, 'episode__subtitle')]");
                if (subtitleNode != null)
                {
                    content.Subtitle = System.Net.WebUtility.HtmlDecode(subtitleNode.InnerText?.Trim() ?? "");
                }

                // Extract main description
                var descriptionNode = doc.DocumentNode.SelectSingleNode(
                    "//div[contains(@class, 'episode-synopsis')]//p | " +
                    "//div[contains(@class, 'synopsis')]//p | " +
                    "//div[contains(@class, 'programme-synopsis')]//p | " +
                    "//p[contains(@class, 'programme__synopsis')]");
                
                if (descriptionNode != null)
                {
                    content.Description = System.Net.WebUtility.HtmlDecode(descriptionNode.InnerText?.Trim() ?? "");
                }

                // Extract brand information
                var brandNode = doc.DocumentNode.SelectSingleNode("//span[contains(@class, 'brand-title')] | //a[contains(@class, 'brand__title')]");
                if (brandNode != null)
                {
                    content.Brand = System.Net.WebUtility.HtmlDecode(brandNode.InnerText?.Trim() ?? "");
                }

                // Extract duration
                var durationNode = doc.DocumentNode.SelectSingleNode("//span[contains(@class, 'duration')] | //*[contains(text(), 'minutes')] | //*[contains(text(), 'hours')]");
                if (durationNode != null)
                {
                    content.Duration = System.Net.WebUtility.HtmlDecode(durationNode.InnerText?.Trim() ?? "");
                }

                // Extract broadcast date
                var dateNode = doc.DocumentNode.SelectSingleNode(
                    "//time | " +
                    "//span[contains(@class, 'broadcast')] | " +
                    "//*[contains(@class, 'date')]");
                
                if (dateNode != null)
                {
                    var dateText = System.Net.WebUtility.HtmlDecode(dateNode.InnerText?.Trim() ?? "");
                    content.BroadcastDate = dateText;
                    
                    // Try to parse the date attribute if available
                    var dateAttr = dateNode.GetAttributeValue("datetime", "");
                    if (!string.IsNullOrEmpty(dateAttr) && DateTime.TryParse(dateAttr, out DateTime parsedDate))
                    {
                        content.BroadcastDateTime = parsedDate;
                    }
                }

                // Extract image URL (reuse the existing logic from BbcScheduleScraper)
                content.ImageUrl = ExtractImageUrl(doc, programmeUrl) ?? "";

                // Extract additional metadata
                ExtractAdditionalMetadata(doc, content);

                return content;
            }
            catch (Exception ex)
            {
                // Return an error content object
                return new ProgrammeContent
                {
                    Url = programmeUrl,
                    Title = "Error loading programme",
                    Description = $"Failed to load programme content: {ex.Message}",
                    HasError = true,
                    ErrorMessage = ex.Message
                };
            }
        }

        private string? ExtractImageUrl(HtmlDocument doc, string programmeUrl)
        {
            try
            {
                // Look for the playout image in the episode-playout section
                var playoutImage = doc.DocumentNode
                    .SelectSingleNode("//div[contains(@class, 'episode-playout')]//img[contains(@class, 'image')] | //img[contains(@class, 'hero__image')] | //img[contains(@class, 'programme-image')]");

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
            catch
            {
                return null;
            }
        }

        private string? ParseBestImageFromSrcSet(string srcSet)
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

        private void ExtractAdditionalMetadata(HtmlDocument doc, ProgrammeContent content)
        {
            // Extract any additional structured data or metadata
            try
            {
                // Look for JSON-LD structured data
                var scriptNodes = doc.DocumentNode.SelectNodes("//script[@type='application/ld+json']");
                if (scriptNodes != null)
                {
                    foreach (var scriptNode in scriptNodes)
                    {
                        try
                        {
                            var jsonContent = scriptNode.InnerText;
                            if (!string.IsNullOrEmpty(jsonContent))
                            {
                                // You could parse this JSON for additional metadata
                                // For now, we'll store it as raw data
                                content.StructuredData = jsonContent;
                                break;
                            }
                        }
                        catch { /* Continue to next script */ }
                    }
                }

                // Extract any genre or category information
                var genreNodes = doc.DocumentNode.SelectNodes("//span[contains(@class, 'genre')] | //a[contains(@class, 'category')]");
                if (genreNodes != null)
                {
                    var genres = genreNodes
                        .Select(n => System.Net.WebUtility.HtmlDecode(n.InnerText?.Trim() ?? ""))
                        .Where(g => !string.IsNullOrEmpty(g))
                        .ToList();
                    
                    if (genres.Any())
                    {
                        content.Genres = string.Join(", ", genres);
                    }
                }
            }
            catch
            {
                // Don't fail the whole operation for metadata extraction issues
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
