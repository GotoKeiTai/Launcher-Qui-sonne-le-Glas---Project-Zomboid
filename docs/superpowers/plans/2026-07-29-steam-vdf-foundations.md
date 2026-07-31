# Fondations Steam & VDF Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace `FakeSteamEnvironment` with a real Windows implementation of `ISteamEnvironment` — registry-based Steam detection, multi-library VDF/ACF parsing for game version and Workshop status.

**Architecture:** Two new pure, cross-platform-testable `Logic/` classes (`SteamLibraryLocator`, `SteamWorkshopReader`) handle all VDF/ACF parsing and are unit-tested with fabricated temp-directory fixtures — no registry, no Windows APIs, run identically on macOS/CI. A new `SteamEnvironment` service orchestrates them and wraps the two genuinely Windows-only primitives (registry read, process check) behind a constructor that takes an already-resolved path, mirroring the existing `FirstRunStore(string filePath)` pattern. DI in `App.axaml.cs` picks the real service on Windows and keeps `FakeSteamEnvironment` everywhere else.

**Tech Stack:** C# / .NET 8, `Gameloop.Vdf` 0.6.2 (VDF/ACF parsing), `Microsoft.Win32.Registry` 5.0.0 (registry read), xUnit.

## Global Constraints

- The launcher targets exactly one Steam AppId, `108600` (Project Zomboid) — fixed, not configurable, defined as a private `const` in each `Logic/` class that needs it.
- Every `ISteamEnvironment` method must return a graceful negative (`null` / `false` / empty list) for any missing, unreadable, or corrupted VDF/ACF file or registry key — never let an exception escape. (Spec: "Gestion des erreurs".)
- `GlasLauncher.Core` has `ImplicitUsings` enabled (`System`, `System.Collections.Generic`, `System.IO`, `System.Linq`, `System.Threading.Tasks` available without explicit `using`). `GlasLauncher.App` does **not** — every new/modified `.cs` file there needs explicit `using` directives for everything it uses.
- Testable-path-injection pattern already established by `FirstRunStore`: constructor takes an already-resolved path/value; a static factory (`CreateDefault()` / here `CreateForCurrentUser()`) does the real OS-specific resolution for production wiring. Reuse this pattern, don't invent a new one.
- No dedicated tests for ViewModels/Views or for `SteamEnvironment` itself (the two Windows-only primitives it wraps are trivial one-line calls to .NET APIs) — consistent with the project's existing testing convention. Manual verification on the Windows VM covers that layer.

---

### Task 1: Add Steam data model and NuGet dependencies

**Files:**
- Create: `src/GlasLauncher.Core/Models/SteamGameLocation.cs`
- Modify: `src/GlasLauncher.Core/GlasLauncher.Core.csproj`

**Interfaces:**
- Produces: `record SteamGameLocation(string LibraryPath, string InstallPath, string BuildId, string Branch)` — consumed by `SteamLibraryLocator` (Task 2) and `SteamEnvironment` (Task 4).

- [ ] **Step 1: Add the two package references**

Modify `src/GlasLauncher.Core/GlasLauncher.Core.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Gameloop.Vdf" Version="0.6.2" />
    <PackageReference Include="Microsoft.Win32.Registry" Version="5.0.0" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Restore packages**

Run: `dotnet restore src/GlasLauncher.Core/GlasLauncher.Core.csproj`
Expected: restores successfully, no errors.

- [ ] **Step 3: Create the `SteamGameLocation` record**

Create `src/GlasLauncher.Core/Models/SteamGameLocation.cs`:

```csharp
namespace GlasLauncher.Core.Models;

public record SteamGameLocation(string LibraryPath, string InstallPath, string BuildId, string Branch);
```

- [ ] **Step 4: Build to verify**

Run: `dotnet build src/GlasLauncher.Core`
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/GlasLauncher.Core/GlasLauncher.Core.csproj src/GlasLauncher.Core/Models/SteamGameLocation.cs
git commit -m "feat(core): add SteamGameLocation model and VDF/registry package references"
```

---

