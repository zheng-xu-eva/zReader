# Android Local TXT Reader Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build an offline Android .NET MAUI reader that imports encrypted TXT books, presents a local shelf, and restores reading progress and settings.

**Architecture:** A .NET MAUI Android app separates UI pages from application services. Text import, encoding detection, and encrypted private-file storage are isolated behind service interfaces; SQLite persists book metadata and per-book reading state. The reader uses a layout-independent character offset as its durable position and derives pages from that offset and current display settings.

**Tech Stack:** .NET 10, .NET MAUI XAML, Android Keystore, AES-GCM, SQLite-net-pcl, xUnit.

## Global Constraints

- Target only Android and .NET 10.
- Text files, source files, and project configuration use UTF-8.
- Scope is limited to TXT import, encrypted local shelf, reading, paging, progress, and per-book font size, line spacing, and light/dark theme.
- Import must copy data to application-private storage as AES-GCM encrypted content and must not retain a plaintext copy.
- Support automatic UTF-8, UTF-8 BOM, GBK, and GB18030 recognition; offer explicit encoding selection when recognition is inconclusive.
- Use Android Keystore to protect the AES key and never store the key in SQLite or a file.

---

## File Structure

- `src/ZReader/ZReader.csproj`: Android-only MAUI application project and packages.
- `src/ZReader/App.xaml`, `src/ZReader/App.xaml.cs`, `src/ZReader/MauiProgram.cs`: application resources, startup, dependency registration.
- `src/ZReader/Domain/Book.cs`, `src/ZReader/Domain/ReadingState.cs`, `src/ZReader/Domain/ReaderTheme.cs`: persisted domain models and settings enum.
- `src/ZReader/Services/ITextEncodingDetector.cs`, `TextEncodingDetector.cs`: detection and explicit decoding choices.
- `src/ZReader/Services/IEncryptedBookStore.cs`, `AndroidEncryptedBookStore.cs`: Keystore-backed AES-GCM storage.
- `src/ZReader/Services/IBookRepository.cs`, `SqliteBookRepository.cs`: book/state persistence.
- `src/ZReader/Services/IReaderPaginator.cs`, `ReaderPaginator.cs`: character-offset page boundaries and progress mapping.
- `src/ZReader/ViewModels/ShelfViewModel.cs`, `ReaderViewModel.cs`: UI commands and screen state.
- `src/ZReader/Pages/ShelfPage.xaml`, `ShelfPage.xaml.cs`: local shelf and import workflow.
- `src/ZReader/Pages/ReaderPage.xaml`, `ReaderPage.xaml.cs`: reading, touch navigation, progress, settings.
- `tests/ZReader.Core.Tests/ZReader.Core.Tests.csproj`: cross-platform service tests.
- `tests/ZReader.Core.Tests/TextEncodingDetectorTests.cs`, `EncryptedBookStoreContractTests.cs`, `ReaderPaginatorTests.cs`, `ReadingStateTests.cs`: core behavior tests.

### Task 1: Create the Android MAUI solution and core domain model

**Files:**
- Create: `ZReader.sln`
- Create: `src/ZReader/ZReader.csproj`
- Create: `src/ZReader/App.xaml`
- Create: `src/ZReader/App.xaml.cs`
- Create: `src/ZReader/MauiProgram.cs`
- Create: `src/ZReader/Domain/Book.cs`
- Create: `src/ZReader/Domain/ReadingState.cs`
- Create: `src/ZReader/Domain/ReaderTheme.cs`
- Create: `tests/ZReader.Core.Tests/ZReader.Core.Tests.csproj`
- Create: `tests/ZReader.Core.Tests/ReadingStateTests.cs`

**Interfaces:**
- Produces `Book`, `ReadingState`, and `ReaderTheme` types used by repositories and view models.
- `ReadingState` has `BookId`, `CharacterOffset`, `FontSize`, `LineSpacing`, `Theme`, and `UpdatedAt` properties.

- [ ] **Step 1: Write the failing state-default test**

