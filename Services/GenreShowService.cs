using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using BbcSoundz.Models;
using BbcSoundz.Helpers;
using HtmlAgilityPack;

namespace BbcSoundz.Services
{
    public class GenreShowService
    {
        private const string GenrePlayerUrl = "https://www.bbc.co.uk/programmes/genres/music/danceandelectronica/player";
        private readonly HttpClient _httpClient;

        public GenreShowService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
        }

        public async Task<List<ShowInfo>> GetShowsAsync()
        {
            var html = await _httpClient.GetStringAsync(GenrePlayerUrl);
            if (string.IsNullOrWhiteSpace(html))
            {
                return new List<ShowInfo>();
            }

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var showNodes = doc.DocumentNode
                .SelectNodes("//ol[contains(@class, 'highlight-box-wrapper')]//div[contains(@class, 'programme')]"
                    + " | //div[contains(@class, 'highlight-box')]//div[contains(@class, 'programme')]");

            if (showNodes == null)
            {
                return new List<ShowInfo>();
            }

            var shows = new List<ShowInfo>();
            //add first item as empty to tell user to select

            var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var node in showNodes)
            {
                var titleLink = node.SelectSingleNode(".//h2[contains(@class, 'programme__titles')]//a");
                if (titleLink == null)
                {
                    continue;
                }

                var rawTitle = WebUtility.HtmlDecode(titleLink.InnerText?.Trim() ?? "");
                if (string.IsNullOrWhiteSpace(rawTitle))
                {
                    continue;
                }

                var href = titleLink.GetAttributeValue("href", "");
                if (string.IsNullOrWhiteSpace(href))
                {
                    continue;
                }

                if (!href.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    href = href.StartsWith("/") ? $"https://www.bbc.co.uk{href}" : $"https://www.bbc.co.uk/{href}";
                }

                var normalizedUrl = ShowUrlNormalizer.Normalize(href);
                if (!seenUrls.Add(normalizedUrl))
                {
                    continue;
                }

                var synopsis = node.SelectSingleNode(".//p[contains(@class, 'programme__synopsis')]")?.InnerText?.Trim();
                var service = node.SelectSingleNode(".//p[contains(@class, 'programme__service')]")?.InnerText?.Trim();

                var imageNode = node.SelectSingleNode(".//div[contains(@class, 'programme__img')]//img");
                var imageUrl = imageNode?.GetAttributeValue("data-src", null)
                    ?? imageNode?.GetAttributeValue("src", null);

                if (!string.IsNullOrWhiteSpace(imageUrl))
                {
                    if (imageUrl.StartsWith("//"))
                    {
                        imageUrl = $"https:{imageUrl}";
                    }
                    else if (imageUrl.StartsWith("/"))
                    {
                        imageUrl = $"https://www.bbc.co.uk{imageUrl}";
                    }
                }

                var detailText = string.IsNullOrWhiteSpace(service)
                    ? synopsis
                    : $"{service} • {synopsis}".Trim(' ', '•');

                var show = new ShowInfo
                {
                    Title = rawTitle,
                    DisplayName = rawTitle,
                    Url = normalizedUrl,
                    Description = detailText ?? string.Empty,
                    ImageUrl = imageUrl,
                    Date = DateTime.Now
                };

                shows.Add(show);
            }

            return shows.OrderBy(s => s.Title).ToList();
        }

    }
}
