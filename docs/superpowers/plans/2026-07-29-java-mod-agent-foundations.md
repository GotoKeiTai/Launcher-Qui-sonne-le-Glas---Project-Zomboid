# Mod Java / agent — Fondations Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace `FakeJavaModService` with a real implementation of `IJavaModService` — a manifest-driven list of required Java mod files (agent + any number of mods), local presence/SHA-256 detection, `localconfig.vdf` launch-option verification, and real download/install in `RepairAsync`.

**Architecture:** New pure `Logic/` classes handle VDF parsing (`SteamLaunchOptionInspector`) and local file inspection (`JavaFileInspector`, `JavaModEvaluator`) — all unit-testable with fabricated temp-directory fixtures, no registry/network. `ISteamEnvironment` gains two methods so all Steam file/registry access stays encapsulated there (established in the Steam & VDF foundations sub-project). A new `JavaModManifestFetcher` does the one real network call (HTTP, injectable `HttpMessageHandler` for tests). `JavaModService` orchestrates all of it; like `SteamEnvironment`, it has no dedicated tests (verified manually on Windows).

**Tech Stack:** C# / .NET 8, `Gameloop.Vdf` 0.6.2 (already referenced), `System.Net.Http.Json` + `System.Text.Json` (in-box, no new package), `System.Security.Cryptography.SHA256`, xUnit.

## Global Constraints

- Steam AppId is fixed: `108600` (Project Zomboid) — already a constant in `SteamEnvironment`/`SteamLibraryLocator`/`SteamWorkshopReader`; reuse the same literal, don't introduce a shared constant class (three duplicated literals is an accepted, already-reviewed tradeoff from the previous sub-project).
- Required Steam launch option: `-agentlib:zbNative --`. Checked with **substring containment**, not exact equality — a player's `LaunchOptions` field may hold other flags too (cahier des charges: "vérifier que l'option **est présente**").
- SteamID64 → Steam `userdata` folder name (account ID) conversion: `accountId = steamId64 - 76561197960265728`.
- `RepairAsync` **never writes** to `localconfig.vdf` — read-only, always (already decided in the Steam & VDF foundations sub-project; this plan does not revisit it).
- `GetStatusAsync()` must never throw — every failure (missing manifest, missing install path, network error, corrupt VDF) degrades to a value the caller can render, matching `SteamEnvironment`'s contract. `RepairAsync()` may throw — `RepairModalViewModel` already has error-handling UI for that.
- `GlasLauncher.Core` has `ImplicitUsings` enabled (`System`, `System.Collections.Generic`, `System.IO`, `System.Linq`, `System.Threading.Tasks` available without explicit `using`). `GlasLauncher.App` does not — explicit usings required there.
- `Version` fields (on `JavaFileEntry`/`JavaFileStatus`) are cosmetic only — the functional up-to-date gate is always the SHA-256 comparison. Same principle already used for `GameVersionRequirement.DisplayVersion` vs `buildid`.
- No dedicated unit tests for `JavaModService` or the `ISteamEnvironment`/`SteamEnvironment` extension methods (network calls, Windows-only registry/VDF orchestration) — consistent with the project's established testing convention. Manual verification on the Windows VM covers that layer.
- `JavaModInfo`'s current shape is not consumed anywhere in the codebase today (verified: only `IJavaModService` and `FakeJavaModService` reference it) — free to replace its shape entirely, no compatibility concern.

---

### Task 1: Models — manifest-driven `JavaModInfo`, `FakeJavaModService` update

**Files:**
- Create: `src/GlasLauncher.Core/Models/JavaFileEntry.cs`
- Create: `src/GlasLauncher.Core/Models/JavaModManifest.cs`
- Create: `src/GlasLauncher.Core/Models/JavaFileStatus.cs`
- Modify: `src/GlasLauncher.Core/Models/JavaModInfo.cs` (replace entirely)
- Modify: `src/GlasLauncher.Core/Services/Fakes/FakeJavaModService.cs`

**Interfaces:**
- Produces: `record JavaFileEntry(string FileName, string Version, string Sha256, string DownloadUrl)`, `record JavaModManifest(IReadOnlyList<JavaFileEntry> Files)`, `record JavaFileStatus(string FileName, string? InstalledVersion, string RequiredVersion, bool IsUpToDate)`, `record JavaModInfo(bool LaunchOptionConfigured, IReadOnlyList<JavaFileStatus> Files)` — consumed by every later task.

- [ ] **Step 1: Create the three new model files**

Create `src/GlasLauncher.Core/Models/JavaFileEntry.cs`:

```csharp
namespace GlasLauncher.Core.Models;

public record JavaFileEntry(string FileName, string Version, string Sha256, string DownloadUrl);
```

Create `src/GlasLauncher.Core/Models/JavaModManifest.cs`:

```csharp
namespace GlasLauncher.Core.Models;

public record JavaModManifest(IReadOnlyList<JavaFileEntry> Files);
```

Create `src/GlasLauncher.Core/Models/JavaFileStatus.cs`:

```csharp
namespace GlasLauncher.Core.Models;

public record JavaFileStatus(string FileName, string? InstalledVersion, string RequiredVersion, bool IsUpToDate);
```

- [ ] **Step 2: Replace `JavaModInfo`**

Replace the full content of `src/GlasLauncher.Core/Models/JavaModInfo.cs`:

```csharp
namespace GlasLauncher.Core.Models;

public record JavaModInfo(bool LaunchOptionConfigured, IReadOnlyList<JavaFileStatus> Files);
```

- [ ] **Step 3: Update `FakeJavaModService` to the new shape**

Replace the full content of `src/GlasLauncher.Core/Services/Fakes/FakeJavaModService.cs`:

