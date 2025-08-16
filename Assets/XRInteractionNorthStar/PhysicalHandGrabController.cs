// File: Assets/NorthStar/Interaction/PhysicalHandGrabController.cs
// 概要:
// - 手階層内の HingeJoint を自動検出
// - AddTorque/ AddForce のみで握り(Grab)/開き(Open)を制御
// - XR Interaction Toolkit の interactor と連動（任意）
// - XRIT 未使用でもデバッグ操舵が可能
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace XRInteractionNorthStar
{
    [DefaultExecutionOrder(50)]
    public class PhysicalHandGrabController : MonoBehaviour
    {
        [Header("Settings Asset")]
        [SerializeField, Tooltip("初期設定として参照します（値はコピーされ、その後は上書きしません）")]
        private PhysicalHandGrabSettings settings;
        [SerializeField, HideInInspector]
        private bool initializedFromSettings = false;

        [Header("Auto-discovery")]
        [SerializeField, Tooltip("子孫階層から HingeJoint を自動検出して指ごとに並べます")]
        private bool autoDiscoverJoints = true;

        [SerializeField, Tooltip("指の識別に使う接頭辞（複数候補）")]
        private string[] fingerPrefixes = new[] { "Thumb", "Index", "Middle", "Ring", "Pinky" };

        [Header("Grab targets (deg)")]
        [SerializeField, Tooltip("開いたときの各関節の角度（deg, ヒンジangle基準）")]
        private float openAngleDeg = 0f;
        [SerializeField, Tooltip("握ったときの各関節の角度（deg, ヒンジangle基準）")]
        private float closedAngleDeg = 75f;

        [Header("PD controller")]
        [SerializeField, Tooltip("比例ゲイン（角度誤差に対するトルク）")]
        private float kp = 0.5f;
        [SerializeField, Tooltip("微分ゲイン（角速度に対する制動）")]
        private float kd = 0.01f;
        [SerializeField, Tooltip("各関節に加える最大トルク（N·m）")]
        private float maxTorque = 4.0f;
        [SerializeField, Tooltip("トルクを質量非依存で加える（Acceleration推奨）")]
        private ForceMode torqueMode = ForceMode.Acceleration;

        [Header("Stabilization (AddForce)")]
        [SerializeField, Tooltip("関節Rigidbodyの線形速度に対して簡易制動を加える（AddForce）")]
        private bool addLinearDampingForce = true;
        [SerializeField, Tooltip("線形速度制動の強さ")]
        private float linearDamping = 0.2f;
        [SerializeField, Tooltip("AddForceに使うモード（Acceleration推奨）")]
        private ForceMode forceMode = ForceMode.Acceleration;

        [Header("Smoothing")]
        [SerializeField, Tooltip("握り目標値(0-1)への追従速度（大きいほど速い）")]
        private float grabLerpSpeed = 120f;
        [SerializeField, Tooltip("入力に対して即座に反映する（スムージング無効）")]
        private bool instantResponse = true;
        [SerializeField, Tooltip("即応答時に PD/最大トルクへ掛ける倍率（到達をさらに加速）")]
        private float responseBoost = 4.0f;

        [Header("Anti-jitter")]
        [SerializeField, Tooltip("Anti-jitterを有効化（ソフトデッドゾーン/角速度LPF/スルーレート/静止保持）")]
        private bool antiJitterEnabled = true;

        [SerializeField, Tooltip("この角度誤差以下ではP項を弱めます（deg, Enter）")]
        private float angleDeadZoneDeg = 0.5f; // Enter しきい値
        [SerializeField, Tooltip("この角度誤差以上でデッドゾーン減衰を解除します（deg, Exit）")]
        private float angleDeadZoneExitDeg = 1.0f; // Exit しきい値（Enter より大きく）
        [SerializeField, Tooltip("この角速度以下ではD項を強めます（deg/s, Enter）")]
        private float velDeadZoneDegPerSec = 2.0f; // Enter しきい値
        [SerializeField, Tooltip("この角速度以上でD項強化を解除します（deg/s, Exit）")]
        private float velDeadZoneExitDegPerSec = 4.0f; // Exit しきい値（Enter より大きく）
        [SerializeField, Tooltip("デッドゾーン内でのD項倍率（保持時の減衰の強さ）")]
        private float holdDampingMultiplier = 1.0f;
        [SerializeField, Tooltip("トルク指令の変化速度を制限します（N·m/秒）")]
        private float torqueSlewRatePerSec = 2000f;
        [SerializeField, Tooltip("角速度のローパスフィルタ カットオフ周波数（Hz）")]
        private float errorFilterCutoffHz = 12f;

        [SerializeField, Tooltip("静止とみなす角度誤差（deg）")]
        private float settleAngleThresholdDeg = 0.3f;
        [SerializeField, Tooltip("静止とみなす角速度（deg/s）")]
        private float settleVelThresholdDegPerSec = 1.0f;
        [SerializeField, Tooltip("静止判定に必要な連続時間（秒）")]
        private float settleTimeSec = 0.25f;
        [SerializeField, Tooltip("目標角がこの値以上変化したら静止保持を解除（deg）")]
        private float settleResetOnTargetChangeDeg = 0.2f;

        [Header("XR Interaction Toolkit (optional)")]
        [SerializeField, Tooltip("XRITのInteractorと連動してGrab量を駆動（selectEntered/Exited）")]
        private bool useXRInteractor = false;
        // XR Toolkit 未導入環境でも参照を保持できるように、型は Object とします（任意で割り当て可）
        [SerializeField] private IXRInteractor xrInteractor;

        [Header("Debug Controls")]
        [SerializeField, Tooltip("XRIT を使わないデバッグ入力を有効化")]
        private bool debugInput = true;
        [SerializeField, Tooltip("スライダーで直接握り量(0..1)を指定")]
        [Range(0, 1)] private float debugGrabSlider = 0f;
        [SerializeField, Tooltip("キー操作: Close=G, Open=H")]
        private bool enableKeyboard = true;
        [SerializeField, Tooltip("キー1回で目標値へ補間するステップ量")]
        private float keyboardStep = 1.0f;

        [Header("Gizmos")]
        [SerializeField, Tooltip("指ボーン（関節間）をGizmosで描画")]
        private bool showFingerBonesGizmos = true;

        private readonly List<FingerChain> _fingers = new();
        private float _grabTarget = 0f; // 0=open, 1=closed
        private float _grab = 0f;

        // 目標角の変化検出用
        private float _prevBaseTargetDeg;
        private bool _hasPrevTargetDeg = false;

        [Serializable]
        private class FingerChain
        {
            public string name;
            public List<HingeJoint> joints = new(3);
            public List<Rigidbody> bodies = new(3);

            // Anti-jitter runtime states per joint
            public List<bool> holdMode = new(3);         // 近傍用の内部状態（未使用でも保持）
            public List<float> prevTorque = new(3);      // 前回適用トルク
            public List<float> filteredAngVel = new(3);  // ローパス済み角速度（deg/s）

            // Settle（静止保持）状態
            public List<float> settleTimer = new(3);     // しきい値内に居続けた時間
            public List<bool> settled = new(3);          // 静止保持中か
        }

        // コンポーネント追加時に、設定アセットが割り当てられていれば初期値としてコピー
        private void Reset()
        {
            ApplySettingsFromSOOnce();
        }

        private void Awake()
        {
            ApplySettingsFromSOOnce();
            BuildJointMapIfNeeded();
        }

        // Note: 初期設定として 1 度だけ ScriptableObject の値をコピーする
        private void ApplySettingsFromSOOnce()
        {
            if (settings == null || initializedFromSettings) return;

            // 値をアセットからコピー（以後はインスペクタ上で自由に上書き可能）
            autoDiscoverJoints = settings.autoDiscoverJoints;
            fingerPrefixes = settings.fingerPrefixes != null && settings.fingerPrefixes.Length > 0
                ? (string[])settings.fingerPrefixes.Clone()
                : fingerPrefixes;

            openAngleDeg = settings.openAngleDeg;
            closedAngleDeg = settings.closedAngleDeg;

            kp = settings.kp;
            kd = settings.kd;
            maxTorque = settings.maxTorque;
            torqueMode = settings.torqueMode;

            addLinearDampingForce = settings.addLinearDampingForce;
            linearDamping = settings.linearDamping;
            forceMode = settings.forceMode;

            grabLerpSpeed = settings.grabLerpSpeed;
            instantResponse = settings.instantResponse;
            responseBoost = settings.responseBoost;

            useXRInteractor = settings.useXRInteractor;

            debugInput = settings.debugInput;
            debugGrabSlider = Mathf.Clamp01(settings.debugGrabSlider);
            enableKeyboard = settings.enableKeyboard;
            keyboardStep = settings.keyboardStep;

            initializedFromSettings = true;
        }

        private void OnEnable()
        {
            // XR Interaction Toolkit 連動は Scripting Define なしのため無効化
        }

        private void OnDisable()
        {
            // XR Interaction Toolkit 連動は Scripting Define なしのため無効化
        }

        private void BuildJointMapIfNeeded()
        {
            _fingers.Clear();

            if (!autoDiscoverJoints)
                return;

            // 1) 全 HingeJoint を収集
            var allJoints = GetComponentsInChildren<HingeJoint>(true)
                .Where(j => j != null && j.GetComponent<Rigidbody>() != null)
                .ToList();

            // 2) 指ごとにグルーピング（名前の接頭辞＋連番想定）
            foreach (var prefix in fingerPrefixes)
            {
                var group = new FingerChain { name = prefix };
                // 例: Index1, Index2, Index3, IndexTip の順でソート
                var matches = allJoints
                    .Where(j => j.name.IndexOf(prefix, StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderBy(j => NameOrderKey(j.name))
                    .ToList();

                foreach (var j in matches)
                {
                    group.joints.Add(j);
                    group.bodies.Add(j.GetComponent<Rigidbody>());
                    // Anti-jitter states init
                    group.holdMode.Add(false);
                    group.prevTorque.Add(0f);
                    group.filteredAngVel.Add(0f);
                    // Settle states init
                    group.settleTimer.Add(0f);
                    group.settled.Add(false);
                }

                if (group.joints.Count > 0)
                    _fingers.Add(group);
            }

            // 3) 万一見つからなければ落ち穂拾い（親子チェーンで近い順）
            if (_fingers.Count == 0 && allJoints.Count > 0)
            {
                var fallback = new FingerChain { name = "All" };
                foreach (var j in allJoints.OrderBy(j => j.transform.GetSiblingIndex()))
                {
                    fallback.joints.Add(j);
                    fallback.bodies.Add(j.GetComponent<Rigidbody>());
                    // Anti-jitter states init
                    fallback.holdMode.Add(false);
                    fallback.prevTorque.Add(0f);
                    fallback.filteredAngVel.Add(0f);
                    // Settle states init
                    fallback.settleTimer.Add(0f);
                    fallback.settled.Add(false);
                }
                _fingers.Add(fallback);
            }

            int NameOrderKey(string n)
            {
                // 数字を抽出して並べ替えのキーにする: "Index12" -> 12
                int acc = 0;
                foreach (char c in n)
                {
                    if (char.IsDigit(c)) { acc = acc * 10 + (c - '0'); }
                }
                return acc;
            }
        }

        // XR Interaction Toolkit 連動は定義シンボル撤去により無効化しています
        // 必要であれば、リフレクション等での遅延バインド実装に差し替えてください。

        private void Update()
        {
            // 現在値を保持
            float nextTarget = _grabTarget;

            // XR 連動が有効なときは XR 側のイベントが _grabTarget を更新する想定。
            // XR を使う場合はデバッグ入力で上書きしない。
            if (!useXRInteractor && debugInput)
            {
                bool changedByKeyboard = false;

                if (enableKeyboard)
                {
                    if (Input.GetKeyDown(KeyCode.G))
                    {
                        nextTarget = Mathf.Clamp01(_grabTarget + keyboardStep);
                        changedByKeyboard = true;
                    }
                    if (Input.GetKeyDown(KeyCode.H))
                    {
                        nextTarget = Mathf.Clamp01(_grabTarget - keyboardStep);
                        changedByKeyboard = true;
                    }
                }

                // キー操作が無かったときだけスライダー反映
                if (!changedByKeyboard)
                {
                    nextTarget = Mathf.Clamp01(debugGrabSlider);
                }
            }

            _grabTarget = nextTarget;

            // スムージング/即応答
            _grab = instantResponse
                ? _grabTarget
                : Mathf.MoveTowards(_grab, _grabTarget, Mathf.Max(0f, grabLerpSpeed) * Time.deltaTime);
        }

        // 追加: 有限値チェック用ユーティリティ
        private static bool IsFinite(float v) => !(float.IsNaN(v) || float.IsInfinity(v));
        private static bool IsFinite(Vector3 v) => IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);

        private void FixedUpdate()
        {
            if (_fingers.Count == 0)
                BuildJointMapIfNeeded();

            // 目標角を計算（この時点で有限化）
            float baseTargetDeg = Mathf.Lerp(openAngleDeg, closedAngleDeg, _grab);
            if (!IsFinite(baseTargetDeg))
                baseTargetDeg = 0f;

            // 角速度ローパス用係数（1次IIR）
            float dt = Time.fixedDeltaTime;
            float cutoff = Mathf.Max(0f, errorFilterCutoffHz);
            float alphaVel = cutoff > 0f ? 1f - Mathf.Exp(-2f * Mathf.PI * cutoff * dt) : 1f;
            alphaVel = Mathf.Clamp01(alphaVel);

            // 目標角が動いたら全関節の静止状態を解除
            if (!_hasPrevTargetDeg)
            {
                _prevBaseTargetDeg = baseTargetDeg;
                _hasPrevTargetDeg = true;
            }
            else
            {
                if (Mathf.Abs(baseTargetDeg - _prevBaseTargetDeg) > settleResetOnTargetChangeDeg)
                {
                    foreach (var f in _fingers)
                    {
                        for (int i = 0; i < f.joints.Count; i++)
                        {
                            if (i < f.settleTimer.Count) f.settleTimer[i] = 0f;
                            if (i < f.settled.Count) f.settled[i] = false;
                            if (i < f.prevTorque.Count) f.prevTorque[i] = 0f;
                        }
                    }
                }
                _prevBaseTargetDeg = baseTargetDeg;
            }

            foreach (var f in _fingers)
            {
                for (int i = 0; i < f.joints.Count; i++)
                {
                    var joint = f.joints[i];
                    var rb = f.bodies[i];
                    if (!joint || !rb) continue;

                    // 軸ベクトル（ワールド）を取得
                    Vector3 axisWorld = joint.transform.TransformDirection(joint.axis);
                    if (!IsFinite(axisWorld) || axisWorld.sqrMagnitude < 1e-8f)
                        continue; // 軸が不正ならスキップ

                    // この関節用の目標角
                    float targetDeg = baseTargetDeg;
                    if (joint.useLimits)
                    {
                        var lim = joint.limits;
                        if (!IsFinite(lim.min)) lim.min = -180f;
                        if (!IsFinite(lim.max)) lim.max = 180f;
                        targetDeg = Mathf.Clamp(targetDeg, lim.min, lim.max);
                    }

                    // 現在角
                    float currentDeg = joint.angle;
                    if (!IsFinite(currentDeg))
                        continue; // 角度が不正ならスキップ

                    // 誤差（deg）
                    float errorDeg = Mathf.DeltaAngle(currentDeg, targetDeg);
                    if (!IsFinite(errorDeg))
                        continue;

                    // 角速度（deg/s）
                    float angVelRad = Vector3.Dot(rb.angularVelocity, axisWorld);
                    if (!IsFinite(angVelRad)) angVelRad = 0f;
                    float angVelDeg = angVelRad * Mathf.Rad2Deg;

                    // Anti-jitter: 角速度LPF
                    float angVelUsed = angVelDeg;
                    if (antiJitterEnabled)
                    {
                        float prevAng = (i < f.filteredAngVel.Count) ? f.filteredAngVel[i] : 0f;
                        float angVelFilt = Mathf.Lerp(prevAng, angVelDeg, alphaVel);
                        if (!IsFinite(angVelFilt)) angVelFilt = 0f;
                        if (i < f.filteredAngVel.Count) f.filteredAngVel[i] = angVelFilt;
                        angVelUsed = angVelFilt;
                    }

                    // ソフトデッドゾーン（Anti-jitter ON時のみ有効）
                    float absErr = Mathf.Abs(errorDeg);
                    float pGainScale = antiJitterEnabled
                        ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(angleDeadZoneDeg, angleDeadZoneExitDeg, absErr))
                        : 1f;
                    float kdScale = antiJitterEnabled
                        ? Mathf.Lerp(holdDampingMultiplier, 1f, pGainScale)
                        : 1f;

                    // PD ゲイン（即応答時のブースト）
                    float kpEff = (instantResponse ? kp * responseBoost : kp) * pGainScale;
                    float kdEff = kd * kdScale;
                    float maxTorqueEff = instantResponse ? maxTorque * responseBoost : maxTorque;

                    // Anti-jitter: 静止保持判定（目標到達後に完全停止）
                    if (antiJitterEnabled)
                    {
                        bool withinSettle =
                            absErr <= settleAngleThresholdDeg &&
                            Mathf.Abs(angVelUsed) <= settleVelThresholdDegPerSec;

                        if (i < f.settleTimer.Count && i < f.settled.Count)
                        {
                            f.settleTimer[i] = withinSettle ? (f.settleTimer[i] + Time.fixedDeltaTime) : 0f;
                            if (!f.settled[i] && f.settleTimer[i] >= settleTimeSec)
                            {
                                f.settled[i] = true;
                                if (i < f.prevTorque.Count) f.prevTorque[i] = 0f;
                            }

                            // 静止保持中はトルクを打ち切り、軸方向の角速度を0にして完全停止
                            if (f.settled[i])
                            {
                                // 目標から外れたら解除
                                if (!withinSettle)
                                {
                                    f.settled[i] = false;
                                    f.settleTimer[i] = 0f;
                                }
                                else
                                {
                                    // 完全停止を継続
                    #if UNITY_6000_0_OR_NEWER
                                    rb.angularVelocity -= axisWorld * angVelRad;
                    #else
                                    rb.angularVelocity -= axisWorld * angVelRad;
                    #endif
                                    continue; // この関節へのトルク投入をスキップ
                                }
                            }
                        }
                    }

                    // トルク計算
                    float targetTorque = kpEff * errorDeg - kdEff * angVelUsed;

                    // Anti-jitter: スルーレート制限
                    float finalCmd = targetTorque;
                    if (antiJitterEnabled)
                    {
                        float prevT = (i < f.prevTorque.Count) ? f.prevTorque[i] : 0f;
                        float maxDelta = Mathf.Max(0f, torqueSlewRatePerSec) * Time.fixedDeltaTime;
                        finalCmd = Mathf.Clamp(targetTorque, prevT - maxDelta, prevT + maxDelta);
                    }

                    // トルク最終クリップ
                    float torque = Mathf.Clamp(finalCmd, -maxTorqueEff, maxTorqueEff);
                    if (!IsFinite(torque)) torque = 0f;
                    if (i < f.prevTorque.Count) f.prevTorque[i] = torque;

                    Vector3 torqueVec = axisWorld * torque;
                    if (!IsFinite(torqueVec))
                        continue;

                    rb.AddTorque(torqueVec, torqueMode);

                    // 線形速度の簡易制動（AddForce）
                    if (addLinearDampingForce)
                    {
#if UNITY_6000_0_OR_NEWER
                        Vector3 v = rb.linearVelocity; // プロジェクトが linearVelocity を使っている前提
#else
                        Vector3 v = rb.velocity;
#endif
                        if (!IsFinite(v)) v = Vector3.zero;
                        Vector3 damp = -v * linearDamping;
                        if (IsFinite(damp))
                            rb.AddForce(damp, forceMode);
                    }
                }
            }
        }

        // API: 外部から握り量を設定（0=open, 1=closed）
        public void SetGrab(float normalized)
        {
            _grabTarget = Mathf.Clamp01(normalized);
        }

        // API: XR と連動させない場合に明示的に呼べます
        public void StartGrab() => _grabTarget = 1f;
        public void EndGrab() => _grabTarget = 0f;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // 指ボーン可視化（常時）
            if (showFingerBonesGizmos)
            {
                if (_fingers.Count == 0)
                {
                    // 可能なら指ジョイントマップを構築してから描画
                    BuildJointMapIfNeeded();
                }

                Gizmos.color = new Color(1f, 0.4f, 0.7f, 1f); // マゼンタ系
                foreach (var f in _fingers)
                {
                    for (int i = 0; i < f.joints.Count; i++)
                    {
                        var jA = f.joints[i];
                        if (!jA) continue;

                        Vector3 a = jA.transform.position;
                        Vector3? b = null;

                        // 次の関節へ
                        if (i + 1 < f.joints.Count && f.joints[i + 1])
                        {
                            b = f.joints[i + 1].transform.position;
                        }
                        else
                        {
                            // Tip があれば Tip まで線を引く
                            Transform tip = FindTipTransform(jA.transform);
                            if (tip != null && tip != jA.transform)
                                b = tip.position;
                        }

                        if (b.HasValue)
                        {
                            Gizmos.DrawLine(a, b.Value);
                            Gizmos.DrawSphere(a, 0.004f);
                            Gizmos.DrawSphere(b.Value, 0.004f);
                        }
                    }
                }

                // ローカル関数: Tip を推定
                Transform FindTipTransform(Transform start)
                {
                    foreach (var c in start.GetComponentsInChildren<Transform>(true))
                    {
                        if (c == start) continue;
                        if (c.name.IndexOf("Tip", StringComparison.OrdinalIgnoreCase) >= 0)
                            return c;
                    }
                    // Tip 名が無い場合は末端子を採用
                    return start.childCount > 0 ? start.GetChild(start.childCount - 1) : null;
                }
            }

            // 確認用にヒンジ軸を描画
            Gizmos.color = Color.cyan;
            foreach (var j in GetComponentsInChildren<HingeJoint>(true))
            {
                if (!j) continue;
                Vector3 p = j.transform.position;
                Vector3 axis = j.transform.TransformDirection(j.axis) * 0.05f;
                Gizmos.DrawLine(p - axis, p + axis);
            }
        }
#endif
    }
}