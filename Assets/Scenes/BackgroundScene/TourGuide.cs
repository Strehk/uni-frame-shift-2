using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class TourGuide : MonoBehaviour
{
    [System.Serializable]
    public class Stop
    {
        public Transform spot;
        public string triggerName;
        public string stateName;

        [Header("Facing (world XZ target)")]
        public bool useLookAt = false;

        [Tooltip("World-space X (x) and Z (y) to face before playing the stop animation. Y is ignored.")]
        public Vector2 lookAtWorldXZ;
    }

    [Header("Intro")]
    public string introLastStateName = "standUp";

    [Header("Stops (in order)")]
    public Stop[] stops;

    [Header("Loop")]
    public bool loopStops = true;

    [Header("NavMesh")]
    public float sampleRadius = 2f;
    public float arriveRadius = 0.8f;

    [Header("Turn Smoothing")]
    [Tooltip("Degrees per second when turning at a stop (lower = slower).")]
    public float turnSpeedDegPerSec = 120f;

    [Tooltip("Stop turning when within this many degrees of the target.")]
    public float turnToleranceDeg = 2f;

    NavMeshAgent agent;
    Animator animator;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        agent.autoBraking = true;
    }

    void Start()
    {
        agent.isStopped = true;
        StartCoroutine(RunRoutine());
    }

    void Update()
    {
        animator.SetFloat("Speed", agent.isStopped ? 0f : agent.velocity.magnitude);
    }

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

    IEnumerator WaitForStateToPlayThenEnd(string stateName)
    {
        while (!animator.GetCurrentAnimatorStateInfo(0).IsName(stateName))
            yield return null;

        while (animator.GetCurrentAnimatorStateInfo(0).IsName(stateName))
            yield return null;
    }
}