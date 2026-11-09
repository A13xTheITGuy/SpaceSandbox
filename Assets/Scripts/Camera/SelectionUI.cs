using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SelectionUI : MonoBehaviour
{
    [SerializeField] private CelestialSelector selector;
    [SerializeField] private CameraController cameraController;

    [Header("UI references")]
    [SerializeField] private Button goToButton;
    [SerializeField] private Button followButton;

    void Awake()
    {
        if (goToButton == null)
            goToButton = GameObject.FindGameObjectWithTag("GoToButton").GetComponent<Button>();
        if (followButton == null)
            followButton = GameObject.FindGameObjectWithTag("FollowButton").GetComponent<Button>();
        if (selector == null)
            selector = FindAnyObjectByType<CelestialSelector>();
        if (cameraController == null)
            cameraController = FindAnyObjectByType<CameraController>();
    }

    void OnEnable()
    {
        if (selector != null)
            selector.SelectionChanged += OnSelectionChanged;

        if (goToButton != null)
            goToButton.onClick.AddListener(OnGoToClicked);
        if (followButton != null)
            followButton.onClick.AddListener(OnFollowClicked);

        OnSelectionChanged(selector != null ? selector.Selected : null);
    }

    void OnDisable()
    {
        if (selector != null)
            selector.SelectionChanged -= OnSelectionChanged;

        if (goToButton != null)
            goToButton.onClick.RemoveListener(OnGoToClicked);
        if (followButton != null)
            followButton.onClick.RemoveListener(OnFollowClicked);
    }

    void OnSelectionChanged(CelestialBody body)
    {
        bool hasSelection = body != null;

        if (goToButton != null)
            goToButton.gameObject.SetActive(hasSelection);
        if (followButton != null)
            followButton.gameObject.SetActive(hasSelection);
    }

    void OnGoToClicked()
    {
        if (cameraController != null)
            cameraController.GoToSelected();
    }

    void OnFollowClicked()
    {
        if (cameraController != null)
            cameraController.FollowSelected();
    }
}
