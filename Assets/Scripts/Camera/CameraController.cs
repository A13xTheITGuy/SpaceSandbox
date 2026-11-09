using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("Camera Movement Settings")]
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float boostMultiplier = 2f; // Shift key -> boost 
    [SerializeField] private float mouseSensitivity = 2f;
    [Tooltip("Free camera: fraction of moveSpeed changed per scroll notch (0.1 = 10%).")]
    [SerializeField, Range(0.01f, 0.5f)] private float scrollSpeedStrength = 0.1f;
    [SerializeField] private float minMoveSpeed = 0.1f;
    [SerializeField] private float maxMoveSpeed = 100000f;

    [Header("Follow Settings")]
    [SerializeField] private CelestialSelector selector;
    [SerializeField] private float followTurnSpeed = 30f;

    [Header("Zoom Settings (Follow / Go To)")]
    [Tooltip("Fraction of the current distance changed per scroll notch (0.1 = 10%).")]
    [SerializeField, Range(0.01f, 0.5f)] private float scrollZoomStrength = 0.1f;
    [Tooltip("Minimum distance from the target surface, in target radii (1 = touching the surface).")]
    [SerializeField] private float minDistanceInRadii = 1.2f;
    [SerializeField] private float maxDistance = 100000f;

    [Header("Go To Settings")]
    [Tooltip("Final distance from the target = radius * this value.")]
    [SerializeField] private float goToDistanceInRadii = 4f;
    [SerializeField] private float goToMoveSpeed = 3f;

    [Header("Move Speed Label")]
    [SerializeField] private float speedLabelVisibleTime = 3f;
    [SerializeField] private float speedLabelFadeTime = 0.5f;
    private TextMeshProUGUI moveSpeedValueLabel;
    private CanvasGroup moveSpeedLabelGroup;
    private Coroutine speedLabelRoutine;

    private CelestialBody followTarget;
    private Vector3 offset;         // camera position relative to the target
    private bool isApproaching;     // Go To: offset is animating towards approachOffset
    private Vector3 approachOffset; // final camera position relative to the target for Go To
    public bool IsFollowing => followTarget != null;

    private bool freeCameraEnable = false;
    private float yaw = 0f;
    private float pitch = 0f;

    void Awake()
    {
        if (selector == null)
            selector = GetComponent<CelestialSelector>();
        if (selector == null)
            selector = FindAnyObjectByType<CelestialSelector>();
        if (moveSpeedValueLabel == null)
            moveSpeedValueLabel = GameObject.FindGameObjectWithTag("MoveSpeedValueLabel")?.GetComponent<TextMeshProUGUI>();

        if (moveSpeedValueLabel != null)
        {
            moveSpeedLabelGroup = moveSpeedValueLabel.GetComponent<CanvasGroup>();
            if (moveSpeedLabelGroup == null)
                moveSpeedLabelGroup = moveSpeedValueLabel.gameObject.AddComponent<CanvasGroup>();

            moveSpeedLabelGroup.alpha = 0f;
            moveSpeedLabelGroup.interactable = false;
            moveSpeedLabelGroup.blocksRaycasts = false;
        }
    }

    void Start()
    {
        // Unlock the cursor and make it visible at the start.
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // Initialize the angles from the camera's current rotation.
        Vector3 rot = transform.eulerAngles;
        yaw = rot.y;
        pitch = rot.x;
    }

    void Update()
    {
        HandleFollowInput();

        // Lock the cursor when right mouse button is held down, unlock it otherwise.
        if (Mouse.current != null && Mouse.current.rightButton.isPressed)
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            freeCameraEnable = true;
        }
        else
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            freeCameraEnable = false;
        }

        // Mouse rotation (! hold down right-click).
        if (Mouse.current != null && freeCameraEnable)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();

            if (IsFollowing)
            {
                // Orbit around the followed body.
                float dAz = mouseDelta.x * mouseSensitivity;
                float dEl = -mouseDelta.y * mouseSensitivity;
                OrbitOffset(ref offset, dAz, dEl);
                if (isApproaching)
                    OrbitOffset(ref approachOffset, dAz, dEl);
            }
            else
            {
                yaw += mouseDelta.x * mouseSensitivity;
                pitch -= mouseDelta.y * mouseSensitivity;
                pitch = Mathf.Clamp(pitch, -90f, 90f); // We limit it to prevent it from flipping over.
                transform.eulerAngles = new Vector3(pitch, yaw, 0f);
            }
        }

        HandleScrollZoom();

        // WASD Movement
        Vector3 move = Vector3.zero;
        if(Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed) move += transform.forward;
            if (Keyboard.current.sKey.isPressed) move -= transform.forward;
            if (Keyboard.current.aKey.isPressed) move -= transform.right;
            if (Keyboard.current.dKey.isPressed) move += transform.right;
            if (Keyboard.current.qKey.isPressed) move -= transform.up;
            if (Keyboard.current.eKey.isPressed) move += transform.up;
        }

        // Boost with Shift key
        float speed = moveSpeed;
        if (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed)
            speed *= boostMultiplier;

        // Apply the movement. While following it changes the offset to the target; disabled during the Go To approach.
        Vector3 delta = move.normalized * speed * Time.unscaledDeltaTime;
        if (IsFollowing)
        {
            if (!isApproaching)
                offset += delta;
        }
        else
        {
            transform.position += delta;
        }
    }

    // Scroll wheel: zooms relative to the followed body, or changes the move speed of the free camera.
    void HandleScrollZoom()
    {
        if (Mouse.current == null)
            return;

        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Approximately(scroll, 0f))
            return;

        if (!IsFollowing)
        {
            // Scroll up = faster, scroll down = slower.
            float speedFactor = 1f + Mathf.Sign(scroll) * scrollSpeedStrength;

            // Clamp the move speed to avoid it going too low or too high.
            moveSpeed = Mathf.Clamp(moveSpeed * speedFactor, minMoveSpeed, maxMoveSpeed);
            if (moveSpeedValueLabel != null)
            {
                moveSpeedValueLabel.text = "Move Speed: " + moveSpeed.ToString("F2");
                ShowMoveSpeedLabel();
            }

            return;
        }

        // Only the direction matters, so it behaves the same regardless of the platform's scroll units.
        float factor = 1f - Mathf.Sign(scroll) * scrollZoomStrength;

        offset = ClampDistance(offset * factor);
        if (isApproaching)
            approachOffset = ClampDistance(approachOffset * factor);
    }

    // Shows the move speed label, keeps it visible for a few seconds, then fades it out.
    void ShowMoveSpeedLabel()
    {
        if (moveSpeedLabelGroup == null)
            return;

        if (speedLabelRoutine != null)
            StopCoroutine(speedLabelRoutine);

        speedLabelRoutine = StartCoroutine(SpeedLabelRoutine());
    }

    IEnumerator SpeedLabelRoutine()
    {
        moveSpeedLabelGroup.alpha = 1f;
        yield return new WaitForSecondsRealtime(speedLabelVisibleTime);

        for (float t = 0f; t < speedLabelFadeTime; t += Time.unscaledDeltaTime)
        {
            moveSpeedLabelGroup.alpha = 1f - t / speedLabelFadeTime;
            yield return null;
        }

        moveSpeedLabelGroup.alpha = 0f;
        speedLabelRoutine = null;
    }

    Vector3 ClampDistance(Vector3 v)
    {
        float min = followTarget.Radius * minDistanceInRadii;
        float length = v.magnitude;
        if (length < Mathf.Epsilon)
            return v;

        return v * (Mathf.Clamp(length, min, maxDistance) / length);
    }

    // Rotates a camera-to-target offset
    static void OrbitOffset(ref Vector3 v, float deltaAzimuth, float deltaElevation)
    {
        float length = v.magnitude;
        if (length < Mathf.Epsilon)
            return;

        float elevation = Mathf.Asin(Mathf.Clamp(v.y / length, -1f, 1f)) * Mathf.Rad2Deg;
        float azimuth = Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;

        azimuth += deltaAzimuth;
        elevation = Mathf.Clamp(elevation + deltaElevation, -89f, 89f);

        float el = elevation * Mathf.Deg2Rad;
        float az = azimuth * Mathf.Deg2Rad;
        v = length * new Vector3(Mathf.Cos(el) * Mathf.Sin(az), Mathf.Sin(el), Mathf.Cos(el) * Mathf.Cos(az));
    }

    void HandleFollowInput()
    {
        if (Keyboard.current == null)
            return;

        // F = follow the selected body: keep aiming at it and keep the current relative position (camera does not approach).
        if (Keyboard.current.fKey.wasPressedThisFrame)
            FollowSelected();

        // G = go to the selected body and keep following it.
        if (Keyboard.current.gKey.wasPressedThisFrame)
            GoToSelected();

        // Esc = stop following / going to.
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            followTarget = null;
            isApproaching = false;
        }
    }

    // Also callable from a UI button.
    public void FollowSelected()
    {
        if (selector == null || selector.Selected == null)
            return;

        followTarget = selector.Selected;
        offset = transform.position - followTarget.transform.position;
        isApproaching = false;
    }

    // Also callable from a UI button.
    public void GoToSelected()
    {
        if (selector == null || selector.Selected == null)
            return;

        followTarget = selector.Selected;
        offset = transform.position - followTarget.transform.position;
        isApproaching = true;

        Vector3 dir = offset.sqrMagnitude > Mathf.Epsilon ? offset.normalized : -transform.forward;
        float distance = Mathf.Max(followTarget.Radius * goToDistanceInRadii, followTarget.Radius + 0.5f);
        approachOffset = dir * distance;
    }

    void LateUpdate()
    {
        if (followTarget == null)
            return;

        if (isApproaching)
        {
            offset = Vector3.Lerp(offset, approachOffset, 1f - Mathf.Exp(-goToMoveSpeed * Time.unscaledDeltaTime));
            if ((offset - approachOffset).sqrMagnitude < 0.0001f * approachOffset.sqrMagnitude)
            {
                offset = approachOffset;
                isApproaching = false;
            }
        }

        // Position is always relative to the target, so the moving body causes no lag.
        transform.position = followTarget.transform.position + offset;

        Vector3 direction = followTarget.transform.position - transform.position;
        if (direction.sqrMagnitude < Mathf.Epsilon)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 1f - Mathf.Exp(-followTurnSpeed * Time.unscaledDeltaTime));
        // Slerp is used to avoid overshoot; it is framerate-independent and smooths the motion.

        // Keep yaw/pitch in sync so free-look continues smoothly after leaving Follow.
        Vector3 euler = transform.eulerAngles;
        yaw = euler.y;
        pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, euler.x), -90f, 90f);
    }
}