### Task 2: `SteamLibraryLocator` — multi-library game detection (TDD)

**Files:**
- Create: `src/GlasLauncher.Core/Logic/SteamLibraryLocator.cs`
- Test: `tests/GlasLauncher.Core.Tests/SteamLibraryLocatorTests.cs`

**Interfaces:**
- Consumes: `SteamGameLocation` (Task 1).
- Produces: `static class SteamLibraryLocator { public static SteamGameLocation? Locate(string steamPath); }` — consumed by `SteamEnvironment` (Task 4).

- [ ] **Step 1: Write the failing tests**

Create `tests/GlasLauncher.Core.Tests/SteamLibraryLocatorTests.cs`:

```csharp
using System.Text;
using GlasLauncher.Core.Logic;
using Xunit;

namespace GlasLauncher.Core.Tests;

public class SteamLibraryLocatorTests
{
    [Fact]
    public void Locate_SingleLibraryGameFound_ReturnsLocation()
    {
        var steamRoot = CreateTempDir();
        WriteLibraryFolders(steamRoot, steamRoot);
        WriteAppManifest(steamRoot, buildId: "18234567", betaKey: null);

        var result = SteamLibraryLocator.Locate(steamRoot);

        Assert.NotNull(result);
        Assert.Equal(steamRoot, result!.LibraryPath);
        Assert.Equal(Path.Combine(steamRoot, "steamapps", "common", "ProjectZomboid"), result.InstallPath);
        Assert.Equal("18234567", result.BuildId);
        Assert.Equal("public", result.Branch);

        Directory.Delete(steamRoot, recursive: true);
    }

    [Fact]
    public void Locate_MultipleLibrariesGameInSecondLibrary_ReturnsLocation()
    {
        var steamRoot = CreateTempDir();
        var secondLibrary = CreateTempDir();
        WriteLibraryFolders(steamRoot, steamRoot, secondLibrary);
        WriteAppManifest(secondLibrary, buildId: "18234567", betaKey: null);

        var result = SteamLibraryLocator.Locate(steamRoot);

        Assert.NotNull(result);
        Assert.Equal(secondLibrary, result!.LibraryPath);

        Directory.Delete(steamRoot, recursive: true);
        Directory.Delete(secondLibrary, recursive: true);
    }

    [Fact]
    public void Locate_LibraryFoldersVdfMissing_ReturnsNull()
    {
        var steamRoot = CreateTempDir();
        Directory.CreateDirectory(steamRoot);

        var result = SteamLibraryLocator.Locate(steamRoot);

        Assert.Null(result);

        Directory.Delete(steamRoot, recursive: true);
    }

    [Fact]
    public void Locate_AppManifestMissingInAllLibraries_ReturnsNull()
    {
        var steamRoot = CreateTempDir();
        WriteLibraryFolders(steamRoot, steamRoot);

        var result = SteamLibraryLocator.Locate(steamRoot);

        Assert.Null(result);

        Directory.Delete(steamRoot, recursive: true);
    }

    [Fact]
    public void Locate_BetaKeyPresent_ReturnsBetaBranch()
    {
        var steamRoot = CreateTempDir();
        WriteLibraryFolders(steamRoot, steamRoot);
        WriteAppManifest(steamRoot, buildId: "18234567", betaKey: "unstable");

        var result = SteamLibraryLocator.Locate(steamRoot);

        Assert.NotNull(result);
        Assert.Equal("unstable", result!.Branch);

        Directory.Delete(steamRoot, recursive: true);
    }

    [Fact]
    public void Locate_NoBetaKey_ReturnsPublicBranch()
    {
        var steamRoot = CreateTempDir();
        WriteLibraryFolders(steamRoot, steamRoot);
        WriteAppManifest(steamRoot, buildId: "18234567", betaKey: null);

        var result = SteamLibraryLocator.Locate(steamRoot);

        Assert.NotNull(result);
        Assert.Equal("public", result!.Branch);

        Directory.Delete(steamRoot, recursive: true);
    }

    [Fact]
    public void Locate_CorruptedLibraryFoldersVdf_ReturnsNull()
    {
        var steamRoot = CreateTempDir();
        Directory.CreateDirectory(Path.Combine(steamRoot, "steamapps"));
        File.WriteAllText(Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf"), "{not valid vdf");

        var result = SteamLibraryLocator.Locate(steamRoot);

        Assert.Null(result);

        Directory.Delete(steamRoot, recursive: true);
    }

    private static string CreateTempDir() =>
        Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    private static void WriteLibraryFolders(string steamRoot, params string[] libraryPaths)
    {
        var steamappsDir = Path.Combine(steamRoot, "steamapps");
        Directory.CreateDirectory(steamappsDir);

        var sb = new StringBuilder();
        sb.AppendLine("\"libraryfolders\"");
        sb.AppendLine("{");
        for (var i = 0; i < libraryPaths.Length; i++)
        {
            sb.AppendLine($"\t\"{i}\"");
            sb.AppendLine("\t{");
            sb.AppendLine($"\t\t\"path\"\t\t\"{libraryPaths[i].Replace(@"\", @"\\")}\"");
            sb.AppendLine("\t\t\"apps\"");
            sb.AppendLine("\t\t{");
            sb.AppendLine("\t\t}");
            sb.AppendLine("\t}");
        }
        sb.AppendLine("}");

        File.WriteAllText(Path.Combine(steamappsDir, "libraryfolders.vdf"), sb.ToString());
    }

    private static void WriteAppManifest(string libraryPath, string buildId, string? betaKey)
    {
        var steamappsDir = Path.Combine(libraryPath, "steamapps");
        Directory.CreateDirectory(steamappsDir);

        var sb = new StringBuilder();
        sb.AppendLine("\"AppState\"");
        sb.AppendLine("{");
        sb.AppendLine("\t\"appid\"\t\t\"108600\"");
        sb.AppendLine("\t\"Universe\"\t\t\"1\"");
        sb.AppendLine("\t\"name\"\t\t\"Project Zomboid\"");
        sb.AppendLine("\t\"StateFlags\"\t\t\"4\"");
        sb.AppendLine("\t\"installdir\"\t\t\"ProjectZomboid\"");
        sb.AppendLine($"\t\"buildid\"\t\t\"{buildId}\"");
        sb.AppendLine("\t\"UserConfig\"");
        sb.AppendLine("\t{");
        sb.AppendLine("\t\t\"language\"\t\t\"french\"");
        if (betaKey is not null)
        {
            sb.AppendLine($"\t\t\"BetaKey\"\t\t\"{betaKey}\"");
        }
        sb.AppendLine("\t}");
        sb.AppendLine("}");

        File.WriteAllText(Path.Combine(steamappsDir, "appmanifest_108600.acf"), sb.ToString());
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/GlasLauncher.Core.Tests --filter SteamLibraryLocatorTests`
Expected: FAIL (or build error) — `SteamLibraryLocator` does not exist yet.

