using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayPauseUI : MonoBehaviour
{
    [SerializeField] private Button playPauseButton;
    [SerializeField] private TMP_Text label;

    private bool isPaused;

    void Awake()
    {
        if (playPauseButton == null)
            playPauseButton = GameObject.FindGameObjectWithTag("PlayPauseButton")?.GetComponent<Button>();
        if (label == null && playPauseButton != null)
            label = playPauseButton.GetComponentInChildren<TMP_Text>();
    }

    void OnEnable()
    {
        if (playPauseButton != null)
            playPauseButton.onClick.AddListener(Toggle);

        Apply();
    }

    void OnDisable()
    {
        if (playPauseButton != null)
            playPauseButton.onClick.RemoveListener(Toggle);

        // Do not leave the simulation paused.
        Time.timeScale = 1f;
    }

    void Toggle()
    {
        isPaused = !isPaused;
        Apply();
    }

    void Apply()
    {
        Time.timeScale = isPaused ? 0f : 1f;

        // The button shows the action it will perform.
        if (label != null)
            label.text = isPaused ? "Play" : "Pause";
    }
    void Update()
    {
        // If the user presses the space bar, toggle play/pause.
        if (Input.GetKeyDown(KeyCode.Space))
            Toggle();
    }
}
