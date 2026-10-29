using UnityEngine;

// Текст в 3D-мире (вывески, доски, таблички). Использует шейдер Supermarket/Text3D,
// поэтому текст НЕ просвечивает через стены (стандартный шейдер TextMesh рисуется поверх всего).
public class Label : MonoBehaviour
{
    public string text = "";
    public float size = 0.2f;              // примерная высота строки в метрах
    public Color color = Color.white;
    public bool billboard = false;         // всегда поворачиваться к игроку (для табличек не нужно)
    public TextAnchor anchor = TextAnchor.MiddleCenter;
    public TextAlignment alignment = TextAlignment.Center;

    static Material sharedText;
    TextMesh tm;
    Font font;
    bool custom;

    void Awake() { Build(); Apply(); }

    static Material GetTextMaterial()
    {
        if (sharedText == null)
        {
            var src = Resources.Load<Material>("Text3D");
            if (src != null) sharedText = new Material(src);
        }
        return sharedText;
    }

    void Build()
    {
        if (tm != null) return;
        var go = new GameObject("TextMesh");
        go.transform.SetParent(transform, false);
        tm = go.AddComponent<TextMesh>();

        Font f = null;
        try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        if (f == null) { try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } }
        if (f == null) f = Font.CreateDynamicFontFromOSFont("Arial", 32);
        font = f;
        tm.font = f;
        var mr = go.GetComponent<MeshRenderer>();
        var m = GetTextMaterial();
        if (m != null) { custom = true; mr.sharedMaterial = m; }
        else mr.sharedMaterial = f.material;
    }

    public void Apply()
    {
        Build();
        tm.text = text;
        tm.fontSize = 100;
        tm.characterSize = size * 0.1f;
        tm.color = color;
        tm.anchor = anchor;
        tm.alignment = alignment;
    }

    public void SetText(string t) { text = t; Apply(); }

    void LateUpdate()
    {
        // атлас шрифта может пересоздаваться — держим текстуру в материале актуальной
        if (custom && sharedText != null && font != null && font.material != null && sharedText.mainTexture != font.material.mainTexture)
            sharedText.mainTexture = font.material.mainTexture;

        if (!billboard) return;
        var cam = Camera.main;
        if (cam == null) return;
        transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
    }

    public static Label Create(Transform parent, Vector3 localPos, string text, float size, Color color, bool billboard)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var l = go.AddComponent<Label>();
        l.text = text; l.size = size; l.color = color; l.billboard = billboard;
        l.Apply();
        return l;
    }
}