- [ ] **Step 3: Implement `SteamLibraryLocator`**

Create `src/GlasLauncher.Core/Logic/SteamLibraryLocator.cs`:

```csharp
using Gameloop.Vdf;
using Gameloop.Vdf.Linq;
using GlasLauncher.Core.Models;

namespace GlasLauncher.Core.Logic;

public static class SteamLibraryLocator
{
    private const string AppId = "108600";

    public static SteamGameLocation? Locate(string steamPath)
    {
        foreach (var libraryPath in GetLibraryPaths(steamPath))
        {
            var manifestPath = Path.Combine(libraryPath, "steamapps", $"appmanifest_{AppId}.acf");
            var location = TryReadManifest(libraryPath, manifestPath);
            if (location is not null)
            {
                return location;
            }
        }

        return null;
    }

    private static List<string> GetLibraryPaths(string steamPath)
    {
        var libraryFoldersPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(libraryFoldersPath))
        {
            return new List<string>();
        }

        try
        {
            var root = VdfConvert.Deserialize(File.ReadAllText(libraryFoldersPath));
            if (root.Value is not VObject libraries)
            {
                return new List<string>();
            }

            var paths = new List<string>();
            foreach (var library in libraries.Properties())
            {
                if (library.Value is VObject entry && entry["path"] is { } path)
                {
                    paths.Add(path.ToString());
                }
            }

            return paths;
        }
        catch (Exception)
        {
            return new List<string>();
        }
    }

    private static SteamGameLocation? TryReadManifest(string libraryPath, string manifestPath)
    {
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            var root = VdfConvert.Deserialize(File.ReadAllText(manifestPath));
            if (root.Value is not VObject appState)
            {
                return null;
            }

            var installDir = appState["installdir"]?.ToString();
            var buildId = appState["buildid"]?.ToString();
            if (string.IsNullOrEmpty(installDir) || string.IsNullOrEmpty(buildId))
            {
                return null;
            }

            var branch = "public";
            if (appState["UserConfig"] is VObject userConfig)
            {
                var betaKeyProperty = userConfig.Properties()
                    .FirstOrDefault(p => string.Equals(p.Key, "BetaKey", StringComparison.OrdinalIgnoreCase));
                var betaKeyValue = betaKeyProperty?.Value.ToString();
                if (!string.IsNullOrEmpty(betaKeyValue))
                {
                    branch = betaKeyValue;
                }
            }

            var installPath = Path.Combine(libraryPath, "steamapps", "common", installDir);
            return new SteamGameLocation(libraryPath, installPath, buildId, branch);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/GlasLauncher.Core.Tests --filter SteamLibraryLocatorTests`
