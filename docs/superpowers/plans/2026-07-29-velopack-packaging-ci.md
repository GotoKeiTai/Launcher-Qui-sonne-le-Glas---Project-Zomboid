# Packaging Velopack & CI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Package GlasLauncher with Velopack (installer + auto-update), replace `FakeUpdateService` with a real Velopack-backed `IUpdateService`, and stand up two GitHub Actions workflows — CI (build+test on every push/PR) and Release (package+publish on a pushed `vX.Y.Z` tag).

**Architecture:** `VelopackApp.Build().Run()` is added as the very first line of `Program.cs` (mandatory Velopack bootstrap). A new pure `UpdateNotesParser` (in `Logic/`) turns a release's markdown notes into the `IReadOnlyList<string>` shape `UpdateModalView` already renders — unit-testable without Velopack/network. `VelopackUpdateService` (in `Services/`) wraps `Velopack.UpdateManager` + `GithubSource`, implementing the (slightly extended) `IUpdateService` interface; like `SteamEnvironment`/`JavaModService`, it has no dedicated tests (real network + Windows install state) and is registered real-on-Windows / fake-elsewhere in DI, same pattern as every other Windows service in this codebase. Two new files under `.github/workflows/` provide CI and Release automation — no C# code, verified by making a real workflow run.

**Tech Stack:** C# / .NET 8, `Velopack` 1.2.0 (NuGet), GitHub Actions (`windows-latest` runners), `vpk` CLI (dotnet global tool), xUnit.

## Global Constraints

- Steam AppId / branch / buildid work is unrelated to this plan — do not touch `SteamEnvironment`, `SteamLibraryLocator`, or any Steam-facing file.
- **No code signing in this plan** — hash + HTTPS only, SmartScreen warning is expected and documented for testers (cahier des charges §8.4). SignPath.io integration is explicitly out of scope, planned for public launch.
- `GlasLauncher.Core` has `ImplicitUsings` enabled (`System`, `System.Collections.Generic`, `System.IO`, `System.Linq`, `System.Threading.Tasks` available without explicit `using`). `GlasLauncher.App` does **not** — explicit usings required there.
- DI pattern for every Windows-real/Fake-elsewhere service in this codebase: `services.AddSingleton<IInterface>(sp => OperatingSystem.IsWindows() ? new RealImpl(...) : new FakeImpl());` registered in `RegisterServices` (`src/GlasLauncher.App/App.axaml.cs`). `VelopackUpdateService` follows this exactly.
- `IUpdateService.CheckForUpdateAsync()` must never throw — same never-throw contract as every other `GetStatusAsync()`-style method in this codebase (`SteamEnvironment`, `JavaModService`, `JavaModManifestFetcher`). `ApplyUpdateAsync()` may throw — `UpdateModalViewModel` already has error-handling UI for that, do not add new error handling there.
- Velopack API surface used in this plan (verified against `docs.velopack.io`, not guessed):
  - `Velopack.VelopackApp.Build().Run()` — call first, before any other startup code.
  - `Velopack.Sources.GithubSource(string repoUrl, string? accessToken, bool prerelease)`.
  - `Velopack.UpdateManager(IUpdateSource source)` — `CheckForUpdatesAsync() : Task<UpdateInfo?>` (null = no update), `CurrentVersion : SemanticVersion?`, `DownloadUpdatesAsync(UpdateInfo, Action<int>? progress, CancellationToken)`, `ApplyUpdatesAndRestart(VelopackAsset? toApply, string[]? restartArgs)`.
  - `Velopack.UpdateInfo.TargetFullRelease : VelopackAsset`.
  - `Velopack.VelopackAsset` — `Version : SemanticVersion`, `NotesMarkdown : string`.
