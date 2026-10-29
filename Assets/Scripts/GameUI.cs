using UnityEngine;
using UnityEngine.UI;

// Интерфейс «кассового планшета» (World-Space Canvas): СТАРТ, время, прогресс, баллы, FPS, кнопка звука.
public class GameUI : MonoBehaviour
{
    public Text title, status, timerText, servedText, scoreText, fpsText;
    public Button startButton, muteButton, quitButton;
    public Text startLabel, muteLabel, quitLabel;

    public event System.Action StartPressed;
    public event System.Action RestartPressed;

    enum Mode { Menu, Playing, Done }
    Mode mode = Mode.Menu;
    float elapsed, fps = 60f, fpsTimer;
    int total;
    bool quitArmed;
    float quitTimer;

    void Awake()
    {
        if (startButton != null) startButton.onClick.AddListener(OnStart);
        if (muteButton != null) muteButton.onClick.AddListener(OnMute);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuit);
    }

    // Выход из игры: первое нажатие просит подтверждение, второе (в течение 3 с) закрывает игру
    void OnQuit()
    {
        if (quitArmed) { PauseMenu.QuitGame(); return; }
        quitArmed = true;
        quitTimer = 3f;
        if (quitLabel != null) quitLabel.text = "ТОЧНО?";
    }

    void OnStart()
    {
        if (mode == Mode.Menu) { if (StartPressed != null) StartPressed(); }
        else if (mode == Mode.Done) { if (RestartPressed != null) RestartPressed(); }
    }

    void OnMute()
    {
        if (SoundManager.Instance == null) return;
        SoundManager.Instance.SetMuted(!SoundManager.Instance.Muted);
        if (muteLabel != null) muteLabel.text = SoundManager.Instance.Muted ? "ЗВУК: ВЫКЛ" : "ЗВУК: ВКЛ";
    }

    public void ShowMenu()
    {
        mode = Mode.Menu;
        if (title != null) title.text = "СУПЕРМАРКЕТ VR";
        if (status != null) status.text = "Нажмите СТАРТ";
        if (startLabel != null) startLabel.text = "СТАРТ";
        if (startButton != null) startButton.interactable = true;
        SetProgress(0, total);
        SetScore(0);
    }

    public void BeginGame(int customers)
    {
        mode = Mode.Playing;
        total = customers;
        elapsed = 0f;
        if (status != null) status.text = "Обслуживайте покупателей";
        if (startLabel != null) startLabel.text = "ИДЁТ ИГРА";
        if (startButton != null) startButton.interactable = false;
        SetProgress(0, customers);
    }

    public void SetStatus(string s) { if (status != null) status.text = s; }
    public void SetProgress(int served, int all) { if (servedText != null) servedText.text = "Обслужено: " + served + "/" + all; }
    public void SetScore(int s) { if (scoreText != null) scoreText.text = "Баллы: " + s; }

    public void ShowDone(string summary)
    {
        mode = Mode.Done;
        if (status != null) status.text = summary;
        if (startLabel != null) startLabel.text = "ЗАНОВО";
        if (startButton != null) startButton.interactable = true;
    }

    void Update()
    {
        if (quitArmed)
        {
            quitTimer -= Time.unscaledDeltaTime;
            if (quitTimer <= 0f) { quitArmed = false; if (quitLabel != null) quitLabel.text = "ВЫХОД"; }
        }
        if (mode == Mode.Playing) elapsed += Time.deltaTime;
        if (timerText != null)
            timerText.text = "Время: " + ((int)elapsed / 60).ToString("00") + ":" + ((int)elapsed % 60).ToString("00");

        // FPS: для VR нужно держать 72–90
        fps = Mathf.Lerp(fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f), 0.05f);
        fpsTimer -= Time.unscaledDeltaTime;
        if (fpsTimer <= 0f && fpsText != null)
        {
            fpsTimer = 0.5f;
            fpsText.text = "FPS: " + Mathf.RoundToInt(fps);
            fpsText.color = fps >= 72f ? new Color(0.5f, 1f, 0.5f) : (fps >= 45f ? new Color(1f, 0.9f, 0.4f) : new Color(1f, 0.4f, 0.4f));
        }
    }

    public float Elapsed { get { return elapsed; } }
}
