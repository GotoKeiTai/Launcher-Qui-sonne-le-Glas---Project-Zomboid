# Java Mod Launch Option — Manifest-Driven Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the hardcoded, now-incorrect ZombieBuddy launch option (`-agentlib:zbNative --`) with a manifest-driven list of required Steam launch options, falling back to a hardcoded placeholder default when the manifest is unavailable (no real hosting exists yet).

**Architecture:** `SteamLaunchOptionInspector.IsLaunchOptionConfigured` (single string) generalizes to `AreLaunchOptionsConfigured` (a list, AND semantics — every required option must be present). `JavaModManifest` and `JavaModInfo` both gain a `RequiredLaunchOptions` field. `JavaModService.GetStatusAsync()` now fetches the manifest *before* checking the launch option (reversed from today), passing the manifest's `RequiredLaunchOptions` (or an empty list if the manifest is unavailable) into `ISteamEnvironment.IsJavaAgentLaunchOptionConfiguredAsync(requiredOptions)`. `SteamEnvironment` falls back to a hardcoded `DefaultRequiredLaunchOption` placeholder whenever the passed-in list is empty — this is what keeps the check observable today, exactly as it was before this fix, while transparently handing control to the manifest once real hosting exists. `JavaModEvaluator`'s failure message is built from `info.RequiredLaunchOptions` instead of an internal hardcoded constant, so the text the player is told to copy always matches what was actually checked.

**Tech Stack:** C# / .NET 8, `Gameloop.Vdf` (already referenced), xUnit.

## Global Constraints

- `GlasLauncher.Core` has `ImplicitUsings` enabled (`System`, `System.Collections.Generic`, `System.IO`, `System.Linq`, `System.Threading.Tasks` available without explicit `using`). `GlasLauncher.App` is not touched by this plan at all — no App-side files change.
- Steam AppId (`"108600"`) and the account-ID offset logic in `SteamLaunchOptionInspector`/`SteamEnvironment` are unrelated to this plan — do not touch them.
- The real required launch option value for the VOIP mod (`GlasVoipMod`) is **not yet known** — `GlasVoipMod` has no stable jar name/release process yet. Use the placeholder `"-javaagent:GlasVoipMod.jar"` everywhere a concrete example value is needed (fakes, defaults, tests) — same treatment as the already-placeholder manifest URL in `JavaModManifestFetcher`. This is not a value to "get right" in this plan; it will be superseded automatically once the manifest is hosted for real.
- `SteamLaunchOptionInspector.AreLaunchOptionsConfigured` must never throw — same defensive contract as today's `IsLaunchOptionConfigured` (every read/parse failure degrades to `false`). An empty `requiredOptions` list degrades to `true` (nothing to check) — this should never actually occur once `SteamEnvironment`'s fallback is in place, but the pure function must handle it safely regardless of caller behavior.
- `JavaModService.GetStatusAsync()` must never throw — same never-throw contract as every other `GetStatusAsync()`-style method in this codebase. The existing top-level `try/catch` already covers the whole method body (added in a prior fix round) and requires no changes for this plan — verify it still wraps the reordered logic, don't remove it.
- No dedicated unit tests for `SteamEnvironment`/`FakeSteamEnvironment`/`JavaModService` (Windows/network orchestration) — consistent with the project's established convention. `SteamLaunchOptionInspector`, `JavaModEvaluator`, and `JavaModManifestFetcher` are pure/testable and get TDD coverage as specified below.
- `JavaModManifest`'s `RequiredLaunchOptions` field must default to an empty list (not `null`) when absent from the fetched JSON — same defensive pattern already applied to `Files` in `JavaModManifestFetcher.FetchAsync()`.
- **Why this is one task, not several:** `JavaModManifest` and `JavaModInfo` are positional records consumed by construction (not just property access) in multiple existing files across both `GlasLauncher.Core` and its test project — `JavaModService.cs`, `FakeJavaModService.cs`, `JavaFileInspectorTests.cs`, and `JavaModEvaluatorTests.cs` all call `new JavaModManifest(...)` or `new JavaModInfo(...)` positionally today. Adding a required constructor parameter to either record breaks every one of those call sites simultaneously. Likewise, `SteamLaunchOptionInspector`'s signature change breaks its one production caller (`SteamEnvironment`) the moment the old method is removed. Because `dotnet build`/`dotnet test` compile a whole project at once — a single broken file anywhere in `GlasLauncher.Core` or its test project blocks every test from running, `--filter` or not — there is no intermediate point between "old shape everywhere" and "new shape everywhere" where the solution compiles. Splitting this into several tasks would mean asking for a build/test verification that is guaranteed to fail through no fault of that task's own work. All the steps below are still small and TDD-ordered internally; they just share one final build+test checkpoint and one commit, because that is the first point at which a checkpoint can actually succeed.

