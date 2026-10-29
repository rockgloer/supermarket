using UnityEngine;

// Звук: фоновая музыка, гул магазина и короткие эффекты (3D-звуки в точке события, 2D — интерфейс).
// Клипы назначает SupermarketBuilder из папки Assets/Audio.
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;

    public AudioClip grab, drop, thud, beep, error, ching, drawer, door, teleport, step, click, hello, fanfare, music, ambience;
    public float sfxVolume = 0.9f;
    public float musicVolume = 0.22f;
    public float ambienceVolume = 0.12f;

    AudioSource sfx2D, musicSrc, ambSrc;
    public bool Muted { get; private set; }

    void Awake()
    {
        Instance = this;
        sfx2D = MakeSource(false, 0f);
        musicSrc = MakeSource(true, musicVolume);
        ambSrc = MakeSource(true, ambienceVolume);
        musicSrc.clip = music;
        ambSrc.clip = ambience;
        if (music != null) musicSrc.Play();
        if (ambience != null) ambSrc.Play();
    }

    AudioSource MakeSource(bool loop, float volume)
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = loop;
        s.volume = volume;
        s.spatialBlend = 0f;
        return s;
    }

    public void SetMuted(bool m)
    {
        Muted = m;
        AudioListener.volume = m ? 0f : 1f;
    }

    // ---- общие методы ----
    static void At(AudioClip c, Vector3 p, float vol)
    {
        if (Instance == null || c == null || Instance.Muted) return;
        AudioSource.PlayClipAtPoint(c, p, vol * Instance.sfxVolume);
    }

    static void Flat(AudioClip c, float vol)
    {
        if (Instance == null || c == null || Instance.sfx2D == null) return;
        Instance.sfx2D.PlayOneShot(c, vol * Instance.sfxVolume);
    }

    // ---- события игры ----
    public static void Grab(Vector3 p) { if (Instance != null) At(Instance.grab, p, 0.8f); }
    public static void Drop(Vector3 p) { if (Instance != null) At(Instance.drop, p, 0.6f); }
    public static void Thud(Vector3 p, float v) { if (Instance != null) At(Instance.thud, p, Mathf.Clamp01(v)); }
    public static void Beep(Vector3 p) { if (Instance != null) At(Instance.beep, p, 0.7f); }
    public static void Error() { if (Instance != null) Flat(Instance.error, 0.8f); }
    public static void Ching(Vector3 p) { if (Instance != null) At(Instance.ching, p, 1f); }
    public static void Drawer(Vector3 p) { if (Instance != null) At(Instance.drawer, p, 0.8f); }
    public static void Door(Vector3 p) { if (Instance != null) At(Instance.door, p, 0.7f); }
    public static void Teleport(Vector3 p) { if (Instance != null) At(Instance.teleport, p, 0.8f); }
    public static void Step(Vector3 p) { if (Instance != null) At(Instance.step, p, 0.35f); }
    public static void Click() { if (Instance != null) Flat(Instance.click, 0.8f); }
    public static void Hello(Vector3 p) { if (Instance != null) At(Instance.hello, p, 0.8f); }
    public static void Fanfare() { if (Instance != null) Flat(Instance.fanfare, 0.9f); }
}
