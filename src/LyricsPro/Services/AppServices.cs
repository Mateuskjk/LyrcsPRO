namespace LyricsPro.Services;

public static class AppServices
{
    public static readonly DatabaseService      Database       = new();
    public static readonly LyricsSearchService  LyricsSearch   = new();
    public static readonly BibleImportService   BibleImport    = new(Database);
    public static readonly BibleDownloadService BibleDownload  = new(Database);
    public static readonly RemoteControlService RemoteControl  = new();
    public static readonly MediaPlayerService   MediaPlayer    = new();
    public static readonly MediaStateService    MediaState     = new();
}