```csharp
using GlasLauncher.Core.Models;

namespace GlasLauncher.Core.Services.Fakes;

public class FakeJavaModService : IJavaModService
{
    public Task<JavaModInfo> GetStatusAsync() =>
        Task.FromResult(new JavaModInfo(
            LaunchOptionConfigured: true,
            Files: new List<JavaFileStatus>
            {
                new("GlasVoipMod.jar", InstalledVersion: "0.1.0", RequiredVersion: "0.1.0", IsUpToDate: true)
            }));

    public async Task RepairAsync(IProgress<RepairProgress> progress)
    {
        progress.Report(new RepairProgress(RepairStepNames.OldVersionRemoved, 10));
        await Task.Delay(300);
        progress.Report(new RepairProgress(RepairStepNames.DownloadingJavaMod, 30, MegabytesDownloaded: 1.5, MegabytesTotal: 5.1));
        await Task.Delay(200);
        progress.Report(new RepairProgress(RepairStepNames.DownloadingJavaMod, 60, MegabytesDownloaded: 3.4, MegabytesTotal: 5.1));
        await Task.Delay(200);
        progress.Report(new RepairProgress(RepairStepNames.VerifyingIntegrity, 85));
        await Task.Delay(200);
        progress.Report(new RepairProgress(RepairStepNames.Installing, 100));
    }
}
```

- [ ] **Step 4: Build to verify**

Run: `dotnet build src/GlasLauncher.Core`
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Run the full test suite**

Run: `dotnet test tests/GlasLauncher.Core.Tests`
Expected: PASS — no regressions. (`FakeJavaModServiceTests.cs` only exercises `RepairAsync`, which keeps its exact existing behavior in Step 3 above — it does not reference `JavaModInfo` and needs no changes.)

- [ ] **Step 6: Commit**

```bash
git add src/GlasLauncher.Core/Models/JavaFileEntry.cs src/GlasLauncher.Core/Models/JavaModManifest.cs src/GlasLauncher.Core/Models/JavaFileStatus.cs src/GlasLauncher.Core/Models/JavaModInfo.cs src/GlasLauncher.Core/Services/Fakes/FakeJavaModService.cs
git commit -m "feat(core): generalize JavaModInfo to a manifest-driven list of Java mod files"
```

---

### Task 2: `SteamLaunchOptionInspector` — Steam launch-option check (TDD)

**Files:**
- Create: `src/GlasLauncher.Core/Logic/SteamLaunchOptionInspector.cs`
- Test: `tests/GlasLauncher.Core.Tests/SteamLaunchOptionInspectorTests.cs`

**Interfaces:**
- Produces: `static class SteamLaunchOptionInspector { public static bool IsLaunchOptionConfigured(string steamPath, string appId, string requiredOption); }` — consumed by `SteamEnvironment` (Task 6).

- [ ] **Step 1: Write the failing tests**

Create `tests/GlasLauncher.Core.Tests/SteamLaunchOptionInspectorTests.cs`:

