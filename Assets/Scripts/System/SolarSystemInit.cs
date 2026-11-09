using Microsoft.Unity.VisualStudio.Editor;
using System.Diagnostics.Metrics;
using System.Linq;
using Unity.Hierarchy;
using Unity.VisualScripting;
using UnityEngine;
using System.Collections.Generic;

[DefaultExecutionOrder(-100)]
public class SolarSystemInit : MonoBehaviour
{
    [Header("Scale (Distance)")]
    [Tooltip("How many km represent one unit.")]
    [SerializeField] private float kmPerDistUnit = 100000f; // 100,000 km = 1 unit
    public float KmPerDistUnit => kmPerDistUnit;

    [Header("Scale (Radius)")]
    [Tooltip("How many km represent one unit for the radus.")]
    [SerializeField] private float kmPerRadiusUnit = 50000f; // 50,000 km = 1 unit

    [Header("Astronomical data (actual values)")]
    [SerializeField] private List<CelestialData> celestials = new List<CelestialData>();

    [Header("GameObjects")]
    [SerializeField] private GameObject sunObject;
    [SerializeField] private GameObject earthObject;
    [SerializeField] private GameObject moonObject;

    [System.Serializable]
    public class CelestialData
    {
        public string name;
        public GameObject gameObject;
        public float massRelativeToEarth; // Earth = 1
        public float radiusKm;
        public float distanceKmFromParent;
        public bool isSun;
        public bool isMoon;
        public string parentName;
    }

    void Awake()
    {
        InitData();
        SetupSolarSistem();
    }

    void InitData()
    {
        celestials.Clear();

        // Sun
        celestials.Add(new CelestialData
        {
            name = "Soare",
            gameObject = sunObject,
            massRelativeToEarth = 333000f,
            radiusKm = 696340f,
            distanceKmFromParent = 0f,
            isSun = true,
            isMoon = false,
            parentName = null
        });

        // Earth
        celestials.Add(new CelestialData
        {
            name = "Pamant",
            gameObject = earthObject,
            massRelativeToEarth = 1f,
            radiusKm = 6378f,
            distanceKmFromParent = 149600000f,
            isSun = false,
            isMoon = false,
            parentName = null
        });

        // Moon
        celestials.Add(new CelestialData
        {
            name = "Luna",
            gameObject = moonObject,
            massRelativeToEarth = 0.0123f,
            radiusKm = 1737f,
            distanceKmFromParent = 384400f,
            isSun = false,
            isMoon = true,
            parentName = "Pamant"
        });
    }
    void SetupSolarSistem()
    {
        foreach (var obj in celestials)
        {
            if (obj.gameObject == null) continue;

            Rigidbody rigidbody = obj.gameObject.GetComponent<Rigidbody>();
            if(rigidbody!=null)
                rigidbody.mass = obj.massRelativeToEarth;

            float scale = obj.radiusKm / kmPerRadiusUnit;
            obj.gameObject.transform.localScale = new Vector3(scale, scale, scale);

            CelestialBody body = obj.gameObject.GetComponent<CelestialBody>();
            if (body == null)
                body = obj.gameObject.AddComponent<CelestialBody>();
            // Default Unity sphere has diameter 1, so world radius = scale / 2.
            body.Setup(obj.name, scale * 0.5f);

            if(obj.isSun)
                obj.gameObject.transform.position = Vector3.zero;   
            else if (obj.isMoon)
            {
                GameObject parent = FindCelestialByName(obj.parentName);
                if(parent!=null)
                {
                    float moonDistance = obj.distanceKmFromParent / kmPerDistUnit;
                    obj.gameObject.transform.position = parent.transform.position + new Vector3(moonDistance, 0, 0);
                }
            }
            else
            {
                float distance = obj.distanceKmFromParent / kmPerDistUnit;
                obj.gameObject.transform.position = new Vector3(distance, 0, 0);
            }
        }

        Debug.Log($" | DEGUGGING | The solar sistem has been initialized!");
    }

    
    public GameObject[] GetBodies()
    {
        List<GameObject> bodies = new List<GameObject>(celestials.Count);
        foreach (CelestialData cdata in celestials)
            if (cdata.gameObject != null)
                bodies.Add(cdata.gameObject);

        return bodies.ToArray();
    }

    GameObject? FindCelestialByName(string name)
    {
        foreach(CelestialData cdata in celestials)
            if(cdata.name.CompareTo(name) == 0 && cdata.gameObject != null)
                return cdata.gameObject;

        return null;
    }
}
