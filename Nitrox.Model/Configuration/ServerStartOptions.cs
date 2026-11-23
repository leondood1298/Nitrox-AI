using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Nitrox.Model.Configuration.Validators;

namespace Nitrox.Model.Configuration;

/// <summary>
///     Configuration with the information needed to start a Nitrox game server.
/// </summary>
public sealed partial record ServerStartOptions
{
    [ConfigurationKeyName("save")]
    [Required]
    [SaveName]
    public string SaveName { get; set; } = "My World";

    /// <summary>
    ///     Gets or sets the root directory containing the game files.
    /// </summary>
    [ConfigurationKeyName("game-path")]
    [Required]
    [DirectoryPath]
    [RootedPath]
    public string? GamePath { get; set; }

    /// <summary>
    ///     Gets or sets the location where Nitrox will store save and cache files.
    /// </summary>
    [ConfigurationKeyName("data-path")]
    [Required]
    [DirectoryPath]
    [RootedPath]
    public string? NitroxAppDataPath { get; set; }

    /// <summary>
    ///     Gets or sets the location of the Nitrox assets. Defaults to the root path of the Nitrox installation.
    /// </summary>
    [ConfigurationKeyName("assets-path")]
    [Required]
    [DirectoryPath]
    [RootedPath]
    public string? NitroxAssetsPath { get; set; }

    /// <summary>
    ///     Gets or sets the folder holding the worlds. Defaults to the "saves" folder in the data folder.
    /// </summary>
    /// <remarks>
    ///     The launcher can keep its worlds in a folder of its choice (its "SavesFolderDir" setting), which the data folder
    ///     alone cannot express.
    /// </remarks>
    [ConfigurationKeyName("saves-path")]
    public string? SavesPath { get; set; }

    [ConfigurationKeyName("embedded")]
    public bool IsEmbedded { get; set; }

    [OptionsValidator]
    public partial class Validator : IValidateOptions<ServerStartOptions>;
}
