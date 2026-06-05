namespace LyricsPro.Models;

public enum ItemKind { Lyrics, Audio, Video, Bible }

public record LibraryItem(
    string Id,
    string Title,
    string Subtitle,
    ItemKind Kind,
    string? ThumbnailPath = null
);
