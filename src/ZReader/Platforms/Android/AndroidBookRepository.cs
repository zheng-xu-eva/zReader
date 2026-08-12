using Android.Database;
using Android.Database.Sqlite;
using ZReader.Core.Domain;
using ZReader.Core.Services;

namespace ZReader.Platforms.Android;

/// <summary>
/// Persists shelf metadata and one reading state per book with Android's built-in SQLite engine.
/// </summary>
public sealed class AndroidBookRepository : IBookRepository
{
    private readonly ReaderDatabase _database = new();

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        _ = _database.WritableDatabase;
        return Task.CompletedTask;
    }

    public Task<Book> AddBookAsync(BookDraft draft, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var database = _database.WritableDatabase!;
        database.BeginTransaction();
        try
        {
            using var values = new global::Android.Content.ContentValues();
            values.Put("Title", draft.Title);
            values.Put("SourceFileName", draft.SourceFileName);
            values.Put("EncryptedRelativePath", draft.EncryptedRelativePath);
            values.Put("ImportedAt", now.ToString("O"));
            values.Put("LastReadAt", now.ToString("O"));
            values.Put("ContentLength", draft.ContentLength);
            values.Put("EncryptionVersion", draft.EncryptionVersion);
            var id = database.InsertOrThrow("Books", null, values);

            var state = ReadingState.CreateDefault(id, now);
            InsertOrUpdateState(database, state, isUpdate: false);
            database.SetTransactionSuccessful();
            return Task.FromResult(new Book
            {
                Id = id,
                Title = draft.Title,
                SourceFileName = draft.SourceFileName,
                EncryptedRelativePath = draft.EncryptedRelativePath,
                ImportedAt = now,
                LastReadAt = now,
                ContentLength = draft.ContentLength,
                EncryptionVersion = draft.EncryptionVersion
            });
        }
        finally
        {
            database.EndTransaction();
        }
    }

    public Task<IReadOnlyList<Book>> GetShelfAsync(CancellationToken cancellationToken)
    {
        var books = new List<Book>();
        using var cursor = _database.ReadableDatabase!.RawQuery(
            "SELECT Id, Title, SourceFileName, EncryptedRelativePath, ImportedAt, LastReadAt, ContentLength, EncryptionVersion FROM Books ORDER BY LastReadAt DESC, ImportedAt DESC", null);
        while (cursor.MoveToNext())
        {
            books.Add(new Book
            {
                Id = cursor.GetLong(0), Title = cursor.GetString(1)!, SourceFileName = cursor.GetString(2)!,
                EncryptedRelativePath = cursor.GetString(3)!, ImportedAt = DateTimeOffset.Parse(cursor.GetString(4)!),
                LastReadAt = DateTimeOffset.Parse(cursor.GetString(5)!), ContentLength = cursor.GetLong(6), EncryptionVersion = cursor.GetInt(7)
            });
        }

        return Task.FromResult<IReadOnlyList<Book>>(books);
    }

    public Task<ReadingState> GetReadingStateAsync(long bookId, CancellationToken cancellationToken)
    {
        using var cursor = _database.ReadableDatabase!.RawQuery(
            "SELECT BookId, CharacterOffset, FontSize, LineSpacing, Theme, UpdatedAt FROM ReadingStates WHERE BookId = ?", [bookId.ToString()]);
        if (!cursor.MoveToFirst())
        {
            throw new KeyNotFoundException($"Reading state for book {bookId} was not found.");
        }

        return Task.FromResult(new ReadingState
        {
            BookId = cursor.GetLong(0), CharacterOffset = cursor.GetLong(1), FontSize = cursor.GetDouble(2),
            LineSpacing = cursor.GetDouble(3), Theme = (ReaderTheme)cursor.GetInt(4), UpdatedAt = DateTimeOffset.Parse(cursor.GetString(5)!)
        });
    }

    public Task SaveReadingStateAsync(ReadingState state, CancellationToken cancellationToken)
    {
        state.UpdatedAt = DateTimeOffset.UtcNow;
        var database = _database.WritableDatabase!;
        database.BeginTransaction();
        try
        {
            InsertOrUpdateState(database, state, isUpdate: true);
            using var bookValues = new global::Android.Content.ContentValues();
            bookValues.Put("LastReadAt", state.UpdatedAt.ToString("O"));
            database.Update("Books", bookValues, "Id = ?", [state.BookId.ToString()]);
            database.SetTransactionSuccessful();
            return Task.CompletedTask;
        }
        finally
        {
            database.EndTransaction();
        }
    }

    private static void InsertOrUpdateState(SQLiteDatabase database, ReadingState state, bool isUpdate)
    {
        using var values = new global::Android.Content.ContentValues();
        values.Put("BookId", state.BookId); values.Put("CharacterOffset", state.CharacterOffset);
        values.Put("FontSize", state.FontSize); values.Put("LineSpacing", state.LineSpacing);
        values.Put("Theme", (int)state.Theme); values.Put("UpdatedAt", state.UpdatedAt.ToString("O"));
        if (isUpdate)
        {
            database.Update("ReadingStates", values, "BookId = ?", [state.BookId.ToString()]);
        }
        else
        {
            database.InsertOrThrow("ReadingStates", null, values);
        }
    }

    private sealed class ReaderDatabase : SQLiteOpenHelper
    {
        public ReaderDatabase() : base(global::Android.App.Application.Context, "zreader.db", null, 1) { }

        public override void OnCreate(SQLiteDatabase database)
        {
            database.ExecSQL("CREATE TABLE Books (Id INTEGER PRIMARY KEY AUTOINCREMENT, Title TEXT NOT NULL, SourceFileName TEXT NOT NULL, EncryptedRelativePath TEXT NOT NULL, ImportedAt TEXT NOT NULL, LastReadAt TEXT NOT NULL, ContentLength INTEGER NOT NULL, EncryptionVersion INTEGER NOT NULL)");
            database.ExecSQL("CREATE TABLE ReadingStates (BookId INTEGER PRIMARY KEY, CharacterOffset INTEGER NOT NULL, FontSize REAL NOT NULL, LineSpacing REAL NOT NULL, Theme INTEGER NOT NULL, UpdatedAt TEXT NOT NULL, FOREIGN KEY(BookId) REFERENCES Books(Id) ON DELETE CASCADE)");
        }

        public override void OnUpgrade(SQLiteDatabase database, int oldVersion, int newVersion) { }
    }
}