---

### Task 1: Manifest-driven required launch options (models, logic, services, evaluator — one atomic unit)

**Files:**
- Modify: `src/GlasLauncher.Core/Models/JavaModManifest.cs`
- Modify: `src/GlasLauncher.Core/Models/JavaModInfo.cs`
- Modify: `src/GlasLauncher.Core/Services/JavaModManifestFetcher.cs`
- Modify: `src/GlasLauncher.Core/Services/Fakes/FakeJavaModService.cs`
- Modify: `src/GlasLauncher.Core/Logic/SteamLaunchOptionInspector.cs`
- Modify: `src/GlasLauncher.Core/Services/ISteamEnvironment.cs`
- Modify: `src/GlasLauncher.Core/Services/SteamEnvironment.cs`
- Modify: `src/GlasLauncher.Core/Services/Fakes/FakeSteamEnvironment.cs`
- Modify: `src/GlasLauncher.Core/Services/JavaModService.cs`
- Modify: `src/GlasLauncher.Core/Logic/JavaModEvaluator.cs`
- Modify: `tests/GlasLauncher.Core.Tests/JavaModManifestFetcherTests.cs`
- Modify: `tests/GlasLauncher.Core.Tests/JavaFileInspectorTests.cs`
- Modify: `tests/GlasLauncher.Core.Tests/SteamLaunchOptionInspectorTests.cs`
- Modify: `tests/GlasLauncher.Core.Tests/JavaModEvaluatorTests.cs`

**Interfaces:**
- Produces: `record JavaModManifest(IReadOnlyList<JavaFileEntry> Files, IReadOnlyList<string> RequiredLaunchOptions)`, `record JavaModInfo(bool LaunchOptionConfigured, IReadOnlyList<string> RequiredLaunchOptions, IReadOnlyList<JavaFileStatus> Files)`, `static class SteamLaunchOptionInspector { public static bool AreLaunchOptionsConfigured(string steamPath, string appId, IReadOnlyList<string> requiredOptions); }`, `ISteamEnvironment.IsJavaAgentLaunchOptionConfiguredAsync(IReadOnlyList<string> requiredOptions) : Task<bool>`.

Work through the steps in the exact order given — later steps depend on earlier ones compiling in a specific intermediate order (test files first, then production code, ending with the Steam-facing layer and the two consumers that tie it all together).

- [ ] **Step 1: Update `JavaModManifestFetcherTests.cs` — add the new field's null-safety test**

Add this test to `tests/GlasLauncher.Core.Tests/JavaModManifestFetcherTests.cs`, right after `FetchAsync_ValidJsonWithoutFilesKey_ReturnsManifestWithEmptyFiles`:

```csharp
    [Fact]
    public async Task FetchAsync_ValidJsonWithoutRequiredLaunchOptionsKey_ReturnsManifestWithEmptyList()
    {
        const string json = "{}";
        var httpClient = new HttpClient(new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        }));
        var fetcher = new JavaModManifestFetcher(httpClient);

        var manifest = await fetcher.FetchAsync();

        Assert.NotNull(manifest);
        Assert.NotNull(manifest!.RequiredLaunchOptions);
        Assert.Empty(manifest.RequiredLaunchOptions);
    }
```

- [ ] **Step 2: Update `JavaFileInspectorTests.cs` — add the second constructor argument to every `JavaModManifest`**

