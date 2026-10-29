using UnityEngine;

public static class PhysicsCompat
{
    public static void SetVelocity(Rigidbody rb, Vector3 v)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = v;
#else
        rb.velocity = v;
#endif
    }
}

// Всё, что можно взять рукой (продукты, корзина, купюра).
[RequireComponent(typeof(Rigidbody))]
public class Grabbable : MonoBehaviour
{
    public Hand Holder { get; private set; }

    Rigidbody rb;
    Vector3 startPos;
    Quaternion startRot;
    Renderer[] rends;
    Color[] baseColors;
    MaterialPropertyBlock block;
    bool lit;
    float nextThud;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        startPos = transform.position;
        startRot = transform.rotation;
        rends = GetComponentsInChildren<Renderer>();
        baseColors = new Color[rends.Length];
        for (int i = 0; i < rends.Length; i++)
        {
            var m = rends[i].sharedMaterial;
            baseColors[i] = (m != null && (m.HasProperty("_BaseColor") || m.HasProperty("_Color"))) ? m.color : Color.white;
        }
    }

    // Подсветка предмета, к которому тянется рука
    public void SetHighlight(bool on)
    {
        if (on == lit || rends == null) return;
        lit = on;
        if (block == null) block = new MaterialPropertyBlock();
        for (int i = 0; i < rends.Length; i++)
        {
            if (rends[i] == null) continue;
            if (!on) { rends[i].SetPropertyBlock(null); continue; }
            Color c = baseColors[i];
            Color h = new Color(Mathf.Min(c.r * 1.4f + 0.08f, 2f), Mathf.Min(c.g * 1.4f + 0.08f, 2f), Mathf.Min(c.b * 1.2f + 0.02f, 2f), c.a);
            block.SetColor("_BaseColor", h);
            block.SetColor("_Color", h);
            rends[i].SetPropertyBlock(block);
        }
    }

    void Start()
    {
        // Предметы не должны толкать/блокировать игрока.
        var cc = FindFirstObjectByType<CharacterController>();
        if (cc == null) return;
        foreach (var c in GetComponentsInChildren<Collider>())
            Physics.IgnoreCollision(c, cc, true);
    }

    public void Attach(Hand h)
    {
        SetHighlight(false);
        Holder = h;
        rb.isKinematic = true;
        transform.SetParent(h.transform, true);
    }

    public void Detach(Vector3 velocity, Vector3 angularVelocity)
    {
        Holder = null;
        transform.SetParent(null, true);
        rb.isKinematic = false;
        PhysicsCompat.SetVelocity(rb, Vector3.ClampMagnitude(velocity, 8f));
        rb.angularVelocity = Vector3.ClampMagnitude(angularVelocity, 10f);
    }

    void OnCollisionEnter(Collision c)
    {
        if (Holder != null || Time.time < nextThud) return;
        float v = c.relativeVelocity.magnitude;
        if (v > 0.8f)
        {
            nextThud = Time.time + 0.15f;
            SoundManager.Thud(transform.position, v / 5f);
        }
    }

    void Update()
    {
        // Если предмет упал под пол — вернуть на старое место.
        if (Holder == null && transform.position.y < -3f)
        {
            transform.position = startPos;
            transform.rotation = startRot;
            PhysicsCompat.SetVelocity(rb, Vector3.zero);
            rb.angularVelocity = Vector3.zero;
        }
    }
}
