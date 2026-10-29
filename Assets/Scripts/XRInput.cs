using UnityEngine;
using UnityEngine.XR;

// Чтение шлема и контроллеров через UnityEngine.XR.InputDevices.
// Работает с OpenXR (SteamVR как рантайм) без настройки Input Actions.
public static class XRInput
{
    public static bool TryDevice(XRNode node, out InputDevice dev)
    {
        dev = InputDevices.GetDeviceAtXRNode(node);
        return dev.isValid;
    }

    public static Vector2 Stick(XRNode node)
    {
        InputDevice d;
        Vector2 v;
        if (TryDevice(node, out d) && d.TryGetFeatureValue(CommonUsages.primary2DAxis, out v))
            return v.magnitude < 0.15f ? Vector2.zero : v;
        return Vector2.zero;
    }

    // Grip или триггер — любая из этих кнопок «берёт» предмет
    public static bool Grip(XRNode node)
    {
        InputDevice d;
        if (!TryDevice(node, out d)) return false;
        bool g = false, t = false;
        d.TryGetFeatureValue(CommonUsages.gripButton, out g);
        d.TryGetFeatureValue(CommonUsages.triggerButton, out t);
        return g || t;
    }

    public static bool Primary(XRNode node)
    {
        InputDevice d;
        bool b = false;
        if (TryDevice(node, out d)) d.TryGetFeatureValue(CommonUsages.primaryButton, out b);
        return b;
    }

    // Аналоговые значения (0..1) для анимации пальцев
    public static float Trigger(XRNode node)
    {
        InputDevice d;
        float v = 0f;
        if (TryDevice(node, out d)) d.TryGetFeatureValue(CommonUsages.trigger, out v);
        return v;
    }

    public static float GripValue(XRNode node)
    {
        InputDevice d;
        float v = 0f;
        if (TryDevice(node, out d)) d.TryGetFeatureValue(CommonUsages.grip, out v);
        return v;
    }

    // Большой палец лежит на кнопках/стике (касание или нажатие)
    public static bool ThumbDown(XRNode node)
    {
        InputDevice d;
        if (!TryDevice(node, out d)) return false;
        bool a = false, b = false, c = false, e = false, f = false;
        d.TryGetFeatureValue(CommonUsages.primaryTouch, out a);
        d.TryGetFeatureValue(CommonUsages.secondaryTouch, out b);
        d.TryGetFeatureValue(CommonUsages.primary2DAxisTouch, out c);
        d.TryGetFeatureValue(CommonUsages.primaryButton, out e);
        d.TryGetFeatureValue(CommonUsages.secondaryButton, out f);
        return a || b || c || e || f;
    }

    public static bool MenuButton(XRNode node)
    {
        InputDevice d;
        bool b = false;
        if (TryDevice(node, out d)) d.TryGetFeatureValue(CommonUsages.menuButton, out b);
        return b;
    }

    public static bool Secondary(XRNode node)
    {
        InputDevice d;
        bool b = false;
        if (TryDevice(node, out d)) d.TryGetFeatureValue(CommonUsages.secondaryButton, out b);
        return b;
    }

    // Вибрация контроллера
    public static void Haptic(XRNode node, float amplitude, float duration)
    {
        InputDevice d;
        if (!TryDevice(node, out d)) return;
        HapticCapabilities cap;
        if (d.TryGetHapticCapabilities(out cap) && cap.supportsImpulse)
            d.SendHapticImpulse(0, amplitude, duration);
    }
}
