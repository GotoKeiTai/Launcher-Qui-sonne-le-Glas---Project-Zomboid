using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlasLauncher.Core.Services;

namespace GlasLauncher.App.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private const string DiscordInviteUrl = "https://discord.gg/UmKM25QUhY";

    private readonly IUpdateService _updateService;
    private readonly ISteamEnvironment _steamEnvironment;
    private readonly IJavaModService _javaModService;
    private readonly IDiagnosticReportService _diagnosticReportService;

    public SettingsViewModel(
        IUpdateService updateService,
        ISteamEnvironment steamEnvironment,
        IJavaModService javaModService,
        IDiagnosticReportService diagnosticReportService)
    {
        _updateService = updateService;
        _steamEnvironment = steamEnvironment;
        _javaModService = javaModService;
        _diagnosticReportService = diagnosticReportService;
        _versionInfoText = $"Launcher {_updateService.GetCurrentVersion()} · Chargement…";
        _ = RefreshVersionInfoAsync();
    }

    [ObservableProperty]
    private string _versionInfoText;

    public async Task RefreshVersionInfoAsync()
    {
        var detectedVersion = await _steamEnvironment.GetInstalledGameVersionAsync();
        var javaModInfo = await _javaModService.GetStatusAsync();
        var javaModFile = javaModInfo.Files.FirstOrDefault();
        var javaModVersionText = javaModFile switch
        {
            null => "non installé",
            { IsUpToDate: true } => $"v{javaModFile.InstalledVersion}",
            _ => "non installé"
        };

        VersionInfoText =
            $"Launcher {_updateService.GetCurrentVersion()} · Project Zomboid {detectedVersion?.BuildId ?? "introuvable"} · Mod Java {javaModVersionText}";
    }

    public event Action? BackRequested;

    [ObservableProperty]
    private string _installPath = @"D:\SteamLibrary\steamapps\common\ProjectZomboid";

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isStatusSuccess;

    [RelayCommand]
    private async Task BrowseAsync()
    {
        var mainWindow = GetMainWindow();
        if (mainWindow?.StorageProvider is null)
        {
            return;
        }

        var folders = await mainWindow.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Sélectionner le dossier d'installation de Project Zomboid",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            InstallPath = folders[0].Path.LocalPath;
        }
    }

    [RelayCommand]
    private async Task GenerateDiagnosticReportAsync()
    {
        try
        {
            var zipPath = await _diagnosticReportService.GenerateAsync();
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{zipPath}\"") { UseShellExecute = true });
            StatusMessage = "Rapport généré et Explorateur ouvert.";
            IsStatusSuccess = true;
        }
        catch (Exception ex)
        {
            StatusMessage = "Impossible de générer le rapport : " + ex.Message;
            IsStatusSuccess = false;
        }
    }

    [RelayCommand]
    private void OpenLauncherLogs()
    {
        try
        {
            var path = GetLauncherLogsPath();
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            StatusMessage = "Impossible d'ouvrir le dossier de logs : " + ex.Message;
            IsStatusSuccess = false;
        }
    }

    [RelayCommand]
    private void OpenPzLogs()
    {
        var path = GetPzLogsPath();
        if (!Directory.Exists(path))
        {
            StatusMessage = "Dossier de logs Project Zomboid introuvable — le jeu n'a peut-être pas encore été lancé.";
            IsStatusSuccess = false;
            return;
        }

        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    [RelayCommand]
    private void JoinDiscord()
    {
        try
        {
            Process.Start(new ProcessStartInfo(DiscordInviteUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            StatusMessage = "Impossible d'ouvrir Discord : " + ex.Message;
            IsStatusSuccess = false;
        }
    }

    [RelayCommand]
    private async Task CopyVersionInfoAsync()
    {
        var clipboard = GetMainWindow()?.Clipboard;
        if (clipboard is null)
        {
            return;
        }

        await clipboard.SetTextAsync(VersionInfoText);
        StatusMessage = "Copié dans le presse-papiers.";
        IsStatusSuccess = true;
    }

    [RelayCommand]
    private void Back() => BackRequested?.Invoke();

    private static Window? GetMainWindow() =>
        Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;

    private static string GetLauncherLogsPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GlasLauncher", "logs");

    private static string GetPzLogsPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Zomboid", "Logs");
}
