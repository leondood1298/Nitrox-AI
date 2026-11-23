using System;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Nitrox.Model.Core;

namespace Nitrox.Model.Serialization;

[DataContract]
public class SaveFileVersion
{
    [DataMember(Order = 1)]
    public int Major { get; }

    [DataMember(Order = 2)]
    public int Minor { get; }

    [DataMember(Order = 3)]
    public int Build { get; }

    [DataMember(Order = 4)]
    public int Revision { get; }

    /// <summary>
    ///     <see cref="NitroxSaveFormat" /> of the world. Absent (0) in worlds from before the number existed.
    /// </summary>
    [DataMember(Order = 5)]
    public int SaveFormat { get; }

    [JsonIgnore]
    public Version Version => new(Major, Minor, Build, Revision);

    /// <summary>
    ///     The version this build writes.
    /// </summary>
    public SaveFileVersion()
    {
        Major = NitroxEnvironment.Version.Major;
        Minor = NitroxEnvironment.Version.Minor;
        Build = NitroxEnvironment.Version.Build;
        Revision = NitroxEnvironment.Version.Revision;
        SaveFormat = NitroxSaveFormat.CURRENT;
    }

    public SaveFileVersion(Version version, int saveFormat = NitroxSaveFormat.UNKNOWN)
    {
        Major = version.Major;
        Minor = version.Minor;
        Build = version.Build;
        Revision = version.Revision;
        SaveFormat = saveFormat;
    }

    [JsonConstructor]
    public SaveFileVersion(int major, int minor, int build, int revision, int saveFormat)
    {
        Major = major;
        Minor = minor;
        Build = build;
        Revision = revision;
        SaveFormat = saveFormat;
    }

    public override string ToString()
    {
        return $"{Major}.{Minor}.{Build}.{Revision}";
    }
}
