using UnityEngine;
using UnityEngine.XR;

// Двигает объект (голова/рука) по данным трекинга XR.
public class XRPose : MonoBehaviour
{
    public XRNode node = XRNode.Head;
    public Vector3 offset = Vector3.zero;   // добавка по высоте, если трекинг не «от пола»

    void Update()
    {
        InputDevice d = InputDevices.GetDeviceAtXRNode(node);
        if (!d.isValid) return;
        Vector3 p;
        Quaternion q;
        if (d.TryGetFeatureValue(CommonUsages.devicePosition, out p) &&
            d.TryGetFeatureValue(CommonUsages.deviceRotation, out q))
        {
            transform.localPosition = p + offset;
            transform.localRotation = q;
        }
    }
}
