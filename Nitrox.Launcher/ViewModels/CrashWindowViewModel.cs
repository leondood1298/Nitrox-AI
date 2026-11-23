using System;
using System.Web;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nitrox.Launcher.ViewModels.Abstract;
using Nitrox.Model.Core;
using Nitrox.Model.Helper;
using Nitrox.Model.Platforms.Discovery.Models;
using Nitrox.Model.Platforms.OS.Shared;

namespace Nitrox.Launcher.ViewModels;

internal partial class CrashWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string? Title { get; set; }

    [ObservableProperty]
    public partial string? Message { get; set; }

    [RelayCommand(CanExecute = nameof(CanRestart))]
    private void Restart()
    {
        ProcessEx.StartSelf("--allow-instances");
        Environment.Exit(0);
    }

    [RelayCommand]
    private void Report()
    {
        string errorTitle = Message[..Math.Min(Message.Length, 100)];
        try
        {
            errorTitle = Message.Substring(0, Math.Max(0, Math.Min(Message.IndexOf("at ", StringComparison.OrdinalIgnoreCase), Message.IndexOf('\n'))));
        }
        catch
        {
            // ignored
        }
        string storeType = NitroxUser.GamePlatform?.Platform switch
        {
            Platform.STEAM => "Steam",
            Platform.EPIC => "Epic",
            Platform.MICROSOFT => "MS-Store",
            _ => "Other"
        };
        OpenUri(CreateIssueUrl(errorTitle, Message ?? "", $"{NitroxEnvironment.Version} ({NitroxEnvironment.GitShortHash})", storeType));
    }

    /// <summary>
    ///     This fork's own issue tracker. The report used to open a pre-filled issue on upstream Nitrox, which has neither
    ///     this fork's code nor any business with its bugs.
    /// </summary>
    internal const string ISSUE_FORM_URL = "https://github.com/leondood1298/Nitrox-AI/issues/new";

    /// <summary>
    ///     Longest crash text put in the address; browsers and GitHub refuse much longer ones, and the rest is in the log.
    /// </summary>
    private const int MAX_REPORTED_MESSAGE_LENGTH = 4000;

    /// <summary>
    ///     A pre-filled issue the user still reads and submits themselves.
    /// </summary>
    internal static string CreateIssueUrl(string errorTitle, string message, string version, string storeType)
    {
        string reported = message.Length > MAX_REPORTED_MESSAGE_LENGTH ? $"{message[..MAX_REPORTED_MESSAGE_LENGTH]}\n(cut short; the full text is in the launcher log)" : message;
        string title = $"Launcher {version} crashed with {errorTitle}";
        string body = $"Game store: {storeType}\nOperating system: {Environment.OSVersion}\n\n```\n{reported}\n```";
        return $"{ISSUE_FORM_URL}?title={HttpUtility.UrlEncode(title)}&body={HttpUtility.UrlEncode(body)}";
    }

    private bool CanRestart() => !string.IsNullOrWhiteSpace(NitroxUser.ExecutableFilePath ?? Environment.ProcessPath);
}
