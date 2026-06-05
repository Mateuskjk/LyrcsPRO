using LyricsPro.Models;
using System.IO;
using System.Text.Json;

namespace LyricsPro.Services;

/// <summary>
/// Imports Bible JSON from the open-source "bible-api" format:
/// { "book": "Genesis", "chapters": [ { "chapter": 1, "verses": [ { "verse": 1, "text": "..." } ] } ] }
/// Or the bulk format used by openbible.info / bolls.life
/// </summary>
public class BibleImportService
{
    private readonly DatabaseService _db;

    public BibleImportService(DatabaseService db) => _db = db;

    // Standard book list (PT names)
    private static readonly (int id, string name, string abbrev, int testament, int chapters)[] BookList =
    [
        (1,"Gênesis","Gn",1,50),(2,"Êxodo","Ex",1,40),(3,"Levítico","Lv",1,27),
        (4,"Números","Nm",1,36),(5,"Deuteronômio","Dt",1,34),(6,"Josué","Js",1,24),
        (7,"Juízes","Jz",1,21),(8,"Rute","Rt",1,4),(9,"1 Samuel","1Sm",1,31),
        (10,"2 Samuel","2Sm",1,24),(11,"1 Reis","1Rs",1,22),(12,"2 Reis","2Rs",1,25),
        (13,"1 Crônicas","1Cr",1,29),(14,"2 Crônicas","2Cr",1,36),(15,"Esdras","Ed",1,10),
        (16,"Neemias","Ne",1,13),(17,"Ester","Et",1,10),(18,"Jó","Jó",1,42),
        (19,"Salmos","Sl",1,150),(20,"Provérbios","Pv",1,31),(21,"Eclesiastes","Ec",1,12),
        (22,"Cantares","Ct",1,8),(23,"Isaías","Is",1,66),(24,"Jeremias","Jr",1,52),
        (25,"Lamentações","Lm",1,5),(26,"Ezequiel","Ez",1,48),(27,"Daniel","Dn",1,12),
        (28,"Oséias","Os",1,14),(29,"Joel","Jl",1,3),(30,"Amós","Am",1,9),
        (31,"Obadias","Ob",1,1),(32,"Jonas","Jn",1,4),(33,"Miquéias","Mq",1,7),
        (34,"Naum","Na",1,3),(35,"Habacuque","Hc",1,3),(36,"Sofonias","Sf",1,3),
        (37,"Ageu","Ag",1,2),(38,"Zacarias","Zc",1,14),(39,"Malaquias","Ml",1,4),
        (40,"Mateus","Mt",2,28),(41,"Marcos","Mc",2,16),(42,"Lucas","Lc",2,24),
        (43,"João","Jo",2,21),(44,"Atos","At",2,28),(45,"Romanos","Rm",2,16),
        (46,"1 Coríntios","1Co",2,16),(47,"2 Coríntios","2Co",2,13),(48,"Gálatas","Gl",2,6),
        (49,"Efésios","Ef",2,6),(50,"Filipenses","Fp",2,4),(51,"Colossenses","Cl",2,4),
        (52,"1 Tessalonicenses","1Ts",2,5),(53,"2 Tessalonicenses","2Ts",2,3),
        (54,"1 Timóteo","1Tm",2,6),(55,"2 Timóteo","2Tm",2,4),(56,"Tito","Tt",2,3),
        (57,"Filemon","Fm",2,1),(58,"Hebreus","Hb",2,13),(59,"Tiago","Tg",2,5),
        (60,"1 Pedro","1Pe",2,5),(61,"2 Pedro","2Pe",2,3),(62,"1 João","1Jo",2,5),
        (63,"2 João","2Jo",2,1),(64,"3 João","3Jo",2,1),(65,"Judas","Jd",2,1),
        (66,"Apocalipse","Ap",2,22)
    ];

    public static IEnumerable<BibleBook> GetStandardBooks() =>
        BookList.Select(b => new BibleBook
        {
            Id = b.id, Name = b.name, Abbrev = b.abbrev,
            Testament = b.testament, ChapterCount = b.chapters
        });

    /// <summary>Import from bolls.life JSON format (array of {b,c,v,t})</summary>
    public async Task ImportFromBollsJsonAsync(string filePath, string version)
    {
        await using var stream = File.OpenRead(filePath);
        var raw = await JsonSerializer.DeserializeAsync<JsonElement[]>(stream);
        if (raw is null) return;

        var verses = raw.Select(el => new BibleVerse
        {
            BookId = el.GetProperty("b").GetInt32(),
            Chapter = el.GetProperty("c").GetInt32(),
            Verse = el.GetProperty("v").GetInt32(),
            Text = el.GetProperty("t").GetString() ?? "",
            Version = version
        }).ToList();

        _db.BulkInsertBible(GetStandardBooks(), verses);
    }
}
