using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using BbcSoundz.Helpers;
using BbcSoundz.Models;
using HtmlAgilityPack;

namespace BbcSoundz.Services
{
    public class ShowProgrammeService
    {
        private readonly HttpClient _httpClient;

        public ShowProgrammeService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Safari/537.36");
        }

        public async Task<List<ShowInfo>> GetProgrammesAsync(string showUrl)
        {
            if (string.IsNullOrWhiteSpace(showUrl))
            {
                return new List<ShowInfo>();
            }

            var normalizedShowUrl = ShowUrlNormalizer.Normalize(showUrl);
            var listingUrl = BuildListingUrl(normalizedShowUrl);

            try
            {
                var html = await _httpClient.GetStringAsync(listingUrl);
                if (string.IsNullOrWhiteSpace(html))
                {
                    return new List<ShowInfo>();
                }

                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var programmeNodes = doc.DocumentNode
                    .SelectNodes("//div[contains(@class,'programme--episode')]"
                        + " | //li[contains(@class,'programme') and .//a[contains(@class,'programme__titles')]]"
                        + " | //div[contains(@class,'episode-item')]");

                if (programmeNodes == null)
                {
                    return new List<ShowInfo>();
                }

                var programmes = new List<ShowInfo>();
                var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var node in programmeNodes)
                {
                    var linkNode = node.SelectSingleNode(".//a[contains(@class,'programme__titles')]"
                                       + " | .//a[contains(@class,'br-blocklink__link')]"
                                       + " | .//a[@href]");

                    if (linkNode == null)
                    {
                        continue;
                    }

                    var href = linkNode.GetAttributeValue("href", string.Empty);
                    if (string.IsNullOrWhiteSpace(href))
                    {
                        continue;
                    }

                    if (!href.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        href = href.StartsWith("/")
                            ? $"https://www.bbc.co.uk{href}"
                            : $"https://www.bbc.co.uk/{href}";
                    }

                    href = ShowUrlNormalizer.Normalize(href);
                    if (!seenUrls.Add(href))
                    {
                        continue;
                    }

                    var title = WebUtility.HtmlDecode(linkNode.InnerText?.Trim() ?? string.Empty);
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        continue;
                    }

                    var description = ExtractText(node, new[]
                    {
                        ".//p[contains(@class,'programme__synopsis')]",
                        ".//p[contains(@class,'text--description')]",
                        ".//p"
                    });

                    var metaText = ExtractText(node, new[]
                    {
                        ".//div[contains(@class,'programme__meta')]",
                        ".//div[contains(@class,'episode-item__info__secondary')]",
                        ".//ul[contains(@class,'episode-item__meta')]"
                    });

                    var broadcastDate = TryParseBroadcastDate(metaText);
                    var imageUrl = ExtractImageUrl(node);

                    var displayName = broadcastDate.HasValue
                        ? $"{title} ({broadcastDate.Value:dd MMM yyyy})"
                        : title;

                    programmes.Add(new ShowInfo
                    {
                        Title = title,
                        DisplayName = displayName,
                        Description = description ?? string.Empty,
                        Url = href,
                        Date = broadcastDate ?? DateTime.MinValue,
                        ImageUrl = imageUrl
                    });
                }

                return programmes;
            }
            catch
            {
                return new List<ShowInfo>();
            }
        }

        private static string BuildListingUrl(string normalizedShowUrl)
        {
            if (string.IsNullOrEmpty(normalizedShowUrl))
            {
                return normalizedShowUrl;
            }

            if (normalizedShowUrl.Contains("/episodes", StringComparison.OrdinalIgnoreCase))
            {
                return normalizedShowUrl;
            }

            return normalizedShowUrl.TrimEnd('/') + "/episodes/player";
        }

        private static string? ExtractText(HtmlNode node, IEnumerable<string> xpaths)
        {
            foreach (var xpath in xpaths)
            {
                var textNode = node.SelectSingleNode(xpath);
                if (textNode != null)
                {
                    var text = WebUtility.HtmlDecode(textNode.InnerText?.Trim() ?? string.Empty);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text;
                    }
                }
            }

            return null;
        }

        private static string? ExtractImageUrl(HtmlNode node)
        {
            var imageNode = node.SelectSingleNode(".//img");
            if (imageNode == null)
            {
                return null;
            }

            var url = imageNode.GetAttributeValue("data-src", null)
                      ?? imageNode.GetAttributeValue("src", null);

            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            if (url.StartsWith("//"))
            {
                url = $"https:{url}";
            }
            else if (url.StartsWith("/"))
            {
                url = $"https://www.bbc.co.uk{url}";
            }

            return url;
        }

        private static DateTime? TryParseBroadcastDate(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var cleaned = WebUtility.HtmlDecode(text)
                .Replace("First broadcast:", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("Available now", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Trim();

            if (DateTime.TryParse(cleaned, CultureInfo.GetCultureInfo("en-GB"),
                    DateTimeStyles.AssumeLocal, out var parsed))
            {
                return parsed;
            }

            // Try to extract date tokens from strings like "Sun 12 Jan 2025"
            var tokens = cleaned.Split(new[] { '•', '-', '|', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim());

            foreach (var token in tokens)
            {
                if (DateTime.TryParse(token, CultureInfo.GetCultureInfo("en-GB"),
                        DateTimeStyles.AssumeLocal, out parsed))
                {
                    return parsed;
                }
            }

            return null;
        }
    }
}
