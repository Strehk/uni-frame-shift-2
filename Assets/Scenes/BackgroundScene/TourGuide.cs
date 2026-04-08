using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Orchestrates the Mac Zimmermann character's navigation and animation through
/// the virtual atelier. Uses NavMeshAgent for pathfinding between configurable
/// waypoints (stops), triggering context-specific animations at each location.
/// Supports an intro sequence, smooth turning, and optional looping of the tour.
/// </summary>
public class TourGuide : MonoBehaviour
{
    /// <summary>
    /// Defines a single waypoint in the tour, including the target position,
    /// the animation to play upon arrival, and an optional facing direction.
    /// </summary>
    [System.Serializable]
    public class Stop
    {
        /// <summary>Transform marking the destination position for this stop.</summary>
        public Transform spot;
        /// <summary>Name of the Animator trigger to fire when the character arrives.</summary>
        public string triggerName;
        /// <summary>Name of the Animator state to wait for before proceeding to the next stop.</summary>
        public string stateName;

        [Header("Facing (world XZ target)")]
        /// <summary>Whether the character should turn to face a specific direction before animating.</summary>
        public bool useLookAt = false;

        [Tooltip("World-space X (x) and Z (y) to face before playing the stop animation. Y is ignored.")]
        /// <summary>World-space XZ coordinates the character should face (X=x, Y=z). The vertical component is ignored.</summary>
        public Vector2 lookAtWorldXZ;
    }

    [Header("Intro")]
    /// <summary>
    /// Name of the last Animator state in the intro sequence (e.g., "standUp").
    /// The tour waits for this state to finish before the character starts walking.
    /// </summary>
    public string introLastStateName = "standUp";

    [Header("Stops (in order)")]
    /// <summary>Ordered array of tour stops the character visits sequentially.</summary>
    public Stop[] stops;

    [Header("Loop")]
    /// <summary>If true, the character loops back to the first stop after completing the tour.</summary>
    public bool loopStops = true;

    [Header("NavMesh")]
    /// <summary>Maximum distance from a stop's position to sample a valid NavMesh point.</summary>
    public float sampleRadius = 2f;
    /// <summary>Distance threshold at which the character is considered to have arrived at a stop.</summary>
    public float arriveRadius = 0.8f;

    [Header("Turn Smoothing")]
    [Tooltip("Degrees per second when turning at a stop (lower = slower).")]
    /// <summary>Rotation speed in degrees per second when smoothly turning to face a target at a stop.</summary>
    public float turnSpeedDegPerSec = 120f;

    [Tooltip("Stop turning when within this many degrees of the target.")]
    /// <summary>Angle tolerance in degrees; turning stops when the remaining angle is within this value.</summary>
    public float turnToleranceDeg = 2f;

    /// <summary>Cached NavMeshAgent component for pathfinding.</summary>
    NavMeshAgent agent;
    /// <summary>Cached Animator component for triggering and monitoring animation states.</summary>
    Animator animator;

    /// <summary>
    /// Caches the NavMeshAgent and Animator components and enables auto-braking.
    /// </summary>
    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        agent.autoBraking = true;
    }

    /// <summary>
    /// Stops the agent initially and starts the main tour coroutine.
    /// </summary>
    void Start()
    {
        agent.isStopped = true;
        StartCoroutine(RunRoutine());
    }

    /// <summary>
    /// Updates the Animator's "Speed" parameter each frame based on the agent's
    /// current velocity, enabling walk/idle animation blending.
    /// </summary>
    void Update()
    {
        animator.SetFloat("Speed", agent.isStopped ? 0f : agent.velocity.magnitude);
    }

    /// <summary>
    /// Main tour coroutine. Waits for the intro animation to complete, then
    /// sequentially navigates the character to each stop, optionally turning
    /// to face a direction and playing an animation before moving to the next.
    /// Loops if <see cref="loopStops"/> is enabled.
    /// </summary>
    IEnumerator RunRoutine()
    {
        // Wait for intro to finish (enter -> leave)
        while (!animator.GetCurrentAnimatorStateInfo(0).IsName(introLastStateName))
            yield return null;

        while (animator.GetCurrentAnimatorStateInfo(0).IsName(introLastStateName))
            yield return null;

        if (!agent.isOnNavMesh)
        {
            Debug.LogError("Agent is not on the NavMesh after intro. Place the character root on the NavMesh.");
            yield break;
        }

        if (stops == null || stops.Length == 0)
            yield break;

        agent.isStopped = false;

        int index = 0;

        while (true)
        {
            var stop = stops[index];

            if (stop.spot != null)
            {
                if (!NavMesh.SamplePosition(stop.spot.position, out var hit, sampleRadius, NavMesh.AllAreas))
                {
                    Debug.LogError($"Stop '{stop.spot.name}' is not near the NavMesh. Move it onto the blue area.");
                }
                else
                {
                    agent.isStopped = false;
                    agent.ResetPath();
                    agent.SetDestination(hit.position);

                    while (agent.pathPending) yield return null;

                    if (!agent.hasPath || agent.pathStatus != NavMeshPathStatus.PathComplete)
                    {
                        Debug.LogError($"No valid path to '{stop.spot.name}'. PathStatus={agent.pathStatus}");
                    }
                    else
                    {
                        while (Vector3.Distance(transform.position, hit.position) > arriveRadius)
                            yield return null;

                        agent.isStopped = true;
                        agent.ResetPath();
                        animator.SetFloat("Speed", 0f);

                        if (stop.useLookAt)
                            yield return TurnTowardWorldXZ(stop.lookAtWorldXZ);

                        if (!string.IsNullOrEmpty(stop.triggerName))
                            animator.SetTrigger(stop.triggerName);

                        if (!string.IsNullOrEmpty(stop.stateName))
                            yield return WaitForStateToPlayThenEnd(stop.stateName);
                    }
                }
            }

            agent.isStopped = false;

            // advance index + loop
            index++;
            if (index >= stops.Length)
            {
                if (!loopStops)
                    yield break;

                index = 0;
            }
        }
    }

    /// <summary>
    /// Smoothly rotates the character on the XZ plane to face the specified
    /// world-space coordinates using Slerp-like interpolation.
    /// </summary>
    /// <param name="worldXZ">Target world-space position (X, Z) to face.</param>
    IEnumerator TurnTowardWorldXZ(Vector2 worldXZ)
    {
        Vector3 target = new Vector3(worldXZ.x, transform.position.y, worldXZ.y);
        Vector3 dir = target - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude <= 0.0001f)
            yield break;

        Quaternion targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);

        while (true)
        {
            float angle = Quaternion.Angle(transform.rotation, targetRot);
            if (angle <= turnToleranceDeg)
                break;

            float maxStep = Mathf.Max(1f, turnSpeedDegPerSec) * Time.deltaTime;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, maxStep);
            yield return null;
        }
    }

    /// <summary>
    /// Waits for the Animator to enter the specified state, then waits for it
    /// to exit that state before returning. Used to block progression until
    /// a stop animation has fully played.
    /// </summary>
    /// <param name="stateName">Name of the Animator state to wait for.</param>
    IEnumerator WaitForStateToPlayThenEnd(string stateName)
    {
        while (!animator.GetCurrentAnimatorStateInfo(0).IsName(stateName))
            yield return null;

        while (animator.GetCurrentAnimatorStateInfo(0).IsName(stateName))
            yield return null;
    }
}