```csharp
[Fact]
public void CreateDefault_uses_first_character_and_readable_defaults()
{
    var state = ReadingState.CreateDefault(bookId: 42, now: DateTimeOffset.UtcNow);

    Assert.Equal(42, state.BookId);
    Assert.Equal(0, state.CharacterOffset);
    Assert.InRange(state.FontSize, 14, 28);
    Assert.InRange(state.LineSpacing, 1.2, 2.2);
    Assert.Equal(ReaderTheme.Light, state.Theme);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/ZReader.Core.Tests/ZReader.Core.Tests.csproj --filter FullyQualifiedName~ReadingStateTests`

Expected: FAIL because the solution and `ReadingState` do not exist.

- [ ] **Step 3: Create the solution, Android MAUI project, test project, and domain types**

```csharp
public enum ReaderTheme { Light, Dark }

public sealed class ReadingState
{
    public static ReadingState CreateDefault(long bookId, DateTimeOffset now) => new()
    {
        BookId = bookId, CharacterOffset = 0, FontSize = 18,
        LineSpacing = 1.6, Theme = ReaderTheme.Light, UpdatedAt = now
    };
}
```

Set `TargetFrameworks` to `net10.0-android` and register XAML compilation. Keep Android permissions to only those required by the system document picker.

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/ZReader.Core.Tests/ZReader.Core.Tests.csproj --filter FullyQualifiedName~ReadingStateTests`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add ZReader.sln src/ZReader tests/ZReader.Core.Tests
git commit -m "chore: scaffold Android MAUI reader"
```

### Task 2: Implement text encoding recognition and manual decoding

**Files:**
- Create: `src/ZReader/Services/ITextEncodingDetector.cs`
- Create: `src/ZReader/Services/TextEncodingDetector.cs`
- Create: `tests/ZReader.Core.Tests/TextEncodingDetectorTests.cs`
- Modify: `src/ZReader/MauiProgram.cs`

**Interfaces:**
- Consumes raw TXT bytes from the import workflow.
- Produces `TextEncodingDetection Detect(ReadOnlySpan<byte> bytes)` and `string Decode(ReadOnlySpan<byte> bytes, TextEncodingChoice choice)`.
- `TextEncodingDetection` contains `SuggestedChoice`, `IsConfident`, and `AvailableChoices`.

- [ ] **Step 1: Write failing encoding tests**

```csharp
[Theory]
[InlineData("hello", TextEncodingChoice.Utf8)]
public void Detect_identifies_valid_utf8(string value, TextEncodingChoice expected)
{
    var result = _detector.Detect(Encoding.UTF8.GetBytes(value));
    Assert.True(result.IsConfident);
    Assert.Equal(expected, result.SuggestedChoice);
}

[Fact]
public void Detect_marks_invalid_utf8_as_manual_choice_required()
{
    var result = _detector.Detect(new byte[] { 0xFF, 0x81, 0x40 });
    Assert.False(result.IsConfident);
    Assert.Contains(TextEncodingChoice.Gb18030, result.AvailableChoices);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/ZReader.Core.Tests/ZReader.Core.Tests.csproj --filter FullyQualifiedName~TextEncodingDetectorTests`

Expected: FAIL because `ITextEncodingDetector` does not exist.

- [ ] **Step 3: Implement strict UTF-8/BOM detection and deterministic fallback choices**

```csharp
public interface ITextEncodingDetector
{
    TextEncodingDetection Detect(ReadOnlySpan<byte> bytes);
    string Decode(ReadOnlySpan<byte> bytes, TextEncodingChoice choice);
}
```

Use UTF-8 configured to throw on invalid byte sequences. Detect BOM before generic UTF-8. When UTF-8 is invalid, return `IsConfident = false` and expose GBK and GB18030 for the user selection; decode the selected encoding with exception fallback disabled. Register code page support once during application startup.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/ZReader.Core.Tests/ZReader.Core.Tests.csproj --filter FullyQualifiedName~TextEncodingDetectorTests`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/ZReader/Services tests/ZReader.Core.Tests/TextEncodingDetectorTests.cs src/ZReader/MauiProgram.cs
git commit -m "feat: detect TXT encodings"
```

### Task 3: Implement encrypted private book storage

**Files:**
- Create: `src/ZReader/Services/IEncryptedBookStore.cs`
- Create: `src/ZReader/Services/AndroidEncryptedBookStore.cs`
- Create: `src/ZReader/Services/EncryptedBookFormat.cs`
- Create: `tests/ZReader.Core.Tests/EncryptedBookStoreContractTests.cs`
- Modify: `src/ZReader/MauiProgram.cs`

