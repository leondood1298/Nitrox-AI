using System.Runtime.CompilerServices;
using Nitrox.Model.DataStructures;
using Nitrox.Server.Subnautica.Models.Helper;

namespace Nitrox.Server.Subnautica.Models.Factories;

internal sealed class RandomFactory(IOptions<SubnauticaServerOptions> options)
{
    private readonly IOptions<SubnauticaServerOptions> options = options;

    public static int CreateSeedInt32(string worldSeed, string csFilePath, int seedId = 0) => $"{worldSeed}{csFilePath}:{seedId}".ToMd5HashedInt32();

    public static string GetCsFilePathFromType(Type type)
    {
        string assemblyName = type.Assembly.GetName().Name ?? throw new Exception($"Failed to get assembly from type {type}");
        string nameSpaceStr = type.Namespace ?? throw new Exception($"Namespace for {type} is unknown");
        Span<char> nameSpace = stackalloc char[nameSpaceStr.Length];
        nameSpaceStr.CopyTo(nameSpace);
        nameSpace = nameSpace.Slice(assemblyName.Length + 1);
        nameSpace.Replace('.', '/');
        return $"{assemblyName}/{nameSpace}/{type.Name}.cs";
    }

    /// <param name="seedId">Sets the unique id for this seed based on the calling .NET code file.</param>
    /// <param name="filePath">File path to the calling .NET code file.</param>
    public Random GetDotnetRandom(int seedId = 0, [CallerFilePath] string filePath = "") => new(CreateSeedInt32(options.Value.Seed, filePath, seedId));

    /// <inheritdoc cref="GetDotnetRandom" />
    public XorRandom GetUnityLikeRandom(int seedId = 0, [CallerFilePath] string filePath = "") => new(CreateSeedInt32(options.Value.Seed, filePath, seedId));

    /// <summary>
    ///     The random that fills one batch of the world. It depends only on the world seed and the batch, so a batch is
    ///     filled the same way whichever batches were filled before it.
    /// </summary>
    public XorRandom GetBatchRandom(NitroxInt3 batchId) => new(CreateSeedInt32(options.Value.Seed, $"WorldBatch/{batchId.X}/{batchId.Y}/{batchId.Z}"));
}
