using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

namespace XRInteractionNorthStar
{
    [AddComponentMenu("XR Interaction NorthStar/XR Grip Value Event")]
    public class XRGripValueEvent : MonoBehaviour
    {
        [Header("XR Interaction Toolkit")]
        [SerializeField, Tooltip("InputActionManager を割り当て。XR Origin (XR Rig) のルートなどに付与されている想定です。")]
        private InputActionManager inputActionManager;

        public enum Handedness { Left, Right }

        [SerializeField, Tooltip("どちらのコントローラからグリップ量を取得するかを指定します（XR Origin の LeftHand/RightHand を対象）")]
        private Handedness handedness = Handedness.Left;

        [SerializeField, Tooltip("Select の代わりに Activate をグリップ入力として扱う場合は有効化")]
        private bool useActivateValue = false;

        [Header("Input Actions (auto-resolved from Starter Assets)")]
        [SerializeField, Tooltip("左手の Select Value アクション（既定: \"XRI LeftHand Interaction/Select Value\"）")]
        private InputActionReference leftSelectAction;
        [SerializeField, Tooltip("右手の Select Value アクション（既定: \"XRI RightHand Interaction/Select Value\"）")]
        private InputActionReference rightSelectAction;
        [SerializeField, Tooltip("左手の Activate Value アクション（既定: \"XRI LeftHand Interaction/Activate Value\"）")]
        private InputActionReference leftActivateAction;
        [SerializeField, Tooltip("右手の Activate Value アクション（既定: \"XRI RightHand Interaction/Activate Value\"）")]
        private InputActionReference rightActivateAction;

        [Header("Invocation")]
        [SerializeField, Tooltip("毎フレーム呼び出す（false の場合は変化量がしきい値以上のときのみ呼び出し）")]
        private bool invokeEveryFrame = true;

        [SerializeField, Tooltip("変化検出のしきい値（invokeEveryFrame=false のときのみ使用）")]
        private float changeThreshold = 0.01f;

        [SerializeField, Tooltip("解決したアクションの ActionMap を実行時に自動 Enable します（他の仕組みで管理している場合は false 推奨）")]
        private bool autoEnableResolvedActions = false;

        [SerializeField, Tooltip("既存の InputAction とは別に、本コンポーネント専用の InputAction を生成して同時に利用します（競合回避用）")]
        private bool useIndependentInputAction = false;

#if UNITY_EDITOR
        [Header("Editor Debug")]
        [SerializeField, Tooltip("Unity Editor 実行時のみ 0/1 のデジタル入力をアナログ値として補間します")]
        private bool smoothInEditor = true;

        [SerializeField, Tooltip("Editor 補間速度（1 秒あたりの変化量）。大きいほど速く 0⇄1 に切り替わります")]
        [Min(0f)]
        private float editorInterpSpeed = 10f;
#endif

        [Serializable]
        public class FloatEvent : UnityEvent<float> { }

        [SerializeField, Tooltip("グリップ量を引数に渡して呼び出されます（0..1 推奨）")]
        private FloatEvent onGripValue = new FloatEvent();

        private float _lastValue = -999f;
#if UNITY_EDITOR
        // Editor 補間用の内部状態
        private float _editorSmoothedValue = 0f;
        private bool _editorInitialized = false;
#endif

        // 本コンポーネント専用の独立 InputAction（既存設定と併用するためのミラー）
        private InputAction _indLeftSelect, _indRightSelect;
        private InputAction _indLeftActivate, _indRightActivate;

        private void Awake()
        {
            AutoResolveActions();
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
                AutoResolveActions();
        }

        private void Update()
        {
            float value = ReadGripValueClamped01();

            if (invokeEveryFrame)
            {
                onGripValue.Invoke(value);
                _lastValue = value;
                return;
            }

            if (Mathf.Abs(value - _lastValue) >= changeThreshold)
            {
                onGripValue.Invoke(value);
                _lastValue = value;
            }
        }

        private void OnEnable()
        {
            if (useIndependentInputAction)
                EnsureIndependentActionsCreatedAndEnabled();
        }

        private void OnDisable()
        {
            if (useIndependentInputAction)
                DisableIndependentActions();
        }

        private void OnDestroy()
        {
            DisposeIndependentActions();
        }

        private void EnsureIndependentActionsCreatedAndEnabled()
        {
            if (_indLeftSelect == null) _indLeftSelect = CreateAxisAction(true, false);
            if (_indRightSelect == null) _indRightSelect = CreateAxisAction(false, false);
            if (_indLeftActivate == null) _indLeftActivate = CreateAxisAction(true, true);
            if (_indRightActivate == null) _indRightActivate = CreateAxisAction(false, true);

            if (!_indLeftSelect.enabled) _indLeftSelect.Enable();
            if (!_indRightSelect.enabled) _indRightSelect.Enable();
            if (!_indLeftActivate.enabled) _indLeftActivate.Enable();
            if (!_indRightActivate.enabled) _indRightActivate.Enable();
        }

        private void DisableIndependentActions()
        {
            _indLeftSelect?.Disable();
            _indRightSelect?.Disable();
            _indLeftActivate?.Disable();
            _indRightActivate?.Disable();
        }

