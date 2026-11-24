using System;
using NitroxClient.GameLogic.PlayerLogic;
using UnityEngine;

namespace NitroxClient.MonoBehaviours;

// Shouldn't this class be named after the armscontroller?
public class AnimationController : MonoBehaviour
{
    private const float SMOOTHING_SPEED = 4f;

    /// <summary>
    ///     A few frames at any frame rate the game is playable at.
    /// </summary>
    private const float MINIMUM_SWITCH_ON_SECONDS = 0.1f;

    private readonly AnimatorPulseHold switchHold = new(MINIMUM_SWITCH_ON_SECONDS);
    private Animator animator;

    public bool UpdatePlayerAnimations { get; set; } = true;
    public Quaternion AimingRotation { get; set; }
    public Vector3 Velocity { get; set; }

    /// <summary>
    ///     When set, gives the world velocity each physics step instead of <see cref="Velocity" />: inside a Cyclops the
    ///     speed is worked out from packets as they arrive and has to fall to zero when they stop.
    /// </summary>
    public Func<Vector3>? VelocityProvider { get; set; }

    public Quaternion BodyRotation { get; set; }

    private Vector3 smoothedVelocity = Vector3.zero;
    private float smoothViewPitch;

    public void Awake()
    {
        animator = GetComponent<Animator>();

        this["is_underwater"] = true;
    }

    public void FixedUpdate()
    {
        if (UpdatePlayerAnimations)
        {
            Vector3 velocity = VelocityProvider != null ? VelocityProvider() : Velocity;
            Vector3 rotationCorrectedVelocity = transform.rotation.GetInverse() * velocity;

            smoothedVelocity = UWE.Utils.SlerpVector(smoothedVelocity, rotationCorrectedVelocity, Vector3.Normalize(rotationCorrectedVelocity - smoothedVelocity) * (SMOOTHING_SPEED * Time.fixedDeltaTime));

            animator.SetFloat("move_speed", smoothedVelocity.magnitude);
            animator.SetFloat("move_speed_x", smoothedVelocity.x);
            animator.SetFloat("move_speed_y", smoothedVelocity.y);
            animator.SetFloat("move_speed_z", smoothedVelocity.z);

            float viewPitch = AimingRotation.eulerAngles.x;
            if (viewPitch > 180f)
            {
                viewPitch -= 360f;
            }

            viewPitch = -viewPitch;
            smoothViewPitch = Mathf.Lerp(smoothViewPitch, viewPitch, 4f * Time.fixedDeltaTime);
            animator.SetFloat("view_pitch", smoothViewPitch);
        }
    }

	public bool this[string name]
	{
		get => animator.GetBool(name);
		set => animator.SetBool(name, value);
	}

    /// <summary>
    ///     Like the game's <see cref="SafeAnimator" />: a parameter that this animator does not have is left alone instead
    ///     of logging a warning every time.
    /// </summary>
    public void SetBoolIfPresent(string name, bool value)
    {
        SafeAnimator.SetBool(animator, name, value);
    }

	internal void SetFloat(string name, float value)
	{
		animator.SetFloat(name, value);
	}

	internal void SetFloat(int id, float value)
	{
		animator.SetFloat(id, value);
	}

    public void Update()
    {
        foreach (string parameter in switchHold.TakeDue(Time.time))
        {
            animator.SetBool(parameter, false);
        }
    }

    /// <summary>
    ///     Sets a switch that the player's own game may only have on for a single frame, such as a tool swing. It is kept
    ///     on long enough for the animator to act on it.
    /// </summary>
    public void SetSwitch(string name, bool value)
    {
        if (switchHold.Request(name, value, Time.time))
        {
            animator.SetBool(name, value);
        }
    }

    public void SetTrigger(string name)
    {
        animator.SetTrigger(name);
    }

	public void Reset()
	{
        switchHold.Clear();
		animator.Rebind();
		animator.Update(0f);
	}
}
