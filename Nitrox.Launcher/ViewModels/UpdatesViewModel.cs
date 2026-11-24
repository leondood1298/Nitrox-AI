using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nitrox.Launcher.Models.Design;
using Nitrox.Launcher.Models.Services;
using Nitrox.Launcher.Models.Utils;
using Nitrox.Launcher.ViewModels.Abstract;
using Nitrox.Model.Core;
using Nitrox.Model.Logger;
using Nitrox.Model.Platforms.OS.Shared;

namespace Nitrox.Launcher.ViewModels;

/// <summary>
///     Shows this build's version and the launcher's installation backups.
/// </summary>
/// <remarks>
///     Upstream's update check, download and changelog are gone: this fork's builds do not come from there, and the offered
///     "update" deleted the fork's files and installed upstream Nitrox, whose code then opened the fork's worlds.
/// </remarks>
internal partial class UpdatesViewModel(DialogService dialogService, Func<Window> mainWindowProvider, BackupService backupService) : RoutableViewModelBase
{
    private readonly DialogService dialogService = dialogService;
    private readonly Func<Window> mainWindowProvider = mainWindowProvider;
    private readonly BackupService backupService = backupService;

    [ObservableProperty]
    public partial AvaloniaList<BackupInfo> AvailableBackups { get; set; } = [];

    public string Version { get; } = $"{NitroxEnvironment.Version} ({NitroxEnvironment.GitShortHash})";

    internal override async Task ViewContentLoadAsync(CancellationToken cancellationToken = default)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            try
            {
                RefreshBackups();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while listing the launcher's backups");
            }
        });
    }

    [RelayCommand]
    private void RefreshBackups()
    {
        AvailableBackups.Clear();
        AvailableBackups.AddRange(backupService.GetAvailableBackups());
    }

    [RelayCommand]
    private async Task DeleteBackup(BackupInfo? backup)
    {
        if (backup == null)
        {
            return;
        }

        DialogBoxViewModel confirmResult = await dialogService.ShowAsync<DialogBoxViewModel>(model =>
        {
            model.Title = "Delete backup?";
            model.Description = $"Are you sure you want to delete the backup '{backup.FileName}'?\nThis action cannot be undone.";
            model.ButtonOptions = ButtonOptions.YesNo;
        });

        if (confirmResult && backupService.DeleteBackup(backup.FilePath))
        {
            AvailableBackups.Remove(backup);
            LauncherNotifier.Success("Backup deleted");
        }
    }

    [RelayCommand]
    private async Task RestoreBackup(BackupInfo? backup)
    {
        if (backup == null)
        {
            return;
        }

        DialogBoxViewModel confirmResult = await dialogService.ShowAsync<DialogBoxViewModel>(model =>
        {
            model.Title = "Restore backup?";
            model.Description = $"This will restore Nitrox to version {backup.Version} and overwrite your current installation.";
            if (backup.IncludesSaves)
            {
                model.Description += "\n\nThis backup includes save files which will also be restored, potentially overwriting your current saves.";
            }
            model.Description += "\n\nThe launcher will close and restart after the restore is complete.\n\nDo you want to continue?";
            model.ButtonOptions = ButtonOptions.YesNo;
        });

        if (!confirmResult)
        {
            return;
        }

        string? scriptPath = await backupService.CreateRestoreScriptAsync(backup.FilePath);
        if (scriptPath == null)
        {
            LauncherNotifier.Error("Failed to create restore script");
            return;
        }

        LauncherNotifier.Success("Restoring backup... The launcher will restart.");

        // Start the restore script and exit
        using Process? script = ProcessEx.StartProcessDetached(new ProcessStartInfo
        {
            FileName = scriptPath,
            CreateNoWindow = true
        });
        mainWindowProvider().CloseByCode();
    }

    [RelayCommand]
    private void OpenBackupsFolder()
    {
        string backupsDir = BackupService.BackupsDirectory;
        Directory.CreateDirectory(backupsDir);

        try
        {
            OpenPath(backupsDir);
        }
        catch (Exception ex)
        {
            LauncherNotifier.Error($"Failed to open backups folder: {ex.Message}");
        }
    }
}
