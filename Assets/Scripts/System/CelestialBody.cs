using UnityEngine;

public class CelestialBody : MonoBehaviour
{
    [SerializeField] private string displayName;
    [SerializeField] private float radius = 1f;

    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;

    // Radius in world units (already scaled).
    public float Radius => radius;

    public void Setup(string bodyName, float worldRadius)
    {
        displayName = bodyName;
        radius = worldRadius;
    }
}
