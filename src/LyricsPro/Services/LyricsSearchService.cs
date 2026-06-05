using System.Net.Http;
using System.Text.Json;
using System.Web;

namespace LyricsPro.Services;

public record LyricsSearchResult(string Title, string Artist, string Url, string? PreviewLyrics = null);

public class LyricsSearchService
{
    private static readonly HttpClient _http = new(new HttpClientHandler { AllowAutoRedirect = true })
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    static LyricsSearchService()
    {
        _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/124 Safari/537.36");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
    }

    // ── Search ───────────────────────────────────────────────────
    public async Task<List<LyricsSearchResult>> SearchAsync(string query)
    {
        var results = new List<LyricsSearchResult>();

        // Primary: lyrics.ovh/suggest (Deezer-backed, multilingual, no key)
        try
        {
            var encoded = HttpUtility.UrlEncode(query);
            var json = await _http.GetStringAsync($"https://api.lyrics.ovh/suggest/{encoded}");
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("data", out var data))
            {
                foreach (var item in data.EnumerateArray().Take(20))
                {
                    var title  = Str(item, "title");
                    var artist = item.TryGetProperty("artist", out var a) ? Str(a, "name") : "";
                    if (string.IsNullOrWhiteSpace(title)) continue;

                    // Store both OVH and lrclib fallback url in the Url field using "|" separator
                    var ovhUrl  = $"https://api.lyrics.ovh/v1/{HttpUtility.UrlEncode(artist)}/{HttpUtility.UrlEncode(title)}";
                    results.Add(new LyricsSearchResult(title, artist, ovhUrl));
                }
            }
        }
        catch { }

        // Secondary: lrclib.net (always add its results as fallback entries)
        try
        {
            var encoded = HttpUtility.UrlEncode(query);
            var json = await _http.GetStringAsync($"https://lrclib.net/api/search?q={encoded}");
            using var doc = JsonDocument.Parse(json);

            foreach (var item in doc.RootElement.EnumerateArray().Take(10))
            {
                var title  = Str(item, "trackName");
                var artist = Str(item, "artistName");
                var id     = item.TryGetProperty("id", out var i) ? i.GetInt32() : 0;
                if (string.IsNullOrWhiteSpace(title) || id == 0) continue;
                if (results.Any(r => r.Title == title && r.Artist == artist)) continue;

                var previewLyrics = item.TryGetProperty("plainLyrics", out var pl) ? pl.GetString() : null;
                results.Add(new LyricsSearchResult(title, artist, $"lrclib:{id}", previewLyrics));
            }
        }
        catch { }

        return results;
    }

    // ── Fetch lyrics ─────────────────────────────────────────────
    public async Task<(string title, string artist, string lyrics)> FetchLyricsAsync(
        LyricsSearchResult result)
    {
        // lrclib path
        if (result.Url.StartsWith("lrclib:"))
        {
            var lyr = result.PreviewLyrics;
            if (string.IsNullOrWhiteSpace(lyr))
            {
                var id = result.Url["lrclib:".Length..];
                try
                {
                    var json = await _http.GetStringAsync($"https://lrclib.net/api/get/{id}");
                    using var doc = JsonDocument.Parse(json);
                    lyr = Str(doc.RootElement, "plainLyrics");
                }
                catch { }
            }
            return (result.Title, result.Artist, lyr ?? "Letra não disponível.");
        }

        // lyrics.ovh path — try this URL, on failure try lrclib search by title+artist
        if (result.Url.StartsWith("https://api.lyrics.ovh"))
        {
            try
            {
                var json = await _http.GetStringAsync(result.Url);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("lyrics", out var lyrEl))
                {
                    var lyr = lyrEl.GetString() ?? "";
                    // lyrics.ovh sometimes prepends "Paroles de la chanson X par Y\n\n"
                    var idx = lyr.IndexOf("\r\n\r\n");
                    if (idx < 0) idx = lyr.IndexOf("\n\n");
                    if (idx > 0 && idx < 100) lyr = lyr[(idx + (lyr[idx] == '\r' ? 4 : 2))..].Trim();
                    if (!string.IsNullOrWhiteSpace(lyr))
                        return (result.Title, result.Artist, lyr);
                }
            }
            catch { }

            // Fallback to lrclib search
            return await FetchFromLrclibAsync(result.Title, result.Artist);
        }

        return (result.Title, result.Artist, "Letra não disponível.");
    }

    private async Task<(string, string, string)> FetchFromLrclibAsync(string title, string artist)
    {
        try
        {
            var q = HttpUtility.UrlEncode($"{title} {artist}");
            var json = await _http.GetStringAsync($"https://lrclib.net/api/search?q={q}");
            using var doc = JsonDocument.Parse(json);
            var first = doc.RootElement.EnumerateArray().FirstOrDefault();
            if (first.ValueKind != JsonValueKind.Undefined)
            {
                var lyr = Str(first, "plainLyrics");
                var id  = first.TryGetProperty("id", out var i) ? i.GetInt32() : 0;
                if (string.IsNullOrWhiteSpace(lyr) && id > 0)
                {
                    var gJson = await _http.GetStringAsync($"https://lrclib.net/api/get/{id}");
                    using var gDoc = JsonDocument.Parse(gJson);
                    lyr = Str(gDoc.RootElement, "plainLyrics");
                }
                if (!string.IsNullOrWhiteSpace(lyr))
                    return (title, artist, lyr);
            }
        }
        catch { }
        return (title, artist, "Letra não encontrada nas fontes disponíveis.");
    }

    // Keep backward compat
    public async Task<(string title, string artist, string lyrics)> FetchLyricsAsync(string url)
        => await FetchLyricsAsync(new LyricsSearchResult("", "", url));

    private static string Str(JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) ? v.GetString() ?? "" : "";
}