Expected: PASS — all 7 tests green.

- [ ] **Step 5: Commit**

```bash
git add src/GlasLauncher.Core/Logic/SteamLibraryLocator.cs tests/GlasLauncher.Core.Tests/SteamLibraryLocatorTests.cs
git commit -m "feat(core): add SteamLibraryLocator for multi-library game detection"
```

---

### Task 3: `SteamWorkshopReader` — installed Workshop item IDs (TDD)

**Files:**
- Create: `src/GlasLauncher.Core/Logic/SteamWorkshopReader.cs`
- Test: `tests/GlasLauncher.Core.Tests/SteamWorkshopReaderTests.cs`

**Interfaces:**
- Produces: `static class SteamWorkshopReader { public static IReadOnlyList<string> GetInstalledItemIds(string libraryPath); }` — consumed by `SteamEnvironment` (Task 4).

*Note: the design spec's file list names only `SteamLibraryLocator.cs` as the new `Logic/` file, folding Workshop-item parsing into `SteamEnvironment`. This plan splits it into its own `Logic/` class instead — same reasoning as `SteamLibraryLocator` (Approach 1, chosen during brainstorming): parsing `appworkshop_*.acf` is plain file I/O, not a Windows-only primitive, so it belongs with the other pure/testable logic rather than untested inside `SteamEnvironment`.*

- [ ] **Step 1: Write the failing tests**

Create `tests/GlasLauncher.Core.Tests/SteamWorkshopReaderTests.cs`:

