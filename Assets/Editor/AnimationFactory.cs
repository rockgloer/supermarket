using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Создаёт анимации (AnimationClip) и контроллеры (Animator Controller) проекта в папке Assets/Animations:
//   Door_Open / Door_OpenHold / Door_Close      — раздвижная входная дверь
//   Drawer_Open                                 — выдвижной ящик кассы
//   Sign_Blink                                  — мигающая вывеска «ОТКРЫТО»
//   Tree_Sway                                   — покачивание деревьев на ветру
//   NPC_Walk / NPC_Idle                         — ходьба и ожидание покупателей
public static class AnimationFactory
{
    const string Dir = "Assets/Animations";

    public static RuntimeAnimatorController Door, Npc, Drawer, Sign, Tree;

    static AnimationClip NewClip(string name)
    {
        Directory.CreateDirectory(Dir);
        string path = Dir + "/" + name + ".anim";
        AssetDatabase.DeleteAsset(path);
        var clip = new AnimationClip();
        clip.name = name;
        clip.frameRate = 30f;
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    static void Curve(AnimationClip clip, string path, Type type, string prop, params float[] timeValue)
    {
        var keys = new Keyframe[timeValue.Length / 2];
        for (int i = 0; i < keys.Length; i++) keys[i] = new Keyframe(timeValue[i * 2], timeValue[i * 2 + 1]);
        var curve = new AnimationCurve(keys);
        for (int i = 0; i < keys.Length; i++) { AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Auto); AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Auto); }
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, type, prop), curve);
    }

    static void Finish(AnimationClip clip, bool loop)
    {
        var s = AnimationUtility.GetAnimationClipSettings(clip);
        s.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, s);
        EditorUtility.SetDirty(clip);
    }

    static AnimatorController NewController(string name)
    {
        string path = Dir + "/" + name + ".controller";
        AssetDatabase.DeleteAsset(path);
        return AnimatorController.CreateAnimatorControllerAtPath(path);
    }

    static AnimatorStateTransition Exit(AnimatorState from, AnimatorState to)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = true;
        t.exitTime = 1f;
        t.duration = 0f;
        return t;
    }

    static AnimatorStateTransition Cond(AnimatorState from, AnimatorState to, AnimatorConditionMode mode, string param, float blend = 0.1f)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = blend;
        t.AddCondition(mode, 0f, param);
        return t;
    }

    public static void BuildAll(float drawerZ)
    {
        Directory.CreateDirectory(Dir);
        Door = BuildDoor();
        Drawer = BuildDrawer(drawerZ);
        Sign = BuildSign();
        Tree = BuildTree();
        Npc = BuildNpc();
        AssetDatabase.SaveAssets();
    }

    // --- дверь: створки разъезжаются на 1.15 м ---
    static RuntimeAnimatorController BuildDoor()
    {
        const string L = "Створка левая", R = "Створка правая";
        var open = NewClip("Door_Open");
        Curve(open, L, typeof(Transform), "m_LocalPosition.x", 0f, -0.6f, 0.6f, -1.75f);
        Curve(open, R, typeof(Transform), "m_LocalPosition.x", 0f, 0.6f, 0.6f, 1.75f);
        Finish(open, false);
        var hold = NewClip("Door_OpenHold");
        Curve(hold, L, typeof(Transform), "m_LocalPosition.x", 0f, -1.75f, 0.1f, -1.75f);
        Curve(hold, R, typeof(Transform), "m_LocalPosition.x", 0f, 1.75f, 0.1f, 1.75f);
        Finish(hold, true);
        var close = NewClip("Door_Close");
        Curve(close, L, typeof(Transform), "m_LocalPosition.x", 0f, -1.75f, 0.6f, -0.6f);
        Curve(close, R, typeof(Transform), "m_LocalPosition.x", 0f, 1.75f, 0.6f, 0.6f);
        Finish(close, false);

        var c = NewController("Door");
        c.AddParameter("open", AnimatorControllerParameterType.Bool);
        var sm = c.layers[0].stateMachine;
        var closed = sm.AddState("Closed");
        var opening = sm.AddState("Opening"); opening.motion = open;
        var opened = sm.AddState("Open"); opened.motion = hold;
        var closing = sm.AddState("Closing"); closing.motion = close;
        sm.defaultState = closed;
        Cond(closed, opening, AnimatorConditionMode.If, "open", 0f);
        Exit(opening, opened);
        Cond(opening, closing, AnimatorConditionMode.IfNot, "open", 0f);
        Cond(opened, closing, AnimatorConditionMode.IfNot, "open", 0f);
        Exit(closing, closed);
        Cond(closing, opening, AnimatorConditionMode.If, "open", 0f);
        return c;
    }

    // --- ящик кассы выезжает на 14 см и возвращается ---
    static RuntimeAnimatorController BuildDrawer(float z)
    {
        var clip = NewClip("Drawer_Open");
        Curve(clip, "", typeof(Transform), "m_LocalPosition.z", 0f, z, 0.25f, z + 0.14f, 1.2f, z + 0.14f, 1.6f, z);
        Finish(clip, false);
        var c = NewController("Drawer");
        c.AddParameter("open", AnimatorControllerParameterType.Trigger);
        var sm = c.layers[0].stateMachine;
        var idle = sm.AddState("Closed");
        var opened = sm.AddState("Open"); opened.motion = clip;
        sm.defaultState = idle;
        Cond(idle, opened, AnimatorConditionMode.If, "open", 0f);
        Exit(opened, idle);
        return c;
    }

    // --- вывеска «ОТКРЫТО»: мигает свет и пульсирует надпись ---
    static RuntimeAnimatorController BuildSign()
    {
        var clip = NewClip("Sign_Blink");
        Curve(clip, "Лампа", typeof(Light), "m_Intensity", 0f, 0.3f, 0.5f, 1.8f, 1.0f, 0.3f, 1.5f, 1.8f, 2.0f, 0.3f);
        Curve(clip, "Надпись", typeof(Transform), "m_LocalScale.x", 0f, 1f, 0.5f, 1.07f, 1.0f, 1f, 1.5f, 1.07f, 2.0f, 1f);
        Curve(clip, "Надпись", typeof(Transform), "m_LocalScale.y", 0f, 1f, 0.5f, 1.07f, 1.0f, 1f, 1.5f, 1.07f, 2.0f, 1f);
        Finish(clip, true);
        var c = NewController("Sign");
        var st = c.layers[0].stateMachine.AddState("Blink");
        st.motion = clip;
        c.layers[0].stateMachine.defaultState = st;
        return c;
    }

    // --- деревья качаются ---
    static RuntimeAnimatorController BuildTree()
    {
        var clip = NewClip("Tree_Sway");
        Curve(clip, "", typeof(Transform), "localEulerAnglesRaw.z", 0f, 0f, 1f, 1.4f, 2f, 0f, 3f, -1.4f, 4f, 0f);
        Finish(clip, true);
        var c = NewController("Tree");
        var st = c.layers[0].stateMachine.AddState("Sway");
        st.motion = clip;
        c.layers[0].stateMachine.defaultState = st;
        return c;
    }

    // --- покупатель: ходьба / ожидание ---
    static RuntimeAnimatorController BuildNpc()
    {
        var walk = NewClip("NPC_Walk");
        float[] legs = { 0f, 0f, 0.225f, 26f, 0.45f, 0f, 0.675f, -26f, 0.9f, 0f };
        float[] legsN = { 0f, 0f, 0.225f, -26f, 0.45f, 0f, 0.675f, 26f, 0.9f, 0f };
        float[] arms = { 0f, 0f, 0.225f, -20f, 0.45f, 0f, 0.675f, 20f, 0.9f, 0f };
        float[] armsN = { 0f, 0f, 0.225f, 20f, 0.45f, 0f, 0.675f, -20f, 0.9f, 0f };
        Curve(walk, "LegPos", typeof(Transform), "localEulerAnglesRaw.x", legs);
        Curve(walk, "LegNeg", typeof(Transform), "localEulerAnglesRaw.x", legsN);
        Curve(walk, "ArmPos", typeof(Transform), "localEulerAnglesRaw.x", arms);
        Curve(walk, "ArmNeg", typeof(Transform), "localEulerAnglesRaw.x", armsN);
        Finish(walk, true);
        var idle = NewClip("NPC_Idle");
        Curve(idle, "ArmPos", typeof(Transform), "localEulerAnglesRaw.z", 0f, 0f, 1.5f, 2f, 3f, 0f);
        Curve(idle, "ArmNeg", typeof(Transform), "localEulerAnglesRaw.z", 0f, 0f, 1.5f, -2f, 3f, 0f);
        Finish(idle, true);

        var c = NewController("NPC");
        c.AddParameter("Walking", AnimatorControllerParameterType.Bool);
        var sm = c.layers[0].stateMachine;
        var sIdle = sm.AddState("Idle"); sIdle.motion = idle;
        var sWalk = sm.AddState("Walk"); sWalk.motion = walk;
        sm.defaultState = sIdle;
        Cond(sIdle, sWalk, AnimatorConditionMode.If, "Walking", 0.15f);
        Cond(sWalk, sIdle, AnimatorConditionMode.IfNot, "Walking", 0.15f);
        return c;
    }
}