```csharp
using System.Text;
using GlasLauncher.Core.Logic;
using Xunit;

namespace GlasLauncher.Core.Tests;

public class SteamLaunchOptionInspectorTests
{
    private const string SteamId64 = "76561197960265729";
    private const string AccountId = "1";
    private const string AppId = "108600";
    private const string RequiredOption = "-agentlib:zbNative --";

    [Fact]
    public void IsLaunchOptionConfigured_OptionPresent_ReturnsTrue()
    {
        var steamPath = CreateTempDir();
        WriteLoginUsers(steamPath, SteamId64, mostRecent: true);
        WriteLocalConfig(steamPath, AccountId, AppId, "-agentlib:zbNative --");

        var result = SteamLaunchOptionInspector.IsLaunchOptionConfigured(steamPath, AppId, RequiredOption);

        Assert.True(result);

        Directory.Delete(steamPath, recursive: true);
    }

    [Fact]
    public void IsLaunchOptionConfigured_OptionPresentAmongOthers_ReturnsTrue()
    {
        var steamPath = CreateTempDir();
        WriteLoginUsers(steamPath, SteamId64, mostRecent: true);
        WriteLocalConfig(steamPath, AccountId, AppId, "-high -agentlib:zbNative --");

        var result = SteamLaunchOptionInspector.IsLaunchOptionConfigured(steamPath, AppId, RequiredOption);

        Assert.True(result);

        Directory.Delete(steamPath, recursive: true);
    }

    [Fact]
    public void IsLaunchOptionConfigured_OptionAbsent_ReturnsFalse()
    {
        var steamPath = CreateTempDir();
        WriteLoginUsers(steamPath, SteamId64, mostRecent: true);
        WriteLocalConfig(steamPath, AccountId, AppId, "-high");

        var result = SteamLaunchOptionInspector.IsLaunchOptionConfigured(steamPath, AppId, RequiredOption);

        Assert.False(result);

        Directory.Delete(steamPath, recursive: true);
    }

    [Fact]
    public void IsLaunchOptionConfigured_AppEntryMissing_ReturnsFalse()
    {
        var steamPath = CreateTempDir();
        WriteLoginUsers(steamPath, SteamId64, mostRecent: true);
        WriteLocalConfig(steamPath, AccountId, AppId, launchOptions: null);

        var result = SteamLaunchOptionInspector.IsLaunchOptionConfigured(steamPath, AppId, RequiredOption);

        Assert.False(result);

        Directory.Delete(steamPath, recursive: true);
    }

    [Fact]
    public void IsLaunchOptionConfigured_LoginUsersVdfMissing_ReturnsFalse()
    {
        var steamPath = CreateTempDir();
        Directory.CreateDirectory(steamPath);

        var result = SteamLaunchOptionInspector.IsLaunchOptionConfigured(steamPath, AppId, RequiredOption);

        Assert.False(result);

        Directory.Delete(steamPath, recursive: true);
    }

    [Fact]
    public void IsLaunchOptionConfigured_NoMostRecentAccount_ReturnsFalse()
    {
        var steamPath = CreateTempDir();
        WriteLoginUsers(steamPath, SteamId64, mostRecent: false);
        WriteLocalConfig(steamPath, AccountId, AppId, "-agentlib:zbNative --");

        var result = SteamLaunchOptionInspector.IsLaunchOptionConfigured(steamPath, AppId, RequiredOption);

        Assert.False(result);

        Directory.Delete(steamPath, recursive: true);
    }

    [Fact]
    public void IsLaunchOptionConfigured_LocalConfigVdfMissing_ReturnsFalse()
    {
        var steamPath = CreateTempDir();
        WriteLoginUsers(steamPath, SteamId64, mostRecent: true);

        var result = SteamLaunchOptionInspector.IsLaunchOptionConfigured(steamPath, AppId, RequiredOption);

        Assert.False(result);

        Directory.Delete(steamPath, recursive: true);
    }

    [Fact]
    public void IsLaunchOptionConfigured_CorruptedLoginUsersVdf_ReturnsFalse()
    {
        var steamPath = CreateTempDir();
        Directory.CreateDirectory(Path.Combine(steamPath, "config"));
        File.WriteAllText(Path.Combine(steamPath, "config", "loginusers.vdf"), "{not valid vdf");

        var result = SteamLaunchOptionInspector.IsLaunchOptionConfigured(steamPath, AppId, RequiredOption);

        Assert.False(result);

        Directory.Delete(steamPath, recursive: true);
    }

    private static string CreateTempDir() =>
        Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    private static void WriteLoginUsers(string steamPath, string steamId64, bool mostRecent)
    {
        var configDir = Path.Combine(steamPath, "config");
        Directory.CreateDirectory(configDir);

        var sb = new StringBuilder();
        sb.AppendLine("\"users\"");
        sb.AppendLine("{");
        sb.AppendLine($"\t\"{steamId64}\"");
        sb.AppendLine("\t{");
        sb.AppendLine("\t\t\"AccountName\"\t\t\"testuser\"");
        sb.AppendLine("\t\t\"PersonaName\"\t\t\"Test User\"");
        sb.AppendLine($"\t\t\"MostRecent\"\t\t\"{(mostRecent ? "1" : "0")}\"");
        sb.AppendLine("\t\t\"Timestamp\"\t\t\"1700000000\"");
        sb.AppendLine("\t}");
        sb.AppendLine("}");

        File.WriteAllText(Path.Combine(configDir, "loginusers.vdf"), sb.ToString());
    }

    private static void WriteLocalConfig(string steamPath, string accountId, string appId, string? launchOptions)
    {
        var configDir = Path.Combine(steamPath, "userdata", accountId, "config");
        Directory.CreateDirectory(configDir);

        var sb = new StringBuilder();
        sb.AppendLine("\"UserLocalConfigStore\"");
        sb.AppendLine("{");
        sb.AppendLine("\t\"Software\"");
        sb.AppendLine("\t{");
        sb.AppendLine("\t\t\"Valve\"");
        sb.AppendLine("\t\t{");
        sb.AppendLine("\t\t\t\"Steam\"");
        sb.AppendLine("\t\t\t{");
        sb.AppendLine("\t\t\t\t\"apps\"");
        sb.AppendLine("\t\t\t\t{");
        if (launchOptions is not null)
        {
            sb.AppendLine($"\t\t\t\t\t\"{appId}\"");
            sb.AppendLine("\t\t\t\t\t{");
            sb.AppendLine($"\t\t\t\t\t\t\"LaunchOptions\"\t\t\"{launchOptions}\"");
            sb.AppendLine("\t\t\t\t\t}");
        }
        sb.AppendLine("\t\t\t\t}");
        sb.AppendLine("\t\t\t}");
        sb.AppendLine("\t\t}");
        sb.AppendLine("\t}");
        sb.AppendLine("}");

        File.WriteAllText(Path.Combine(configDir, "localconfig.vdf"), sb.ToString());
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/GlasLauncher.Core.Tests --filter SteamLaunchOptionInspectorTests`
Expected: FAIL (or build error) — `SteamLaunchOptionInspector` does not exist yet.

- [ ] **Step 3: Implement `SteamLaunchOptionInspector`**

Create `src/GlasLauncher.Core/Logic/SteamLaunchOptionInspector.cs`:

```csharp
using Gameloop.Vdf;
using Gameloop.Vdf.Linq;

namespace GlasLauncher.Core.Logic;

public static class SteamLaunchOptionInspector
{
    private const ulong AccountIdOffset = 76561197960265728;

    public static bool IsLaunchOptionConfigured(string steamPath, string appId, string requiredOption)
    {
        var accountId = FindMostRecentAccountId(steamPath);
        if (accountId is null)
        {
            return false;
        }

        var localConfigPath = Path.Combine(steamPath, "userdata", accountId, "config", "localconfig.vdf");
        var launchOptions = ReadLaunchOptions(localConfigPath, appId);
        return launchOptions is not null && launchOptions.Contains(requiredOption);
    }

    private static string? FindMostRecentAccountId(string steamPath)
    {
        var loginUsersPath = Path.Combine(steamPath, "config", "loginusers.vdf");
        if (!File.Exists(loginUsersPath))
        {
            return null;
        }

        try
        {
            var root = VdfConvert.Deserialize(File.ReadAllText(loginUsersPath));
            if (root.Value is not VObject users)
            {
                return null;
            }

            foreach (var user in users.Properties())
            {
                if (user.Value is VObject entry
                    && entry["MostRecent"]?.ToString() == "1"
                    && ulong.TryParse(user.Key, out var steamId64))
                {
                    return (steamId64 - AccountIdOffset).ToString();
                }
            }

            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? ReadLaunchOptions(string localConfigPath, string appId)
    {
        if (!File.Exists(localConfigPath))
        {
            return null;
        }

        try
        {
            var root = VdfConvert.Deserialize(File.ReadAllText(localConfigPath));
            if (root.Value is not VObject store
                || store["Software"] is not VObject software
                || software["Valve"] is not VObject valve
                || valve["Steam"] is not VObject steam
                || steam["apps"] is not VObject apps
                || apps[appId] is not VObject app)
            {
                return null;
            }

            return app["LaunchOptions"]?.ToString();
        }
        catch (Exception)
        {
            return null;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/GlasLauncher.Core.Tests --filter SteamLaunchOptionInspectorTests`
