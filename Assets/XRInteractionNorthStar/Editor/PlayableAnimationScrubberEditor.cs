#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayableAnimationScrubber))]
public class PlayableAnimationScrubberEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var scrubber = (PlayableAnimationScrubber)target;
        serializedObject.Update();

        // クリップ
        EditorGUI.BeginChangeCheck();
        var clipProp = serializedObject.FindProperty("clip");
        EditorGUILayout.PropertyField(clipProp);
        bool clipChanged = EditorGUI.EndChangeCheck();

        AnimationClip clip = clipProp.objectReferenceValue as AnimationClip;
        float length = clip != null ? Mathf.Max(clip.length, 0.0001f) : 1f;

        // time（秒）スライダー（指定した秒のポーズを表示）
        EditorGUI.BeginChangeCheck();
        float newTime = EditorGUILayout.Slider("Time (s)", scrubber.TimeSeconds, 0f, length);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(scrubber, "Set Time (s)");
            scrubber.SetTime(newTime);
            EditorUtility.SetDirty(scrubber);
        }

        // 正規化スライダー（0-1）
        if (clip != null)
        {
            float norm = clip.length > 0f ? Mathf.Clamp01(scrubber.TimeSeconds / clip.length) : 0f;
            EditorGUI.BeginChangeCheck();
            float newNorm = EditorGUILayout.Slider("Normalized Time", norm, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(scrubber, "Set Time (Normalized)");
                scrubber.SetTimeNormalized(newNorm);
                EditorUtility.SetDirty(scrubber);
            }
        }

        // ループ設定のみ（自動再生はないため、速度・Play On Set は削除）
        EditorGUILayout.Space();
        var loopProp = serializedObject.FindProperty("loop");
        EditorGUILayout.PropertyField(loopProp, new GUIContent("Loop"));

        if (clipChanged)
        {
            // クリップ変更時は 0 秒へ初期化しポーズを更新
            if (clip != null)
            {
                scrubber.SetTime(0f);
                EditorUtility.SetDirty(scrubber);
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