**Interfaces:**
- Consumes a decoded `string`, `bookId`, and Android application-private root.
- Produces `Task<string> WriteAsync(long bookId, string content, CancellationToken)` and `Task<string> ReadAsync(string relativePath, CancellationToken)`.
- `WriteAsync` returns a relative encrypted path. `ReadAsync` throws `CryptographicException` if authentication fails.

- [ ] **Step 1: Write failing encryption format tests**

```csharp
[Fact]
public void Encrypt_then_decrypt_returns_original_text()
{
    var payload = EncryptedBookFormat.EncryptForTest("正文", key, nonce);
    Assert.Equal("正文", EncryptedBookFormat.DecryptForTest(payload, key));
}

[Fact]
public void Decrypt_rejects_modified_ciphertext()
{
    var payload = EncryptedBookFormat.EncryptForTest("正文", key, nonce);
    payload[^1] ^= 1;
    Assert.Throws<CryptographicException>(() => EncryptedBookFormat.DecryptForTest(payload, key));
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/ZReader.Core.Tests/ZReader.Core.Tests.csproj --filter FullyQualifiedName~EncryptedBookStoreContractTests`

Expected: FAIL because `EncryptedBookFormat` does not exist.

- [ ] **Step 3: Implement versioned AES-GCM format and Android Keystore key retrieval**

```csharp
public interface IEncryptedBookStore
{
    Task<string> WriteAsync(long bookId, string content, CancellationToken cancellationToken);
    Task<string> ReadAsync(string relativePath, CancellationToken cancellationToken);
    Task DeleteAsync(string relativePath, CancellationToken cancellationToken);
}
```

Define a binary format containing a fixed magic value, format version, nonce length, tag length, nonce, tag, and ciphertext. Generate a 12-byte random nonce for every write. On Android, create or obtain a non-exportable AES key using `Android.Security.Keystore.KeyGenParameterSpec`; use it to encrypt/decrypt and write only to `FileSystem.AppDataDirectory/books`. Do not create temporary plaintext files.

- [ ] **Step 4: Run tests and build Android target**

Run: `dotnet test tests/ZReader.Core.Tests/ZReader.Core.Tests.csproj --filter FullyQualifiedName~EncryptedBookStoreContractTests`

Expected: PASS.

Run: `dotnet build src/ZReader/ZReader.csproj -f net10.0-android`

Expected: Build succeeds.

- [ ] **Step 5: Commit**

```powershell
git add src/ZReader/Services tests/ZReader.Core.Tests/EncryptedBookStoreContractTests.cs src/ZReader/MauiProgram.cs
git commit -m "feat: encrypt imported book content"
```

### Task 4: Persist the shelf and per-book reading state

**Files:**
- Create: `src/ZReader/Services/IBookRepository.cs`
- Create: `src/ZReader/Services/SqliteBookRepository.cs`
- Create: `tests/ZReader.Core.Tests/BookRepositoryTests.cs`
- Modify: `src/ZReader/MauiProgram.cs`

**Interfaces:**
- Produces `Task<Book> AddBookAsync(BookDraft draft, CancellationToken)`, `Task<IReadOnlyList<Book>> GetShelfAsync(CancellationToken)`, `Task<ReadingState> GetReadingStateAsync(long bookId, CancellationToken)`, and `Task SaveReadingStateAsync(ReadingState state, CancellationToken)`.
- Shelf results are sorted by `LastReadAt` descending, then `ImportedAt` descending.

- [ ] **Step 1: Write a failing persistence round-trip test**

```csharp
[Fact]
public async Task SaveReadingState_persists_per_book_preferences_and_offset()
{
    var book = await _repository.AddBookAsync(_draft, CancellationToken.None);
    await _repository.SaveReadingStateAsync(new ReadingState
    {
        BookId = book.Id, CharacterOffset = 321, FontSize = 22,
        LineSpacing = 1.8, Theme = ReaderTheme.Dark, UpdatedAt = _now
    }, CancellationToken.None);

    var state = await _repository.GetReadingStateAsync(book.Id, CancellationToken.None);
    Assert.Equal(321, state.CharacterOffset);
    Assert.Equal(ReaderTheme.Dark, state.Theme);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/ZReader.Core.Tests/ZReader.Core.Tests.csproj --filter FullyQualifiedName~BookRepositoryTests`

