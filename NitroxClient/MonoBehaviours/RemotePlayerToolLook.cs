using Nitrox.Model.DataStructures;
using NitroxClient.GameLogic;
using UnityEngine;

namespace NitroxClient.MonoBehaviours;

/// <summary>
///     The look of the tool in another player's hand while they use it: the scanner's beam, the builder's beams, the
///     stasis rifle's charge, the propulsion cannon's beam and the seaglide's propeller. The game shows these from the
///     local player's input in each tool's own update, which does nothing for a tool in another player's hand; here they
///     follow that player's arm animation (using_tool and using_tool_alt) and what they told about their tool. Sounds are
///     left to the sound sync.
/// </summary>
public sealed class RemotePlayerToolLook : MonoBehaviour
{
    /// <summary>
    ///     Further than the builder reaches, as the aim of another player is only known roughly.
    /// </summary>
    private const float BUILDER_TARGET_RANGE = 8f;

    /// <summary>
    ///     Around the hand, for when the aim misses what is being built.
    /// </summary>
    private const float BUILDER_TARGET_NEAR_RANGE = 3f;

    /// <summary>
    ///     A seaglide's propeller turns while its holder swims faster than this, in metres per second.
    /// </summary>
    private const float SEAGLIDE_MOVING_SPEED = 0.5f;

    private RemotePlayer player;
    private PlayerTool? lookedAfterTool;
    private bool wasUsingTool;
    private PropulsionCannon? beamingCannon;
    private GameObject? detachedEffect;
    private NitroxId? grabbedId;
    private GameObject? grabbed;
    private Seaglide? glidingSeaglide;
    private bool seaglideActive;

    public void Initialize(RemotePlayer remotePlayer)
    {
        player = remotePlayer;
    }

    public void SetPropulsionGrab(NitroxId? id)
    {
        grabbedId = id;
        grabbed = id != null && NitroxEntity.TryGetObjectFrom(id, out GameObject grabbedObject) ? grabbedObject : null;
    }

    public void Update()
    {
        if (player == null || !player.AnimationController)
        {
            return;
        }

        PlayerTool? tool = player.HeldTool;
        if (tool != lookedAfterTool)
        {
            StopLook(lookedAfterTool);
            lookedAfterTool = tool;
            wasUsingTool = false;
        }
        if (!tool)
        {
            return;
        }

        bool usingTool = player.AnimationController["using_tool"];
        bool usingToolAlt = player.AnimationController["using_tool_alt"];
        switch (tool)
        {
            case ScannerTool scanner:
                UpdateScanner(scanner, usingTool, usingToolAlt);
                break;
            case BuilderTool builder when usingTool:
                // BuilderTool.LateUpdate draws the beams to the constructable it is given in the same frame, and forgets it
                builder.constructable = FindConstructable();
                break;
            case StasisRifle rifle:
                UpdateStasisRifle(rifle, usingTool);
                break;
            case PropulsionCannonWeapon weapon:
                UpdatePropulsionBeam(weapon.propulsionCannon);
                break;
        }
        wasUsingTool = usingTool;
    }

    /// <summary>
    ///     Replaces <c>Seaglide.Update</c> for a seaglide in another player's hand. The game's own would read the local
    ///     input and use up the battery of this copy of the seaglide.
    /// </summary>
    /// <returns>False if the seaglide is not in another player's hand.</returns>
    public static bool TryAnimateSeaglide(Seaglide seaglide)
    {
        RemotePlayerToolLook look = seaglide.GetComponentInParent<RemotePlayerToolLook>();
        if (!look || look.player == null || look.player.HeldTool != seaglide)
        {
            return false;
        }
        look.AnimateSeaglide(seaglide);
        return true;
    }

    private void AnimateSeaglide(Seaglide seaglide)
    {
        bool active = player.IsUnderwater && player.RigidBody && player.RigidBody.velocity.magnitude > SEAGLIDE_MOVING_SPEED;
        if (seaglide.animator && seaglide.animator.gameObject.activeInHierarchy)
        {
            SafeAnimator.SetBool(seaglide.animator, "moving", active);
        }
        if (active != seaglideActive || glidingSeaglide != seaglide)
        {
            seaglide.SetVFXActive(active);
            seaglideActive = active;
            glidingSeaglide = seaglide;
        }
    }

    private static void UpdateScanner(ScannerTool scanner, bool usingTool, bool usingToolAlt)
    {
        // ScannerTool.LateUpdate and SetFXActive; the scanning effect on the target is left out, as only the local
        // player's target is known
        if (scanner.scanBeam)
        {
            scanner.scanBeam.SetActive(usingTool || usingToolAlt);
        }
        if (scanner.animator && scanner.animator.isActiveAndEnabled)
        {
            SafeAnimator.SetBool(scanner.animator, "using_tool", usingTool);
            SafeAnimator.SetBool(scanner.animator, "using_tool_alt", usingToolAlt);
        }
    }