In `tests/GlasLauncher.Core.Tests/JavaFileInspectorTests.cs`, there are 4 positional constructions of `new JavaModManifest(new[] { ... })`, one in each of `GetFileStatuses_FileMatchesHash_ReturnsUpToDate`, `GetFileStatuses_FileMissing_ReturnsNotUpToDate`, `GetFileStatuses_FileHashMismatch_ReturnsNotUpToDate`, and `GetFileStatuses_MultipleEntries_ReturnsOneStatusPerEntryInOrder`. In each of the 4, add a trailing `, Array.Empty<string>()` argument right after the closing `}` of the `JavaFileEntry` array and before the closing `)` of the `JavaModManifest(...)` call. For example, in the first test, change:

```csharp
        var manifest = new JavaModManifest(new[]
        {
            new JavaFileEntry("GlasVoipMod.jar", "0.1.0", MatchingSha256, "https://example.com/GlasVoipMod.jar")
        });
```

to:

```csharp
        var manifest = new JavaModManifest(
            new[]
            {
                new JavaFileEntry("GlasVoipMod.jar", "0.1.0", MatchingSha256, "https://example.com/GlasVoipMod.jar")
            },
            Array.Empty<string>());
```

Apply the same pattern (wrap the existing array argument, add `Array.Empty<string>()` as the second argument) to all 4 call sites in this file, including the two-entry array in `GetFileStatuses_MultipleEntries_ReturnsOneStatusPerEntryInOrder`. These tests don't exercise launch options at all — an empty list is correct and sufficient for all 4.

- [ ] **Step 3: Update `SteamLaunchOptionInspectorTests.cs` — migrate the 8 existing tests to a list, add 3 new tests**

Replace the constant declaration:

```csharp
    private const string RequiredOption = "-agentlib:zbNative --";
```

with:

```csharp
    private static readonly string[] RequiredOptions = { "-agentlib:zbNative --" };
```

Then, in every one of the 8 existing test methods, replace every call:

```csharp
        var result = SteamLaunchOptionInspector.IsLaunchOptionConfigured(steamPath, AppId, RequiredOption);
```

with:

```csharp
        var result = SteamLaunchOptionInspector.AreLaunchOptionsConfigured(steamPath, AppId, RequiredOptions);
```

There are 8 occurrences, one per `[Fact]` method. Also rename each of the 8 method names from `IsLaunchOptionConfigured_...` to `AreLaunchOptionsConfigured_...` (same suffix, only the prefix changes), so the test names keep matching the method they exercise:

- `IsLaunchOptionConfigured_OptionPresent_ReturnsTrue` → `AreLaunchOptionsConfigured_OptionPresent_ReturnsTrue`
- `IsLaunchOptionConfigured_OptionPresentAmongOthers_ReturnsTrue` → `AreLaunchOptionsConfigured_OptionPresentAmongOthers_ReturnsTrue`
- `IsLaunchOptionConfigured_OptionAbsent_ReturnsFalse` → `AreLaunchOptionsConfigured_OptionAbsent_ReturnsFalse`
- `IsLaunchOptionConfigured_AppEntryMissing_ReturnsFalse` → `AreLaunchOptionsConfigured_AppEntryMissing_ReturnsFalse`
- `IsLaunchOptionConfigured_LoginUsersVdfMissing_ReturnsFalse` → `AreLaunchOptionsConfigured_LoginUsersVdfMissing_ReturnsFalse`
- `IsLaunchOptionConfigured_NoMostRecentAccount_ReturnsFalse` → `AreLaunchOptionsConfigured_NoMostRecentAccount_ReturnsFalse`
- `IsLaunchOptionConfigured_LocalConfigVdfMissing_ReturnsFalse` → `AreLaunchOptionsConfigured_LocalConfigVdfMissing_ReturnsFalse`
- `IsLaunchOptionConfigured_CorruptedLoginUsersVdf_ReturnsFalse` → `AreLaunchOptionsConfigured_CorruptedLoginUsersVdf_ReturnsFalse`

Do not change any other part of these 8 tests — their setup, VDF fixtures, and assertions are unaffected by this signature change.

Then add these 3 new tests to the same file, after the renamed `AreLaunchOptionsConfigured_CorruptedLoginUsersVdf_ReturnsFalse`:

