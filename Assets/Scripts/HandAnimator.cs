using UnityEngine;
using UnityEngine.XR;

// Анимация пальцев руки по аналоговым значениям контроллера:
// курок -> указательный, grip -> средний/безымянный/мизинец, касание кнопок/стика -> большой палец.
// Если рука держит предмет, пальцы остаются сжатыми вокруг него.
public class HandAnimator : MonoBehaviour
{
    public XRNode node = XRNode.RightHand;
    public Hand hand;
    public float side = 1f;                       // 1 — правая рука, -1 — левая (зеркальная)
    public Transform[] index = new Transform[3];
    public Transform[] middle = new Transform[3];
    public Transform[] ring = new Transform[3];
    public Transform[] pinky = new Transform[3];
    public Transform[] thumb = new Transform[2];
    public float smoothing = 18f;

    static readonly float[] FingerMax = { 75f, 95f, 65f };
    static readonly float[] GripMul = { 1.0f, 1.05f, 1.1f };
    const float ThumbMax0 = 55f, ThumbMax1 = 60f;

    float tIndex, tGrip, tThumb;
    Transform[][] chains;
    Quaternion[][] rest;
    Quaternion thumbRest = Quaternion.identity;
    Vector3 thumbAxis = Vector3.forward;

    void Start()
    {
        chains = new[] { index, middle, ring, pinky };
        rest = new Quaternion[chains.Length][];
        for (int f = 0; f < chains.Length; f++)
        {
            rest[f] = new Quaternion[chains[f].Length];
            for (int j = 0; j < chains[f].Length; j++)
                rest[f][j] = chains[f][j] != null ? chains[f][j].localRotation : Quaternion.identity;
        }
        if (thumb != null && thumb.Length > 0 && thumb[0] != null)
        {
            thumbRest = thumb[0].localRotation;
            thumbAxis = Quaternion.Inverse(thumbRest) * Vector3.forward;
        }
    }

    void Update()
    {
        if (chains == null) return;
        float trig, grip, th;
        if (XRSettings.isDeviceActive)
        {
            trig = XRInput.Trigger(node);
            grip = XRInput.GripValue(node);
            th = XRInput.ThumbDown(node) ? 1f : 0f;
        }
        else
        {
            // режим без шлема: расслабленная рука / хват
            trig = 0.1f; grip = 0.15f; th = 0.1f;
        }
        if (hand != null && hand.Held != null)
        {
            grip = Mathf.Max(grip, 0.7f);
            trig = Mathf.Max(trig, 0.45f);
            th = Mathf.Max(th, 0.6f);
        }

        float k = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
        tIndex = Mathf.Lerp(tIndex, trig, k);
        tGrip = Mathf.Lerp(tGrip, grip, k);
        tThumb = Mathf.Lerp(tThumb, th, k);

        for (int f = 0; f < chains.Length; f++)
        {
            float t = f == 0 ? tIndex : tGrip * GripMul[f - 1];
            for (int j = 0; j < chains[f].Length && j < FingerMax.Length; j++)
                if (chains[f][j] != null)
                    chains[f][j].localRotation = rest[f][j] * Quaternion.AngleAxis(-side * t * FingerMax[j], Vector3.up);
        }

        if (thumb != null && thumb.Length >= 2 && thumb[0] != null && thumb[1] != null)
        {
            thumb[0].localRotation = Quaternion.AngleAxis(side * tThumb * ThumbMax0, Vector3.forward) * thumbRest;
            thumb[1].localRotation = Quaternion.AngleAxis(side * tThumb * ThumbMax1, thumbAxis);
        }
    }
}