Expected: PASS — all 8 tests green.

- [ ] **Step 5: Commit**

```bash
git add src/GlasLauncher.Core/Logic/SteamLaunchOptionInspector.cs tests/GlasLauncher.Core.Tests/SteamLaunchOptionInspectorTests.cs
git commit -m "feat(core): add SteamLaunchOptionInspector for the Java agent launch-option check"
```

---

### Task 3: `JavaFileInspector` — local file presence/SHA-256 (TDD)

**Files:**
- Create: `src/GlasLauncher.Core/Logic/JavaFileInspector.cs`
- Test: `tests/GlasLauncher.Core.Tests/JavaFileInspectorTests.cs`

**Interfaces:**
- Consumes: `JavaModManifest`, `JavaFileEntry`, `JavaFileStatus` (Task 1).
- Produces: `static class JavaFileInspector { public static IReadOnlyList<JavaFileStatus> GetFileStatuses(string installPath, JavaModManifest manifest); }` — consumed by `JavaModService` (Task 7).

- [ ] **Step 1: Write the failing tests**

Create `tests/GlasLauncher.Core.Tests/JavaFileInspectorTests.cs`:

```csharp
using GlasLauncher.Core.Logic;
using GlasLauncher.Core.Models;
using Xunit;

namespace GlasLauncher.Core.Tests;

public class JavaFileInspectorTests
{
    // SHA-256("fake glasjavamod content")
    private const string MatchingContent = "fake glasjavamod content";
    private const string MatchingSha256 = "1b5cbd75fa450e543bdce0a9fa501a9ad4ad229d3fb07d16c8e1d6f76a761703";

    [Fact]
    public void GetFileStatuses_FileMatchesHash_ReturnsUpToDate()
    {
        var installPath = CreateTempDir();
        Directory.CreateDirectory(installPath);
        File.WriteAllText(Path.Combine(installPath, "GlasVoipMod.jar"), MatchingContent);

        var manifest = new JavaModManifest(new[]
        {
            new JavaFileEntry("GlasVoipMod.jar", "0.1.0", MatchingSha256, "https://example.com/GlasVoipMod.jar")
        });

        var result = JavaFileInspector.GetFileStatuses(installPath, manifest);

        Assert.Single(result);
        Assert.Equal("GlasVoipMod.jar", result[0].FileName);
        Assert.Equal("0.1.0", result[0].InstalledVersion);
        Assert.Equal("0.1.0", result[0].RequiredVersion);
        Assert.True(result[0].IsUpToDate);

        Directory.Delete(installPath, recursive: true);
    }

    [Fact]
    public void GetFileStatuses_FileMissing_ReturnsNotUpToDate()
    {
        var installPath = CreateTempDir();
        Directory.CreateDirectory(installPath);

        var manifest = new JavaModManifest(new[]
        {
            new JavaFileEntry("GlasVoipMod.jar", "0.1.0", MatchingSha256, "https://example.com/GlasVoipMod.jar")
        });

        var result = JavaFileInspector.GetFileStatuses(installPath, manifest);

        Assert.Single(result);
        Assert.Null(result[0].InstalledVersion);
        Assert.False(result[0].IsUpToDate);

        Directory.Delete(installPath, recursive: true);
    }

    [Fact]
    public void GetFileStatuses_FileHashMismatch_ReturnsNotUpToDate()
    {
        var installPath = CreateTempDir();
        Directory.CreateDirectory(installPath);
        File.WriteAllText(Path.Combine(installPath, "GlasVoipMod.jar"), "wrong content");

        var manifest = new JavaModManifest(new[]
        {
            new JavaFileEntry("GlasVoipMod.jar", "0.1.0", MatchingSha256, "https://example.com/GlasVoipMod.jar")
        });

        var result = JavaFileInspector.GetFileStatuses(installPath, manifest);

        Assert.Single(result);
        Assert.Null(result[0].InstalledVersion);
        Assert.False(result[0].IsUpToDate);

        Directory.Delete(installPath, recursive: true);
    }

    [Fact]
    public void GetFileStatuses_MultipleEntries_ReturnsOneStatusPerEntryInOrder()
    {
        var installPath = CreateTempDir();
        Directory.CreateDirectory(installPath);
        File.WriteAllText(Path.Combine(installPath, "GlasVoipMod.jar"), MatchingContent);
        // ZombieBuddy.jar intentionally left missing.

        var manifest = new JavaModManifest(new[]
        {
            new JavaFileEntry("GlasVoipMod.jar", "0.1.0", MatchingSha256, "https://example.com/GlasVoipMod.jar"),
            new JavaFileEntry("ZombieBuddy.jar", "1.0.0", "0000000000000000000000000000000000000000000000000000000000000000", "https://example.com/ZombieBuddy.jar")
        });

        var result = JavaFileInspector.GetFileStatuses(installPath, manifest);

        Assert.Equal(2, result.Count);
        Assert.Equal("GlasVoipMod.jar", result[0].FileName);
        Assert.True(result[0].IsUpToDate);
        Assert.Equal("ZombieBuddy.jar", result[1].FileName);
        Assert.False(result[1].IsUpToDate);

        Directory.Delete(installPath, recursive: true);
    }

    private static string CreateTempDir() =>
        Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/GlasLauncher.Core.Tests --filter JavaFileInspectorTests`
Expected: FAIL (or build error) — `JavaFileInspector` does not exist yet.

- [ ] **Step 3: Implement `JavaFileInspector`**

Create `src/GlasLauncher.Core/Logic/JavaFileInspector.cs`:

