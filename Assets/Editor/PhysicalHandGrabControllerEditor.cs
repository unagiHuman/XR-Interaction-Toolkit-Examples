#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using XRInteractionNorthStar;

[CustomEditor(typeof(PhysicalHandGrabController))]
public class PhysicalHandGrabControllerEditor : Editor
{
    private static float runtimeGrab = 0f;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Debug / How To", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "XR Interaction Toolkit を使わずにデバッグするには:\n" +
            "1) 「Debug Controls > Debug Input」を ON\n" +
            "2) 「Debug Controls > Debug Grab Slider」を 0..1 で操作、または\n" +
            "3) キー操作: G=握る, H=開く（「Debug Controls > Enable Keyboard」を ON）\n" +
            "\n" +
            "XR Interaction Toolkit と連動するには:\n" +
            "1) 「XR Interaction Toolkit (optional) > Use XR Interactor」を ON\n" +
            "2) Interactor を割り当てる（Select Enter/Exit に応じて握る/開く）\n" +
            "\n" +
            "補足: 手階層内の HingeJoint を自動検出し、各関節を AddTorque/AddForce のみで制御します。\n" +
            "Gizmos 表示で各ヒンジの軸を確認できます。", MessageType.Info);

        var ctrl = (PhysicalHandGrabController)target;
        if (Application.isPlaying)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime Quick Controls", EditorStyles.boldLabel);
            runtimeGrab = EditorGUILayout.Slider("Set Grab (0..1)", runtimeGrab, 0f, 1f);
            if (GUILayout.Button("Apply Grab Value"))
            {
                ctrl.SetGrab(runtimeGrab);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Start Grab")) ctrl.StartGrab();
                if (GUILayout.Button("End Grab")) ctrl.EndGrab();
            }
        }
        else
        {
            EditorGUILayout.HelpBox("再生中に「Runtime Quick Controls」が有効になります。", MessageType.None);
        }
    }
}
#endif
