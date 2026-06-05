using LyricsPro.Models;
using System.Net.Http;
using System.Text.Json;

namespace LyricsPro.Services;

public record BibleTranslation(string Code, string Name, string Language, string Source = "bolls");

public class BibleDownloadService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly DatabaseService _db;

    // ── CDN translations (thiagobodruk/biblia — free, no key, PT-only) ──
    private static readonly Dictionary<string, (string file, string name)> CdnTranslations = new()
    {
        ["ACF"]  = ("acf.json", "Almeida Corrigida Fiel"),
        ["NVI"]  = ("nvi.json", "Nova Versão Internacional"),
        ["AA"]   = ("aa.json",  "Almeida Atualizada"),
    };

    // ── bolls.life translations (multilingual, requires polling) ──
    public static readonly BibleTranslation[] KnownTranslations =
    [
        // PT — CDN (fast, reliable)
        new("ACF",  "Almeida Corrigida Fiel",             "Português", "cdn"),
        new("NVI",  "Nova Versão Internacional",           "Português", "cdn"),
        new("AA",   "Almeida Atualizada",                  "Português", "cdn"),
        // PT — bolls.life
        new("NTLH", "Nova Tradução na Ling. de Hoje",      "Português", "bolls"),
        new("ARC",  "Almeida Revista e Corrigida",         "Português", "bolls"),
        new("NVT",  "Nova Versão Transformadora",          "Português", "bolls"),
        // EN
        new("KJV",  "King James Version",                  "English",   "bolls"),
        new("ESV",  "English Standard Version",            "English",   "bolls"),
        new("NIV",  "New International Version",           "English",   "bolls"),
        new("NKJV", "New King James Version",              "English",   "bolls"),
        new("NLT",  "New Living Translation",              "English",   "bolls"),
        new("WEB",  "World English Bible",                 "English",   "bolls"),
        // ES
        new("RVR",  "Reina Valera 1960",                   "Español",   "bolls"),
        new("RVC",  "Reina Valera Contemporánea",          "Español",   "bolls"),
    ];

    public BibleDownloadService(DatabaseService db) => _db = db;

    // ── Check availability ────────────────────────────────────────

    public async Task<List<BibleTranslation>> GetAvailableTranslationsAsync(
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var available = new List<BibleTranslation>();

        foreach (var t in KnownTranslations)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report($"Verificando {t.Code}...");
            try
            {
                if (t.Source == "cdn")
                {
                    // CDN always available
                    available.Add(t);
                }
                else
                {
                    var json = await _http.GetStringAsync(
                        $"https://bolls.life/get-books/{t.Code}/", ct);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array
                        && doc.RootElement.GetArrayLength() > 0)
                        available.Add(t);
                }
            }
            catch { }
            await Task.Delay(80, ct);
        }
        return available;
    }

    // ── Download ──────────────────────────────────────────────────

    public async Task DownloadAsync(
        BibleTranslation translation,
        IProgress<(int current, int total, string message)>? progress = null,
        CancellationToken ct = default)
    {
        if (translation.Source == "cdn")
            await DownloadFromCdnAsync(translation, progress, ct);
        else
            await DownloadFromBollsAsync(translation, progress, ct);
    }

    // ── CDN source (thiagobodruk/biblia) ─────────────────────────

    private static readonly string[] BookNames =
    [
        "Gênesis","Êxodo","Levítico","Números","Deuteronômio","Josué","Juízes","Rute",
        "1 Samuel","2 Samuel","1 Reis","2 Reis","1 Crônicas","2 Crônicas","Esdras",
        "Neemias","Ester","Jó","Salmos","Provérbios","Eclesiastes","Cantares","Isaías",
        "Jeremias","Lamentações","Ezequiel","Daniel","Oséias","Joel","Amós","Obadias",
        "Jonas","Miquéias","Naum","Habacuque","Sofonias","Ageu","Zacarias","Malaquias",
        "Mateus","Marcos","Lucas","João","Atos","Romanos","1 Coríntios","2 Coríntios",
        "Gálatas","Efésios","Filipenses","Colossenses","1 Tessalonicenses",
        "2 Tessalonicenses","1 Timóteo","2 Timóteo","Tito","Filemon","Hebreus","Tiago",
        "1 Pedro","2 Pedro","1 João","2 João","3 João","Judas","Apocalipse"
    ];

    private static readonly string[] BookAbbrevs =
    [
        "Gn","Ex","Lv","Nm","Dt","Js","Jz","Rt","1Sm","2Sm","1Rs","2Rs","1Cr","2Cr",
        "Ed","Ne","Et","Jó","Sl","Pv","Ec","Ct","Is","Jr","Lm","Ez","Dn","Os","Jl",
        "Am","Ob","Jn","Mq","Na","Hc","Sf","Ag","Zc","Ml","Mt","Mc","Lc","Jo","At",
        "Rm","1Co","2Co","Gl","Ef","Fp","Cl","1Ts","2Ts","1Tm","2Tm","Tt","Fm","Hb",
        "Tg","1Pe","2Pe","1Jo","2Jo","3Jo","Jd","Ap"
    ];

    private async Task DownloadFromCdnAsync(
        BibleTranslation translation,
        IProgress<(int, int, string)>? progress,
        CancellationToken ct)
    {
        if (!CdnTranslations.TryGetValue(translation.Code, out var cdnInfo))
            throw new Exception($"Tradução {translation.Code} não encontrada no CDN.");

        progress?.Report((0, 1, $"Baixando {translation.Name} do CDN..."));

        var url  = $"https://cdn.jsdelivr.net/gh/thiagobodruk/biblia@master/json/{cdnInfo.file}";
        var json = await _http.GetStringAsync(url, ct);

        using var doc = JsonDocument.Parse(json);
        var booksArr  = doc.RootElement.EnumerateArray().ToList();

        var books  = new List<BibleBook>();
        var verses = new List<BibleVerse>();

        for (int bi = 0; bi < booksArr.Count && bi < 66; bi++)
        {
            var bookEl = booksArr[bi];
            var bookId = bi + 1;

            var book = new BibleBook
            {
                Id          = bookId,
                Name        = bi < BookNames.Length ? BookNames[bi] : $"Livro {bookId}",
                Abbrev      = bi < BookAbbrevs.Length ? BookAbbrevs[bi] : $"L{bookId}",
                Testament   = bookId <= 39 ? 1 : 2,
                ChapterCount = 0
            };

            if (!bookEl.TryGetProperty("chapters", out var chapters)) continue;
            var chapList = chapters.EnumerateArray().ToList();
            book.ChapterCount = chapList.Count;
            books.Add(book);

            for (int ci = 0; ci < chapList.Count; ci++)
            {
                var verseArr = chapList[ci].EnumerateArray().ToList();
                for (int vi = 0; vi < verseArr.Count; vi++)
                {
                    var text = verseArr[vi].GetString() ?? "";
                    if (!string.IsNullOrEmpty(text))
                        verses.Add(new BibleVerse
                        {
                            BookId  = bookId,
                            Chapter = ci + 1,
                            Verse   = vi + 1,
                            Text    = StripHtml(text),
                            Version = translation.Code
                        });
                }
                progress?.Report((bi * 10 + ci, booksArr.Count * 10, $"{book.Name} {ci + 1}/{chapList.Count}"));
            }
        }

        _db.BulkInsertBible(books, verses);
    }

    // ── bolls.life source ─────────────────────────────────────────

    private async Task DownloadFromBollsAsync(
        BibleTranslation translation,
        IProgress<(int, int, string)>? progress,
        CancellationToken ct)
    {
        var booksJson = await _http.GetStringAsync(
            $"https://bolls.life/get-books/{translation.Code}/", ct);
        using var booksDoc = JsonDocument.Parse(booksJson);
        var rawBooks = booksDoc.RootElement.ValueKind == JsonValueKind.Array
            ? booksDoc.RootElement.EnumerateArray().ToList()
            : throw new Exception($"Sem livros para {translation.Code}");

        var books = rawBooks.Select(b =>
        {
            int id = TryInt(b, "bookid", "book_id", "id");
            return new BibleBook
            {
                Id           = id,
                Name         = TryStr(b, "name", "long_name"),
                Abbrev       = TryStr(b, "slug", "abbrev", "short_name"),
                Testament    = id <= 39 ? 1 : 2,
                ChapterCount = TryInt(b, "chapters", "chapter_count")
            };
        }).Where(b => b.Id > 0).ToList();

        var allVerses = new List<BibleVerse>();
        int total = books.Sum(b => b.ChapterCount), done = 0;

        foreach (var book in books)
        {
            for (int ch = 1; ch <= book.ChapterCount; ch++)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var chJson = await _http.GetStringAsync(
                        $"https://bolls.life/get-chapter/{translation.Code}/{book.Id}/{ch}/", ct);
                    using var chDoc = JsonDocument.Parse(chJson);
                    if (chDoc.RootElement.ValueKind == JsonValueKind.Array)
                        foreach (var v in chDoc.RootElement.EnumerateArray())
                        {
                            int vn  = TryInt(v, "verse", "verse_number", "v");
                            var txt = TryStr(v, "text", "content", "verse_text");
                            if (vn > 0 && !string.IsNullOrEmpty(txt))
                                allVerses.Add(new BibleVerse
                                {
                                    BookId  = book.Id, Chapter = ch,
                                    Verse   = vn, Text = StripHtml(txt),
                                    Version = translation.Code
                                });
                        }
                }
                catch { }
                done++;
                progress?.Report((done, total, $"[{translation.Code}] {book.Name} {ch}/{book.ChapterCount}"));
                await Task.Delay(40, ct);
            }
        }
        _db.BulkInsertBible(books, allVerses);
    }

    // ── Helpers ───────────────────────────────────────────────────

    private static string StripHtml(string html)
    {
        html = System.Text.RegularExpressions.Regex.Replace(
            html, @"<br\s*/?>", "\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        html = System.Text.RegularExpressions.Regex.Replace(html, @"<[^>]+>", "");
        return System.Net.WebUtility.HtmlDecode(html).Trim();
    }

    private static string TryStr(JsonElement el, params string[] keys)
    {
        foreach (var k in keys)
            if (el.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString() ?? "";
        return "";
    }

    private static int TryInt(JsonElement el, params string[] keys)
    {
        foreach (var k in keys)
        {
            if (!el.TryGetProperty(k, out var v)) continue;
            if (v.ValueKind == JsonValueKind.Number) return v.GetInt32();
            if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out var i)) return i;
        }
        return 0;
    }
}