Expected: FAIL because `IBookRepository` does not exist.

- [ ] **Step 3: Implement SQLite tables and repository operations**

```csharp
public interface IBookRepository
{
    Task<Book> AddBookAsync(BookDraft draft, CancellationToken cancellationToken);
    Task<IReadOnlyList<Book>> GetShelfAsync(CancellationToken cancellationToken);
    Task<ReadingState> GetReadingStateAsync(long bookId, CancellationToken cancellationToken);
    Task SaveReadingStateAsync(ReadingState state, CancellationToken cancellationToken);
}
```

Add unique `Book.Id` and `ReadingState.BookId` indexes. Create a default `ReadingState` in the same database transaction as the Book. `SaveReadingStateAsync` updates existing state and Book.LastReadAt. Use the SQLite connection asynchronously and keep the database in `FileSystem.AppDataDirectory`.

- [ ] **Step 4: Run repository tests**

Run: `dotnet test tests/ZReader.Core.Tests/ZReader.Core.Tests.csproj --filter FullyQualifiedName~BookRepositoryTests`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/ZReader/Services tests/ZReader.Core.Tests/BookRepositoryTests.cs src/ZReader/MauiProgram.cs
git commit -m "feat: persist local book shelf"
```

### Task 5: Add deterministic character-offset pagination and progress calculation

**Files:**
- Create: `src/ZReader/Services/IReaderPaginator.cs`
- Create: `src/ZReader/Services/ReaderPaginator.cs`
- Create: `src/ZReader/Services/ReaderPageSlice.cs`
- Create: `tests/ZReader.Core.Tests/ReaderPaginatorTests.cs`

**Interfaces:**
- Consumes `string content`, a target character count per page, and a character offset.
- Produces `ReaderPageSlice GetPage(string content, int targetCharacterCount, long offset)`, `long GetNextOffset(...)`, `long GetPreviousOffset(...)`, and `double GetProgress(long offset, int contentLength)`.
- `ReaderPageSlice` has `StartOffset`, `EndOffset`, and `Text`.

- [ ] **Step 1: Write failing pagination tests**

```csharp
[Fact]
public void GetPage_prefers_a_paragraph_boundary_before_page_limit()
{
    var page = _paginator.GetPage("甲乙丙\n\n丁戊己", targetCharacterCount: 5, offset: 0);
    Assert.Equal("甲乙丙", page.Text);
    Assert.Equal(3, page.EndOffset);
}

[Theory]
[InlineData(0, 100, 0d)]
[InlineData(50, 100, .5d)]
[InlineData(200, 100, 1d)]
public void GetProgress_clamps_to_valid_range(long offset, int length, double expected)
    => Assert.Equal(expected, _paginator.GetProgress(offset, length));
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/ZReader.Core.Tests/ZReader.Core.Tests.csproj --filter FullyQualifiedName~ReaderPaginatorTests`

Expected: FAIL because `IReaderPaginator` does not exist.

- [ ] **Step 3: Implement pure pagination functions**

```csharp
public interface IReaderPaginator
{
    ReaderPageSlice GetPage(string content, int targetCharacterCount, long offset);
    long GetNextOffset(string content, int targetCharacterCount, long offset);
    long GetPreviousOffset(string content, int targetCharacterCount, long offset);
    double GetProgress(long offset, int contentLength);
}
```

Clamp offsets to `[0, content.Length]`. Prefer a blank-line paragraph boundary before the target length; otherwise use the target length. Compute previous offsets by walking pages from zero for predictable correctness in the first implementation. UI code calculates `targetCharacterCount` from reader bounds, font size, and line spacing.

- [ ] **Step 4: Run pagination tests**

Run: `dotnet test tests/ZReader.Core.Tests/ZReader.Core.Tests.csproj --filter FullyQualifiedName~ReaderPaginatorTests`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/ZReader/Services tests/ZReader.Core.Tests/ReaderPaginatorTests.cs
git commit -m "feat: paginate reader content"
```

### Task 6: Wire import workflow and local shelf UI

