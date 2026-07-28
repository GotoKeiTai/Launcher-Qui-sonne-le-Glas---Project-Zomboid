using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlasLauncher.Core.Models;
using GlasLauncher.Core.Services;

namespace GlasLauncher.App.ViewModels;

public partial class NewsViewModel : ViewModelBase
{
    private readonly IServerInfoService _serverInfoService;

    public event Action? BackRequested;

    public NewsViewModel(IServerInfoService serverInfoService)
    {
        _serverInfoService = serverInfoService;
        NewsItems = new ObservableCollection<NewsItem>();
        ChangelogEntries = new ObservableCollection<ChangelogEntry>();
        IsNewsTabActive = true;

        _ = LoadAsync();
    }

    [ObservableProperty]
    private bool _isNewsTabActive;

    [ObservableProperty]
    private bool _isChangelogTabActive;

    [ObservableProperty]
    private string? _statusMessage;

    public ObservableCollection<NewsItem> NewsItems { get; }

    public ObservableCollection<ChangelogEntry> ChangelogEntries { get; }

    [RelayCommand]
    private void ShowNewsTab()
    {
        IsNewsTabActive = true;
        IsChangelogTabActive = false;
    }

    [RelayCommand]
    private void ShowChangelogTab()
    {
        IsNewsTabActive = false;
        IsChangelogTabActive = true;
    }

    [RelayCommand]
    private void Back() => BackRequested?.Invoke();

    private async Task LoadAsync()
    {
        try
        {
            foreach (var item in await _serverInfoService.GetNewsAsync())
            {
                NewsItems.Add(item);
            }

            foreach (var entry in await _serverInfoService.GetChangelogAsync())
            {
                ChangelogEntries.Add(entry);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Erreur lors du chargement : " + ex.Message;
        }
    }
}