        private void DisposeIndependentActions()
        {
            DisableIndependentActions();

            _indLeftSelect?.Dispose(); _indLeftSelect = null;
            _indRightSelect?.Dispose(); _indRightSelect = null;
            _indLeftActivate?.Dispose(); _indLeftActivate = null;
            _indRightActivate?.Dispose(); _indRightActivate = null;
        }

        private static InputAction CreateAxisAction(bool isLeft, bool useTrigger)
        {
            var action = new InputAction(type: InputActionType.Value, expectedControlType: "Axis");
            string hand = isLeft ? "{LeftHand}" : "{RightHand}";
            string control = useTrigger ? "trigger" : "grip";
            action.AddBinding($"<XRController>{hand}/{control}");
            return action;
        }

        private static float ReadFromAction(InputAction action)
        {
            if (action == null) return 0f;
            float v = action.enabled ? action.ReadValue<float>() : 0f;
            return Mathf.Clamp01(float.IsNaN(v) || float.IsInfinity(v) ? 0f : v);
        }

        private void AutoResolveActions()
        {
            // 既に割り当て済みなら何もしない（不足分のみ補完）
            var iam = inputActionManager;

            if (iam == null) return;

            // Starter Assets 既定名
            const string LSelect = "XRI Left Interaction/Select Value";
            const string RSelect = "XRI Right Interaction/Select Value";
            const string LActivate = "XRI Left Interaction/Activate Value";
            const string RActivate = "XRI Right Interaction/Activate Value";

            foreach (var asset in iam.actionAssets)
            {
                if (!asset) continue;

                if (leftSelectAction == null)
                {
                    var a = asset.FindAction(LSelect, throwIfNotFound: false);
                    if (a != null) leftSelectAction = InputActionReference.Create(a);
                }
                if (rightSelectAction == null)
                {
                    var a = asset.FindAction(RSelect, throwIfNotFound: false);
                    if (a != null) rightSelectAction = InputActionReference.Create(a);
                }
                if (leftActivateAction == null)
                {
                    var a = asset.FindAction(LActivate, throwIfNotFound: false);
                    if (a != null) leftActivateAction = InputActionReference.Create(a);
                }
                if (rightActivateAction == null)
                {
                    var a = asset.FindAction(RActivate, throwIfNotFound: false);
                    if (a != null) rightActivateAction = InputActionReference.Create(a);
                }
            }

            // 必要な場合のみ、ActionMap を自動有効化（他のシステムと競合しないように任意化）
            if (Application.isPlaying && autoEnableResolvedActions)
            {
                var refs = new InputActionReference[] { leftSelectAction, rightSelectAction, leftActivateAction, rightActivateAction };
                foreach (var r in refs)
                {
                    var map = r != null ? r.action?.actionMap : null;
                    if (map != null && !map.enabled) map.Enable();
                }
            }
        }

        private static float ReadFromActionRef(InputActionReference actionRef)
        {
            if (actionRef == null || actionRef.action == null)
                return 0f;
            var ia = actionRef.action;
            // ここでは有効化状態を変更しない（他システムとの競合を避ける）
            float v = ia.enabled ? ia.ReadValue<float>() : 0f;
            return Mathf.Clamp01(float.IsNaN(v) || float.IsInfinity(v) ? 0f : v);
        }

        private float ReadGripValueClamped01()
        {
            bool isLeft = handedness == Handedness.Left;
            var selectRef = isLeft ? leftSelectAction : rightSelectAction;
            var activateRef = isLeft ? leftActivateAction : rightActivateAction;

            // 優先するアクションを選択
            var preferred = useActivateValue ? activateRef : selectRef;
            var fallback = useActivateValue ? selectRef : activateRef;

            float v = ReadFromActionRef(preferred);
            if (v <= 0f && fallback != null)
            {
                // 片方が未設定/未反応のときのフォールバック
                float alt = ReadFromActionRef(fallback);
                if (alt > v) v = alt;
            }

            // 独立アクション（本コンポーネント専用）も同時に読み取り、より強い入力を採用
            if (useIndependentInputAction)
            {
                InputAction ind = useActivateValue
                    ? (isLeft ? _indLeftActivate : _indRightActivate)
                    : (isLeft ? _indLeftSelect : _indRightSelect);

                float iv = ReadFromAction(ind);
                if (iv > v) v = iv;
            }

#if UNITY_EDITOR
            if (smoothInEditor)
            {
                if (!_editorInitialized)
                {
                    _editorSmoothedValue = v;
                    _editorInitialized = true;
                }
                _editorSmoothedValue = Mathf.MoveTowards(_editorSmoothedValue, v, editorInterpSpeed * Time.deltaTime);
                return _editorSmoothedValue;
            }
#endif
            return v;
        }

        // 公開 API（インスペクタ操作の代替）
        public void SetInputActionManager(InputActionManager manager)
        {
            inputActionManager = manager;
            AutoResolveActions();
        }
        public void UseActivate(bool useActivate) => useActivateValue = useActivate;
        public void SetInvokeEveryFrame(bool everyFrame) => invokeEveryFrame = everyFrame;
        public void SetChangeThreshold(float threshold) => changeThreshold = Mathf.Max(0f, threshold);

        // UnityEvent の購読用プロパティ
        public FloatEvent OnGripValue => onGripValue;
    }
}
