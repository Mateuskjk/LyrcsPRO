namespace LyricsPro.Models;

public class BibleBook
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Abbrev { get; set; } = string.Empty;
    public int Testament { get; set; } // 1=OT 2=NT
    public int ChapterCount { get; set; }
}

public class BibleVerse
{
    public int Id { get; set; }
    public int BookId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }
    public string Text { get; set; } = string.Empty;
    public string Version { get; set; } = "ACF";
}