**Files:**
- Create: `src/ZReader/ViewModels/ShelfViewModel.cs`
- Create: `src/ZReader/Pages/ShelfPage.xaml`
- Create: `src/ZReader/Pages/ShelfPage.xaml.cs`
- Modify: `src/ZReader/App.xaml.cs`
- Modify: `src/ZReader/MauiProgram.cs`

**Interfaces:**
- Consumes `FilePicker`, `ITextEncodingDetector`, `IEncryptedBookStore`, and `IBookRepository`.
- Produces `ImportCommand`, `Books`, `IsBusy`, and `ImportError` properties on `ShelfViewModel`.
- Navigates to `ReaderPage` with the selected `Book.Id`.

- [ ] **Step 1: Write a failing import orchestration test with fakes**

```csharp
[Fact]
public async Task Import_with_confirmed_encoding_creates_encrypted_book_and_shelf_entry()
{
    await _viewModel.ImportAsync(_pickedFile, TextEncodingChoice.Gb18030);
    Assert.Single(_repository.Books);
    Assert.Equal("book.zr", _repository.Books[0].EncryptedRelativePath);
    Assert.Equal("内容", _store.LastWrittenContent);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/ZReader.Core.Tests/ZReader.Core.Tests.csproj --filter FullyQualifiedName~ShelfViewModelTests`

Expected: FAIL because `ShelfViewModel` does not exist.

- [ ] **Step 3: Implement import command and XAML shelf**

```csharp
public async Task ImportAsync(FileResult file, TextEncodingChoice? selectedEncoding)
{
    await using var input = await file.OpenReadAsync();
    var bytes = await ReadAllBytesAsync(input, _cancellationToken);
    var detection = _encodingDetector.Detect(bytes);
    var choice = detection.IsConfident ? detection.SuggestedChoice : selectedEncoding
        ?? throw new EncodingSelectionRequiredException(detection.AvailableChoices);
    var content = _encodingDetector.Decode(bytes, choice);
    // Create the database record, encrypt content, then update its path; remove encrypted output on failures.
}
```

Build a shelf page with a compact book list, import toolbar button, error message region, and a modal choice for GBK or GB18030 only when automatic detection is inconclusive. Use filename without extension as the title. Reload shelf when navigation returns from reader.

- [ ] **Step 4: Run unit test and Android build**

Run: `dotnet test tests/ZReader.Core.Tests/ZReader.Core.Tests.csproj --filter FullyQualifiedName~ShelfViewModelTests`

Expected: PASS.

Run: `dotnet build src/ZReader/ZReader.csproj -f net10.0-android`

Expected: Build succeeds.

- [ ] **Step 5: Commit**

```powershell
git add src/ZReader/ViewModels src/ZReader/Pages src/ZReader/App.xaml.cs src/ZReader/MauiProgram.cs tests/ZReader.Core.Tests
git commit -m "feat: import TXT books to local shelf"
```

### Task 7: Implement reader screen, gesture/tap paging, progress, and settings

**Files:**
- Create: `src/ZReader/ViewModels/ReaderViewModel.cs`
- Create: `src/ZReader/Pages/ReaderPage.xaml`
- Create: `src/ZReader/Pages/ReaderPage.xaml.cs`
- Create: `tests/ZReader.Core.Tests/ReaderViewModelTests.cs`
- Modify: `src/ZReader/Pages/ShelfPage.xaml.cs`
- Modify: `src/ZReader/MauiProgram.cs`

**Interfaces:**
- Consumes `IBookRepository`, `IEncryptedBookStore`, and `IReaderPaginator`.
- Produces `Task LoadAsync(long bookId)`, `Task NextPageAsync()`, `Task PreviousPageAsync()`, `Task SeekAsync(double progress)`, and `Task SaveSettingsAsync(double fontSize, double lineSpacing, ReaderTheme theme)`.
- Exposes `PageText`, `Progress`, `FontSize`, `LineSpacing`, `Theme`, and `ErrorMessage` for bindings.

- [ ] **Step 1: Write failing reader behavior tests**

