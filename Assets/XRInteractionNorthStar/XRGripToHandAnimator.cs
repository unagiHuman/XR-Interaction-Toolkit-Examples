using UnityEngine;
using UnityEngine.InputSystem;

namespace XRInteractionNorthStar
{
    /// <summary>
    /// XR Interaction Toolkit のコントローラー Grip(0..1) と
    /// 手の Animator パラメータを同期するコンポーネント。
    /// 
    /// 使い方:
    /// - Hand Root（または Animator を持つオブジェクト）にアタッチ
    /// - Animator と Grip 用パラメータ名を指定（デフォルト "Grip"）
    /// - XRI の Input Action（SelectValue/Grip など）を gripAction に割り当て
    /// - [コンテキストメニュー] Generate Hand Open/Grip Animations で基本アニメを自動生成
    /// </summary>
    [DisallowMultipleComponent]
    public class XRGripToHandAnimator : MonoBehaviour
    {
        [Header("Targets")]
        [SerializeField] private Animator handAnimator;
        [SerializeField] private string gripParameterName = "Grip";

        [Header("Input (Input System)")]
        [Tooltip("XR Interaction Toolkit のコントローラーの Grip/SelectValue を参照する Input Action")]
        [SerializeField] private InputActionReference gripAction;

        [Header("Options")]
        [Tooltip("入力値 0..1 を Animator パラメータへそのまま書き込みます。カーブ/デッドゾーン/スムージングで整形可能")]
        [SerializeField] private bool applySmoothing = true;
        [SerializeField, Range(0.0f, 1.0f)] private float smoothTime = 0.05f;
        [SerializeField, Range(0.0f, 0.5f)] private float deadZone = 0.02f;
        [SerializeField] private AnimationCurve responseCurve = AnimationCurve.Linear(0, 0, 1, 1);

        [Header("Auto Animation Creation")]
        [Tooltip("指ボーンツリーのルート（未指定時はこのコンポーネントの transform を基準に探索）")]
        [SerializeField] private Transform handRoot;
        [Tooltip("親指も含めて生成するか")]
        [SerializeField] private bool includeThumb = true;
        [Tooltip("指のカール角度（Open=0, Grip時の角度[deg]）")]
        [SerializeField, Range(0f, 120f)] private float fingerCurl1 = 35f; // 近位
        [SerializeField, Range(0f, 120f)] private float fingerCurl2 = 55f; // 中節
        [SerializeField, Range(0f, 120f)] private float fingerCurl3 = 65f; // 遠位
        [SerializeField, Range(0f, 120f)] private float thumbCurl1 = 25f;
        [SerializeField, Range(0f, 120f)] private float thumbCurl2 = 35f;
        [SerializeField, Range(0f, 120f)] private float thumbCurl3 = 45f;

        private float _current;
        private float _velocity; // for SmoothDamp

        private void Reset()
        {
            handAnimator = GetComponentInChildren<Animator>();
            if (handRoot == null) handRoot = transform;
        }

        private void Awake()
        {
            if (handAnimator == null)
                handAnimator = GetComponentInChildren<Animator>();
            if (handRoot == null)
                handRoot = transform;

            if (handAnimator == null)
                Debug.LogWarning($"{nameof(XRGripToHandAnimator)}: Animator が見つかりません。手のアニメーションを適用できません。");
        }

        private void OnEnable()
        {
            TryEnableAction();
        }

        private void OnDisable()
        {
            TryDisableAction();
        }

        private void Update()
        {
            if (handAnimator == null) return;

            float input = ReadGrip();
            // デッドゾーン
            if (input < deadZone) input = 0f;
            // レスポンスカーブ適用
            input = Mathf.Clamp01(responseCurve.Evaluate(Mathf.Clamp01(input)));

            if (applySmoothing)
            {
                _current = Mathf.SmoothDamp(_current, input, ref _velocity, Mathf.Max(0.0001f, smoothTime));
            }
            else
            {
                _current = input;
            }

            handAnimator.SetFloat(gripParameterName, _current);
        }

        private float ReadGrip()
        {
            if (gripAction != null && gripAction.action != null)
            {
                try
                {
                    return Mathf.Clamp01(gripAction.action.ReadValue<float>());
                }
                catch
                {
                    // 型が違う場合などは 0 扱い
                }
            }
            return 0f;
        }

