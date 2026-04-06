using UnityEditor;
using UnityEngine;

public class ToonLitMaskGUI : ShaderGUI
{
    enum Section { Surface, Mask, Emissive, Toon, Rim, Outline, Advanced }

    static readonly string[] _sectionLabels =
        { "Surface", "RGB Mask Tint", "Emissive", "Toon Shading", "Rim Light", "Outline", "Advanced" };

    static readonly string _foldoutKeyPrefix = "ToonLitMask_Foldout_";

    static bool GetFoldout(Section s) => SessionState.GetBool(_foldoutKeyPrefix + s, s <= Section.Mask);
    static void SetFoldout(Section s, bool v) => SessionState.SetBool(_foldoutKeyPrefix + s, v);

    public override void OnGUI(MaterialEditor editor, MaterialProperty[] props)
    {
        bool isOutlineVariant = ((Material)editor.target).shader.name.Contains("Outline");

        DrawSection(Section.Surface, () =>
        {
            editor.TexturePropertySingleLine(
                new GUIContent("Base Map"),
                FindProperty("_BaseMap", props),
                FindProperty("_BaseColor", props));
        });

        DrawSection(Section.Mask, () =>
        {
            editor.TexturePropertySingleLine(new GUIContent("Mask (RGB)"), FindProperty("_MaskMap", props));
            EditorGUILayout.Space(2);
            DrawColorSwatch(editor, "R Channel", FindProperty("_ColorR", props), new Color(1f, 0.3f, 0.3f, 0.15f));
            DrawColorSwatch(editor, "G Channel", FindProperty("_ColorG", props), new Color(0.3f, 1f, 0.3f, 0.15f));
            DrawColorSwatch(editor, "B Channel", FindProperty("_ColorB", props), new Color(0.3f, 0.5f, 1f, 0.15f));
        });

        DrawSection(Section.Emissive, () =>
        {
            MaterialProperty useProp = FindProperty("_UseEmissive", props);
            if (!DrawToggle(useProp, "Enable")) return;
            editor.TexturePropertySingleLine(new GUIContent("Emissive Mask"), FindProperty("_EmissiveMask", props));
            editor.ShaderProperty(FindProperty("_EmissiveColor", props), "Color");
            editor.ShaderProperty(FindProperty("_EmissiveStrength", props), "Strength");
        });

        DrawSection(Section.Toon, () =>
        {
            editor.ShaderProperty(FindProperty("_ShadowColor", props), "Shadow Color");
            editor.ShaderProperty(FindProperty("_Threshold", props), "Threshold");
            editor.ShaderProperty(FindProperty("_Smoothness", props), "Smoothness");
        });

        DrawSection(Section.Rim, () =>
        {
            MaterialProperty useProp = FindProperty("_UseRim", props);
            if (!DrawToggle(useProp, "Enable")) return;
            editor.ShaderProperty(FindProperty("_RimColor", props), "Color");
            editor.ShaderProperty(FindProperty("_RimPower", props), "Power");
            editor.ShaderProperty(FindProperty("_RimThreshold", props), "Threshold");
            editor.ShaderProperty(FindProperty("_RimSmoothness", props), "Smoothness");
            editor.ShaderProperty(FindProperty("_RimStrength", props), "Strength");
        });

        if (isOutlineVariant)
        {
            DrawSection(Section.Outline, () =>
            {
                MaterialProperty widthProp = FindProperty("_OutlineWidth", props);
                editor.ShaderProperty(widthProp, "Width");

                if (widthProp.floatValue <= 0f) return;

                editor.ShaderProperty(FindProperty("_OutlineColor", props), "Color");
                MaterialProperty coloredProp = FindProperty("_OutlineColored", props);
                if (DrawToggle(coloredProp, "Tint from Mask"))
                    editor.ShaderProperty(FindProperty("_OutlineTintBlend", props), "Tint Blend");

                EditorGUILayout.Space(2);
                DrawOutlinePreview(
                    FindProperty("_OutlineColor", props),
                    FindProperty("_BaseColor", props),
                    coloredProp,
                    FindProperty("_OutlineTintBlend", props),
                    widthProp);
            });
        }

        DrawSection(Section.Advanced, () =>
        {
            editor.ShaderProperty(FindProperty("_Cull", props), "Cull Mode");
            EditorGUILayout.Space(2);
            editor.RenderQueueField();
            editor.EnableInstancingField();
        });

        EditorGUILayout.Space(4);

        foreach (Object target in editor.targets)
            SyncKeywords((Material)target, isOutlineVariant);
    }

