using UnityEngine;

namespace XRInteractionNorthStar
{
    [CreateAssetMenu(menuName = "Data/Physical Hand Grab Settings", fileName = "PhysicalHandGrabSettings")]
    public class PhysicalHandGrabSettings : ScriptableObject
    {
        [Header("Auto-discovery")]
        [Tooltip("子孫階層から HingeJoint を自動検出して指ごとに並べます")]
        public bool autoDiscoverJoints = true;

        [Tooltip("指の識別に使う接頭辞（複数候補）")]
        public string[] fingerPrefixes = new[] { "Thumb", "Index", "Middle", "Ring", "Pinky" };

        [Header("Grab targets (deg)")]
        [Tooltip("開いたときの各関節の角度（deg, ヒンジangle基準）")]
        public float openAngleDeg = 0f;
        [Tooltip("握ったときの各関節の角度（deg, ヒンジangle基準）")]
        public float closedAngleDeg = 75f;

        [Header("PD controller")]
        [Tooltip("比例ゲイン（角度誤差に対するトルク）")]
        public float kp = 0.25f;
        [Tooltip("微分ゲイン（角速度に対する制動）")]
        public float kd = 0.02f;
        [Tooltip("各関節に加える最大トルク（N·m）")]
        public float maxTorque = 2.0f;
        [Tooltip("トルクを質量非依存で加える（Acceleration推奨）")]
        public ForceMode torqueMode = ForceMode.Acceleration;

        [Header("Stabilization (AddForce)")]
        [Tooltip("関節Rigidbodyの線形速度に対して簡易制動を加える（AddForce）")]
        public bool addLinearDampingForce = true;
        [Tooltip("線形速度制動の強さ")]
        public float linearDamping = 0.2f;
        [Tooltip("AddForceに使うモード（Acceleration推奨）")]
        public ForceMode forceMode = ForceMode.Acceleration;

        [Header("Smoothing")]
        [Tooltip("握り目標値(0-1)への追従速度（大きいほど速い）")]
        public float grabLerpSpeed = 30f;
        [Tooltip("入力に対して即座に反映する（スムージング無効）")]
        public bool instantResponse = true;
        [Tooltip("即応答時に PD/最大トルクへ掛ける倍率（到達をさらに加速）")]
        public float responseBoost = 2.0f;

        [Header("XR Interaction Toolkit (optional)")]
        [Tooltip("XRITのInteractorと連動してGrab量を駆動（selectEntered/Exited）")]
        public bool useXRInteractor = false;

        [Header("Debug Controls")]
        [Tooltip("XRIT を使わないデバッグ入力を有効化")]
        public bool debugInput = true;
        [Tooltip("スライダーで直接握り量(0..1)を指定")]
        [Range(0, 1)] public float debugGrabSlider = 0f;
        [Tooltip("キー操作: Close=G, Open=H")]
        public bool enableKeyboard = true;
        [Tooltip("キー1回で目標値へ補間するステップ量")]
        public float keyboardStep = 1.0f;
    }
}
