namespace LyricsPro.Models;

public enum MediaKind { Audio, Video, Image }

public class MediaEntry
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public MediaKind Kind { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
