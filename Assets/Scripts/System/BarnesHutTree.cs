using UnityEngine;

/// <summary>
/// Allocation-free Barnes-Hut octree. Rebuilt every physics step.
/// </summary>
public class BarnesHutTree
{
    /* Barnes–Hut simulation - Wikipedia
     * 
     * Link: https://en.wikipedia.org/wiki/Barnes%E2%80%93Hut_simulation
     * 
     */

    private const int MaxDepth = 32;
    private const int AggregateLeaf = -2;

    private struct Node
    {
        public Vector3 center;
        public float halfSize;
        public float mass;
        public Vector3 weightedPos; // sum(m * pos)
        public int body;            // body index for single-body leaf, -1 for empty/internal, -2 for aggregated leaf
        public int firstChild;      // -1 if leaf, otherwise index of the first of 8 contiguous children
    }

    private Node[] nodes = new Node[64];
    private int nodeCount;
    private readonly int[] stack = new int[MaxDepth * 8 + 8];
    private Vector3[] bodyPositions;

    public void Build(Vector3[] positions, float[] masses, int count)
    {
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        bool any = false;

        for (int i = 0; i < count; i++)
        {
            if (masses[i] <= 0f)
                continue;

            min = Vector3.Min(min, positions[i]);
            max = Vector3.Max(max, positions[i]);
            any = true;
        }

        bodyPositions = positions;
        nodeCount = 0;
        if (!any)
            return;

        Vector3 size = max - min;
        float half = Mathf.Max(size.x, Mathf.Max(size.y, size.z)) * 0.5f + 0.001f;
        NewNode((min + max) * 0.5f, half);

        for (int i = 0; i < count; i++)
        {
            if (masses[i] > 0f)
                Insert(i, positions[i], masses[i]);
        }
    }

    public Vector3 ComputeForce(int index, Vector3 position, float mass, float G, float theta)
    {
        Vector3 total = Vector3.zero;
        if (nodeCount == 0)
            return total;

        float thetaSqr = theta * theta;
        int sp = 0;
        stack[sp++] = 0;

        while (sp > 0)
        {
            int idx = stack[--sp];
            Node node = nodes[idx];
            if (node.mass <= 0f)
                continue;

            bool isLeaf = node.firstChild < 0;
            if (isLeaf && node.body == index)
                continue;

            Vector3 com = node.weightedPos / node.mass;
            Vector3 direction = com - position;
            float sqrDist = direction.sqrMagnitude;
            float sizeSqr = 4f * node.halfSize * node.halfSize;

            if (isLeaf || sizeSqr < thetaSqr * sqrDist)
            {
                if (sqrDist < Mathf.Epsilon)
                    continue;

                float invDist3 = 1f / (sqrDist * Mathf.Sqrt(sqrDist));
                total += direction * (G * mass * node.mass * invDist3);
            }
            else
            {
                for (int c = 0; c < 8; c++)
                    stack[sp++] = node.firstChild + c;
            }
        }

        return total;
    }

    private int NewNode(Vector3 center, float halfSize)
    {
        if (nodeCount == nodes.Length)
            System.Array.Resize(ref nodes, nodes.Length * 2);

        nodes[nodeCount] = new Node
        {
            center = center,
            halfSize = halfSize,
            mass = 0f,
            weightedPos = Vector3.zero,
            body = -1,
            firstChild = -1
        };
        return nodeCount++;
    }

    private static int Octant(Vector3 center, Vector3 p)
    {
        return (p.x > center.x ? 1 : 0) | (p.y > center.y ? 2 : 0) | (p.z > center.z ? 4 : 0);
    }

    private void Subdivide(int nodeIndex)
    {
        Vector3 center = nodes[nodeIndex].center;
        float quarter = nodes[nodeIndex].halfSize * 0.5f;

        int first = nodeCount;
        for (int c = 0; c < 8; c++)
        {
            Vector3 offset = new Vector3(
                (c & 1) != 0 ? quarter : -quarter,
                (c & 2) != 0 ? quarter : -quarter,
                (c & 4) != 0 ? quarter : -quarter);
            NewNode(center + offset, quarter);
        }

        nodes[nodeIndex].firstChild = first;
    }

    private void Insert(int bodyIndex, Vector3 pos, float mass)
    {
        int node = 0;
        int depth = 0;

        while (true)
        {
            // Empty leaf: store the body here.
            if (nodes[node].firstChild < 0 && nodes[node].mass <= 0f)
            {
                nodes[node].body = bodyIndex;
                nodes[node].mass = mass;
                nodes[node].weightedPos = pos * mass;
                return;
            }

            nodes[node].mass += mass;
            nodes[node].weightedPos += pos * mass;

            if (nodes[node].firstChild < 0)
            {
                // Occupied leaf: too deep (coincident bodies) -> keep as aggregate.
                if (depth >= MaxDepth)
                {
                    nodes[node].body = AggregateLeaf;
                    return;
                }

                int existing = nodes[node].body;
                float existingMass = nodes[node].mass - mass;
                Subdivide(node);
                nodes[node].body = -1;

                if (existing >= 0)
                {
                    Vector3 existingPos = bodyPositions[existing];
                    int child = nodes[node].firstChild + Octant(nodes[node].center, existingPos);
                    nodes[child].body = existing;
                    nodes[child].mass = existingMass;
                    nodes[child].weightedPos = existingPos * existingMass;
                }
            }

            node = nodes[node].firstChild + Octant(nodes[node].center, pos);
            depth++;
        }
    }
}