```csharp
using System.Security.Cryptography;
using GlasLauncher.Core.Models;

namespace GlasLauncher.Core.Logic;

public static class JavaFileInspector
{
    public static IReadOnlyList<JavaFileStatus> GetFileStatuses(string installPath, JavaModManifest manifest)
    {
        var statuses = new List<JavaFileStatus>();

        foreach (var entry in manifest.Files)
        {
            var filePath = Path.Combine(installPath, entry.FileName);
            var localSha256 = TryComputeSha256(filePath);
            var isUpToDate = localSha256 is not null
                && string.Equals(localSha256, entry.Sha256, StringComparison.OrdinalIgnoreCase);

            statuses.Add(new JavaFileStatus(
                entry.FileName,
                InstalledVersion: isUpToDate ? entry.Version : null,
                RequiredVersion: entry.Version,
                IsUpToDate: isUpToDate));
        }

        return statuses;
    }

    private static string? TryComputeSha256(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(filePath);
            return Convert.ToHexString(SHA256.HashData(stream));
        }
        catch (Exception)
        {
            return null;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/GlasLauncher.Core.Tests --filter JavaFileInspectorTests`
Expected: PASS — all 4 tests green.

- [ ] **Step 5: Commit**

```bash
git add src/GlasLauncher.Core/Logic/JavaFileInspector.cs tests/GlasLauncher.Core.Tests/JavaFileInspectorTests.cs
git commit -m "feat(core): add JavaFileInspector for local Java mod file detection"
```

---

### Task 4: `JavaModEvaluator` (TDD)

**Files:**
- Create: `src/GlasLauncher.Core/Logic/JavaModEvaluator.cs`
- Test: `tests/GlasLauncher.Core.Tests/JavaModEvaluatorTests.cs`

**Interfaces:**
- Consumes: `JavaModInfo`, `JavaFileStatus` (Task 1), `CheckResult`/`CheckStatus` (existing, `GlasLauncher.Core.Models`).
- Produces: `static class JavaModEvaluator { public static CheckResult Evaluate(JavaModInfo info); }` — consumed by `DashboardViewModel` (Task 9).

- [ ] **Step 1: Write the failing tests**

Create `tests/GlasLauncher.Core.Tests/JavaModEvaluatorTests.cs`:

```csharp
using GlasLauncher.Core.Logic;
using GlasLauncher.Core.Models;
using Xunit;

namespace GlasLauncher.Core.Tests;

public class JavaModEvaluatorTests
{
    [Fact]
    public void Evaluate_NoFilesVerified_ReturnsFailed()
    {
        var info = new JavaModInfo(LaunchOptionConfigured: true, Files: Array.Empty<JavaFileStatus>());

        var result = JavaModEvaluator.Evaluate(info);

        Assert.Equal(CheckStatus.Failed, result.Status);
    }

    [Fact]
    public void Evaluate_LaunchOptionNotConfigured_ReturnsFailed()
    {
        var info = new JavaModInfo(
            LaunchOptionConfigured: false,
            Files: new[] { new JavaFileStatus("GlasVoipMod.jar", "0.1.0", "0.1.0", IsUpToDate: true) });

        var result = JavaModEvaluator.Evaluate(info);

        Assert.Equal(CheckStatus.Failed, result.Status);
    }

    [Fact]
    public void Evaluate_FileOutdated_ReturnsFailed()
    {
        var info = new JavaModInfo(
            LaunchOptionConfigured: true,
            Files: new[] { new JavaFileStatus("GlasVoipMod.jar", null, "0.1.0", IsUpToDate: false) });

        var result = JavaModEvaluator.Evaluate(info);

        Assert.Equal(CheckStatus.Failed, result.Status);
    }

    [Fact]
    public void Evaluate_LaunchOptionConfiguredAndAllFilesUpToDate_ReturnsPassed()
    {
        var info = new JavaModInfo(
            LaunchOptionConfigured: true,
            Files: new[]
            {
                new JavaFileStatus("ZombieBuddy.jar", "1.0.0", "1.0.0", IsUpToDate: true),
                new JavaFileStatus("GlasVoipMod.jar", "0.1.0", "0.1.0", IsUpToDate: true)
            });

        var result = JavaModEvaluator.Evaluate(info);

        Assert.Equal(CheckStatus.Passed, result.Status);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/GlasLauncher.Core.Tests --filter JavaModEvaluatorTests`
Expected: FAIL (or build error) — `JavaModEvaluator` does not exist yet.

- [ ] **Step 3: Implement `JavaModEvaluator`**

Create `src/GlasLauncher.Core/Logic/JavaModEvaluator.cs`:

```csharp
using System.Linq;
using GlasLauncher.Core.Models;

namespace GlasLauncher.Core.Logic;

public static class JavaModEvaluator
{
    private const string CheckName = "Mod Java à jour";

    public static CheckResult Evaluate(JavaModInfo info)
    {
        if (info.Files.Count == 0)
        {
            return new CheckResult(CheckName, CheckStatus.Failed, "Impossible de vérifier le mod Java.");
        }

        if (!info.LaunchOptionConfigured)
        {
            return new CheckResult(
                CheckName,
                CheckStatus.Failed,
                "Option de lancement Steam manquante pour l'agent Java.");
        }

        if (info.Files.Any(f => !f.IsUpToDate))
        {
            return new CheckResult(CheckName, CheckStatus.Failed, "Le mod Java n'est pas à jour.");
        }

        return new CheckResult(CheckName, CheckStatus.Passed, "Agent Java synchronisé.");
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/GlasLauncher.Core.Tests --filter JavaModEvaluatorTests`
Expected: PASS — all 4 tests green.

- [ ] **Step 5: Commit**

```bash
git add src/GlasLauncher.Core/Logic/JavaModEvaluator.cs tests/GlasLauncher.Core.Tests/JavaModEvaluatorTests.cs
git commit -m "feat(core): add JavaModEvaluator to gate the Dashboard Java mod check"
```

---

### Task 5: `JavaModManifestFetcher` (TDD)

**Files:**
- Create: `src/GlasLauncher.Core/Services/JavaModManifestFetcher.cs`
- Test: `tests/GlasLauncher.Core.Tests/JavaModManifestFetcherTests.cs`

**Interfaces:**
- Consumes: `JavaModManifest`, `JavaFileEntry` (Task 1).
- Produces: `class JavaModManifestFetcher { public JavaModManifestFetcher(HttpClient httpClient); public static JavaModManifestFetcher CreateDefault(); public Task<JavaModManifest?> FetchAsync(); }` — consumed by `JavaModService` (Task 7).

