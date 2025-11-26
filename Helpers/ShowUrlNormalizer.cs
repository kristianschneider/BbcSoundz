using System;

namespace BbcSoundz.Helpers
{
    internal static class ShowUrlNormalizer
    {
        public static string Normalize(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return string.Empty;
            }

            var trimmed = url.Trim();
            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            {
                var builder = new UriBuilder(uri) { Scheme = Uri.UriSchemeHttps, Host = "www.bbc.co.uk", Port = -1 };
                var normalized = builder.Uri.GetLeftPart(UriPartial.Path).TrimEnd('/').ToLowerInvariant();
                return normalized;
            }

            return trimmed.TrimEnd('/');
        }
    }
}