        private void TryEnableAction()
        {
            if (gripAction != null && gripAction.action != null)
            {
                if (!gripAction.action.enabled)
                    gripAction.action.Enable();
            }
        }

        private void TryDisableAction()
        {
            if (gripAction != null && gripAction.action != null)
            {
                // 他所で共有されている可能性があるため、ここでは Disable は任意
                // 必要なら以下を有効化:
                // gripAction.action.Disable();
            }
        }

        // ---------------- Editor-only: アニメーション自動生成 ----------------
#if UNITY_EDITOR
        [ContextMenu("Generate Hand Open/Grip Animations (and Animator BlendTree)")]
        private void GenerateHandAnimationsAndController()
        {
            var root = handRoot != null ? handRoot : transform;
            if (root == null)
            {
                Debug.LogError($"{nameof(XRGripToHandAnimator)}: handRoot がありません。");
                return;
            }

            // ボーンを名前から探索
            var skeleton = FindSkeleton(root);
            if (skeleton == null)
            {
                Debug.LogError($"{nameof(XRGripToHandAnimator)}: 指ボーンが見つかりません。命名は Thumb/Index/Middle/Ring/Pinky + 1..3 を推奨します。");
                return;
            }

            // 保存先フォルダ
            string baseFolder = "Assets/XRInteractionNorthStar/Animations";
            EnsureFolder(baseFolder);

            // クリップ生成
            var openClip = CreateClip("Hand_Open", skeleton, isGrip:false);
            var gripClip = CreateClip("Hand_Grip", skeleton, isGrip:true);

            // アセット保存（重複時は番号付与）
            string openPath = UnityEditor.AssetDatabase.GenerateUniqueAssetPath($"{baseFolder}/{gameObject.name}_Open.anim");
            string gripPath = UnityEditor.AssetDatabase.GenerateUniqueAssetPath($"{baseFolder}/{gameObject.name}_Grip.anim");
            UnityEditor.AssetDatabase.CreateAsset(openClip, openPath);
            UnityEditor.AssetDatabase.CreateAsset(gripClip, gripPath);

            // AnimatorController + BlendTree を作成／割り当て
            AssignOrCreateController(openClip, gripClip, baseFolder);

            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh();
            Debug.Log($"{nameof(XRGripToHandAnimator)}: Open/Grip アニメーションと BlendTree を生成・割り当てました。");
        }

        // ボーン探索
        private class FingerJoints
        {
            public Transform j1, j2, j3;
        }

        private class HandSkeleton
        {
            public FingerJoints Thumb = new();
            public FingerJoints Index = new();
            public FingerJoints Middle = new();
            public FingerJoints Ring = new();
            public FingerJoints Pinky = new();
        }

        private HandSkeleton FindSkeleton(Transform root)
        {
            Transform FindLike(string name) => FindChildByPredicate(root, t => t.name.ToLower().Contains(name.ToLower()));

            var s = new HandSkeleton
            {
                Thumb = includeThumb ? new FingerJoints
                {
                    j1 = FindLike("Thumb1"),
                    j2 = FindLike("Thumb2"),
                    j3 = FindLike("Thumb3"),
                } : null,
                Index = new FingerJoints
                {
                    j1 = FindLike("Index1"),
                    j2 = FindLike("Index2"),
                    j3 = FindLike("Index3"),
                },
                Middle = new FingerJoints
                {
                    j1 = FindLike("Middle1"),
                    j2 = FindLike("Middle2"),
                    j3 = FindLike("Middle3"),
                },
                Ring = new FingerJoints
                {
                    j1 = FindLike("Ring1"),
                    j2 = FindLike("Ring2"),
                    j3 = FindLike("Ring3"),
                },
                Pinky = new FingerJoints
                {
                    j1 = FindLike("Pinky1"),
                    j2 = FindLike("Pinky2"),
                    j3 = FindLike("Pinky3"),
                }
            };

            // 少なくとも1本分見つかっていれば OK とする
            bool any =
                (s.Thumb?.j1 || s.Index.j1 || s.Middle.j1 || s.Ring.j1 || s.Pinky.j1) != null;
            return any ? s : null;
        }