- [ ] **Step 1: Write the failing tests**

Create `tests/GlasLauncher.Core.Tests/JavaModManifestFetcherTests.cs`:

```csharp
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using GlasLauncher.Core.Services;
using Xunit;

namespace GlasLauncher.Core.Tests;

public class JavaModManifestFetcherTests
{
    [Fact]
    public async Task FetchAsync_ValidJson_ReturnsManifest()
    {
        const string json = """
            {
                "files": [
                    { "fileName": "GlasVoipMod.jar", "version": "0.1.0", "sha256": "abc123", "downloadUrl": "https://example.com/GlasVoipMod.jar" }
                ]
            }
            """;
        var httpClient = new HttpClient(new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        }));
        var fetcher = new JavaModManifestFetcher(httpClient);

        var manifest = await fetcher.FetchAsync();

        Assert.NotNull(manifest);
        Assert.Single(manifest!.Files);
        Assert.Equal("GlasVoipMod.jar", manifest.Files[0].FileName);
        Assert.Equal("0.1.0", manifest.Files[0].Version);
        Assert.Equal("abc123", manifest.Files[0].Sha256);
        Assert.Equal("https://example.com/GlasVoipMod.jar", manifest.Files[0].DownloadUrl);
    }

    [Fact]
    public async Task FetchAsync_HttpErrorStatus_ReturnsNull()
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.NotFound)));
        var fetcher = new JavaModManifestFetcher(httpClient);

        var manifest = await fetcher.FetchAsync();

        Assert.Null(manifest);
    }

    [Fact]
    public async Task FetchAsync_MalformedJson_ReturnsNull()
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{not valid json", Encoding.UTF8, "application/json")
        }));
        var fetcher = new JavaModManifestFetcher(httpClient);

        var manifest = await fetcher.FetchAsync();

        Assert.Null(manifest);
    }

    [Fact]
    public async Task FetchAsync_NetworkFailure_ReturnsNull()
    {
        var httpClient = new HttpClient(new ThrowingHttpMessageHandler());
        var fetcher = new JavaModManifestFetcher(httpClient);

        var manifest = await fetcher.FetchAsync();

        Assert.Null(manifest);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public StubHttpMessageHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_response);
    }

    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("simulated network failure");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/GlasLauncher.Core.Tests --filter JavaModManifestFetcherTests`
Expected: FAIL (or build error) — `JavaModManifestFetcher` does not exist yet.

- [ ] **Step 3: Implement `JavaModManifestFetcher`**

Create `src/GlasLauncher.Core/Services/JavaModManifestFetcher.cs`:

```csharp
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using GlasLauncher.Core.Models;

namespace GlasLauncher.Core.Services;

public class JavaModManifestFetcher
{
    // Placeholder — no real hosting exists yet (see spec, §8.3 of the cahier des charges).
    // Update once the manifest is actually published somewhere.
    private const string ManifestUrl = "https://raw.githubusercontent.com/GotoKeiTai/glas-launcher-hosting/main/java-mod-manifest.json";

    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;

    public JavaModManifestFetcher(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public static JavaModManifestFetcher CreateDefault() => new(new HttpClient());

    public async Task<JavaModManifest?> FetchAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<JavaModManifest>(ManifestUrl, SerializerOptions);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/GlasLauncher.Core.Tests --filter JavaModManifestFetcherTests`
Expected: PASS — all 4 tests green.

- [ ] **Step 5: Commit**

```bash
git add src/GlasLauncher.Core/Services/JavaModManifestFetcher.cs tests/GlasLauncher.Core.Tests/JavaModManifestFetcherTests.cs
git commit -m "feat(core): add JavaModManifestFetcher for the remote Java mod manifest"
```

---

### Task 6: Extend `ISteamEnvironment` with install path and launch-option checks

**Files:**
- Modify: `src/GlasLauncher.Core/Services/ISteamEnvironment.cs`
- Modify: `src/GlasLauncher.Core/Services/SteamEnvironment.cs`
- Modify: `src/GlasLauncher.Core/Services/Fakes/FakeSteamEnvironment.cs`

**Interfaces:**
- Consumes: `SteamLaunchOptionInspector.IsLaunchOptionConfigured(string, string, string) : bool` (Task 2).
- Produces: `ISteamEnvironment.GetGameInstallPathAsync() : Task<string?>`, `ISteamEnvironment.IsJavaAgentLaunchOptionConfiguredAsync() : Task<bool>` — consumed by `JavaModService` (Task 7).

No dedicated tests for this task (Windows-only orchestration on top of already-tested `SteamLaunchOptionInspector` and the already-cached `_location` field) — consistent with `SteamEnvironment`'s existing no-test convention. Verified by build success here and manual check later.

- [ ] **Step 1: Add the two methods to `ISteamEnvironment`**

Replace the full content of `src/GlasLauncher.Core/Services/ISteamEnvironment.cs`:

```csharp
using GlasLauncher.Core.Models;

namespace GlasLauncher.Core.Services;

public interface ISteamEnvironment
{
    Task<bool> IsSteamInstalledAsync();
    Task<bool> IsSteamRunningAsync();
    Task<GameVersionInfo?> GetInstalledGameVersionAsync();
    Task<WorkshopStatus> GetWorkshopStatusAsync(IReadOnlyList<string> requiredIds, string collectionId);
    Task LaunchGameAsync();
    Task<string?> GetGameInstallPathAsync();
    Task<bool> IsJavaAgentLaunchOptionConfiguredAsync();
}
```

- [ ] **Step 2: Implement both in `SteamEnvironment`**

In `src/GlasLauncher.Core/Services/SteamEnvironment.cs`, add a second constant next to the existing `AppId` one:

```csharp
    private const string AppId = "108600";
    private const string RequiredLaunchOption = "-agentlib:zbNative --";
```

