using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class CelestialSelector : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private float maxDistance = 100000f;
    [SerializeField] private LayerMask selectableLayers = ~0;

    [SerializeField] private TextMeshProUGUI SelectedObj_label;

    public CelestialBody Selected { get; private set; }

    public event Action<CelestialBody> SelectionChanged;

    void Awake()
    {
        if(SelectedObj_label == null)
            SelectedObj_label = GameObject.FindGameObjectWithTag("SelectedObj_label")?.GetComponent<TextMeshProUGUI>();
        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || targetCamera == null)
            return;

        // Esc deselects.
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ClearSelection();
            return;
        }

        // Left click selects, but only while right click (free-look) is not held.
        if (!mouse.leftButton.wasPressedThisFrame || mouse.rightButton.isPressed)
            return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        Ray ray = targetCamera.ScreenPointToRay(mouse.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, selectableLayers))
        {
            CelestialBody body = hit.collider.GetComponentInParent<CelestialBody>();
            Select(body);
        }
        else
        {
            // Click on empty space deselects.
            ClearSelection();
        }
    }

    public void Select(CelestialBody body)
    {
        if (body == Selected)
            return;

        Selected = body;
        // Debug.Log($" | DEBUGGING | Selected: {(body != null ? body.DisplayName : "none")}");
        if (SelectedObj_label != null)
            SelectedObj_label.text = body != null ? $"Selected: {body.DisplayName} | Press ESC to deselect" : "";
        SelectionChanged?.Invoke(Selected);
    }

    public void ClearSelection()
    {
        Select(null);
    }
}
