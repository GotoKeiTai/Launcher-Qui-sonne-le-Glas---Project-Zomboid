using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace GlasLauncher.App.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private const string DiscordInviteUrl = "https://discord.gg/UmKM25QUhY";

    public event Action? BackRequested;

    [ObservableProperty]
    private string _installPath = @"D:\SteamLibrary\steamapps\common\ProjectZomboid";

    [ObservableProperty]
    private string? _statusMessage;

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
    private void GenerateDiagnosticReport()
    {
        StatusMessage = "Rapport généré (simulation).";
    }

    [RelayCommand]
    private void OpenLauncherLogs()
    {
        var path = GetLauncherLogsPath();
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    [RelayCommand]
    private void OpenPzLogs()
    {
        var path = GetPzLogsPath();
        if (!Directory.Exists(path))
        {
            StatusMessage = "Dossier de logs Project Zomboid introuvable — le jeu n'a peut-être pas encore été lancé.";
            return;
        }

        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    [RelayCommand]
    private void JoinDiscord()
    {
        Process.Start(new ProcessStartInfo(DiscordInviteUrl) { UseShellExecute = true });
    }

    [RelayCommand]
    private async Task CopyVersionInfoAsync()
    {
        var clipboard = GetMainWindow()?.Clipboard;
        if (clipboard is null)
        {
            return;
        }

        await clipboard.SetTextAsync("Launcher v0.1.0 · Project Zomboid 41.78.16 · Mod Java v1.0.0");
        StatusMessage = "Copié dans le presse-papiers.";
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