    private void UpdateStasisRifle(StasisRifle rifle, bool usingTool)
    {
        if (usingTool == wasUsingTool)
        {
            return;
        }
        // StasisRifle.BeginCharge and EndCharge
        if (usingTool)
        {
            if (rifle.fxControl)
            {
                rifle.fxControl.Play(0);
            }
        }
        else if (rifle.fxControl)
        {
            rifle.fxControl.StopAndDestroy(0, 0f);
        }
        rifle.Animate(usingTool);
    }

    private void UpdatePropulsionBeam(PropulsionCannon cannon)
    {
        if (!cannon)
        {
            return;
        }
        if (!grabbed && grabbedId != null && NitroxEntity.TryGetObjectFrom(grabbedId, out GameObject grabbedObject))
        {
            // The object may have been spawned after the grab was told
            grabbed = grabbedObject;
        }
        if (!grabbed)
        {
            StopPropulsionBeam();
            return;
        }

        if (beamingCannon != cannon)
        {
            StopPropulsionBeam();
            beamingCannon = cannon;
            // PropulsionCannon.set_grabbedObject
            SetActive(cannon.grabbedEffect, true);
            SetActive(cannon.fxBeam, true);
            if (cannon.grabbedEffect)
            {
                cannon.grabbedEffect.transform.parent = null;
                detachedEffect = cannon.grabbedEffect;
            }
        }

        // PropulsionCannon.Update
        Vector3 center = GetCenter(grabbed);
        if (cannon.elecLines != null && cannon.muzzle)
        {
            foreach (VFXElectricLine line in cannon.elecLines)
            {
                if (line)
                {
                    line.origin = cannon.muzzle.position;
                    line.target = center;
                    line.originVector = cannon.muzzle.forward;
                }
            }
        }
        if (cannon.grabbedEffect)
        {
            cannon.grabbedEffect.transform.position = center;
        }
    }

    private void StopPropulsionBeam()
    {
        if (!beamingCannon)
        {
            // The glow was taken off the cannon while it showed; with the cannon gone it would stay in the world
            if (detachedEffect)
            {
                Destroy(detachedEffect);
            }
            detachedEffect = null;
            beamingCannon = null;
            return;
        }
        SetActive(beamingCannon.grabbedEffect, false);
        SetActive(beamingCannon.fxBeam, false);
        if (beamingCannon.grabbedEffect)
        {
            beamingCannon.grabbedEffect.transform.parent = beamingCannon.transform;
        }
        detachedEffect = null;
        beamingCannon = null;
    }

    private void StopLook(PlayerTool? tool)
    {
        switch (tool)
        {
            case ScannerTool scanner:
                UpdateScanner(scanner, false, false);
                break;
            case StasisRifle rifle when wasUsingTool:
                UpdateStasisRifle(rifle, false);
                break;
            case PropulsionCannonWeapon:
                StopPropulsionBeam();
                break;
            case Seaglide seaglide when seaglideActive:
                seaglide.SetVFXActive(false);
                seaglideActive = false;
                break;
        }
    }

    private Constructable? FindConstructable()
    {
        Vector3 origin = player.ItemAttachPoint ? player.ItemAttachPoint.position : transform.position;
        if (Physics.Raycast(origin, player.AimDirection, out RaycastHit hit, BUILDER_TARGET_RANGE, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            Constructable aimedAt = hit.collider.GetComponentInParent<Constructable>();
            if (aimedAt)
            {
                return aimedAt;
            }
        }

        Constructable? nearest = null;
        float nearestDistance = float.MaxValue;
        foreach (Collider nearby in Physics.OverlapSphere(origin, BUILDER_TARGET_NEAR_RANGE, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            Constructable constructable = nearby.GetComponentInParent<Constructable>();
            if (!constructable || constructable.constructed)
            {
                continue;
            }
            float distance = (constructable.transform.position - origin).sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = constructable;
            }
        }
        return nearest;
    }

    private static Vector3 GetCenter(GameObject target)
    {
        Renderer renderer = target.GetComponentInChildren<Renderer>();
        return renderer ? renderer.bounds.center : target.transform.position;
    }

    private static void SetActive(GameObject? gameObject, bool active)
    {
        if (gameObject)
        {
            gameObject.SetActive(active);
        }
    }

    public void OnDestroy()
    {
        StopPropulsionBeam();
    }
}