Then add these two methods to the class (after `LaunchGameAsync`, before the closing brace):

```csharp
    public Task<string?> GetGameInstallPathAsync() =>
        Task.FromResult(_location.Value?.InstallPath);

    public Task<bool> IsJavaAgentLaunchOptionConfiguredAsync() =>
        Task.FromResult(_steamPath is not null
            && SteamLaunchOptionInspector.IsLaunchOptionConfigured(_steamPath, AppId, RequiredLaunchOption));
```

- [ ] **Step 3: Implement both in `FakeSteamEnvironment`**

In `src/GlasLauncher.Core/Services/Fakes/FakeSteamEnvironment.cs`, add these two methods to the class (after `LaunchGameAsync`, before the closing brace):

```csharp
    public Task<string?> GetGameInstallPathAsync() =>
        Task.FromResult<string?>("/fake/steam/library/steamapps/common/ProjectZomboid");

    public Task<bool> IsJavaAgentLaunchOptionConfiguredAsync() => Task.FromResult(true);
```

- [ ] **Step 4: Build to verify**

Run: `dotnet build src/GlasLauncher.Core`
Expected: Build succeeded, 0 errors, 0 warnings.

- [ ] **Step 5: Run the full test suite**

Run: `dotnet test tests/GlasLauncher.Core.Tests`
Expected: PASS — all tests green (no regressions).

- [ ] **Step 6: Commit**

```bash
git add src/GlasLauncher.Core/Services/ISteamEnvironment.cs src/GlasLauncher.Core/Services/SteamEnvironment.cs src/GlasLauncher.Core/Services/Fakes/FakeSteamEnvironment.cs
git commit -m "feat(core): expose game install path and Java agent launch-option check on ISteamEnvironment"
```

---

### Task 7: `JavaModService` — real `IJavaModService` implementation

**Files:**
- Create: `src/GlasLauncher.Core/Services/JavaModService.cs`

**Interfaces:**
- Consumes: `ISteamEnvironment.GetGameInstallPathAsync()`, `ISteamEnvironment.IsJavaAgentLaunchOptionConfiguredAsync()` (Task 6), `JavaModManifestFetcher.FetchAsync()` (Task 5), `JavaFileInspector.GetFileStatuses(string, JavaModManifest)` (Task 3), existing `IJavaModService`, `JavaModInfo`, `RepairProgress`, `RepairStepNames`.
- Produces: `class JavaModService : IJavaModService` with `JavaModService(ISteamEnvironment steamEnvironment, JavaModManifestFetcher manifestFetcher)` — consumed by DI wiring (Task 8).

No dedicated unit tests for this class (network + file-system orchestration) — consistent with `SteamEnvironment`'s established convention. Verified by build success here and manual check on the Windows VM after Task 8.

- [ ] **Step 1: Implement `JavaModService`**

Create `src/GlasLauncher.Core/Services/JavaModService.cs`:

```csharp
using System.Net.Http;
using System.Security.Cryptography;
using GlasLauncher.Core.Logic;
using GlasLauncher.Core.Models;

namespace GlasLauncher.Core.Services;

public class JavaModService : IJavaModService
{
    private readonly ISteamEnvironment _steamEnvironment;
    private readonly JavaModManifestFetcher _manifestFetcher;

    public JavaModService(ISteamEnvironment steamEnvironment, JavaModManifestFetcher manifestFetcher)
    {
        _steamEnvironment = steamEnvironment;
        _manifestFetcher = manifestFetcher;
    }

    public async Task<JavaModInfo> GetStatusAsync()
    {
        var launchOptionConfigured = await _steamEnvironment.IsJavaAgentLaunchOptionConfiguredAsync();
        var installPath = await _steamEnvironment.GetGameInstallPathAsync();
        if (installPath is null)
        {
            return new JavaModInfo(launchOptionConfigured, Array.Empty<JavaFileStatus>());
        }

        var manifest = await _manifestFetcher.FetchAsync();
        if (manifest is null)
        {
            return new JavaModInfo(launchOptionConfigured, Array.Empty<JavaFileStatus>());
        }

        var files = JavaFileInspector.GetFileStatuses(installPath, manifest);
        return new JavaModInfo(launchOptionConfigured, files);
    }

    public async Task RepairAsync(IProgress<RepairProgress> progress)
    {
        var installPath = await _steamEnvironment.GetGameInstallPathAsync()
            ?? throw new InvalidOperationException("Dossier d'installation de Project Zomboid introuvable.");

        var manifest = await _manifestFetcher.FetchAsync()
            ?? throw new InvalidOperationException("Impossible de récupérer le manifeste du mod Java.");

        var statuses = JavaFileInspector.GetFileStatuses(installPath, manifest);
        var outdatedEntries = manifest.Files
            .Where(entry => statuses.First(s => s.FileName == entry.FileName).IsUpToDate == false)
            .ToList();

        progress.Report(new RepairProgress(RepairStepNames.OldVersionRemoved, 10));
        foreach (var entry in outdatedEntries)
        {
            var path = Path.Combine(installPath, entry.FileName);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        progress.Report(new RepairProgress(RepairStepNames.DownloadingJavaMod, 30));

        using var httpClient = new HttpClient();
        var totalBytes = 0L;
        foreach (var entry in outdatedEntries)
        {
            using var headResponse = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, entry.DownloadUrl));
            totalBytes += headResponse.Content.Headers.ContentLength ?? 0;
        }

        var tempFiles = new Dictionary<string, string>();
        var downloadedBytes = 0L;
        for (var i = 0; i < outdatedEntries.Count; i++)
        {
            var entry = outdatedEntries[i];
            var tempPath = Path.GetTempFileName();
            await using (var responseStream = await httpClient.GetStreamAsync(entry.DownloadUrl))
            await using (var fileStream = File.Create(tempPath))
            {
                await responseStream.CopyToAsync(fileStream);
            }

            tempFiles[entry.FileName] = tempPath;
            downloadedBytes += new FileInfo(tempPath).Length;

            progress.Report(new RepairProgress(
                RepairStepNames.DownloadingJavaMod,
                PercentComplete: 30 + (int)(30.0 * (i + 1) / outdatedEntries.Count),
                MegabytesDownloaded: downloadedBytes / 1024.0 / 1024.0,
                MegabytesTotal: totalBytes / 1024.0 / 1024.0));
        }

        progress.Report(new RepairProgress(RepairStepNames.VerifyingIntegrity, 85));
        foreach (var entry in outdatedEntries)
        {
            var tempPath = tempFiles[entry.FileName];
            string localSha256;
            await using (var stream = File.OpenRead(tempPath))
            {
                localSha256 = Convert.ToHexString(await SHA256.HashDataAsync(stream));
            }

            if (!string.Equals(localSha256, entry.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Intégrité invalide pour {entry.FileName}.");
            }
        }

        progress.Report(new RepairProgress(RepairStepNames.Installing, 100));
        foreach (var entry in outdatedEntries)
        {
            var tempPath = tempFiles[entry.FileName];
            var destinationPath = Path.Combine(installPath, entry.FileName);
            File.Move(tempPath, destinationPath, overwrite: true);
        }
    }
}
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build src/GlasLauncher.Core`
Expected: Build succeeded, 0 errors, 0 warnings.

