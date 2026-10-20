using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class ButtonSceneLoader : MonoBehaviour
{
    public UIDocument uiDocument;

    private Button startButton;
    private Button sliceButton;
    private Button exitButton;

    void OnEnable()
    {
        var root = uiDocument.rootVisualElement;

        startButton = root.Q<Button>("START");
        sliceButton = root.Q<Button>("SLICE");
        exitButton = root.Q<Button>("EXIT");

        if (startButton != null)
            startButton.clicked += StartSimulation;

        if (sliceButton != null)
            sliceButton.clicked += SliceSimulation;

        if (exitButton != null)
            exitButton.clicked += ExitGame;
    }

    void OnDisable()
    {
        if (startButton != null)
            startButton.clicked -= StartSimulation;

        if (sliceButton != null)
            sliceButton.clicked -= SliceSimulation;

        if (exitButton != null)
            exitButton.clicked -= ExitGame;
    }

    void StartSimulation()
    {
        SceneManager.LoadScene("fire simulation");
    }

    void SliceSimulation()
    {
        Debug.Log("Slice button clicked.");
        // Add your slicing code here.
    }

    void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}