    static void DrawSection(Section section, System.Action content)
    {
        EditorGUILayout.Space(2);
        bool foldout = GetFoldout(section);
        bool next = DrawSectionHeader(_sectionLabels[(int)section], foldout);
        if (next != foldout) SetFoldout(section, next);
        if (!next) return;

        EditorGUI.indentLevel++;
        EditorGUILayout.Space(2);
        content?.Invoke();
        EditorGUILayout.Space(2);
        EditorGUI.indentLevel--;
    }

    static bool DrawSectionHeader(string title, bool expanded)
    {
        Color bg = EditorGUIUtility.isProSkin
            ? new Color(0.22f, 0.22f, 0.22f, 1f)
            : new Color(0.76f, 0.76f, 0.76f, 1f);

        Rect rect = GUILayoutUtility.GetRect(1f, 22f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, bg);

        Event e = Event.current;
        if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
        {
            expanded = !expanded;
            e.Use();
        }

        EditorGUI.Foldout(new Rect(rect.x + 2f, rect.y + 2f, 14f, 14f), expanded, GUIContent.none);
        EditorGUI.LabelField(new Rect(rect.x + 16f, rect.y, rect.width - 16f, rect.height), title, EditorStyles.boldLabel);
        return expanded;
    }

    static bool DrawToggle(MaterialProperty prop, string label)
    {
        EditorGUI.BeginChangeCheck();
        bool val = prop.floatValue > 0.5f;
        val = EditorGUILayout.Toggle(label, val);
        if (EditorGUI.EndChangeCheck())
            prop.floatValue = val ? 1f : 0f;
        return val;
    }

    static void DrawColorSwatch(MaterialEditor editor, string label, MaterialProperty prop, Color bgTint)
    {
        Rect rect = EditorGUILayout.GetControlRect(true, 20f);
        EditorGUI.DrawRect(rect, bgTint);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y + 1f, 8f, 18f), prop.colorValue);
        editor.ShaderProperty(new Rect(rect.x + 14f, rect.y, rect.width - 14f, rect.height), prop, label);
    }

    static void DrawOutlinePreview(
        MaterialProperty outlineColor, MaterialProperty baseColor,
        MaterialProperty colored, MaterialProperty tintBlend,
        MaterialProperty width)
    {
        Rect rect = GUILayoutUtility.GetRect(1f, 32f, GUILayout.ExpandWidth(true));
        Rect inner = new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, rect.height - 8f);

        Color oc = outlineColor.colorValue;
        if (colored.floatValue > 0.5f)
            oc = Color.Lerp(oc, baseColor.colorValue * oc, tintBlend.floatValue);

        EditorGUI.DrawRect(rect, oc);
        EditorGUI.DrawRect(inner, baseColor.colorValue);

        float lum = oc.r * 0.299f + oc.g * 0.587f + oc.b * 0.114f;
        GUIStyle style = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = lum > 0.5f ? Color.black : Color.white }
        };
        EditorGUI.LabelField(inner, string.Format("Width: {0:F2}", width.floatValue), style);
    }

    static void SyncKeywords(Material mat, bool isOutline)
    {
        SetKW(mat, "_EMISSIVE", mat.GetFloat("_UseEmissive") > 0.5f);
        SetKW(mat, "_RIM", mat.GetFloat("_UseRim") > 0.5f);
        if (isOutline)
            SetKW(mat, "_OUTLINE_COLORED", mat.GetFloat("_OutlineColored") > 0.5f);
    }

    static void SetKW(Material mat, string kw, bool on)
    {
        if (on) mat.EnableKeyword(kw);
        else mat.DisableKeyword(kw);
    }
}