```csharp
    [Fact]
    public void AreLaunchOptionsConfigured_MultipleOptionsAllPresent_ReturnsTrue()
    {
        var steamPath = CreateTempDir();
        WriteLoginUsers(steamPath, SteamId64, mostRecent: true);
        WriteLocalConfig(steamPath, AccountId, AppId, "-javaagent:GlasVoipMod.jar -agentlib:zbNative --");

        var result = SteamLaunchOptionInspector.AreLaunchOptionsConfigured(
            steamPath, AppId, new[] { "-javaagent:GlasVoipMod.jar", "-agentlib:zbNative --" });

        Assert.True(result);

        Directory.Delete(steamPath, recursive: true);
    }

    [Fact]
    public void AreLaunchOptionsConfigured_OneOfMultipleOptionsMissing_ReturnsFalse()
    {
        var steamPath = CreateTempDir();
        WriteLoginUsers(steamPath, SteamId64, mostRecent: true);
        WriteLocalConfig(steamPath, AccountId, AppId, "-javaagent:GlasVoipMod.jar");

        var result = SteamLaunchOptionInspector.AreLaunchOptionsConfigured(
            steamPath, AppId, new[] { "-javaagent:GlasVoipMod.jar", "-agentlib:zbNative --" });

        Assert.False(result);

        Directory.Delete(steamPath, recursive: true);
    }

    [Fact]
    public void AreLaunchOptionsConfigured_EmptyRequiredList_ReturnsTrue()
    {
        var steamPath = CreateTempDir();
        WriteLoginUsers(steamPath, SteamId64, mostRecent: true);
        WriteLocalConfig(steamPath, AccountId, AppId, "-high");

        var result = SteamLaunchOptionInspector.AreLaunchOptionsConfigured(steamPath, AppId, Array.Empty<string>());

        Assert.True(result);

        Directory.Delete(steamPath, recursive: true);
    }
```

- [ ] **Step 4: Update `JavaModEvaluatorTests.cs` — migrate to the new `JavaModInfo` shape, add a multi-option test**

