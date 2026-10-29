using System.Collections.Generic;
using UnityEngine;

// Корзина: считает, какие продукты лежат внутри (не в руке).
[RequireComponent(typeof(Grabbable))]
public class BasketContents : MonoBehaviour
{
    public Zone zone;   // объём внутри корзины

    public Grabbable Grab { get { return GetComponent<Grabbable>(); } }

    public Dictionary<string, int> GetCounts(List<Product> found = null)
    {
        var counts = new Dictionary<string, int>();
        if (zone == null) return counts;
        foreach (var p in zone.Find<Product>())
        {
            var g = p.GetComponent<Grabbable>();
            if (g != null && g.Holder != null) continue; // в руке — не считаем
            if (counts.ContainsKey(p.productName)) counts[p.productName]++;
            else counts[p.productName] = 1;
            if (found != null) found.Add(p);
        }
        return counts;
    }
}