```csharp
using System.Text;
using GlasLauncher.Core.Logic;
using Xunit;

namespace GlasLauncher.Core.Tests;

public class SteamWorkshopReaderTests
{
    [Fact]
    public void GetInstalledItemIds_FileMissing_ReturnsEmpty()
    {
        var libraryPath = CreateTempDir();
        Directory.CreateDirectory(libraryPath);

        var result = SteamWorkshopReader.GetInstalledItemIds(libraryPath);

        Assert.Empty(result);

        Directory.Delete(libraryPath, recursive: true);
    }

    [Fact]
    public void GetInstalledItemIds_ItemsPresent_ReturnsIds()
    {
        var libraryPath = CreateTempDir();
        WriteAppWorkshop(libraryPath, "111", "222", "333");

        var result = SteamWorkshopReader.GetInstalledItemIds(libraryPath);

        Assert.Equal(new[] { "111", "222", "333" }, result);

        Directory.Delete(libraryPath, recursive: true);
    }

    [Fact]
    public void GetInstalledItemIds_NoItemsInstalledSection_ReturnsEmpty()
    {
        var libraryPath = CreateTempDir();
        WriteAppWorkshop(libraryPath);

        var result = SteamWorkshopReader.GetInstalledItemIds(libraryPath);

        Assert.Empty(result);

        Directory.Delete(libraryPath, recursive: true);
    }

    [Fact]
    public void GetInstalledItemIds_CorruptedFile_ReturnsEmpty()
    {
        var libraryPath = CreateTempDir();
        var workshopDir = Path.Combine(libraryPath, "steamapps", "workshop");
        Directory.CreateDirectory(workshopDir);
        File.WriteAllText(Path.Combine(workshopDir, "appworkshop_108600.acf"), "{not valid vdf");

        var result = SteamWorkshopReader.GetInstalledItemIds(libraryPath);

        Assert.Empty(result);

        Directory.Delete(libraryPath, recursive: true);
    }

    private static string CreateTempDir() =>
        Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    private static void WriteAppWorkshop(string libraryPath, params string[] itemIds)
    {
        var workshopDir = Path.Combine(libraryPath, "steamapps", "workshop");
        Directory.CreateDirectory(workshopDir);

        var sb = new StringBuilder();
        sb.AppendLine("\"AppWorkshop\"");
        sb.AppendLine("{");
        sb.AppendLine("\t\"appid\"\t\t\"108600\"");
        sb.AppendLine("\t\"WorkshopItemsInstalled\"");
        sb.AppendLine("\t{");
        foreach (var id in itemIds)
        {
            sb.AppendLine($"\t\t\"{id}\"");
            sb.AppendLine("\t\t{");
            sb.AppendLine("\t\t\t\"size\"\t\t\"1000\"");
            sb.AppendLine("\t\t\t\"manifest\"\t\t\"1\"");
            sb.AppendLine("\t\t}");
        }
        sb.AppendLine("\t}");
        sb.AppendLine("}");

        File.WriteAllText(Path.Combine(workshopDir, "appworkshop_108600.acf"), sb.ToString());
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/GlasLauncher.Core.Tests --filter SteamWorkshopReaderTests`
Expected: FAIL (or build error) — `SteamWorkshopReader` does not exist yet.

- [ ] **Step 3: Implement `SteamWorkshopReader`**

Create `src/GlasLauncher.Core/Logic/SteamWorkshopReader.cs`:

```csharp
using Gameloop.Vdf;
using Gameloop.Vdf.Linq;

namespace GlasLauncher.Core.Logic;

public static class SteamWorkshopReader
{
    private const string AppId = "108600";

    public static IReadOnlyList<string> GetInstalledItemIds(string libraryPath)
    {
        var manifestPath = Path.Combine(libraryPath, "steamapps", "workshop", $"appworkshop_{AppId}.acf");
        if (!File.Exists(manifestPath))
        {
            return Array.Empty<string>();
        }

        try
        {
            var root = VdfConvert.Deserialize(File.ReadAllText(manifestPath));
            if (root.Value is not VObject appWorkshop)
            {
                return Array.Empty<string>();
            }

            if (appWorkshop["WorkshopItemsInstalled"] is not VObject installed)
            {
                return Array.Empty<string>();
            }

            return installed.Properties().Select(p => p.Key).ToList();
        }
        catch (Exception)
        {
            return Array.Empty<string>();
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/GlasLauncher.Core.Tests --filter SteamWorkshopReaderTests`
Expected: PASS — all 4 tests green.

- [ ] **Step 5: Commit**

```bash
git add src/GlasLauncher.Core/Logic/SteamWorkshopReader.cs tests/GlasLauncher.Core.Tests/SteamWorkshopReaderTests.cs
git commit -m "feat(core): add SteamWorkshopReader for installed Workshop item IDs"
```

---

### Task 4: `SteamEnvironment` — real `ISteamEnvironment` implementation

**Files:**
- Create: `src/GlasLauncher.Core/Services/SteamEnvironment.cs`

