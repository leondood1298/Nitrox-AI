using Nitrox.Model.DataStructures;
using UnityEngine;

namespace NitroxClient.MonoBehaviours;

/// <summary>
///     Put on a rocket: the id of its time capsule entity, which the game's single time capsule takes when the capsule is
///     opened from this rocket's cockpit (see <see cref="NitroxClient.GameLogic.TimeCapsules" />), and what the capsule
///     held the last time this game knew, for the rocket's preflight check while another rocket's capsule is shown.
/// </summary>
public class RocketTimeCapsule : MonoBehaviour
{
    public NitroxId CapsuleId { get; set; }

    public bool HasText { get; set; }

    public bool HasItems { get; set; }

    public static RocketTimeCapsule Attach(GameObject rocketObject, NitroxId capsuleId)
    {
        RocketTimeCapsule reference = rocketObject.EnsureComponent<RocketTimeCapsule>();
        reference.CapsuleId = capsuleId;
        return reference;
    }
}
