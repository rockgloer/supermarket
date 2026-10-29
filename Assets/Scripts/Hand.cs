using UnityEngine;
using UnityEngine.XR;

// Рука: берёт ближайший Grabbable в радиусе, подсвечивает его при наведении,
// бросает с текущей скоростью руки и вибрирует контроллером.
public class Hand : MonoBehaviour
{
    public XRNode node = XRNode.RightHand;
    public float grabRadius = 0.12f;
    public Grabbable Held { get; private set; }
    public Grabbable Hover { get; private set; }

    Vector3 lastPos;
    Quaternion lastRot;
    Vector3 vel, angVel;
    readonly Collider[] hits = new Collider[32];

    void Start()
    {
        lastPos = transform.position;
        lastRot = transform.rotation;
    }

    void OnDisable()
    {
        if (Hover != null) Hover.SetHighlight(false);
        Hover = null;
    }

    void Update()
    {
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 v = (transform.position - lastPos) / dt;
        vel = Vector3.Lerp(vel, v, 0.5f);

        Quaternion d = transform.rotation * Quaternion.Inverse(lastRot);
        float ang;
        Vector3 axis;
        d.ToAngleAxis(out ang, out axis);
        if (ang > 180f) ang -= 360f;
        Vector3 av = axis * (ang * Mathf.Deg2Rad / dt);
        if (float.IsNaN(av.x) || float.IsNaN(av.y) || float.IsNaN(av.z) || float.IsInfinity(av.x)) av = Vector3.zero;
        angVel = Vector3.Lerp(angVel, av, 0.5f);

        lastPos = transform.position;
        lastRot = transform.rotation;

        // подсветка ближайшего предмета, пока рука пуста
        if (Held == null)
        {
            Grabbable g = FindBest();
            if (g != Hover)
            {
                if (Hover != null) Hover.SetHighlight(false);
                Hover = g;
                if (Hover != null)
                {
                    Hover.SetHighlight(true);
                    Haptic(0.12f, 0.02f);
                }
            }
        }
        else if (Hover != null)
        {
            Hover.SetHighlight(false);
            Hover = null;
        }
    }

    Grabbable FindBest()
    {
        int n = Physics.OverlapSphereNonAlloc(transform.position, grabRadius, hits, ~0, QueryTriggerInteraction.Ignore);
        Grabbable best = null;
        float bestDist = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            var g = hits[i].GetComponentInParent<Grabbable>();
            if (g == null) continue;
            float dist = (hits[i].ClosestPoint(transform.position) - transform.position).sqrMagnitude;
            if (dist < bestDist) { bestDist = dist; best = g; }
        }
        return best;
    }

    public void TryGrab()
    {
        if (Held != null) return;
        Grabbable best = FindBest();
        if (best == null) return;
        if (best.Holder != null) best.Holder.Release();
        best.SetHighlight(false);
        Hover = null;
        Held = best;
        best.Attach(this);
        Haptic(0.5f, 0.08f);
        SoundManager.Grab(transform.position);
    }

    public void Release()
    {
        if (Held == null) return;
        var g = Held;
        Held = null;
        g.Detach(vel, angVel);
        Haptic(0.2f, 0.04f);
        SoundManager.Drop(transform.position);
    }

    public void Haptic(float amplitude, float duration)
    {
        if (XRSettings.isDeviceActive) XRInput.Haptic(node, amplitude, duration);
    }
}