- [ ] **Step 3: Commit**

```bash
git add src/GlasLauncher.Core/Services/JavaModService.cs
git commit -m "feat(core): add real JavaModService implementation of IJavaModService"
```

---

### Task 8: Wire `JavaModService` into DI

**Files:**
- Modify: `src/GlasLauncher.App/App.axaml.cs`

**Interfaces:**
- Consumes: `JavaModService` (Task 7), `JavaModManifestFetcher.CreateDefault()` (Task 5), existing `FakeJavaModService`.

- [ ] **Step 1: Replace the `IJavaModService` registration**

In `src/GlasLauncher.App/App.axaml.cs`, replace:

```csharp
        services.AddSingleton<IJavaModService, FakeJavaModService>();
```

with:

```csharp
        services.AddSingleton<IJavaModService>(sp =>
            OperatingSystem.IsWindows()
                ? new JavaModService(sp.GetRequiredService<ISteamEnvironment>(), JavaModManifestFetcher.CreateDefault())
                : new FakeJavaModService());
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build src/GlasLauncher.App`
Expected: Build succeeded, 0 errors, 0 warnings.

- [ ] **Step 3: Commit**

```bash
git add src/GlasLauncher.App/App.axaml.cs
git commit -m "feat(app): register real JavaModService on Windows, Fake elsewhere"
```

---

### Task 9: Wire the Dashboard "Mod Java à jour" check to the real service

**Files:**
- Modify: `src/GlasLauncher.App/ViewModels/DashboardViewModel.cs`

**Interfaces:**
- Consumes: `IJavaModService.GetStatusAsync()` (existing interface, real implementation from Task 7/8), `JavaModEvaluator.Evaluate(JavaModInfo)` (Task 4).

- [ ] **Step 1: Add the `IJavaModService` dependency**

In `src/GlasLauncher.App/ViewModels/DashboardViewModel.cs`, replace:

```csharp
public partial class DashboardViewModel : ViewModelBase
{
    private readonly ISteamEnvironment _steamEnvironment;
    private readonly IServerInfoService _serverInfoService;

    public DashboardViewModel(ISteamEnvironment steamEnvironment, IServerInfoService serverInfoService)
    {
        _steamEnvironment = steamEnvironment;
        _serverInfoService = serverInfoService;
        Checks = new ObservableCollection<CheckItemViewModel>();
        News = new ObservableCollection<NewsItem>();

        _ = RefreshAsync();
    }
```

with:

```csharp
public partial class DashboardViewModel : ViewModelBase
{
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

- [ ] **Step 2: Replace the hardcoded check**

Replace:

```csharp
            // Placeholder until IJavaModService is wired into the dashboard checks in a later plan.
            Checks.Add(new CheckItemViewModel(new CheckResult("Mod Java à jour", CheckStatus.Passed, "Agent Java synchronisé.")));
```

with:

```csharp
            var javaModInfo = await _javaModService.GetStatusAsync();
            Checks.Add(new CheckItemViewModel(JavaModEvaluator.Evaluate(javaModInfo)));
```

- [ ] **Step 3: Build the full solution**

Run: `dotnet build`
Expected: Build succeeded, 0 errors. (`DashboardViewModel` is resolved by DI via constructor injection — `IJavaModService` is already registered from Task 8, no change needed to its `AddSingleton<DashboardViewModel>()` registration.)

- [ ] **Step 4: Run the full test suite**

Run: `dotnet test tests/GlasLauncher.Core.Tests`
Expected: PASS — all tests green.

- [ ] **Step 5: Commit**

```bash
git add src/GlasLauncher.App/ViewModels/DashboardViewModel.cs
git commit -m "feat(app): wire the Dashboard Java mod check to the real JavaModService"
```

---

## Manual verification (Windows VM, after all tasks)

Automated tests cover all VDF parsing, file-hashing, and manifest-parsing logic cross-platform. The Windows-only orchestration (`SteamEnvironment`'s two new methods) and the real network path (`JavaModManifestFetcher` against a real URL, `RepairAsync`'s download/verify/install flow) need a manual pass:

1. Since the manifest URL is a placeholder with no real hosting yet (see spec, Hors-scope), `GetStatusAsync()` will report `Files: []` on any real machine until that's updated — confirm the Dashboard shows "Mod Java à jour" as Failed with the "Impossible de vérifier" message, not a crash.
2. Confirm `IsJavaAgentLaunchOptionConfiguredAsync()` correctly reflects whether `-agentlib:zbNative --` is present in Steam's launch options for Project Zomboid (set it manually via Steam's game properties, relaunch the app, confirm the check flips).
3. Once a real manifest URL and hosted files exist (future work): run `Repair`, confirm the 4-step modal completes, confirm the downloaded files land in the game's install folder with correct SHA-256.