Replace the full content of `tests/GlasLauncher.Core.Tests/JavaModEvaluatorTests.cs`:

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
        var info = new JavaModInfo(
            LaunchOptionConfigured: true,
            RequiredLaunchOptions: new[] { "-javaagent:GlasVoipMod.jar" },
            Files: Array.Empty<JavaFileStatus>());

        var result = JavaModEvaluator.Evaluate(info);

        Assert.Equal(CheckStatus.Failed, result.Status);
    }

    [Fact]
    public void Evaluate_LaunchOptionNotConfigured_ReturnsFailed()
    {
        var info = new JavaModInfo(
            LaunchOptionConfigured: false,
            RequiredLaunchOptions: new[] { "-javaagent:GlasVoipMod.jar" },
            Files: new[] { new JavaFileStatus("GlasVoipMod.jar", "0.1.0", "0.1.0", IsUpToDate: true) });

        var result = JavaModEvaluator.Evaluate(info);

        Assert.Equal(CheckStatus.Failed, result.Status);
    }

    [Fact]
    public void Evaluate_FileOutdated_ReturnsFailed()
    {
        var info = new JavaModInfo(
            LaunchOptionConfigured: true,
            RequiredLaunchOptions: new[] { "-javaagent:GlasVoipMod.jar" },
            Files: new[] { new JavaFileStatus("GlasVoipMod.jar", null, "0.1.0", IsUpToDate: false) });

        var result = JavaModEvaluator.Evaluate(info);

        Assert.Equal(CheckStatus.Failed, result.Status);
    }

    [Fact]
    public void Evaluate_LaunchOptionNotConfiguredAndNoFiles_ReturnsFailedWithLaunchOptionMessage()
    {
        var info = new JavaModInfo(
            LaunchOptionConfigured: false,
            RequiredLaunchOptions: new[] { "-javaagent:GlasVoipMod.jar" },
            Files: Array.Empty<JavaFileStatus>());

        var result = JavaModEvaluator.Evaluate(info);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains("-javaagent:GlasVoipMod.jar", result.Message);
    }

    [Fact]
    public void Evaluate_LaunchOptionNotConfiguredWithMultipleRequiredOptions_MessageContainsAllOfThem()
    {
        var info = new JavaModInfo(
            LaunchOptionConfigured: false,
            RequiredLaunchOptions: new[] { "-javaagent:GlasVoipMod.jar", "-agentlib:zbNative --" },
            Files: Array.Empty<JavaFileStatus>());

        var result = JavaModEvaluator.Evaluate(info);

        Assert.Contains("-javaagent:GlasVoipMod.jar", result.Message);
        Assert.Contains("-agentlib:zbNative --", result.Message);
    }

    [Fact]
    public void Evaluate_LaunchOptionConfiguredAndAllFilesUpToDate_ReturnsPassed()
    {
        var info = new JavaModInfo(
            LaunchOptionConfigured: true,
            RequiredLaunchOptions: new[] { "-javaagent:GlasVoipMod.jar" },
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

- [ ] **Step 5: Confirm the current state fails to build**

Run: `dotnet build` (full solution, from repo root)
Expected: Build FAILS. This is the combined "red" checkpoint for this whole unit — the 4 test files just updated now reference shapes (`JavaModManifest` with 2 args, `JavaModInfo` with 3 args, `SteamLaunchOptionInspector.AreLaunchOptionsConfigured`) that don't exist in production code yet. Do not be alarmed by a long list of errors — confirm they are all in the test project and reference the symbols this plan is about to add, not something unrelated.

- [ ] **Step 6: Replace `JavaModManifest`**

Replace the full content of `src/GlasLauncher.Core/Models/JavaModManifest.cs`:

```csharp
namespace GlasLauncher.Core.Models;

public record JavaModManifest(IReadOnlyList<JavaFileEntry> Files, IReadOnlyList<string> RequiredLaunchOptions);
```

- [ ] **Step 7: Replace `JavaModInfo`**

Replace the full content of `src/GlasLauncher.Core/Models/JavaModInfo.cs`:

```csharp
namespace GlasLauncher.Core.Models;

public record JavaModInfo(bool LaunchOptionConfigured, IReadOnlyList<string> RequiredLaunchOptions, IReadOnlyList<JavaFileStatus> Files);
```

- [ ] **Step 8: Extend `JavaModManifestFetcher.FetchAsync()`'s null-safety to `RequiredLaunchOptions`**

In `src/GlasLauncher.Core/Services/JavaModManifestFetcher.cs`, replace the `FetchAsync` method body's return line:

```csharp
            return manifest is null ? null : manifest with { Files = manifest.Files ?? Array.Empty<JavaFileEntry>() };
```

with:

```csharp
            return manifest is null
                ? null
                : manifest with
                {
                    Files = manifest.Files ?? Array.Empty<JavaFileEntry>(),
                    RequiredLaunchOptions = manifest.RequiredLaunchOptions ?? Array.Empty<string>()
                };
```

- [ ] **Step 9: Update `FakeJavaModService` to the new `JavaModInfo` shape**

Replace the full content of `src/GlasLauncher.Core/Services/Fakes/FakeJavaModService.cs`:

```csharp
using GlasLauncher.Core.Models;

namespace GlasLauncher.Core.Services.Fakes;

public class FakeJavaModService : IJavaModService
{
    public Task<JavaModInfo> GetStatusAsync() =>
        Task.FromResult(new JavaModInfo(
            LaunchOptionConfigured: true,
            RequiredLaunchOptions: new[] { "-javaagent:GlasVoipMod.jar" },
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

- [ ] **Step 10: Implement `AreLaunchOptionsConfigured` in `SteamLaunchOptionInspector`**

In `src/GlasLauncher.Core/Logic/SteamLaunchOptionInspector.cs`, replace:

```csharp
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
```

with:

```csharp
    public static bool AreLaunchOptionsConfigured(string steamPath, string appId, IReadOnlyList<string> requiredOptions)
    {
        var accountId = FindMostRecentAccountId(steamPath);
        if (accountId is null)
        {
            return false;
        }

        var localConfigPath = Path.Combine(steamPath, "userdata", accountId, "config", "localconfig.vdf");
        var launchOptions = ReadLaunchOptions(localConfigPath, appId);
        return launchOptions is not null && requiredOptions.All(launchOptions.Contains);
    }
```

The two private helpers `FindMostRecentAccountId`/`ReadLaunchOptions` below this method are unaffected — do not modify them. No new `using` is needed for `.All()` — `System.Linq` is part of `GlasLauncher.Core`'s implicit-usings set.

- [ ] **Step 11: Update `ISteamEnvironment`**

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
    Task<bool> IsJavaAgentLaunchOptionConfiguredAsync(IReadOnlyList<string> requiredOptions);
}
```

- [ ] **Step 12: Update `SteamEnvironment`**

In `src/GlasLauncher.Core/Services/SteamEnvironment.cs`, replace:

```csharp
    private const string AppId = "108600";
    private const string RequiredLaunchOption = "-agentlib:zbNative --";
```

with:

```csharp
    private const string AppId = "108600";

    // Placeholder — the VOIP mod (GlasVoipMod) has no stable jar name/release yet. Used only
    // as a fallback when the remote manifest doesn't supply RequiredLaunchOptions (i.e. no real
    // hosting exists yet, same situation as JavaModManifestFetcher's placeholder URL). Once the
    // manifest is hosted for real, its RequiredLaunchOptions takes over automatically and this
    // default is never consulted.
    private const string DefaultRequiredLaunchOption = "-javaagent:GlasVoipMod.jar";
```

Then replace:

```csharp
    public Task<bool> IsJavaAgentLaunchOptionConfiguredAsync() =>
        Task.FromResult(_steamPath is not null
            && SteamLaunchOptionInspector.IsLaunchOptionConfigured(_steamPath, AppId, RequiredLaunchOption));
```

with:

```csharp
    public Task<bool> IsJavaAgentLaunchOptionConfiguredAsync(IReadOnlyList<string> requiredOptions)
    {
        var effectiveOptions = requiredOptions.Count > 0 ? requiredOptions : new[] { DefaultRequiredLaunchOption };
        return Task.FromResult(_steamPath is not null
            && SteamLaunchOptionInspector.AreLaunchOptionsConfigured(_steamPath, AppId, effectiveOptions));
    }
```

- [ ] **Step 13: Update `FakeSteamEnvironment`**

In `src/GlasLauncher.Core/Services/Fakes/FakeSteamEnvironment.cs`, replace:

```csharp
    public Task<bool> IsJavaAgentLaunchOptionConfiguredAsync() => Task.FromResult(true);
```

with:

```csharp
    public Task<bool> IsJavaAgentLaunchOptionConfiguredAsync(IReadOnlyList<string> requiredOptions) => Task.FromResult(true);
```

(The Fake's "always succeeds" behavior is unchanged — it still ignores the parameter entirely, same as it ignored having no parameter before.)

- [ ] **Step 14: Reorder `JavaModService.GetStatusAsync()` to fetch the manifest first**

Replace the full body of `GetStatusAsync()` in `src/GlasLauncher.Core/Services/JavaModService.cs` (do not touch `RepairAsync` below it):

```csharp
    public async Task<JavaModInfo> GetStatusAsync()
    {
        try
        {
            var manifest = await _manifestFetcher.FetchAsync();
            var requiredLaunchOptions = manifest?.RequiredLaunchOptions ?? Array.Empty<string>();
            var launchOptionConfigured = await _steamEnvironment.IsJavaAgentLaunchOptionConfiguredAsync(requiredLaunchOptions);

            var installPath = await _steamEnvironment.GetGameInstallPathAsync();
            if (installPath is null || manifest is null)
            {
                return new JavaModInfo(launchOptionConfigured, requiredLaunchOptions, Array.Empty<JavaFileStatus>());
            }

            var files = JavaFileInspector.GetFileStatuses(installPath, manifest);
            return new JavaModInfo(launchOptionConfigured, requiredLaunchOptions, files);
        }
        catch (Exception)
        {
            return new JavaModInfo(false, Array.Empty<string>(), Array.Empty<JavaFileStatus>());
        }
    }
```

This collapses the two previous nested null-checks (`installPath is null` / `manifest is null`, each returning separately) into one combined check, and moves the whole method inside a single `try`/`catch` (matching the never-throw contract already required).

- [ ] **Step 15: Build the dynamic message into `JavaModEvaluator`**

Replace the full content of `src/GlasLauncher.Core/Logic/JavaModEvaluator.cs`:

```csharp
using System.Linq;
using GlasLauncher.Core.Models;

namespace GlasLauncher.Core.Logic;

public static class JavaModEvaluator
{
    private const string CheckName = "Mod Java à jour";

    public static CheckResult Evaluate(JavaModInfo info)
    {
        if (!info.LaunchOptionConfigured)
        {
            return new CheckResult(
                CheckName,
                CheckStatus.Failed,
                "Option de lancement Steam manquante pour l'agent Java. Ajoutez ceci aux options de " +
                $"lancement du jeu (Steam > clic droit sur Project Zomboid > Propriétés) :\n{string.Join(" ", info.RequiredLaunchOptions)}");
        }

        if (info.Files.Count == 0)
        {
            return new CheckResult(CheckName, CheckStatus.Failed, "Impossible de vérifier le mod Java.");
        }

        if (info.Files.Any(f => !f.IsUpToDate))
        {
            return new CheckResult(CheckName, CheckStatus.Failed, "Le mod Java n'est pas à jour.");
        }

        return new CheckResult(CheckName, CheckStatus.Passed, "Agent Java synchronisé.");
    }
}
```

- [ ] **Step 16: Build the full solution**

Run: `dotnet build`
Expected: Build succeeded, 0 errors, 0 warnings. This is the first point since Step 5 where the solution compiles — every file this plan touches has now been updated.

- [ ] **Step 17: Run the full test suite**

Run: `dotnet test tests/GlasLauncher.Core.Tests`
Expected: PASS — all tests green, 0 failed. The total test count should be 5 more than before this plan (JavaModManifestFetcherTests +1, SteamLaunchOptionInspectorTests +3, JavaModEvaluatorTests +1); confirm no test anywhere in the suite fails, not just the files this plan touched.

- [ ] **Step 18: Commit**

```bash
git add src/GlasLauncher.Core/Models/JavaModManifest.cs src/GlasLauncher.Core/Models/JavaModInfo.cs src/GlasLauncher.Core/Services/JavaModManifestFetcher.cs src/GlasLauncher.Core/Services/Fakes/FakeJavaModService.cs src/GlasLauncher.Core/Logic/SteamLaunchOptionInspector.cs src/GlasLauncher.Core/Services/ISteamEnvironment.cs src/GlasLauncher.Core/Services/SteamEnvironment.cs src/GlasLauncher.Core/Services/Fakes/FakeSteamEnvironment.cs src/GlasLauncher.Core/Services/JavaModService.cs src/GlasLauncher.Core/Logic/JavaModEvaluator.cs tests/GlasLauncher.Core.Tests/JavaModManifestFetcherTests.cs tests/GlasLauncher.Core.Tests/JavaFileInspectorTests.cs tests/GlasLauncher.Core.Tests/SteamLaunchOptionInspectorTests.cs tests/GlasLauncher.Core.Tests/JavaModEvaluatorTests.cs
git commit -m "fix(core): make the required Java mod launch option manifest-driven

ZombieBuddy investigation on the GlasVoipMod side found the port
technically blocked; the VOIP mod stays a standalone -javaagent:,
so the previously hardcoded -agentlib:zbNative -- checked the wrong
thing. Required launch options now come from the remote manifest,
falling back to a placeholder default until real hosting exists."
```

---

## Manual verification (Windows VM, after this task)

Automated tests cover all VDF-parsing, evaluator, and manifest-parsing logic cross-platform. The Windows-only orchestration (`SteamEnvironment.IsJavaAgentLaunchOptionConfiguredAsync`, now taking a parameter) needs a manual pass, same as when this check was first built:

1. Since the manifest URL is still a placeholder with no real hosting, confirm `GetStatusAsync()` uses the `DefaultRequiredLaunchOption` fallback (`-javaagent:GlasVoipMod.jar`) — the Dashboard's "Mod Java à jour" check, when failing on the launch option, should now show this placeholder in its copyable message instead of the old `-agentlib:zbNative --`.
2. Confirm the check still flips correctly: add `-javaagent:GlasVoipMod.jar` to Project Zomboid's Steam launch options manually, relaunch the app, confirm the check no longer reports the launch-option failure (it will still report the generic "Impossible de vérifier le mod Java." since there's still no real manifest — that's expected, matches the pre-existing limitation documented in `docs/session-notes.md`).
3. Once `GlasVoipMod` has a real hosted manifest with its own `RequiredLaunchOptions` (future work, not part of this plan): confirm the Dashboard message updates automatically to the real value with no launcher code changes needed.