**Interfaces:**
- Consumes: `SteamLibraryLocator.Locate(string) : SteamGameLocation?` (Task 2), `SteamWorkshopReader.GetInstalledItemIds(string) : IReadOnlyList<string>` (Task 3), existing `ISteamEnvironment`, `GameVersionInfo`, `WorkshopStatus`.
- Produces: `class SteamEnvironment : ISteamEnvironment` with `SteamEnvironment(string? steamPath)` constructor and `static SteamEnvironment CreateForCurrentUser()` factory — consumed by DI wiring (Task 5).

No dedicated unit tests for this class (see Global Constraints) — verified by build success here and manual check on the Windows VM after Task 5.

- [ ] **Step 1: Implement `SteamEnvironment`**

Create `src/GlasLauncher.Core/Services/SteamEnvironment.cs`:

```csharp
using System.Diagnostics;
using System.Runtime.Versioning;
using GlasLauncher.Core.Logic;
using GlasLauncher.Core.Models;
using Microsoft.Win32;

namespace GlasLauncher.Core.Services;

public class SteamEnvironment : ISteamEnvironment
{
    private const string AppId = "108600";

    private readonly string? _steamPath;
    private readonly Lazy<SteamGameLocation?> _location;

    public SteamEnvironment(string? steamPath)
    {
        _steamPath = steamPath;
        _location = new Lazy<SteamGameLocation?>(() =>
            _steamPath is null ? null : SteamLibraryLocator.Locate(_steamPath));
    }

    [SupportedOSPlatform("windows")]
    public static SteamEnvironment CreateForCurrentUser()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        var steamPath = key?.GetValue("SteamPath") as string;
        return new SteamEnvironment(steamPath);
    }

    public Task<bool> IsSteamInstalledAsync() =>
        Task.FromResult(_steamPath is not null && Directory.Exists(_steamPath));

    public Task<bool> IsSteamRunningAsync() =>
        Task.FromResult(Process.GetProcessesByName("steam").Length > 0);

    public Task<GameVersionInfo?> GetInstalledGameVersionAsync()
    {
        var location = _location.Value;
        return Task.FromResult(location is null ? null : new GameVersionInfo(location.BuildId, location.Branch));
    }

    public Task<WorkshopStatus> GetWorkshopStatusAsync(IReadOnlyList<string> requiredIds, string collectionId)
    {
        var location = _location.Value;
        if (location is null)
        {
            return Task.FromResult(new WorkshopStatus(Array.Empty<string>(), requiredIds, collectionId));
        }

        var installedIds = SteamWorkshopReader.GetInstalledItemIds(location.LibraryPath);
        return Task.FromResult(new WorkshopStatus(installedIds, requiredIds, collectionId));
    }

    public Task LaunchGameAsync()
    {
        Process.Start(new ProcessStartInfo($"steam://run/{AppId}") { UseShellExecute = true });
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build src/GlasLauncher.Core`
Expected: Build succeeded, 0 errors, 0 warnings (in particular no CA1416 platform-compatibility warning — `CreateForCurrentUser` is the only member touching the registry and is annotated `[SupportedOSPlatform("windows")]`).

- [ ] **Step 3: Commit**

```bash
git add src/GlasLauncher.Core/Services/SteamEnvironment.cs
git commit -m "feat(core): add real SteamEnvironment implementation of ISteamEnvironment"
```

---

### Task 5: Wire `SteamEnvironment` into DI

**Files:**
- Modify: `src/GlasLauncher.App/App.axaml.cs:53-75`

**Interfaces:**
- Consumes: `SteamEnvironment.CreateForCurrentUser()` (Task 4), existing `FakeSteamEnvironment`.

- [ ] **Step 1: Replace the `ISteamEnvironment` registration**

In `src/GlasLauncher.App/App.axaml.cs`, replace:

