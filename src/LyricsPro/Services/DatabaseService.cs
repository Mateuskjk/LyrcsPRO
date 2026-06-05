using Dapper;
using LyricsPro.Models;
using Microsoft.Data.Sqlite;
using System.IO;

namespace LyricsPro.Services;

public class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LyricsPro");
        Directory.CreateDirectory(folder);
        var dbPath = Path.Combine(folder, "lyricspro.db");
        _connectionString = $"Data Source={dbPath};";
        InitializeSchema();
    }

    // Every caller must dispose via `using`
    private SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    private void InitializeSchema()
    {
        using var db = Open();
        db.Execute("""
            CREATE TABLE IF NOT EXISTS Lyrics (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                Artist TEXT NOT NULL DEFAULT '',
                LyricText TEXT NOT NULL DEFAULT '',
                BackgroundImagePath TEXT,
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                UpdatedAt TEXT NOT NULL DEFAULT (datetime('now'))
            );
            CREATE TABLE IF NOT EXISTS Media (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                FilePath TEXT NOT NULL,
                Kind INTEGER NOT NULL,
                FileSizeBytes INTEGER NOT NULL DEFAULT 0,
                AddedAt TEXT NOT NULL DEFAULT (datetime('now'))
            );
            CREATE TABLE IF NOT EXISTS BibleBooks (
                Id INTEGER PRIMARY KEY,
                Name TEXT NOT NULL,
                Abbrev TEXT NOT NULL,
                Testament INTEGER NOT NULL,
                ChapterCount INTEGER NOT NULL
            );
            CREATE TABLE IF NOT EXISTS BibleVerses (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                BookId INTEGER NOT NULL,
                Chapter INTEGER NOT NULL,
                Verse INTEGER NOT NULL,
                Text TEXT NOT NULL,
                Version TEXT NOT NULL DEFAULT 'ACF'
            );
            CREATE INDEX IF NOT EXISTS IX_BibleVerses_Lookup
                ON BibleVerses(BookId, Chapter, Verse, Version);
        """);
    }

    // ── Lyrics ──────────────────────────────────────────────────

    public List<LyricEntry> GetAllLyrics()
    {
        using var db = Open();
        return db.Query<LyricEntry>(
            "SELECT * FROM Lyrics ORDER BY UpdatedAt DESC").ToList();
    }

    public List<LyricEntry> SearchLyrics(string query)
    {
        using var db = Open();
        return db.Query<LyricEntry>(
            "SELECT * FROM Lyrics WHERE Title LIKE @q OR Artist LIKE @q ORDER BY Title",
            new { q = $"%{query}%" }).ToList();
    }

    public LyricEntry? GetLyric(int id)
    {
        using var db = Open();
        return db.QueryFirstOrDefault<LyricEntry>(
            "SELECT * FROM Lyrics WHERE Id=@id", new { id });
    }

    public int SaveLyric(LyricEntry entry)
    {
        using var db = Open();
        if (entry.Id == 0)
            return db.ExecuteScalar<int>("""
                INSERT INTO Lyrics (Title,Artist,LyricText,BackgroundImagePath,CreatedAt,UpdatedAt)
                VALUES (@Title,@Artist,@LyricText,@BackgroundImagePath,datetime('now'),datetime('now'));
                SELECT last_insert_rowid();
                """, entry);

        db.Execute("""
            UPDATE Lyrics
            SET Title=@Title, Artist=@Artist, LyricText=@LyricText,
                BackgroundImagePath=@BackgroundImagePath, UpdatedAt=datetime('now')
            WHERE Id=@Id
            """, entry);
        return entry.Id;
    }

    public void DeleteLyric(int id)
    {
        using var db = Open();
        db.Execute("DELETE FROM Lyrics WHERE Id=@id", new { id });
    }

    // ── Media ────────────────────────────────────────────────────

    public List<MediaEntry> GetMedia(MediaKind? kind = null)
    {
        using var db = Open();
        return kind is null
            ? db.Query<MediaEntry>("SELECT * FROM Media ORDER BY AddedAt DESC").ToList()
            : db.Query<MediaEntry>(
                "SELECT * FROM Media WHERE Kind=@k ORDER BY Title",
                new { k = (int)kind }).ToList();
    }

    public int SaveMedia(MediaEntry entry)
    {
        using var db = Open();
        if (entry.Id == 0)
            return db.ExecuteScalar<int>("""
                INSERT INTO Media (Title,FilePath,Kind,FileSizeBytes,AddedAt)
                VALUES (@Title,@FilePath,@Kind,@FileSizeBytes,datetime('now'));
                SELECT last_insert_rowid();
                """, entry);

        db.Execute("UPDATE Media SET Title=@Title WHERE Id=@Id", entry);
        return entry.Id;
    }

    public void DeleteMedia(int id)
    {
        using var db = Open();
        db.Execute("DELETE FROM Media WHERE Id=@id", new { id });
    }

    // ── Bible ────────────────────────────────────────────────────

    public bool BibleHasData(string version)
    {
        using var db = Open();
        return db.ExecuteScalar<int>(
            "SELECT COUNT(1) FROM BibleVerses WHERE Version=@v LIMIT 1",
            new { v = version }) > 0;
    }

    public List<string> GetDownloadedVersions()
    {
        using var db = Open();
        return db.Query<string>(
            "SELECT DISTINCT Version FROM BibleVerses ORDER BY Version").ToList();
    }

    public void DeleteBibleVersion(string version)
    {
        using var db = Open();
        db.Execute("DELETE FROM BibleVerses WHERE Version=@v", new { v = version });
    }

    public List<BibleBook> GetBibleBooks()
    {
        using var db = Open();
        return db.Query<BibleBook>(
            "SELECT * FROM BibleBooks ORDER BY Id").ToList();
    }

    public List<BibleVerse> GetVerses(int bookId, int chapter, string version)
    {
        using var db = Open();
        return db.Query<BibleVerse>(
            "SELECT * FROM BibleVerses WHERE BookId=@bookId AND Chapter=@chapter AND Version=@v ORDER BY Verse",
            new { bookId, chapter, v = version }).ToList();
    }

    public int GetChapterCount(int bookId, string version)
    {
        using var db = Open();
        return db.ExecuteScalar<int>(
            "SELECT MAX(Chapter) FROM BibleVerses WHERE BookId=@bookId AND Version=@v",
            new { bookId, v = version });
    }

    public void BulkInsertBible(IEnumerable<BibleBook> books, IEnumerable<BibleVerse> verses)
    {
        using var db = Open();
        var version = verses.FirstOrDefault()?.Version ?? "ACF";
        using var tx = db.BeginTransaction();

        db.Execute("DELETE FROM BibleVerses WHERE Version=@v", new { v = version }, tx);

        foreach (var b in books)
            db.Execute("""
                INSERT OR REPLACE INTO BibleBooks (Id,Name,Abbrev,Testament,ChapterCount)
                VALUES (@Id,@Name,@Abbrev,@Testament,@ChapterCount)
                """, b, tx);

        foreach (var v in verses)
            db.Execute("""
                INSERT INTO BibleVerses (BookId,Chapter,Verse,Text,Version)
                VALUES (@BookId,@Chapter,@Verse,@Text,@Version)
                """, v, tx);

        tx.Commit();
    }
}
