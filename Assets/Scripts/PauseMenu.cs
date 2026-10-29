using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;

// Меню паузы: Esc / P на клавиатуре или кнопка меню на левом контроллере.
// Пункты: ПРОДОЛЖИТЬ, ЗАНОВО, ВЫЙТИ ИЗ ИГРЫ (выход с подтверждением: два нажатия подряд).
// Панель появляется перед глазами игрока; пока меню открыто, игра стоит на паузе.
public class PauseMenu : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    public GameObject panel;
    public Transform head;
    public Button resumeButton, restartButton, quitButton;
    public Text quitLabel;
    public float distance = 1.2f;

    float quitTimer;
    bool prevMenu;
    bool quitArmed;

    void Awake()
    {
        IsOpen = false;
        Time.timeScale = 1f;
        if (panel != null) panel.SetActive(false);
        if (resumeButton != null) resumeButton.onClick.AddListener(Close);
        if (restartButton != null) restartButton.onClick.AddListener(Restart);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuit);
    }

    void OnDestroy()
    {
        IsOpen = false;
        Time.timeScale = 1f;
    }

    void Update()
    {
        var kb = Keyboard.current;
        bool toggle = kb != null && (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame);
        bool menu = XRInput.MenuButton(XRNode.LeftHand);
        if (menu && !prevMenu) toggle = true;
        prevMenu = menu;
        if (toggle) { if (IsOpen) Close(); else Open(); }

        if (quitArmed)
        {
            quitTimer -= Time.unscaledDeltaTime;
            if (quitTimer <= 0f) DisarmQuit();
        }
    }

    void Open()
    {
        IsOpen = true;
        Place();
        panel.SetActive(true);
        DisarmQuit();
        Time.timeScale = 0f;
        SoundManager.Click();
    }

    void Close()
    {
        IsOpen = false;
        panel.SetActive(false);
        Time.timeScale = 1f;
    }

    void Place()
    {
        if (head == null) return;
        Vector3 f = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (f.sqrMagnitude < 0.01f) f = Vector3.ProjectOnPlane(head.up, Vector3.up);
        f.Normalize();
        panel.transform.position = head.position + f * distance + Vector3.down * 0.05f;
        panel.transform.rotation = Quaternion.LookRotation(f);
    }

    void Restart()
    {
        Time.timeScale = 1f;
        IsOpen = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void OnQuit()
    {
        if (quitArmed) { QuitGame(); return; }
        quitArmed = true;
        quitTimer = 3f;
        if (quitLabel != null) quitLabel.text = "НАЖМИТЕ ЕЩЁ РАЗ";
    }

    void DisarmQuit()
    {
        quitArmed = false;
        if (quitLabel != null) quitLabel.text = "ВЫЙТИ ИЗ ИГРЫ";
    }

    // Закрывает игру (в EXE) или останавливает Play Mode (в редакторе)
    public static void QuitGame()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
