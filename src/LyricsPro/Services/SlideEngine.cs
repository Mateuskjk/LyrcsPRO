using LyricsPro.Models;
using System.Text.RegularExpressions;

namespace LyricsPro.Services;

public static class SlideEngine
{
    /// <summary>Split a lyric entry into presentation slides (one stanza each).</summary>
    public static List<Slide> FromLyric(LyricEntry lyric)
    {
        // Split on blank lines (stanza breaks)
        var stanzas = Regex.Split(lyric.LyricText.Trim(), @"\r?\n\s*\r?\n")
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        if (stanzas.Count == 0)
            stanzas.Add(lyric.LyricText.Trim());

        var total = stanzas.Count;
        return stanzas.Select((text, i) => new Slide(
            Text: text,
            Title: lyric.Title,
            Subtitle: lyric.Artist,
            BackgroundImagePath: lyric.BackgroundImagePath,
            Index: i,
            Total: total,
            Source: SlideSource.Lyrics
        )).ToList();
    }

    /// <summary>Build slides from a list of Bible verses (one verse per slide).</summary>
    public static List<Slide> FromBibleVerses(IEnumerable<BibleVerse> verses, string bookName, int chapter)
    {
        var list = verses.ToList();
        var total = list.Count;
        return list.Select((v, i) => new Slide(
            Text: v.Text,
            Title: $"{bookName} {chapter}:{v.Verse}",
            Subtitle: v.Version,
            BackgroundImagePath: null,
            Index: i,
            Total: total,
            Source: SlideSource.Bible
        )).ToList();
    }
}