- Repo URL constant (GitHub repo, already public as of this plan — see Task 7's prerequisite note): `https://github.com/GotoKeiTai/Launcher-Qui-sonne-le-Glas---Project-Zomboid`.
- Velopack `packId`: `GlasLauncher` (produces `GlasLauncher-win-Setup.exe`, matching the cahier des charges' named artifact).
- This plan intentionally only fixes the **launcher's own** hardcoded version string. `SettingsViewModel.cs`'s diagnostic clipboard text also hardcodes a game version (`41.78.16`) and mod version (`v1.0.0`) — those stay untouched, they are pre-existing gaps unrelated to Velopack.
- No dedicated unit tests for `VelopackUpdateService` itself (network + Windows install-state orchestration) — consistent with the project's established convention for this class of service. Verified by build success and the manual testing pass described at the end of this plan.

---

### Task 1: Add the Velopack package and startup hook

**Files:**
- Modify: `src/GlasLauncher.App/GlasLauncher.App.csproj`
- Modify: `src/GlasLauncher.App/Program.cs`

**Interfaces:**
- Produces: nothing consumed by later tasks — purely the mandatory Velopack app-lifecycle bootstrap.

- [ ] **Step 1: Add the Velopack package reference**

In `src/GlasLauncher.App/GlasLauncher.App.csproj`, add to the existing `<ItemGroup>` containing `PackageReference` entries (after `Microsoft.Extensions.DependencyInjection`):

```xml
    <PackageReference Include="Velopack" Version="1.2.0" />
```

- [ ] **Step 2: Add the `VelopackApp` startup hook**

Replace the full content of `src/GlasLauncher.App/Program.cs`:

```csharp
using Avalonia;
using System;
using Velopack;

namespace GlasLauncher.App;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Must run first, before anything else — handles Velopack's install/update/uninstall
        // lifecycle hooks (e.g. creating shortcuts on first install).
        VelopackApp.Build().Run();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
```

- [ ] **Step 3: Build to verify**

Run: `dotnet build src/GlasLauncher.App`
Expected: Build succeeded, 0 errors, 0 warnings (NuGet restore pulls in `Velopack` 1.2.0).

- [ ] **Step 4: Commit**

```bash
git add src/GlasLauncher.App/GlasLauncher.App.csproj src/GlasLauncher.App/Program.cs
git commit -m "feat(app): add Velopack package and startup hook"
```

---

### Task 2: `UpdateNotesParser` — markdown release notes to bullet list (TDD)

**Files:**
- Create: `src/GlasLauncher.Core/Logic/UpdateNotesParser.cs`
- Test: `tests/GlasLauncher.Core.Tests/UpdateNotesParserTests.cs`

**Interfaces:**
- Produces: `static class UpdateNotesParser { public static IReadOnlyList<string> Parse(string notesMarkdown); }` — consumed by `VelopackUpdateService` (Task 3).

- [ ] **Step 1: Write the failing tests**

Create `tests/GlasLauncher.Core.Tests/UpdateNotesParserTests.cs`:

```csharp
using GlasLauncher.Core.Logic;
using Xunit;

namespace GlasLauncher.Core.Tests;

public class UpdateNotesParserTests
{
    [Fact]
    public void Parse_SingleBulletLine_ReturnsOneEntryWithoutMarker()
    {
        var result = UpdateNotesParser.Parse("- Correction du crash au lancement");

        Assert.Single(result);
        Assert.Equal("Correction du crash au lancement", result[0]);
    }

    [Fact]
    public void Parse_MultipleBulletLines_ReturnsOneEntryPerLine()
    {
        var result = UpdateNotesParser.Parse("- Première ligne\n- Deuxième ligne\n* Troisième ligne");

        Assert.Equal(3, result.Count);
        Assert.Equal("Première ligne", result[0]);
        Assert.Equal("Deuxième ligne", result[1]);
        Assert.Equal("Troisième ligne", result[2]);
    }

    [Fact]
    public void Parse_WindowsLineEndings_StripsCarriageReturn()
    {
        var result = UpdateNotesParser.Parse("- Première ligne\r\n- Deuxième ligne\r\n");

        Assert.Equal(2, result.Count);
        Assert.Equal("Première ligne", result[0]);
        Assert.Equal("Deuxième ligne", result[1]);
    }

    [Fact]
    public void Parse_BlankLinesBetweenEntries_SkipsEmptyLines()
    {
        var result = UpdateNotesParser.Parse("- Première ligne\n\n\n- Deuxième ligne");

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Parse_LineWithoutBulletMarker_ReturnsLineAsIs()
    {
        var result = UpdateNotesParser.Parse("Ligne sans puce");

        Assert.Single(result);
        Assert.Equal("Ligne sans puce", result[0]);
    }

    [Fact]
    public void Parse_EmptyString_ReturnsEmptyList()
    {
        var result = UpdateNotesParser.Parse("");

        Assert.Empty(result);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/GlasLauncher.Core.Tests --filter UpdateNotesParserTests`
Expected: FAIL (or build error) — `UpdateNotesParser` does not exist yet.

- [ ] **Step 3: Implement `UpdateNotesParser`**

Create `src/GlasLauncher.Core/Logic/UpdateNotesParser.cs`:

```csharp
namespace GlasLauncher.Core.Logic;

public static class UpdateNotesParser
{
    public static IReadOnlyList<string> Parse(string notesMarkdown) =>
        notesMarkdown
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.TrimStart('-', '*', ' '))
            .Where(line => line.Length > 0)
            .ToList();
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/GlasLauncher.Core.Tests --filter UpdateNotesParserTests`
Expected: PASS — all 6 tests green.

- [ ] **Step 5: Commit**

```bash
git add src/GlasLauncher.Core/Logic/UpdateNotesParser.cs tests/GlasLauncher.Core.Tests/UpdateNotesParserTests.cs
git commit -m "feat(core): add UpdateNotesParser for release-notes markdown"
```

---

### Task 3: Extend `IUpdateService`, update `FakeUpdateService`, add `VelopackUpdateService`

**Files:**
- Modify: `src/GlasLauncher.Core/Services/IUpdateService.cs`
- Modify: `src/GlasLauncher.Core/Services/Fakes/FakeUpdateService.cs`
- Create: `src/GlasLauncher.Core/Services/VelopackUpdateService.cs`

**Interfaces:**
- Consumes: `UpdateNotesParser.Parse(string) : IReadOnlyList<string>` (Task 2), existing `UpdateInfo` model (`GlasLauncher.Core.Models`, unchanged shape).
- Produces: `IUpdateService.GetCurrentVersion() : string` (new interface member), `class VelopackUpdateService : IUpdateService` — both consumed by DI wiring (Task 4) and the ViewModels (Task 5).

Current `IUpdateService.cs` (for reference — you are replacing this file):

```csharp
using GlasLauncher.Core.Models;

namespace GlasLauncher.Core.Services;

public interface IUpdateService
{
    Task<UpdateInfo?> CheckForUpdateAsync();
    Task ApplyUpdateAsync();
}
```

Current `FakeUpdateService.cs` (for reference — you are modifying this file):

```csharp
using GlasLauncher.Core.Models;

namespace GlasLauncher.Core.Services.Fakes;

public class FakeUpdateService : IUpdateService
{
    // No update available: kept as a no-update fake until a real Velopack-backed
    // IUpdateService exists (see docs/session-notes.md, sub-project #3).
    public Task<UpdateInfo?> CheckForUpdateAsync() => Task.FromResult<UpdateInfo?>(null);

    public async Task ApplyUpdateAsync()
    {
        await Task.Delay(500);
    }
}
```

- [ ] **Step 1: Add the Velopack package reference to `GlasLauncher.Core`**

`VelopackUpdateService` (Step 6 below) lives in `GlasLauncher.Core`, not `GlasLauncher.App` — PackageReferences do not flow downstream from a referencing project to the project it references (`GlasLauncher.App` → `GlasLauncher.Core`), so `GlasLauncher.Core` needs its own direct reference to the `Velopack` package (the one added to `GlasLauncher.App` in Task 1 only covers `Program.cs`'s use of `VelopackApp` in the App project).

In `src/GlasLauncher.Core/GlasLauncher.Core.csproj`, add to the existing `<ItemGroup>` containing `PackageReference` entries:

```xml
    <PackageReference Include="Velopack" Version="1.2.0" />
```

- [ ] **Step 2: Add `GetCurrentVersion()` to `IUpdateService`**

Replace the full content of `src/GlasLauncher.Core/Services/IUpdateService.cs`:

```csharp
using GlasLauncher.Core.Models;

namespace GlasLauncher.Core.Services;

public interface IUpdateService
{
    Task<UpdateInfo?> CheckForUpdateAsync();
    Task ApplyUpdateAsync();
    string GetCurrentVersion();
}
```

- [ ] **Step 3: Implement `GetCurrentVersion()` on `FakeUpdateService`**

In `src/GlasLauncher.Core/Services/Fakes/FakeUpdateService.cs`, add this method to the class (after `ApplyUpdateAsync`, before the closing brace):

```csharp
    public string GetCurrentVersion() => "0.1.0-dev";
```

- [ ] **Step 4: Build to verify the Fake still compiles**

Run: `dotnet build src/GlasLauncher.Core`
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Run the existing Fake update service tests**

Run: `dotnet test tests/GlasLauncher.Core.Tests --filter FakeUpdateServiceTests`
Expected: PASS — no regressions (existing tests don't reference `GetCurrentVersion`, still valid).

- [ ] **Step 6: Implement `VelopackUpdateService`**

Create `src/GlasLauncher.Core/Services/VelopackUpdateService.cs`:

```csharp
using GlasLauncher.Core.Logic;
using GlasLauncher.Core.Models;
using Velopack;
using Velopack.Sources;

namespace GlasLauncher.Core.Services;

public class VelopackUpdateService : IUpdateService
{
    private const string RepoUrl = "https://github.com/GotoKeiTai/Launcher-Qui-sonne-le-Glas---Project-Zomboid";

    private readonly UpdateManager _manager = new(new GithubSource(RepoUrl, accessToken: null, prerelease: false));
    private Velopack.UpdateInfo? _pendingUpdate;

    public async Task<UpdateInfo?> CheckForUpdateAsync()
    {
        try
        {
            _pendingUpdate = await _manager.CheckForUpdatesAsync();
        }
        catch (Exception)
        {
            return null;
        }

        if (_pendingUpdate is null)
        {
            return null;
        }

        return new UpdateInfo(
            CurrentVersion: GetCurrentVersion(),
            LatestVersion: _pendingUpdate.TargetFullRelease.Version.ToString(),
            ChangelogEntries: UpdateNotesParser.Parse(_pendingUpdate.TargetFullRelease.NotesMarkdown));
    }

    public async Task ApplyUpdateAsync()
    {
        if (_pendingUpdate is null)
        {
            throw new InvalidOperationException("Aucune mise à jour en attente.");
        }

        await _manager.DownloadUpdatesAsync(_pendingUpdate);
        _manager.ApplyUpdatesAndRestart(_pendingUpdate.TargetFullRelease);
    }

    public string GetCurrentVersion() => _manager.CurrentVersion?.ToString() ?? "dev";
}
```

- [ ] **Step 7: Build to verify**

Run: `dotnet build src/GlasLauncher.Core`
Expected: Build succeeded, 0 errors, 0 warnings.

- [ ] **Step 8: Run the full test suite**

Run: `dotnet test tests/GlasLauncher.Core.Tests`
Expected: PASS — no regressions (no new tests added for `VelopackUpdateService` itself, per the no-test convention for this class of service — `UpdateNotesParserTests` and `FakeUpdateServiceTests` cover everything testable here).

- [ ] **Step 9: Commit**

```bash
git add src/GlasLauncher.Core/GlasLauncher.Core.csproj src/GlasLauncher.Core/Services/IUpdateService.cs src/GlasLauncher.Core/Services/Fakes/FakeUpdateService.cs src/GlasLauncher.Core/Services/VelopackUpdateService.cs
git commit -m "feat(core): add real VelopackUpdateService implementation of IUpdateService"
```

---

### Task 4: Wire `VelopackUpdateService` into DI

**Files:**
- Modify: `src/GlasLauncher.App/App.axaml.cs`

**Interfaces:**
- Consumes: `VelopackUpdateService` (Task 3), existing `FakeUpdateService`.

- [ ] **Step 1: Replace the `IUpdateService` registration**

In `src/GlasLauncher.App/App.axaml.cs`, replace:

```csharp
        services.AddSingleton<IUpdateService, FakeUpdateService>();
```

with:

```csharp
        services.AddSingleton<IUpdateService>(_ =>
            OperatingSystem.IsWindows()
                ? new VelopackUpdateService()
                : new FakeUpdateService());
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build src/GlasLauncher.App`
Expected: Build succeeded, 0 errors, 0 warnings.

- [ ] **Step 3: Commit**

```bash
git add src/GlasLauncher.App/App.axaml.cs
git commit -m "feat(app): register real VelopackUpdateService on Windows, Fake elsewhere"
```

---

### Task 5: Show the real launcher version instead of a hardcoded string

**Files:**
- Modify: `src/GlasLauncher.App/ViewModels/DashboardViewModel.cs`
- Modify: `src/GlasLauncher.App/Views/DashboardView.axaml`
- Modify: `src/GlasLauncher.App/ViewModels/SettingsViewModel.cs`

**Interfaces:**
- Consumes: `IUpdateService.GetCurrentVersion() : string` (Task 3).

- [ ] **Step 1: Inject `IUpdateService` into `DashboardViewModel` and expose the version**

In `src/GlasLauncher.App/ViewModels/DashboardViewModel.cs`, replace:

```csharp
    private readonly ISteamEnvironment _steamEnvironment;
    private readonly IServerInfoService _serverInfoService;
    private readonly IJavaModService _javaModService;

    public DashboardViewModel(ISteamEnvironment steamEnvironment, IServerInfoService serverInfoService, IJavaModService javaModService)
    {
        _steamEnvironment = steamEnvironment;
        _serverInfoService = serverInfoService;
        _javaModService = javaModService;
        Checks = new ObservableCollection<CheckItemViewModel>();
        News = new ObservableCollection<NewsItem>();

        _ = RefreshAsync();
    }
```

with:

```csharp
    private readonly ISteamEnvironment _steamEnvironment;
    private readonly IServerInfoService _serverInfoService;
    private readonly IJavaModService _javaModService;
    private readonly IUpdateService _updateService;

    public DashboardViewModel(
        ISteamEnvironment steamEnvironment,
        IServerInfoService serverInfoService,
        IJavaModService javaModService,
        IUpdateService updateService)
    {
        _steamEnvironment = steamEnvironment;
        _serverInfoService = serverInfoService;
        _javaModService = javaModService;
        _updateService = updateService;
        Checks = new ObservableCollection<CheckItemViewModel>();
        News = new ObservableCollection<NewsItem>();
        LauncherVersionText = _updateService.GetCurrentVersion();

        _ = RefreshAsync();
    }
```

Then add the new observable property next to the other `[ObservableProperty]` fields (e.g. right after the `_workshopSubscribeUrl` field):

```csharp
    [ObservableProperty]
    private string _launcherVersionText = "";
```

- [ ] **Step 2: Bind the footer to the real version**

In `src/GlasLauncher.App/Views/DashboardView.axaml`, replace:

```xml
            <TextBlock Text="Launcher" Foreground="{StaticResource InkFaintBrush}" FontSize="11" />
            <TextBlock Grid.Column="1" Text="v0.1.0" Foreground="{StaticResource InkDimBrush}" FontSize="11" />
```

with:

```xml
            <TextBlock Text="Launcher" Foreground="{StaticResource InkFaintBrush}" FontSize="11" />
            <TextBlock Grid.Column="1" Text="{Binding LauncherVersionText}" Foreground="{StaticResource InkDimBrush}" FontSize="11" />
```

(This is the "Launcher" row specifically — the two rows below it, "Project Zomboid" and "Mod Java", are unrelated to this plan and must stay untouched.)

- [ ] **Step 3: Inject `IUpdateService` into `SettingsViewModel`**

In `src/GlasLauncher.App/ViewModels/SettingsViewModel.cs`, the class currently has no constructor (implicit parameterless). Add one. Replace:

```csharp
public partial class SettingsViewModel : ViewModelBase
{
    private const string DiscordInviteUrl = "https://discord.gg/UmKM25QUhY";

    public event Action? BackRequested;
```

with:

```csharp
public partial class SettingsViewModel : ViewModelBase
{
    private const string DiscordInviteUrl = "https://discord.gg/UmKM25QUhY";

    private readonly IUpdateService _updateService;

    public SettingsViewModel(IUpdateService updateService)
    {
        _updateService = updateService;
    }

    public event Action? BackRequested;
```

Add the required `using` at the top of the file (this project has `ImplicitUsings` disabled):

```csharp
using GlasLauncher.Core.Services;
```

- [ ] **Step 4: Use the real version in the diagnostic clipboard text**

In `src/GlasLauncher.App/ViewModels/SettingsViewModel.cs`, replace:

```csharp
        await clipboard.SetTextAsync("Launcher v0.1.0 · Project Zomboid 41.78.16 · Mod Java v1.0.0");
```

with:

```csharp
        await clipboard.SetTextAsync($"Launcher {_updateService.GetCurrentVersion()} · Project Zomboid 41.78.16 · Mod Java v1.0.0");
```

(Only the launcher segment changes — the game version and mod version segments stay hardcoded, out of scope for this plan.)

- [ ] **Step 5: Build the full solution**

Run: `dotnet build`
Expected: Build succeeded, 0 errors. Both `DashboardViewModel` and `SettingsViewModel` are resolved by DI via constructor injection — `IUpdateService` is already registered from Task 4, no change needed to their `AddSingleton<...>()` registrations in `App.axaml.cs`.

- [ ] **Step 6: Run the full test suite**

Run: `dotnet test tests/GlasLauncher.Core.Tests`
Expected: PASS — no regressions (this task only touches `GlasLauncher.App`, which has no test project).

- [ ] **Step 7: Commit**

```bash
git add src/GlasLauncher.App/ViewModels/DashboardViewModel.cs src/GlasLauncher.App/Views/DashboardView.axaml src/GlasLauncher.App/ViewModels/SettingsViewModel.cs
git commit -m "fix(app): show the real launcher version instead of a hardcoded string"
```

---

### Task 6: CI workflow — build and test on every push/PR

**Files:**
- Create: `.github/workflows/ci.yml`

**Interfaces:**
- None — pure CI configuration, no code consumed or produced.

- [ ] **Step 1: Create the CI workflow**

Create `.github/workflows/ci.yml`:

```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:

jobs:
  build-and-test:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Build
        run: dotnet build

      - name: Test
        run: dotnet test tests/GlasLauncher.Core.Tests
```

- [ ] **Step 2: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "feat: add CI workflow (build + test on push/PR)"
```

This workflow cannot be verified by running it locally — it only executes once the commit reaches GitHub. That verification is covered in the "Manual verification" section at the end of this plan, not as a step here.

---

### Task 7: Release workflow — package and publish on a version tag

**Files:**
- Create: `.github/workflows/release.yml`

**Interfaces:**
- Consumes: `src/GlasLauncher.App/Assets/shield.ico` (already exists, added in a previous sub-project).

**Prerequisite (not a code step — flag this to your human partner, do not act on it yourself):** this workflow only produces a downloadable, publicly-accessible release once the GitHub repository (`GotoKeiTai/Launcher-Qui-sonne-le-Glas---Project-Zomboid`) is public. It is currently private. Making it public is a deliberate, visible action for a human to take (not something to script or do silently) — flag it in your task report as an open item, do not attempt to change repo visibility yourself.

- [ ] **Step 1: Create the release workflow**

Create `.github/workflows/release.yml`:

```yaml
name: Release

on:
  push:
    tags: ['v*.*.*']

permissions:
  contents: write

jobs:
  package-and-release:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Test
        run: dotnet test tests/GlasLauncher.Core.Tests

      - name: Publish
        run: dotnet publish src/GlasLauncher.App -c Release -r win-x64 --self-contained -o publish

      - name: Install vpk
        run: dotnet tool install --global vpk

      - name: Extract version and release notes from the tag
        run: |
          $version = "${{ github.ref_name }}".TrimStart('v')
          "VERSION=$version" >> $env:GITHUB_ENV
          git tag -l --format='%(contents)' ${{ github.ref_name }} > release-notes.md

      - name: Download previous release (enables delta updates)
        run: vpk download github --repoUrl https://github.com/GotoKeiTai/Launcher-Qui-sonne-le-Glas---Project-Zomboid --token ${{ secrets.GITHUB_TOKEN }}
        continue-on-error: true

      - name: Pack
        run: vpk pack --packId GlasLauncher --packVersion $env:VERSION --packDir publish --mainExe GlasLauncher.App.exe --icon src/GlasLauncher.App/Assets/shield.ico --releaseNotes release-notes.md

      - name: Upload to GitHub Releases
        run: vpk upload github --repoUrl https://github.com/GotoKeiTai/Launcher-Qui-sonne-le-Glas---Project-Zomboid --token ${{ secrets.GITHUB_TOKEN }} --publish --outputDir Releases --releaseName "Glas Launcher $env:VERSION" --tag ${{ github.ref_name }}
```

- [ ] **Step 2: Commit**

```bash
git add .github/workflows/release.yml
git commit -m "feat: add Release workflow (package + publish on version tag)"
```

Like `ci.yml`, this workflow only runs once a `vX.Y.Z` tag is actually pushed to GitHub — that verification is covered in the "Manual verification" section at the end of this plan, not as a step here.

---

## Manual verification (Windows VM, after all tasks — requires the repo to be public first)

Automated tests cover `UpdateNotesParser` and confirm `FakeUpdateService`/`VelopackUpdateService` compile correctly. The real Velopack packaging/update flow and both GitHub Actions workflows need a manual pass on a real, public GitHub repo:

1. Confirm the repo is public (flagged as a prerequisite in Task 7 — a human decision, not something done as part of this plan's implementation).
2. Push a commit to `main` (or open a PR) — confirm `ci.yml` runs and passes on `windows-latest`.
3. Push tag `v0.1.0` — confirm `release.yml` runs, and that a GitHub Release appears with `GlasLauncher-win-Setup.exe` attached.
4. Install via that `.exe` on the Windows VM — confirm installation into `%LocalAppData%` with no UAC/admin prompt, a Start Menu shortcut is created, and the app's Dashboard footer shows "0.1.0" (or whatever the real installed `CurrentVersion` resolves to) instead of a hardcoded string.
5. Push tag `v0.1.1` (a trivial follow-up commit + tag) — confirm the already-installed app's update modal detects the new version, applies it, and restarts into the new version.
6. Confirm the expected SmartScreen warning appears on first run of the installer (no code signing yet) and document this for beta testers, per cahier des charges §8.4.
