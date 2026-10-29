using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

// Игрок (кассир).
// В шлеме (как в SteamVR): ТЕЛЕПОРТ — отклоните любой стик вперёд, появится дуга
// (синяя — можно, красная — нельзя), отпустите — перенесётесь с затемнением.
// Стик влево/вправо — поворот по 45°. Grip/триггер — взять/отпустить.
// Без шлема: WASD + мышь + ЛКМ (режим для проверки на обычном ПК).
[RequireComponent(typeof(CharacterController))]
public class VRPlayer : MonoBehaviour
{
    public Transform head;
    public Hand leftHand, rightHand;
    public Label statusLabel;          // надпись «режим VR / клавиатура» на стене
    public float moveSpeed = 2.0f;     // скорость ходьбы (только режим без шлема)
    public float runMultiplier = 1.8f;
    public float snapAngle = 45f;
    public float aimPitchOffset = 0f;  // если дуга смотрит не туда — подправьте угол (градусы)
    public float teleportSpeed = 7f;   // «сила броска» дуги
    public float teleportMaxDistance = 14f;
    public ParticleSystem teleportBurst;   // вспышка частиц в точке прибытия

    CharacterController cc;
    XRPose headPose, leftPose, rightPose;
    bool desktop = true;
    bool modeInitialized;
    bool snapReady = true;
    bool prevGripL, prevGripR;
    float pitch, vy, statusTimer;
    float reach = 0.9f;
    string lastStatus = "";

