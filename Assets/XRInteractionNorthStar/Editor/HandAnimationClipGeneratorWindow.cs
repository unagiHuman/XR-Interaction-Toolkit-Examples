#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class HandAnimationClipGeneratorWindow : EditorWindow
{
    private const string DefaultHandRootName = "__HC_GEN_GeneratedHand_R";

    [SerializeField] private Transform handRoot;
    [SerializeField] private float duration = 1.0f;

    // AIルール: 指・関節の分類
    private enum FingerType { Thumb, Index, Middle, Ring, Little, Unknown }
    private enum JointType { Metacarpal, Proximal, Intermediate, Distal, ThumbCMC, ThumbMCP, ThumbIP, Unknown }

    [MenuItem("Tools/Hand Animation/Hand Close Clip Generator (R)")]
    public static void OpenWindow()
    {
        var wnd = GetWindow<HandAnimationClipGeneratorWindow>("Hand Clip Generator");
        wnd.minSize = new Vector2(440, 180);
        wnd.TryAutoAssignHandRoot();
    }

    private void OnEnable()
    {
        // シーン再読み込み時などに名前から自動割り当て
        TryAutoAssignHandRoot();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Hand Close Animation Clip Generator (AI Rules)", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        using (new EditorGUILayout.HorizontalScope())
        {
            handRoot = (Transform)EditorGUILayout.ObjectField("Hand Root", handRoot, typeof(Transform), true);
            if (GUILayout.Button("Find '" + DefaultHandRootName + "'", GUILayout.Width(180)))
            {
                TryAutoAssignHandRoot();
            }
        }

        duration = EditorGUILayout.Slider("Clip Duration (sec)", Mathf.Max(0.05f, duration), 0.05f, 5f);

        EditorGUILayout.Space();

        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = handRoot != null;
            if (GUILayout.Button("Generate AnimationClip (AI Rules)...", GUILayout.Height(28)))
            {
                GenerateAndSaveClipAIRules();
            }
            GUI.enabled = true;
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "AIルールにより、関節名から指と関節を自動判別して、開く→握る の回転アニメーションを生成します。\n" +
            "- 人手で角度を指定する必要はありません\n" +
            "- 『開いた手』は全ての関節のローカル回転=0（単位クォータニオン）として定義します\n" +
            "- すべての指でローカルX軸回りのみ回転させ、Position は変化させません\n" +
            "- CapsuleCollider を持つオブジェクトは回転させません（角度は維持されます）",
            MessageType.Info);
    }

    private void TryAutoAssignHandRoot()
    {
        if (handRoot == null)
        {
            var go = GameObject.Find(DefaultHandRootName);
            if (go != null)
            {
                handRoot = go.transform;
                Repaint();
            }
        }
    }

    private IEnumerable<Transform> EnumerateHierarchy(Transform root)
    {
        var stack = new Stack<Transform>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current;
            for (int i = 0; i < current.childCount; i++)
                stack.Push(current.GetChild(i));
        }
    }

    private static string GetRelativePath(Transform root, Transform target)
    {
        if (target == root) return string.Empty;
        var path = target.name;
        var t = target.parent;
        while (t != null && t != root)
        {
            path = t.name + "/" + path;
            t = t.parent;
        }
        return path;
    }

    private static bool TryClassify(Transform t, out FingerType finger, out JointType joint)
    {
        string n = t.name.ToLowerInvariant();

        // 指の分類
        if (n.Contains("thumb")) finger = FingerType.Thumb;
        else if (n.Contains("index")) finger = FingerType.Index;
        else if (n.Contains("middle")) finger = FingerType.Middle;
        else if (n.Contains("ring")) finger = FingerType.Ring;
        else if (n.Contains("little") || n.Contains("pinky") || n.Contains("pinkie")) finger = FingerType.Little;
        else finger = FingerType.Unknown;

        // 末端の"end"や"tip"は除外
        if (n.Contains("end") || n.Contains("tip"))
        {
            joint = JointType.Unknown;
            return finger != FingerType.Unknown; // 指は分かっても末端は対象外
        }

        // 関節の分類（名前や番号で推定）
        joint = JointType.Unknown;
        if (finger == FingerType.Thumb)
        {
            if (n.Contains("cmc") || n.Contains("carpal") || n.Contains("thumb0")) joint = JointType.ThumbCMC;
            else if (n.Contains("mcp") || n.Contains("thumb1")) joint = JointType.ThumbMCP;
            else if (n.Contains("ip") || n.Contains("thumb2") || n.Contains("thumb3")) joint = JointType.ThumbIP;
        }
        else
        {
            if (n.Contains("metacarpal") || n.EndsWith("0")) joint = JointType.Metacarpal;
            else if (n.Contains("proximal") || n.Contains("proxi") || n.EndsWith("1")) joint = JointType.Proximal;
            else if (n.Contains("intermediate") || n.Contains("middle") || n.EndsWith("2")) joint = JointType.Intermediate;
            else if (n.Contains("distal") || n.Contains("dist") || n.EndsWith("3")) joint = JointType.Distal;
        }

        return finger != FingerType.Unknown && joint != JointType.Unknown;
    }

    // AIルール: 握るときの屈曲角（度）
    private static float GetTargetFlexDegrees(FingerType finger, JointType joint)
    {
        switch (finger)
        {
            case FingerType.Thumb:
                switch (joint)
                {
                    case JointType.ThumbCMC: return 20f;  // 付け根の反り
                    case JointType.ThumbMCP: return 45f;  // 親指MCP
                    case JointType.ThumbIP:  return 55f;  // 親指IP
                }
                break;

            default: // Index/Middle/Ring/Little
                switch (joint)
                {
                    case JointType.Metacarpal:   return 10f;  // 掌側への軽い屈曲
                    case JointType.Proximal:     return 60f;  // MCP
                    case JointType.Intermediate: return 75f;  // PIP
                    case JointType.Distal:       return 65f;  // DIP
                }
                break;
        }
        return 0f;
    }

    // AIルール: カール軸（ローカル軸）
    private static Vector3 GetCurlAxisLocal(FingerType finger)
    {
        // 全ての指でローカルX回りに回転させる
        return Vector3.right;
    }

    private void GenerateAndSaveClipAIRules()
    {
        if (handRoot == null)
        {
            EditorUtility.DisplayDialog("Error", "Hand Root が設定されていません。", "OK");
            return;
        }

        string defaultName = "HandClose_R_AI.anim";
        string path = EditorUtility.SaveFilePanelInProject("Save Hand Close Animation (AI)", defaultName, "anim", "保存先を選択してください。");
        if (string.IsNullOrEmpty(path)) return;

        var clip = new AnimationClip();
        clip.legacy = false;
        clip.frameRate = 60f;

        int curveCount = 0;

        foreach (var t in EnumerateHierarchy(handRoot))
        {
            if (t == handRoot) continue; // ルートは回転しない

            // CapsuleCollider が付いたオブジェクトは回転を変更しない
            if (t.GetComponent<CapsuleCollider>() != null)
                continue;

            if (!TryClassify(t, out var finger, out var joint))
                continue;

            float flexDeg = GetTargetFlexDegrees(finger, joint);
            if (Mathf.Approximately(flexDeg, 0f))
                continue;

            // 開いた手（Open）は全関節ローカル回転=0（X=0度）
            float openX = 0f;
            float closedX = openX + flexDeg; // ローカルXのみ回転

            string relativePath = GetRelativePath(handRoot, t);
            AddLocalEulerXCurve(clip, relativePath, openX, closedX, 0f, duration);
            curveCount++;
        }

        // ループなし設定
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        AssetDatabase.CreateAsset(clip, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<AnimationClip>(path));
        EditorUtility.DisplayDialog("Success",
            $"AIルールでアニメーションクリップを生成しました。\n" +
            $"- 保存先: {path}\n" +
            $"- 対象関節数: {curveCount}\n" +
            $"- 長さ: {duration:0.###} 秒",
            "OK");
    }

    private static void AddLocalEulerXCurve(AnimationClip clip, string path, float fromDeg, float toDeg, float t0, float t1)
    {
        // ローカルEuler角のXのみキー化（Position/他軸は一切触らない）
        var curveX = new AnimationCurve(
            new Keyframe(t0, fromDeg), new Keyframe(t1, toDeg)
        );
        // 低コストで補間が素直になるように両キーの重みを締める
        for (int i = 0; i < curveX.keys.Length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curveX, i, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(curveX, i, AnimationUtility.TangentMode.Linear);
        }

        var bindingX = EditorCurveBinding.FloatCurve(path, typeof(Transform), "localEulerAnglesRaw.x");
        AnimationUtility.SetEditorCurve(clip, bindingX, curveX);
    }
}
#endif
