using System.Collections.Generic;
using UnityEngine;

// Точка телепорта («якорь»): светящаяся площадка с названием. Если дуга телепорта приземляется рядом —
// игрок «примагничивается» к точке. Так можно быстро попасть на кассу, ко входу, к лестнице и отделам.
public class TeleportPoint : MonoBehaviour
{
    public static readonly List<TeleportPoint> All = new List<TeleportPoint>();
    public string pointName = "Точка";
    public float snapRadius = 0.9f;

    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() { All.Remove(this); }

    // Ближайшая точка в радиусе привязки (и на том же этаже)
    public static TeleportPoint Nearest(Vector3 p)
    {
        TeleportPoint best = null;
        float bd = float.MaxValue;
        foreach (var t in All)
        {
            if (t == null) continue;
            Vector3 d = t.transform.position - p;
            if (Mathf.Abs(d.y) > 0.8f) continue;
            d.y = 0f;
            float m = d.magnitude;
            if (m < t.snapRadius && m < bd) { bd = m; best = t; }
        }
        return best;
    }
}