        private static Transform FindChildByPredicate(Transform root, System.Predicate<Transform> pred)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == root) continue;
                if (pred(t)) return t;
            }
            return null;
        }

        private AnimationClip CreateClip(string name, HandSkeleton s, bool isGrip)
        {
            var clip = new AnimationClip { name = name };
            // オイラー角アニメーションを有効化
            UnityEditor.AnimationUtility.SetAnimationClipSettings(clip, new UnityEditor.AnimationClipSettings
            {
                loopTime = false
            });

            void BindFinger(FingerJoints f, float a1, float a2, float a3)
            {
                if (f == null) return;
                if (f.j1) SetLocalEulerXCurve(clip, f.j1, isGrip ? a1 : 0f);
                if (f.j2) SetLocalEulerXCurve(clip, f.j2, isGrip ? a2 : 0f);
                if (f.j3) SetLocalEulerXCurve(clip, f.j3, isGrip ? a3 : 0f);
            }

            // 通常の指
            BindFinger(s.Index, fingerCurl1, fingerCurl2, fingerCurl3);
            BindFinger(s.Middle, fingerCurl1, fingerCurl2, fingerCurl3);
            BindFinger(s.Ring, fingerCurl1, fingerCurl2, fingerCurl3);
            BindFinger(s.Pinky, fingerCurl1, fingerCurl2, fingerCurl3);
            // 親指
            if (includeThumb)
                BindFinger(s.Thumb, thumbCurl1, thumbCurl2, thumbCurl3);

            return clip;
        }

        private void SetLocalEulerXCurve(AnimationClip clip, Transform target, float degrees)
        {
            string path = GetRelativePath(target, (handRoot != null ? handRoot : transform));
            var binding = UnityEditor.EditorCurveBinding.FloatCurve(path, typeof(Transform), "localEulerAnglesRaw.x");
            var curve = new AnimationCurve(
                new Keyframe(0f, degrees, 0f, 0f)
            );
            UnityEditor.AnimationUtility.SetEditorCurve(clip, binding, curve);
        }

        private string GetRelativePath(Transform target, Transform root)
        {
            if (target == null || root == null) return string.Empty;
            System.Collections.Generic.List<string> elems = new();
            var t = target;
            while (t != null && t != root)
            {
                elems.Add(t.name);
                t = t.parent;
            }
            elems.Reverse();
            return string.Join("/", elems);
        }

        private void AssignOrCreateController(AnimationClip openClip, AnimationClip gripClip, string baseFolder)
        {
            if (handAnimator == null)
            {
                Debug.LogWarning($"{nameof(XRGripToHandAnimator)}: Animator が未割り当てのため Controller の自動設定をスキップしました。");
                return;
            }

            UnityEditor.Animations.AnimatorController controller = handAnimator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;

            if (controller == null)
            {
                string controllerPath = UnityEditor.AssetDatabase.GenerateUniqueAssetPath($"{baseFolder}/{gameObject.name}_Hand.controller");
                controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                handAnimator.runtimeAnimatorController = controller;
            }

            // パラメータ準備（Grip）
            if (controller.parameters == null || System.Array.Find(controller.parameters, p => p.name == gripParameterName) == default)
            {
                controller.AddParameter(gripParameterName, AnimatorControllerParameterType.Float);
            }

            // BlendTree 作成（Open=0, Grip=1）
            var sm = controller.layers[0].stateMachine;
            // 既存のデフォルトステートは残す/上書き
            var state = sm.defaultState ?? sm.AddState("HandBlend");
            state.name = "HandBlend";

            // BlendTree を生成し、Open/Grip 2点で1Dブレンド
            var bt = new UnityEditor.Animations.BlendTree
            {
                name = "GripBlendTree",
                blendType = UnityEditor.Animations.BlendTreeType.Simple1D,
                blendParameter = gripParameterName,
                hideFlags = HideFlags.HideInHierarchy
            };
            UnityEditor.AssetDatabase.AddObjectToAsset(bt, controller);
            bt.AddChild(openClip, 0f);
            bt.AddChild(gripClip, 1f);

            state.motion = bt;
            // デフォルトステートに設定
            sm.defaultState = state;
        }

        private void EnsureFolder(string folderPath)
        {
            // "Assets/Sub/Folder" を段階的に作成
            string[] parts = folderPath.Split('/');
            string cur = parts[0];
            if (cur != "Assets")
            {
                Debug.LogError("フォルダパスは Assets から始めてください。");
                return;
            }
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!UnityEditor.AssetDatabase.IsValidFolder(next))
                {
                    UnityEditor.AssetDatabase.CreateFolder(cur, parts[i]);
                }
                cur = next;
            }
        }
#endif
    }
}