```csharp
[Fact]
public async Task NextPage_persists_the_new_character_offset()
{
    await _viewModel.LoadAsync(bookId: 1);
    await _viewModel.NextPageAsync();
    Assert.True(_repository.LastSavedState.CharacterOffset > 0);
}

[Fact]
public async Task SaveSettings_keeps_current_character_offset()
{
    await _viewModel.LoadAsync(bookId: 1);
    var offset = _viewModel.CurrentOffset;
    await _viewModel.SaveSettingsAsync(22, 1.8, ReaderTheme.Dark);
    Assert.Equal(offset, _repository.LastSavedState.CharacterOffset);
    Assert.Equal(ReaderTheme.Dark, _repository.LastSavedState.Theme);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/ZReader.Core.Tests/ZReader.Core.Tests.csproj --filter FullyQualifiedName~ReaderViewModelTests`

Expected: FAIL because `ReaderViewModel` does not exist.

- [ ] **Step 3: Implement the reader view model and XAML page**

```csharp
public async Task NextPageAsync()
{
    CurrentOffset = _paginator.GetNextOffset(_content, PageCharacterCount, CurrentOffset);
    await RefreshAndPersistAsync();
}

public async Task SaveSettingsAsync(double fontSize, double lineSpacing, ReaderTheme theme)
{
    FontSize = fontSize; LineSpacing = lineSpacing; Theme = theme;
    await RefreshAndPersistAsync();
}
```

Bind a center reading text area, a bottom progress slider, and a compact settings panel containing sliders for font size and line spacing plus a light/dark segmented selector. Handle left/right taps by comparing the tap X coordinate to the page width; handle swipe threshold gestures using the same `NextPageAsync` and `PreviousPageAsync` methods. Recalculate page character capacity when the reading area size or settings change, then reload from the existing `CurrentOffset`.

- [ ] **Step 4: Run reader tests and Android build**

Run: `dotnet test tests/ZReader.Core.Tests/ZReader.Core.Tests.csproj --filter FullyQualifiedName~ReaderViewModelTests`

Expected: PASS.

Run: `dotnet build src/ZReader/ZReader.csproj -f net10.0-android`

Expected: Build succeeds.

- [ ] **Step 5: Commit**

```powershell
git add src/ZReader/ViewModels/ReaderViewModel.cs src/ZReader/Pages/ReaderPage.xaml src/ZReader/Pages/ReaderPage.xaml.cs tests/ZReader.Core.Tests/ReaderViewModelTests.cs src/ZReader/Pages/ShelfPage.xaml.cs src/ZReader/MauiProgram.cs
git commit -m "feat: add offline reading experience"
```

### Task 8: Verify complete Android workflow

**Files:**
- Modify: `README.md`

**Interfaces:**
- Produces documented build prerequisites and repeatable manual verification steps.

- [ ] **Step 1: Write the acceptance checklist in README**

```markdown
1. Install the APK on an Android 10+ device or emulator.
2. Import UTF-8, GBK, and GB18030 TXT samples; when detection is inconclusive, select the intended encoding.
3. Confirm each title appears in the shelf, then remove the source TXT outside the app and reopen the book.
4. Use a left tap, right tap, and horizontal swipe; confirm navigation and progress agree.
5. Change font size, line spacing, and theme; force-stop and reopen the app; confirm the same book restores all values and location.
```

- [ ] **Step 2: Run all automated tests**

Run: `dotnet test tests/ZReader.Core.Tests/ZReader.Core.Tests.csproj`

Expected: PASS with all encoding, encryption, persistence, pagination, import, and reader tests passing.

- [ ] **Step 3: Build a release APK**

Run: `dotnet publish src/ZReader/ZReader.csproj -f net10.0-android -c Release -p:AndroidPackageFormats=apk`

Expected: Publish succeeds and writes an APK under `src/ZReader/bin/Release/net10.0-android/publish`.

- [ ] **Step 4: Perform the README acceptance checklist on an Android emulator or device**

Expected: All six listed behaviors work, including opening a book after its original TXT has been deleted.

- [ ] **Step 5: Commit**

```powershell
git add README.md
git commit -m "docs: document reader verification"
```

## Self-Review

- Spec coverage: Tasks 1-7 cover Android MAUI, SQLite shelf/state, encrypted private storage, encoding detection/manual selection, both paging inputs, character-offset progress, and per-book settings. Task 8 covers end-to-end verification.
- Completeness scan: every task specifies concrete files, interfaces, test commands, and expected outcomes.
- Type consistency: All later task interfaces use the domain types and service signatures defined in Tasks 1-5; UI tasks consume only those interfaces.
