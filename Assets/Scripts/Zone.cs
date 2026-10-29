using System.Collections.Generic;
using UnityEngine;

// Невидимая зона (BoxCollider-триггер). Умеет находить объекты внутри себя.
[RequireComponent(typeof(BoxCollider))]
public class Zone : MonoBehaviour
{
    BoxCollider box;
    readonly Collider[] hits = new Collider[64];

    public List<T> Find<T>() where T : Component
    {
        var list = new List<T>();
        if (box == null) box = GetComponent<BoxCollider>();
        Vector3 center = transform.TransformPoint(box.center);
        Vector3 half = Vector3.Scale(box.size * 0.5f, transform.lossyScale);
        int n = Physics.OverlapBoxNonAlloc(center, half, hits, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var t = hits[i].GetComponentInParent<T>();
            if (t != null && !list.Contains(t)) list.Add(t);
        }
        return list;
    }

    void OnDrawGizmos()
    {
        var b = GetComponent<BoxCollider>();
        if (b == null) return;
        Gizmos.color = new Color(0f, 1f, 0.4f, 0.8f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(b.center, b.size);
    }
}