```csharp
    private static void RegisterServices(ServiceCollection services)
    {
        // Real Windows-specific implementations are registered here in a later
        // plan, guarded by OperatingSystem.IsWindows(). Until then, every
        // platform (including macOS during development) uses the fakes.
        // MainWindowViewModel depends on the concrete FakeSteamEnvironment (not ISteamEnvironment)
        // for its dev-only scenario-switcher toggle. When real Windows services are added here,
        // this registration and MainWindowViewModel's constructor will need to be revisited —
        // either keep FakeSteamEnvironment registered everywhere (toggle becomes a no-op on
        // real builds) or give the switcher its own dev-mode gate.
        services.AddSingleton<FakeSteamEnvironment>();
        services.AddSingleton<ISteamEnvironment>(sp => sp.GetRequiredService<FakeSteamEnvironment>());
        services.AddSingleton<IJavaModService, FakeJavaModService>();
```

with:

```csharp
    private static void RegisterServices(ServiceCollection services)
    {
        // Real Windows-specific implementations land here as each "vrais services
        // Windows" sub-project ships (docs/session-notes.md). Steam & VDF is the
        // first: on Windows, SteamEnvironment reads the registry and parses VDF/ACF
        // files for real. Every other platform (macOS during development) keeps
        // using FakeSteamEnvironment.
        services.AddSingleton<ISteamEnvironment>(_ =>
            OperatingSystem.IsWindows()
                ? SteamEnvironment.CreateForCurrentUser()
                : new FakeSteamEnvironment());
        services.AddSingleton<IJavaModService, FakeJavaModService>();
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build src/GlasLauncher.App`
Expected: fails — `MainWindowViewModel` still requires a `FakeSteamEnvironment` constructor parameter that is no longer registered as its own concrete type. This is expected; Task 6 fixes it.

- [ ] **Step 3: Commit**

```bash
git add src/GlasLauncher.App/App.axaml.cs
git commit -m "feat(app): register real SteamEnvironment on Windows, Fake elsewhere"
```

---

### Task 6: Remove the dev Workshop-scenario toggle

**Files:**
- Modify: `src/GlasLauncher.App/ViewModels/MainWindowViewModel.cs`
- Modify: `src/GlasLauncher.App/Views/MainWindow.axaml:26-31`

**Interfaces:**
- None produced or consumed — this task only removes the `MainWindowViewModel` dependency on the concrete `FakeSteamEnvironment` (per spec: the dev toggle is deleted now that the real service exists).

- [ ] **Step 1: Remove the toggle from `MainWindowViewModel`**

In `src/GlasLauncher.App/ViewModels/MainWindowViewModel.cs`, replace the file's top (usings, class fields, constructor) from:

```csharp
using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlasLauncher.Core.Services;
using GlasLauncher.Core.Services.Fakes;

namespace GlasLauncher.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly FakeSteamEnvironment _fakeSteamEnvironment;
    private readonly IJavaModService _javaModService;
    private readonly IUpdateService _updateService;
    private readonly DashboardViewModel _dashboard;
    private readonly FirstRunViewModel _firstRun;

    [ObservableProperty]
    private ViewModelBase _currentPage;

    [ObservableProperty]
    private ViewModelBase? _currentModal;

    public MainWindowViewModel(
        DashboardViewModel dashboard,
        SettingsViewModel settings,
        NewsViewModel news,
        FirstRunViewModel firstRun,
        FakeSteamEnvironment fakeSteamEnvironment,
        IJavaModService javaModService,
        IUpdateService updateService)
    {
        _dashboard = dashboard;
        _firstRun = firstRun;
        _fakeSteamEnvironment = fakeSteamEnvironment;
        _javaModService = javaModService;
        _updateService = updateService;
        _currentPage = dashboard;
```

to:

```csharp
using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using GlasLauncher.Core.Services;

namespace GlasLauncher.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IJavaModService _javaModService;
    private readonly IUpdateService _updateService;
    private readonly DashboardViewModel _dashboard;
    private readonly FirstRunViewModel _firstRun;

    [ObservableProperty]
    private ViewModelBase _currentPage;

    [ObservableProperty]
    private ViewModelBase? _currentModal;

    public MainWindowViewModel(
        DashboardViewModel dashboard,
        SettingsViewModel settings,
        NewsViewModel news,
        FirstRunViewModel firstRun,
        IJavaModService javaModService,
        IUpdateService updateService)
    {
        _dashboard = dashboard;
        _firstRun = firstRun;
        _javaModService = javaModService;
        _updateService = updateService;
        _currentPage = dashboard;
```