    // телепорт
    LineRenderer teleLine;
    GameObject teleMarker;
    Renderer fadeRenderer;
    Material fadeMat;
    bool aiming, teleValid, teleporting;
    Hand aimHand;
    Vector3 teleTarget;
    readonly List<Vector3> telePoints = new List<Vector3>();

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        Application.runInBackground = true;   // иначе игра на паузе, когда активно окно SteamVR
    }

    void Start()
    {
        headPose = head.GetComponent<XRPose>();
        leftPose = leftHand.GetComponent<XRPose>();
        rightPose = rightHand.GetComponent<XRPose>();
        BuildTeleportVisuals();
        StartCoroutine(SetFloorOrigin());
    }

    static Material SimpleMaterial(Color c)
    {
        Shader sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Unlit/Color");
        var m = new Material(sh);
        m.color = c;
        return m;
    }

    void BuildTeleportVisuals()
    {
        var lg = new GameObject("TeleportLine");
        lg.transform.SetParent(transform, false);
        teleLine = lg.AddComponent<LineRenderer>();
        teleLine.useWorldSpace = true;
        teleLine.widthMultiplier = 0.025f;
        teleLine.material = SimpleMaterial(Color.white);
        teleLine.enabled = false;

        teleMarker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        teleMarker.name = "TeleportMarker";
        Destroy(teleMarker.GetComponent<Collider>());
        teleMarker.transform.localScale = new Vector3(0.6f, 0.01f, 0.6f);
        teleMarker.GetComponent<Renderer>().sharedMaterial = SimpleMaterial(new Color(0.3f, 0.7f, 1f, 0.75f));
        teleMarker.SetActive(false);

        // плоскость перед камерой для затемнения при телепорте
        var fade = GameObject.CreatePrimitive(PrimitiveType.Quad);
        fade.name = "TeleportFade";
        Destroy(fade.GetComponent<Collider>());
        fade.transform.SetParent(head, false);
        fade.transform.localPosition = new Vector3(0f, 0f, 0.12f);
        fade.transform.localScale = new Vector3(2f, 2f, 1f);
        fadeMat = SimpleMaterial(new Color(0f, 0f, 0f, 0f));
        fadeRenderer = fade.GetComponent<Renderer>();
        fadeRenderer.sharedMaterial = fadeMat;
        fadeRenderer.enabled = false;
    }

    // Просим трекинг «от пола». Если не получилось — добавляем рост вручную.
    IEnumerator SetFloorOrigin()
    {
        for (int i = 0; i < 15; i++)
        {
            yield return new WaitForSeconds(1f);
            var subs = new List<XRInputSubsystem>();
            SubsystemManager.GetSubsystems(subs);
            if (subs.Count == 0) continue;
            bool floor = false;
            foreach (var s in subs)
            {
                s.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
                if ((s.GetTrackingOriginMode() & TrackingOriginModeFlags.Floor) != 0) floor = true;
            }
            Vector3 off = floor ? Vector3.zero : new Vector3(0f, 1.6f, 0f);
            if (headPose != null) headPose.offset = off;
            if (leftPose != null) leftPose.offset = off;
            if (rightPose != null) rightPose.offset = off;
            yield break;
        }
    }

    void SetMode(bool desktopMode)
    {
        desktop = desktopMode;
        modeInitialized = true;
        if (headPose != null) headPose.enabled = !desktopMode;
        if (leftPose != null) leftPose.enabled = !desktopMode;
        if (rightPose != null) rightPose.enabled = !desktopMode;

        if (desktopMode)
        {
            head.localPosition = new Vector3(0f, 1.65f, 0f);
            head.localRotation = Quaternion.identity;
            leftHand.grabRadius = rightHand.grabRadius = 0.3f;
            leftHand.gameObject.SetActive(false);
        }
        else
        {
            leftHand.gameObject.SetActive(true);
            leftHand.grabRadius = rightHand.grabRadius = 0.12f;
            Cursor.lockState = CursorLockMode.None;
        }
    }

    void Update()
    {
        bool vr = XRSettings.isDeviceActive;
        if (!modeInitialized || vr == desktop) SetMode(!vr);

        Vector3 move = desktop ? DesktopUpdate() : VRUpdate();

        if (cc.enabled)
        {
            if (cc.isGrounded) vy = -1f;
            else vy += Physics.gravity.y * Time.deltaTime;
            cc.Move((move + Vector3.up * vy) * Time.deltaTime);
        }

        statusTimer -= Time.deltaTime;
        if (statusTimer <= 0f) { statusTimer = 0.5f; UpdateStatus(); }
    }

    void UpdateStatus()
    {
        string text;
        if (XRSettings.isDeviceActive)
            text = "РЕЖИМ: VR (шлем активен)\nУстройство: " + XRSettings.loadedDeviceName;
        else if (string.IsNullOrEmpty(XRSettings.loadedDeviceName))
            text = "РЕЖИМ: клавиатура\nVR не запущен. Включите OpenXR:\nProject Settings > XR Plug-in Management";
        else
            text = "РЕЖИМ: клавиатура\nOpenXR запущен, но шлем не активен.\nВключите SteamVR и наденьте шлем.";
        if (text != lastStatus)
        {
            lastStatus = text;
            Debug.Log("[Супермаркет] " + text.Replace("\n", " | "));
            if (statusLabel != null) statusLabel.SetText(text);
        }
    }

    // ---------------- VR: телепорт + поворот + хват ----------------
    Vector3 VRUpdate()
    {
        if (PauseMenu.IsOpen)                      // в меню паузы: без телепорта и хвата, только нажатие кнопок рукой
        {
            aiming = false;
            teleLine.enabled = false;
            teleMarker.SetActive(false);
            return Vector3.zero;
        }
        bool gl = XRInput.Grip(XRNode.LeftHand);
        bool gr = XRInput.Grip(XRNode.RightHand);
        if (gl && !prevGripL) leftHand.TryGrab();
        if (!gl && prevGripL) leftHand.Release();
        if (gr && !prevGripR) rightHand.TryGrab();
        if (!gr && prevGripR) rightHand.Release();
        prevGripL = gl;
        prevGripR = gr;

        if (teleporting) return Vector3.zero;

        Vector2 sl = XRInput.Stick(XRNode.LeftHand);
        Vector2 sr = XRInput.Stick(XRNode.RightHand);

        // стик вперёд (любой руки) — целимся
        Hand want = null;
        if (sl.y > 0.75f && Mathf.Abs(sl.x) < 0.5f) want = leftHand;
        else if (sr.y > 0.75f && Mathf.Abs(sr.x) < 0.5f) want = rightHand;

        if (want != null)
        {
            aiming = true;
            aimHand = want;
            UpdateTeleportAim(aimHand);
        }
        else
        {
            if (aiming)
            {
                aiming = false;
                teleLine.enabled = false;
                teleMarker.SetActive(false);
                if (teleValid) StartCoroutine(TeleportRoutine(teleTarget, aimHand));
            }
            // поворот по 45° стиком влево/вправо
            float tx = Mathf.Abs(sl.x) > Mathf.Abs(sr.x) ? sl.x : sr.x;
            if (Mathf.Abs(tx) > 0.7f)
            {
                if (snapReady)
                {
                    transform.RotateAround(head.position, Vector3.up, Mathf.Sign(tx) * snapAngle);
                    snapReady = false;
                }
            }
            else if (Mathf.Abs(tx) < 0.3f) snapReady = true;
        }
        return Vector3.zero;
    }

    // Откуда целиться: объект HandModel (направление пальцев), иначе сама рука
    static Transform Pointer(Hand h)
    {
        var m = h.transform.Find("HandModel");
        return m != null ? m : h.transform;
    }

    void UpdateTeleportAim(Hand h)
    {
        Transform ptr = Pointer(h);
        Vector3 dir = Quaternion.AngleAxis(aimPitchOffset, ptr.right) * ptr.forward;
        Vector3 vel = dir * teleportSpeed;
        Vector3 p = ptr.position;
        telePoints.Clear();
        telePoints.Add(p);
        teleValid = false;
        const float dt = 0.04f;
        for (int i = 0; i < 50; i++)
        {
            vel += Physics.gravity * dt;
            Vector3 next = p + vel * dt;
            Vector3 seg = next - p;
            float dist = seg.magnitude;
            if (dist < 0.0001f) break;
            RaycastHit[] hs = Physics.RaycastAll(p, seg / dist, dist, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            RaycastHit bestHit = new RaycastHit();
            bool got = false;
            foreach (var hit in hs)
            {
                if (hit.collider.GetComponentInParent<VRPlayer>() != null) continue;   // свои коллайдеры
                if (hit.collider.GetComponentInParent<Grabbable>() != null) continue;  // предметы не мешают
                if (hit.distance < best) { best = hit.distance; bestHit = hit; got = true; }
            }
            if (got)
            {
                telePoints.Add(bestHit.point);
                teleTarget = bestHit.point;
                teleValid = bestHit.normal.y > 0.7f
                            && bestHit.collider.GetComponentInParent<TeleportSurface>() != null
                            && (bestHit.point - transform.position).magnitude < teleportMaxDistance;
                if (teleValid)
                {
                    var tp = TeleportPoint.Nearest(bestHit.point);      // «магнит» к точкам телепорта
                    if (tp != null) teleTarget = tp.transform.position;
                }
                break;
            }
            telePoints.Add(next);
            p = next;
        }
        teleLine.enabled = true;
        teleLine.positionCount = telePoints.Count;
        for (int i = 0; i < telePoints.Count; i++) teleLine.SetPosition(i, telePoints[i]);
        Color c = teleValid ? new Color(0.25f, 0.65f, 1f, 0.95f) : new Color(1f, 0.25f, 0.25f, 0.9f);
        teleLine.startColor = c;
        teleLine.endColor = c;
        teleMarker.SetActive(teleValid);
        if (teleValid) teleMarker.transform.position = teleTarget + Vector3.up * 0.012f;
    }

    // Телепорт с затемнением («моргание»), как в SteamVR
    IEnumerator TeleportRoutine(Vector3 target, Hand h)
    {
        teleporting = true;
        yield return Fade(0f, 1f, 0.09f);

        Vector3 headFloor = new Vector3(head.position.x, transform.position.y, head.position.z);
        Vector3 delta = target - headFloor;
        cc.enabled = false;
        transform.position = new Vector3(transform.position.x + delta.x, target.y + 0.05f, transform.position.z + delta.z);
        cc.enabled = true;
        vy = 0f;
        if (h != null) h.Haptic(0.35f, 0.06f);
        SoundManager.Teleport(target);
        if (teleportBurst != null) { teleportBurst.transform.position = target + Vector3.up * 0.05f; teleportBurst.Play(); }

        yield return new WaitForSeconds(0.04f);
        yield return Fade(1f, 0f, 0.14f);
        teleporting = false;
    }

    IEnumerator Fade(float from, float to, float time)
    {
        fadeRenderer.enabled = true;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            SetFade(Mathf.Lerp(from, to, t / time));
            yield return null;
        }
        SetFade(to);
        if (to <= 0f) fadeRenderer.enabled = false;
    }

    void SetFade(float a)
    {
        fadeMat.color = new Color(0f, 0f, 0f, a);
    }

    // ---------------- режим без шлема ----------------
    Vector3 DesktopUpdate()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null) return Vector3.zero;

        if (kb.escapeKey.wasPressedThisFrame) Cursor.lockState = CursorLockMode.None;

        if (mouse.leftButton.wasPressedThisFrame && !PauseMenu.IsOpen)
        {
            if (Cursor.lockState != CursorLockMode.Locked) Cursor.lockState = CursorLockMode.Locked;
            else
            {
                if (rightHand.Held == null) rightHand.TryGrab();
                else rightHand.Release();
            }
        }

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            Vector2 d = mouse.delta.ReadValue() * 0.08f;
            transform.Rotate(0f, d.x, 0f);
            pitch = Mathf.Clamp(pitch - d.y, -80f, 80f);
            head.localEulerAngles = new Vector3(pitch, 0f, 0f);
        }

        reach = Mathf.Clamp(reach + mouse.scroll.ReadValue().y * 0.001f, 0.4f, 1.8f);
        rightHand.transform.position = head.position + head.forward * reach + Vector3.down * 0.1f;
        rightHand.transform.rotation = head.rotation;

        float x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
        float z = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
        Vector3 dir = transform.right * x + transform.forward * z;
        if (dir.sqrMagnitude > 1f) dir.Normalize();
        return dir * moveSpeed * (kb.leftShiftKey.isPressed ? runMultiplier : 1f);
    }
}
