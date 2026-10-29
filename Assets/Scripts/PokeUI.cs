using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR;

// «Нажатие» кнопок интерфейса рукой в VR (дотронуться кончиком руки до кнопки) и клавишей E при взгляде на кнопку
// в режиме без шлема. Работает с кнопками на World-Space Canvas, EventSystem не нужен.
public class PokeUI : MonoBehaviour
{
    public Button[] buttons;
    public Hand[] hands;
    public Transform head;
    public float touchDepth = 0.035f;

    float[] cooldown;
    Color[] baseColor;

    void Start()
    {
        cooldown = new float[buttons.Length];
        baseColor = new Color[buttons.Length];
        for (int i = 0; i < buttons.Length; i++)
            baseColor[i] = buttons[i].targetGraphic != null ? buttons[i].targetGraphic.color : Color.white;
    }

    void Update()
    {
        for (int i = 0; i < buttons.Length; i++) cooldown[i] -= Time.unscaledDeltaTime;
        bool vr = XRSettings.isDeviceActive;
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        bool pressKey = (kb != null && kb.eKey.wasPressedThisFrame) || (PauseMenu.IsOpen && mouse != null && mouse.leftButton.wasPressedThisFrame);

        for (int i = 0; i < buttons.Length; i++)
        {
            var b = buttons[i];
            if (b == null || !b.gameObject.activeInHierarchy) continue;
            var rt = (RectTransform)b.transform;
            bool hover = false, press = false;

            if (vr)
            {
                foreach (var h in hands)
                {
                    if (h == null || !h.gameObject.activeInHierarchy) continue;
                    Vector3 lp = rt.InverseTransformPoint(h.transform.position);
                    float depth = lp.z * Mathf.Abs(rt.lossyScale.z);
                    if (rt.rect.Contains(new Vector2(lp.x, lp.y)) && Mathf.Abs(depth) < touchDepth * 2f)
                    {
                        hover = true;
                        if (Mathf.Abs(depth) < touchDepth && cooldown[i] <= 0f) { press = true; h.Haptic(0.4f, 0.05f); }
                    }
                }
            }
            else if (head != null)
            {
                var plane = new Plane(rt.forward, rt.position);
                var ray = new Ray(head.position, head.forward);
                float enter;
                if (plane.Raycast(ray, out enter) && enter < 3f)
                {
                    Vector3 lp = rt.InverseTransformPoint(ray.GetPoint(enter));
                    if (rt.rect.Contains(new Vector2(lp.x, lp.y)))
                    {
                        hover = true;
                        if (pressKey && cooldown[i] <= 0f) press = true;
                    }
                }
            }

            if (b.targetGraphic != null)
                b.targetGraphic.color = hover ? Color.Lerp(baseColor[i], Color.white, 0.35f) : baseColor[i];
            if (press && b.interactable)
            {
                cooldown[i] = 0.7f;
                SoundManager.Click();
                b.onClick.Invoke();
            }
        }
    }
}
