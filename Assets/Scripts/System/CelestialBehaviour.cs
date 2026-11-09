using UnityEngine;

public class CelestialBehaviour : MonoBehaviour
{
    public enum GravityMode
    {
        Auto,       // Exact below the threshold, Barnes-Hut above it
        Exact,      // O(n^2), every unique pair
        BarnesHut   // O(n log n) approximation
    }

    // Standard gravitational parameter of Earth (G * M_earth) in km^3/s^2. Masses are relative to Earth.
    private const float EarthGM = 398600.4418f;
    private const float SecondsPerDay = 86400f;

    [Header("Gravity constant")]
    [Tooltip("If enabled (and a Solar System is assigned), G is computed from the distance scale and the simulated time scale. Otherwise the manual G below is used.")]
    [SerializeField] private bool autoComputeG = true;
    [Tooltip("Simulated days that pass in one second of game time (1 = one day per second, so a year takes ~365 s).")]
    [SerializeField] private float simulatedDaysPerSecond = 1f;
    [Tooltip("Physics step in seconds. Smaller = more accurate orbits, more CPU. Unity default is 0.02.")]
    [SerializeField] private float fixedTimeStep = 0.01f;
    [Tooltip("Manual value, or the computed value when auto compute is on (read-only in practice).")]
    [SerializeField] private float G = 100;

    [Header("Gravity algorithm")]
    [SerializeField] private GravityMode gravityMode = GravityMode.Auto;
    [Tooltip("Auto mode: Barnes-Hut is used when the number of bodies reaches this value.")]
    [SerializeField] private int barnesHutThreshold = 50;
    [Tooltip("Barnes-Hut accuracy: lower = more accurate but slower (0.5 is a common value).")]
    [SerializeField, Range(0.1f, 1.5f)] private float theta = 0.5f;
    [Tooltip("Optional. If set, bodies are taken from it. Otherwise the list below, then the 'Celestials' tag.")]
    [SerializeField] private SolarSystemInit solarSystem;
    [SerializeField] private GameObject[] celestials;

    private Rigidbody[] celestialRigidbodies;
    private Transform[] celestialTransforms;

    private BarnesHutTree tree;
    private Vector3[] positions;
    private float[] masses;

    void Start()
    {
        if (solarSystem != null)
            celestials = solarSystem.GetBodies();
        else if (celestials == null || celestials.Length == 0)
            celestials = GameObject.FindGameObjectsWithTag("Celestials");

        ConfigureGravity();

        celestialRigidbodies = new Rigidbody[celestials.Length];
        celestialTransforms = new Transform[celestials.Length];

        for (int i = 0; i < celestials.Length; i++)
        {
            GameObject celestial = celestials[i];
            celestialTransforms[i] = celestial.transform;
            celestialRigidbodies[i] = celestial.GetComponent<Rigidbody>();
        }

        InitialVelocity();
    }

    // G_sim = GM_earth * T^2 / L^3, with L = km per distance unit and T = simulated seconds per game second.
    void ConfigureGravity()
    {
        if (fixedTimeStep > 0f)
            Time.fixedDeltaTime = fixedTimeStep;

        if (!autoComputeG)
            return;

        if (solarSystem == null)
        {
            Debug.LogWarning(" | DEBUGGING | Auto G needs a SolarSystemInit reference; using the manual G.");
            return;
        }

        double L = solarSystem.KmPerDistUnit;
        double T = simulatedDaysPerSecond * SecondsPerDay;
        G = (float)(EarthGM * T * T / (L * L * L));

        Debug.Log($" | DEBUGGING | Computed G = {G}");
    }

    void FixedUpdate()
    {
        bool useBarnesHut = gravityMode == GravityMode.BarnesHut ||
                            (gravityMode == GravityMode.Auto && celestials.Length >= barnesHutThreshold);

        if (useBarnesHut)
            GravityBarnesHut();
        else
            Gravity();
    }

    void GravityBarnesHut()
    {
        int count = celestials.Length;
        if (tree == null)
        {
            tree = new BarnesHutTree();
            positions = new Vector3[count];
            masses = new float[count];
        }

        for (int i = 0; i < count; i++)
        {
            bool valid = celestialRigidbodies[i] != null;
            positions[i] = celestialTransforms[i].position;
            masses[i] = valid ? celestialRigidbodies[i].mass : 0f;
        }

        tree.Build(positions, masses, count);

        for (int i = 0; i < count; i++)
        {
            if (masses[i] <= 0f)
                continue;

            celestialRigidbodies[i].AddForce(tree.ComputeForce(i, positions[i], masses[i], G, theta));
        }
    }

    void Gravity()
    {
        for (int i = 0; i < celestials.Length; i++)
        {
            if (celestialRigidbodies[i] == null)
                continue;

            for (int j = i + 1; j < celestials.Length; j++)
            {
                if (celestialRigidbodies[j] == null)
                    continue;

                Vector3 direction = celestialTransforms[j].position - celestialTransforms[i].position;
                float sqrDist = direction.sqrMagnitude;
                if (sqrDist < Mathf.Epsilon)
                    continue;

                // F = G*m1*m2 / r^2, direction = dir / r  =>  force = dir * G*m1*m2 / r^3
                float invDist3 = 1f / (sqrDist * Mathf.Sqrt(sqrDist));
                Vector3 force = direction * (G * celestialRigidbodies[i].mass * celestialRigidbodies[j].mass * invDist3);

                celestialRigidbodies[i].AddForce(force);
                celestialRigidbodies[j].AddForce(-force);
            }
        }
    }

    void InitialVelocity()
    {
        int count = celestials.Length;

        // Order bodies by mass (heaviest first) so a parent's velocity is set before its children.
        float[] masses = new float[count];
        int[] order = new int[count];
        for (int i = 0; i < count; i++)
        {
            masses[i] = celestialRigidbodies[i] != null ? celestialRigidbodies[i].mass : 0f;
            order[i] = i;
        }
        System.Array.Sort(masses, order);
        System.Array.Reverse(order);

        for (int k = 1; k < count; k++)
        {
            int child = order[k];
            if (celestialRigidbodies[child] == null)
                continue;

            // Parent = nearest body among the heavier ones (Earth -> Sun, Moon -> Earth).
            int parent = -1;
            float bestSqrDist = float.MaxValue;
            for (int m = 0; m < k; m++)
            {
                int candidate = order[m];
                if (celestialRigidbodies[candidate] == null)
                    continue;

                float sqrDist = (celestialTransforms[candidate].position - celestialTransforms[child].position).sqrMagnitude;
                if (sqrDist < bestSqrDist)
                {
                    bestSqrDist = sqrDist;
                    parent = candidate;
                }
            }

            if (parent < 0 || bestSqrDist < Mathf.Epsilon)
                continue;

            Vector3 toParent = celestialTransforms[parent].position - celestialTransforms[child].position;
            float r = Mathf.Sqrt(bestSqrDist);
            float speed = Mathf.Sqrt(G * celestialRigidbodies[parent].mass / r);
            Vector3 tangent = Vector3.Cross(Vector3.up, toParent).normalized;

            // Orbital speed around the parent + the parent's own velocity.
            celestialRigidbodies[child].linearVelocity = celestialRigidbodies[parent].linearVelocity + tangent * speed;
        }
    }
}
