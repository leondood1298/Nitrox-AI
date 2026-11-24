using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using UnityEngine;

namespace NitroxClient.MonoBehaviours.Vehicles;

/// <summary>
///     The <see cref="VehicleMetadata.NameVersion" /> this game knows for a Seamoth or Prawn suit: the last one it received,
///     raised by one for each rename or recolour made here.
/// </summary>
public sealed class VehicleNameVersion : MonoBehaviour
{
    public int Value { get; private set; }

    public static int Of(GameObject vehicle)
    {
        return vehicle.TryGetComponent(out VehicleNameVersion version) ? version.Value : 0;
    }

    public static void Set(GameObject vehicle, int value)
    {
        vehicle.EnsureComponent<VehicleNameVersion>().Value = value;
    }

    public static void Raise(GameObject vehicle)
    {
        VehicleNameVersion version = vehicle.EnsureComponent<VehicleNameVersion>();
        version.Value++;
    }
}
