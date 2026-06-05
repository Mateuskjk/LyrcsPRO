namespace LyricsPro.Models;

public record Slide(
    string Text,
    string Title,
    string Subtitle,
    string? BackgroundImagePath,
    int Index,
    int Total,
    SlideSource Source
);

public enum SlideSource { Lyrics, Bible, Blank }
