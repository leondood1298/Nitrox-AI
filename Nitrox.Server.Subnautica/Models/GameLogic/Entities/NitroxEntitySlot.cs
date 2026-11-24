using System.Collections.Generic;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

/// <param name="Density">The slot's density: the game divides each prefab's probability by it.</param>
[Serializable]
public record struct NitroxEntitySlot(string BiomeType, List<string> AllowedTypes, float Density = 1f);
