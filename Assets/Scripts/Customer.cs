using System.Collections.Generic;
using System.Text;
using UnityEngine;

// NPC-покупатель: идёт по маршруту, качает руками и ногами, смотрит головой на игрока,
// называет заказ, платит, уходит.
public class Customer : MonoBehaviour
{
    public string displayName = "Покупатель";
    public string[] order;
    public float speed = 1.3f;

    // части тела (назначает SupermarketBuilder; «Pos» — сторона +X, «Neg» — сторона -X)
    public Transform head, armPos, armNeg, legPos, legNeg;
    public Transform lookTarget;                 // если не задан — берётся главная камера (игрок)
    public float lookDistance = 12f;
    public Material signMaterial;                // подложка таблички над головой (назначает builder)

    public bool Arrived { get; private set; }

    Vector3[] path;
    int pathIndex;
    bool moving;
    float phase, swing;
    Animator animator;                           // клипы Walk/Idle (назначает builder); если нет — ходьба скриптом
    float stepTimer;
    Label statusFront, statusBack;               // верхняя строка таблички (реплика)

    void Awake()
    {
        animator = GetComponent<Animator>();
        BuildSign();
    }

    // Табличка над головой: имя, список заказа и реплика. Читается и спереди, и сзади, вместе с NPC не «вертится».
    void BuildSign()
    {
        var sign = new GameObject("Табличка").transform;
        sign.SetParent(transform, false);
        sign.localPosition = new Vector3(0f, 2.3f, 0f);

        var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = "Подложка";
        Destroy(panel.GetComponent<Collider>());
        panel.transform.SetParent(sign, false);
        panel.transform.localScale = new Vector3(0.58f, 0.46f, 0.02f);
        if (signMaterial != null) panel.GetComponent<Renderer>().sharedMaterial = signMaterial;

        string list = OrderText();
        var yellow = new Color(1f, 0.95f, 0.45f);
        // спереди (зритель спереди NPC): текст повёрнут на 180°
        var lf = Label.Create(sign, new Vector3(0f, -0.04f, 0.014f), list, 0.062f, Color.white, false);
        lf.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        statusFront = Label.Create(sign, new Vector3(0f, 0.18f, 0.014f), "", 0.055f, yellow, false);
        statusFront.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        // сзади
        Label.Create(sign, new Vector3(0f, -0.04f, -0.014f), list, 0.062f, Color.white, false);
        statusBack = Label.Create(sign, new Vector3(0f, 0.18f, -0.014f), "", 0.055f, yellow, false);
    }

    string OrderText()
    {
        var sb = new StringBuilder();
        sb.Append(displayName).Append(":\n");
        if (order != null)
        {
            var counts = new Dictionary<string, int>();
            var names = new List<string>();
            foreach (var o in order)
            {
                if (!counts.ContainsKey(o)) { counts[o] = 0; names.Add(o); }
                counts[o]++;
            }
            foreach (var n in names)
            {
                sb.Append("- ").Append(n);
                if (counts[n] > 1) sb.Append(" x").Append(counts[n]);
                sb.Append('\n');
            }
        }
        return sb.ToString().TrimEnd('\n');
    }

    // Идти по точкам маршрута, начиная с точки startIndex
    public void GoPath(Vector3[] points, int startIndex)
    {
        path = points;
        pathIndex = startIndex;
        moving = path != null && pathIndex < path.Length;
        Arrived = !moving;
    }

    public void FaceTowards(Vector3 worldPoint)
    {
        Vector3 d = worldPoint - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(d);
    }

    // Короткая реплика над списком заказа («Здравствуйте!», «Это не то!»)
    public void Say(string text)
    {
        if (statusFront != null) statusFront.SetText(text);
        if (statusBack != null) statusBack.SetText(text);
    }

    void Update()
    {
        if (moving) MoveStep();
        AnimateWalk();
        LookAtPlayer();
    }

    void MoveStep()
    {
        Vector3 pos = transform.position;
        Vector3 to = path[pathIndex] - pos;
        to.y = 0f;
        if (to.magnitude < 0.06f)
        {
            pathIndex++;
            if (pathIndex >= path.Length)
            {
                moving = false;
                Arrived = true;
                transform.position = new Vector3(pos.x, 0f, pos.z);
            }
            return;
        }
        Vector3 dir = to.normalized;
        pos += dir * speed * Time.deltaTime;
        pos.y = Mathf.Abs(Mathf.Sin(phase)) * 0.02f;     // лёгкое покачивание корпуса при шаге
        transform.position = pos;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 8f * Time.deltaTime);
    }

    // Анимация ходьбы: через Animator (клипы Walk/Idle), а если его нет — маятник ног и рук скриптом
    void AnimateWalk()
    {
        if (moving)
        {
            stepTimer -= Time.deltaTime;
            if (stepTimer <= 0f) { stepTimer = 0.42f; SoundManager.Step(transform.position); }
        }
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.SetBool("Walking", moving);
            if (moving) phase += Time.deltaTime * speed * 5.5f;
            return;
        }
        swing = Mathf.MoveTowards(swing, moving ? 1f : 0f, Time.deltaTime * 5f);
        if (swing > 0.001f) phase += Time.deltaTime * speed * 5.5f;
        float a = Mathf.Sin(phase) * swing;
        if (legPos != null) legPos.localRotation = Quaternion.Euler(a * 26f, 0f, 0f);
        if (legNeg != null) legNeg.localRotation = Quaternion.Euler(-a * 26f, 0f, 0f);
        if (armPos != null) armPos.localRotation = Quaternion.Euler(-a * 20f, 0f, 0f);
        if (armNeg != null) armNeg.localRotation = Quaternion.Euler(a * 20f, 0f, 0f);
    }

    // Голова поворачивается к игроку (в пределах разумных углов)
    void LookAtPlayer()
    {
        if (head == null) return;
        Transform target = lookTarget;
        if (target == null && Camera.main != null) target = Camera.main.transform;
        Quaternion desired = Quaternion.identity;
        if (target != null)
        {
            Vector3 local = transform.InverseTransformDirection(target.position - head.position);
            float horiz = new Vector2(local.x, local.z).magnitude;
            if (local.magnitude < lookDistance)
            {
                float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -75f, 75f);
                float pitch = Mathf.Clamp(-Mathf.Atan2(local.y, Mathf.Max(horiz, 0.01f)) * Mathf.Rad2Deg, -30f, 30f);
                desired = Quaternion.Euler(pitch, yaw, 0f);
            }
        }
        head.localRotation = Quaternion.Slerp(head.localRotation, desired, 1f - Mathf.Exp(-8f * Time.deltaTime));
    }
}
