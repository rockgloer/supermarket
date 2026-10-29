using UnityEngine;
using UnityEngine.XR;

// «Браслет-планшет»: показывает текущий заказ над левой рукой, когда вы смотрите на неё.
// Кнопка X/Y на левом контроллере — включить/выключить.
public class WristDisplay : MonoBehaviour
{
    public Transform hand;
    public Transform head;
    public GameObject content;
    public float height = 0.14f;

    bool userOn = true;
    bool prevButton;

    void Update()
    {
        if (content == null || hand == null || head == null) return;

        bool vr = XRSettings.isDeviceActive;
        bool btn = XRInput.Primary(XRNode.LeftHand) || XRInput.Secondary(XRNode.LeftHand);
        if (btn && !prevButton) userOn = !userOn;
        prevButton = btn;

        bool show = vr && userOn && hand.gameObject.activeInHierarchy;
        if (show)
        {
            Vector3 toHand = hand.position - head.position;
            show = toHand.magnitude < 0.85f && Vector3.Angle(head.forward, toHand) < 55f;
        }
        if (content.activeSelf != show) content.SetActive(show);
        if (!show) return;

        transform.position = hand.position + Vector3.up * height;
        Vector3 away = transform.position - head.position;
        if (away.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(away);
    }
}