Then replace the end of the file, from:

```csharp
    private void OnRepairRequested()
    {
        var modal = new RepairModalViewModel(_javaModService);
        modal.Completed += async () =>
        {
            CurrentModal = null;
            if (_dashboard.RefreshCommand.CanExecute(null))
            {
                await _dashboard.RefreshCommand.ExecuteAsync(null);
            }
        };
        CurrentModal = modal;
        _ = modal.RunRepairAsync();
    }

    [RelayCommand]
    private async Task ToggleWorkshopScenarioAsync()
    {
        _fakeSteamEnvironment.SimulateWorkshopMissing = !_fakeSteamEnvironment.SimulateWorkshopMissing;
        if (_dashboard.RefreshCommand.CanExecute(null))
        {
            await _dashboard.RefreshCommand.ExecuteAsync(null);
        }
    }
}
```

to:

```csharp
    private void OnRepairRequested()
    {
        var modal = new RepairModalViewModel(_javaModService);
        modal.Completed += async () =>
        {
            CurrentModal = null;
            if (_dashboard.RefreshCommand.CanExecute(null))
            {
                await _dashboard.RefreshCommand.ExecuteAsync(null);
            }
        };
        CurrentModal = modal;
        _ = modal.RunRepairAsync();
    }
}
```

- [ ] **Step 2: Remove the toggle button from `MainWindow.axaml`**

In `src/GlasLauncher.App/Views/MainWindow.axaml`, replace:

```xml
          <StackPanel Grid.Column="1" Orientation="Horizontal" Spacing="2" VerticalAlignment="Center">
            <Button Content="Scénario : mods manquants"
                    Command="{Binding ToggleWorkshopScenarioCommand}"
                    FontSize="10.5" Padding="8,4" Margin="0,0,10,0"
                    Background="Transparent" Foreground="{StaticResource InkFaintBrush}" />
            <Button Content="–" Width="34" Height="30" Background="Transparent" Click="OnMinimizeClick" />
```

with:

```xml
          <StackPanel Grid.Column="1" Orientation="Horizontal" Spacing="2" VerticalAlignment="Center">
            <Button Content="–" Width="34" Height="30" Background="Transparent" Click="OnMinimizeClick" />
```

- [ ] **Step 3: Build the full solution**

Run: `dotnet build`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Run the full test suite**

Run: `dotnet test tests/GlasLauncher.Core.Tests`
Expected: PASS — all tests green (including the new `SteamLibraryLocatorTests` and `SteamWorkshopReaderTests` from Tasks 2–3).

- [ ] **Step 5: Commit**

```bash
git add src/GlasLauncher.App/ViewModels/MainWindowViewModel.cs src/GlasLauncher.App/Views/MainWindow.axaml
git commit -m "fix(app): remove dev Workshop-scenario toggle now that ISteamEnvironment is real"
```

---

## Manual verification (Windows VM, after all tasks)

Automated tests cover all VDF/ACF parsing logic cross-platform. The two Windows-only primitives (registry read, process check) and the end-to-end wiring need a manual pass on the actual Windows VM, per the project's established convention for I/O/UI layers:

1. Run `dotnet run --project src/GlasLauncher.App` with Steam installed and Project Zomboid installed and up to date → Dashboard should show "Version conforme" and "Mods Workshop synchronisés" as Passed (assuming the required buildid/Workshop IDs match — these currently come from `FakeServerInfoService`/hardcoded IDs in `DashboardViewModel`, unrelated to this plan).
2. Quit Steam entirely, relaunch the app → checks should reflect Steam not running / not installed gracefully (no crash, no unhandled exception in the status message).
3. Confirm the dev "Scénario : mods manquants" button is gone from the titlebar.
