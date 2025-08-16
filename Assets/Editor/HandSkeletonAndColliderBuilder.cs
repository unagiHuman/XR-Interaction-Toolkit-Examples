// Unity 6000+ 用 Editor 拡張: スケルトン（Transform階層）＋物理コライダーを自動生成
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using NorthStar;
using XRInteractionNorthStar;

public class HandSkeletonAndCollidersBuilder : EditorWindow
{
    private const string GenPrefix = "__HC_GEN_";

    private Vector2 _scrollPos;

    private enum Handedness { Right, Left }
    private enum Axis { x, y, z }

    [Header("Target Parent")]
    [SerializeField] private Transform parentRoot; // 既存の親（空ならシーン直下）
    [SerializeField] private string newRootName = "GeneratedHand";
    [SerializeField] private Handedness handed = Handedness.Right;

    [Header("Palm (m)")]
    [SerializeField] private float palmWidth = 0.085f;
    [SerializeField] private float palmLength = 0.09f;
    [SerializeField] private float palmThickness = 0.028f;

    [Header("Fingers length scale")]
    [SerializeField, Tooltip("Index, Middle, Ring, Pinky の長さ倍率（Middle 基準=1.0）")]
    private Vector4 fingerLengthScales = new Vector4(0.95f, 1.00f, 0.95f, 0.75f); // x=Index, y=Middle, z=Ring, w=Pinky
    [SerializeField, Tooltip("Thumb の長さ倍率")]
    private float thumbLengthScale = 0.7f;

    [Header("Fingers layout (m/deg)")]
    [SerializeField, Tooltip("各指の基部の横方向間隔（幅）")]
    private float fingerSpread = 0.018f;
    [SerializeField, Tooltip("指の根元のZオフセット（掌の中心から指方向へ）")]
    private float fingerBaseZ = 0.02f;
    [SerializeField, Tooltip("親指基部のZオフセット")]
    private float thumbBaseZ = 0.01f;
    [SerializeField, Tooltip("親指の外転（+で外側へ）")]
    private float thumbYawDeg = 35f;
    [SerializeField, Tooltip("親指の掌面からの立ち上がり角（+で上へ）")]
    private float thumbPitchDeg = 10f;

    [Header("Collider settings")]
    [SerializeField, Tooltip("節の長さに対する半径比の目安")]
    private float radiusScale = 0.22f;
    [SerializeField, Tooltip("最小半径(m)")]
    private float minRadius = 0.004f;
    [SerializeField] private bool isTrigger = true;
    [SerializeField] private string layerName = "Default";
    [SerializeField] private PhysicsMaterial physicMaterial;

    [Header("Optional generation")]
    [SerializeField, Tooltip("掌/指のコライダーを自動生成するか")]
    private bool createColliders = false;
    [SerializeField, Tooltip("手ルートに Rigidbody を追加するか")]
    private bool addRigidbodyToRoot = false;

    [Header("HandColliders")]
    [SerializeField, Tooltip("HandColliders を取り付け、子孫の Collider をキャッシュして有効/無効を一括制御します")]
    private bool setupHandColliders = false;

    [Header("PhysicalHand setup")]
    [SerializeField, Tooltip("PhysicalHand と必要なコンポーネントを自動追加")]
    private bool attachPhysicalHand = false;
    [SerializeField, Tooltip("Wrist 直下に HandAnchor を自動生成して割り当て")]
    private bool createHandAnchorUnderWrist = true;
    [SerializeField, Tooltip("CriticallyDampendSpringJoint が存在する場合は追加・割り当て")]
    private bool addDampendSpringJoint = true;

    [Header("Grip Animator setup")]
    [SerializeField, Tooltip("XRGripToHandAnimator を手ルートの最後にアタッチ")]
    private bool attachGripAnimator = false;

    [Header("Grab Controller setup")]
    [SerializeField, Tooltip("PhysicalHandGrabController を手ルートに追加して設定")]
    private bool attachGrabController = false;

    [Header("Debug View")]
    [SerializeField, Tooltip("FingerPrimitiveView を手ルートに追加して可視化")]
    private bool attachFingerPrimitiveView = true;

    [Header("Joints")]
    [SerializeField, Tooltip("指の各ボーン間に HingeJoint を追加")]
    private bool addHingeJoints = false;
    [SerializeField, Tooltip("ヒンジの回転軸（ローカル軸）")]
    private Axis hingeAxis = Axis.x;
    [SerializeField, Tooltip("ヒンジ角度制限を使用")]
    private bool useHingeLimits = true;
    [SerializeField, Tooltip("ヒンジ角度 下限(deg)")]
    private float hingeLimitMin = 0f;
    [SerializeField, Tooltip("ヒンジ角度 上限(deg)")]
    private float hingeLimitMax = 90f;
    [SerializeField, Tooltip("Palm に固定用の Kinematic Rigidbody を付与")]
    private bool addPalmKinematicBody = true;
    [SerializeField, Tooltip("指ボーンに Rigidbody が無い場合は付与")]
    private bool addRigidbodiesForHinges = true;
    [SerializeField, Tooltip("自動付与する指ボーン剛体の質量(kg)")]
    private float fingerBoneMass = 0.02f;

    [MenuItem("Tools/Hand Colliders/Skeleton + Colliders Builder")]
    public static void Open()
    {
        var w = GetWindow<HandSkeletonAndCollidersBuilder>("Hand Skeleton Builder");
        w.minSize = new Vector2(520, 640);
        w.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Hand Skeleton + Colliders Auto Builder", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        parentRoot = (Transform)EditorGUILayout.ObjectField("Parent Root (optional)", parentRoot, typeof(Transform), true);
        newRootName = EditorGUILayout.TextField("New Root Name", newRootName);
        handed = (Handedness)EditorGUILayout.EnumPopup("Handedness", handed);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Palm (m)", EditorStyles.boldLabel);
        palmWidth = EditorGUILayout.Slider("Palm Width", palmWidth, 0.05f, 0.12f);
        palmLength = EditorGUILayout.Slider("Palm Length", palmLength, 0.06f, 0.12f);
        palmThickness = EditorGUILayout.Slider("Palm Thickness", palmThickness, 0.015f, 0.05f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Fingers length scale", EditorStyles.boldLabel);
        fingerLengthScales = EditorGUILayout.Vector4Field("Index/Middle/Ring/Pinky", fingerLengthScales);
        thumbLengthScale = EditorGUILayout.Slider("Thumb Scale", thumbLengthScale, 0.5f, 1.0f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Fingers layout", EditorStyles.boldLabel);
        fingerSpread = EditorGUILayout.Slider("Finger Spread (m)", fingerSpread, 0.01f, 0.03f);
        fingerBaseZ = EditorGUILayout.Slider("Finger Base Z (m)", fingerBaseZ, 0.0f, 0.04f);
        thumbBaseZ = EditorGUILayout.Slider("Thumb Base Z (m)", thumbBaseZ, -0.01f, 0.03f);
        thumbYawDeg = EditorGUILayout.Slider("Thumb Yaw (deg)", thumbYawDeg, 0f, 60f);
        thumbPitchDeg = EditorGUILayout.Slider("Thumb Pitch (deg)", thumbPitchDeg, -20f, 40f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Collider settings", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(!createColliders))
        {
            radiusScale = EditorGUILayout.Slider("Radius Scale", radiusScale, 0.1f, 0.4f);
            minRadius = EditorGUILayout.Slider("Min Radius (m)", minRadius, 0.001f, 0.02f);
            isTrigger = EditorGUILayout.Toggle("Is Trigger", isTrigger);
            layerName = EditorGUILayout.TextField("Layer", layerName);
            physicMaterial = (PhysicsMaterial)EditorGUILayout.ObjectField("Physic Material", physicMaterial, typeof(PhysicsMaterial), false);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Optional generation", EditorStyles.boldLabel);
        createColliders = EditorGUILayout.Toggle("Generate Colliders", createColliders);
        addRigidbodyToRoot = EditorGUILayout.Toggle("Add Rigidbody to Root", addRigidbodyToRoot);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("HandColliders", EditorStyles.boldLabel);
        setupHandColliders = EditorGUILayout.Toggle("Attach (cache children)", setupHandColliders);

        if (createColliders && setupHandColliders)
        {
            EditorGUILayout.HelpBox("Generate Colliders と HandColliders の併用はコライダー重複の原因になります。どちらか一方の利用を推奨します。", MessageType.Warning);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("PhysicalHand setup", EditorStyles.boldLabel);
        attachPhysicalHand = EditorGUILayout.Toggle("Attach PhysicalHand", attachPhysicalHand);
        using (new EditorGUI.DisabledScope(!attachPhysicalHand))
        {
            createHandAnchorUnderWrist = EditorGUILayout.Toggle("Create Hand Anchor under Wrist", createHandAnchorUnderWrist);
            addDampendSpringJoint = EditorGUILayout.Toggle("Add CriticallyDampendSpringJoint (if exists)", addDampendSpringJoint);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Grip Animator setup", EditorStyles.boldLabel);
        attachGripAnimator = EditorGUILayout.Toggle("Attach XRGripToHandAnimator (last)", attachGripAnimator);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Grab Controller setup", EditorStyles.boldLabel);
        attachGrabController = EditorGUILayout.Toggle("Attach PhysicalHandGrabController", attachGrabController);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Debug View", EditorStyles.boldLabel);
        attachFingerPrimitiveView = EditorGUILayout.Toggle("Attach FingerPrimitiveView", attachFingerPrimitiveView);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Joints", EditorStyles.boldLabel);
        addHingeJoints = EditorGUILayout.Toggle("Add HingeJoints", addHingeJoints);
        using (new EditorGUI.DisabledScope(!addHingeJoints))
        {
            hingeAxis = (Axis)EditorGUILayout.EnumPopup("Hinge Axis (local)", hingeAxis);
            useHingeLimits = EditorGUILayout.Toggle("Use Limits", useHingeLimits);
            hingeLimitMin = EditorGUILayout.FloatField("Limit Min (deg)", hingeLimitMin);
            hingeLimitMax = EditorGUILayout.FloatField("Limit Max (deg)", hingeLimitMax);
            addPalmKinematicBody = EditorGUILayout.Toggle("Add Palm Kinematic RB", addPalmKinematicBody);
            addRigidbodiesForHinges = EditorGUILayout.Toggle("Auto Add RB to Finger Bones", addRigidbodiesForHinges);
            fingerBoneMass = EditorGUILayout.Slider("Finger RB Mass (kg)", fingerBoneMass, 0.001f, 0.2f);
        }

        EditorGUILayout.Space(10);
        if (GUILayout.Button("Generate / Rebuild"))
        {
            try
            {
                Generate();
            }
            catch (Exception ex)
            {
                Debug.LogError($"HandSkeletonBuilder: 生成に失敗しました\n{ex}");
            }
        }
        if (GUILayout.Button("Clean Generated"))
        {
            try
            {
                int n = CleanGenerated(parentRoot != null ? parentRoot.gameObject.scene : default);
                Debug.Log($"HandSkeletonBuilder: Removed {n} generated objects.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"HandSkeletonBuilder: クリーンに失敗しました\n{ex}");
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void Generate()
    {
        if (string.IsNullOrWhiteSpace(newRootName))
            newRootName = "GeneratedHand";

        // 1) ルート作成
        Transform root = CreateOrReplaceRoot(newRootName, parentRoot);
        int layer = LayerMask.NameToLayer(layerName);
        if (layer < 0) Debug.LogWarning($"Layer '{layerName}' が見つかりません（Default を使用）");

        // 2) 基本座標系
        float handedSign = (handed == Handedness.Right) ? 1f : -1f;

        // 3) ボーン作成（Wrist -> Palm）
        Transform wrist = CreateBone(root, "WristRoot", Vector3.zero, Quaternion.identity);
        Transform palm = CreateBone(wrist, "HandPalm", Vector3.zero, Quaternion.identity);

        // 4) 掌 BoxCollider（任意）
        if (createColliders)
        {
            var palmColGO = CreateGenChild(palm, GenPrefix + "PalmBox");
            var box = palmColGO.AddComponent<BoxCollider>();
            box.center = Vector3.zero;
            box.size = new Vector3(palmWidth, palmThickness, palmLength);
            ApplyCommonColliderSettings(box, layer);
        }

        // 5) 指作成
        float baseMiddleTotal = 0.095f;
        float prox = baseMiddleTotal * 0.45f;
        float inter = baseMiddleTotal * 0.30f;
        float dist = baseMiddleTotal * 0.25f;

        var fingers = new[]
        {
            new { Name="Index",  Scale=fingerLengthScales.x, SpreadOffset= fingerSpread *  0.5f },
            new { Name="Middle", Scale=fingerLengthScales.y, SpreadOffset= fingerSpread *  1.5f },
            new { Name="Ring",   Scale=fingerLengthScales.z, SpreadOffset= fingerSpread *  2.5f },
            new { Name="Pinky",  Scale=fingerLengthScales.w, SpreadOffset= fingerSpread *  3.5f },
        };

        foreach (var f in fingers)
        {
            float sx = handedSign * (f.SpreadOffset - fingerSpread * 2.0f);
            Vector3 basePos = new Vector3(sx, 0, fingerBaseZ);

            float s = Mathf.Max(0.5f, f.Scale);
            float L1 = prox * s;
            float L2 = inter * s;
            float L3 = dist * s;

            BuildFingerChain(palm, f.Name, basePos, Quaternion.identity, L1, L2, L3, layer, createColliders);
        }

        // 親指
        {
            string name = "Thumb";
            Vector3 basePos = new Vector3(handedSign * (palmWidth * 0.35f), 0, thumbBaseZ);
            Quaternion dirRot = Quaternion.AngleAxis(handedSign * thumbYawDeg, Vector3.up) * Quaternion.AngleAxis(-thumbPitchDeg, Vector3.right);

            float thumbTotal = baseMiddleTotal * thumbLengthScale;
            float T1 = thumbTotal * 0.55f;
            float T2 = thumbTotal * 0.45f;

            BuildThumbChain(palm, name, basePos, dirRot, T1, T2, layer, createColliders);
        }

        // 6) Root へ Rigidbody（任意）
        if (addRigidbodyToRoot)
        {
            var rb = root.GetComponent<Rigidbody>();
            if (!rb) rb = Undo.AddComponent<Rigidbody>(root.gameObject);
            rb.mass = 1.0f;
            rb.angularDamping = 0.05f;
            rb.linearDamping = 0.0f;
            rb.useGravity = false;
            rb.isKinematic = false;
        }

        // 7) HandColliders の取り付けと自動設定（任意）
        if (setupHandColliders)
        {
            SetupHandColliders(root, wrist, palm);
        }

        // 8) PhysicalHand の取り付けと必須追加（任意）
        if (attachPhysicalHand)
        {
            SetupPhysicalHand(root, wrist);
        }

        // 9) XRGripToHandAnimator を最後にアタッチ（任意）
        if (attachGripAnimator)
        {
            SetupGripAnimator(root);
        }

        // 10) PhysicalHandGrabController を追加・セットアップ（任意）
        if (attachGrabController)
        {
            SetupGrabController(root);
        }

        // 11) FingerPrimitiveView をアタッチ（任意）
        if (attachFingerPrimitiveView)
        {
            SetupFingerPrimitiveView(root);
        }

        EditorGUIUtility.PingObject(root);
        Debug.Log("HandSkeletonBuilder: スケルトン生成が完了しました。");
    }

    private Transform CreateOrReplaceRoot(string baseName, Transform parent)
    {
        if (parent != null)
        {
            var toDelete = new List<GameObject>();
            for (int i = 0; i < parent.childCount; i++)
            {
                var t = parent.GetChild(i);
                if (t != null && t.name.StartsWith(GenPrefix, StringComparison.Ordinal))
                {
                    toDelete.Add(t.gameObject);
                }
            }
            foreach (var go in toDelete)
            {
                Undo.DestroyObjectImmediate(go);
            }
        }

        string rootName = $"{GenPrefix}{baseName}_{(handed == Handedness.Right ? "R" : "L")}";
        var rootGO = new GameObject(rootName);
        Undo.RegisterCreatedObjectUndo(rootGO, "Create Hand Root");
        if (parent) rootGO.transform.SetParent(parent, false);
        rootGO.transform.localPosition = Vector3.zero;
        rootGO.transform.localRotation = Quaternion.identity;
        rootGO.transform.localScale = Vector3.one;
        return rootGO.transform;
    }

    private Transform CreateBone(Transform parent, string name, Vector3 localPos, Quaternion localRot)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create Bone");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        go.transform.localScale = Vector3.one;
        return go.transform;
    }

    private void BuildFingerChain(Transform palm, string fingerName, Vector3 baseLocalPos, Quaternion baseLocalRot, float L1, float L2, float L3, int layer, bool withColliders)
    {
        var f1 = CreateBone(palm, $"{fingerName}1", baseLocalPos, baseLocalRot);
        var f2 = CreateBone(f1, $"{fingerName}2", new Vector3(0, 0, L1), Quaternion.identity);
        var f3 = CreateBone(f2, $"{fingerName}3", new Vector3(0, 0, L2), Quaternion.identity);
        var tip = CreateBone(f3, $"{fingerName}Tip", new Vector3(0, 0, L3 * 0.9f), Quaternion.identity);

        // ヒンジジョイントの追加（任意）
        if (addHingeJoints)
        {
            SetupFingerHinges(palm, f1, f2, f3, tip);
        }

        if (!withColliders) return;

        CreateCapsuleOnSegment(f1, L1, layer);
        CreateCapsuleOnSegment(f2, L2, layer);
        CreateCapsuleOnSegment(f3, L3, layer);
        CreateSphereOnTip(tip, Mathf.Max(minRadius * 0.8f, L3 * radiusScale * 0.7f), layer);
    }

    private void BuildThumbChain(Transform palm, string fingerName, Vector3 baseLocalPos, Quaternion dirLocalRot, float L1, float L2, int layer, bool withColliders)
    {
        var t1 = CreateBone(palm, $"{fingerName}1", baseLocalPos, dirLocalRot);
        var t2 = CreateBone(t1, $"{fingerName}2", new Vector3(0, 0, L1), Quaternion.identity);
        var t3 = CreateBone(t2, $"{fingerName}3", new Vector3(0, 0, L2 * 0.7f), Quaternion.identity);
        var tip = CreateBone(t3, $"{fingerName}Tip", new Vector3(0, 0, L2 * 0.6f), Quaternion.identity);

        // ヒンジジョイントの追加（任意）
        if (addHingeJoints)
        {
            SetupFingerHinges(palm, t1, t2, t3, tip);
        }

        if (!withColliders) return;

        CreateCapsuleOnSegment(t1, L1, layer);
        CreateCapsuleOnSegment(t2, L2, layer);
        CreateSphereOnTip(tip, Mathf.Max(minRadius * 0.8f, L2 * radiusScale * 0.7f), layer);
    }

    private void CreateCapsuleOnSegment(Transform seg, float length, int layer)
    {
        var colGO = CreateGenChild(seg, $"{GenPrefix}Capsule_{seg.name}");
        var cap = colGO.AddComponent<CapsuleCollider>();
        cap.direction = 2; // Z
        float radius = Mathf.Max(minRadius, length * radiusScale);
        cap.height = Mathf.Max(0.001f, length + 2f * radius);
        cap.radius = radius;
        cap.center = new Vector3(0, 0, length * 0.5f);
        ApplyCommonColliderSettings(cap, layer);
    }

    private void CreateSphereOnTip(Transform tip, float radius, int layer)
    {
        var colGO = CreateGenChild(tip, $"{GenPrefix}Sphere_{tip.name}");
        var sph = colGO.AddComponent<SphereCollider>();
        sph.center = Vector3.zero;
        sph.radius = radius;
        ApplyCommonColliderSettings(sph, layer);
    }

    // ===== Hinge + Rigidbody helpers =====
    private void SetupFingerHinges(Transform palm, Transform seg1, Transform seg2, Transform seg3, Transform tip)
    {
        // Palm に固定用RB
        if (addPalmKinematicBody)
        {
            var palmRb = palm.GetComponent<Rigidbody>();
            if (!palmRb)
            {
                palmRb = Undo.AddComponent<Rigidbody>(palm.gameObject);
                palmRb.isKinematic = true;
                palmRb.useGravity = false;
            }
        }

        // 各節にRBを用意
        var rb1 = EnsureRigidbody(seg1);
        var rb2 = EnsureRigidbody(seg2);
        var rb3 = EnsureRigidbody(seg3);
        var rbTip = EnsureRigidbody(tip);

        // Palm-1, 1-2, 2-3, 3-Tip をヒンジで接続
        CreateHingeBetween(seg1, palm);
        CreateHingeBetween(seg2, seg1);
        CreateHingeBetween(seg3, seg2);
        CreateHingeBetween(tip, seg3);
    }

    private Rigidbody EnsureRigidbody(Transform t)
    {
        var rb = t.GetComponent<Rigidbody>();
        if (!rb && addRigidbodiesForHinges)
        {
            rb = Undo.AddComponent<Rigidbody>(t.gameObject);
            rb.mass = Mathf.Max(0.001f, fingerBoneMass);
            rb.useGravity = false;
            rb.angularDamping = 0.05f;
            rb.linearDamping = 0.0f;
        }
        return rb;
    }

    private void CreateHingeBetween(Transform child, Transform parent)
    {
        // ヒンジは child 側に付与し、parent の RB に接続
        var joint = child.GetComponent<HingeJoint>();
        if (!joint)
            joint = Undo.AddComponent<HingeJoint>(child.gameObject);

        var parentRb = parent.GetComponent<Rigidbody>();
        if (!parentRb)
        {
            // 必要に応じて固定用キネマティックRBを付与
            parentRb = Undo.AddComponent<Rigidbody>(parent.gameObject);
            parentRb.isKinematic = true;
            parentRb.useGravity = false;
        }
        joint.connectedBody = parentRb;

        // 軸設定（ローカル）
        switch (hingeAxis)
        {
            case Axis.x: joint.axis = Vector3.right; break;
            case Axis.y: joint.axis = Vector3.up; break;
            case Axis.z: joint.axis = Vector3.forward; break;
        }

        joint.autoConfigureConnectedAnchor = true;
        joint.anchor = Vector3.zero;

        if (useHingeLimits)
        {
            JointLimits lim = joint.limits;
            lim.min = hingeLimitMin;
            lim.max = hingeLimitMax;
            joint.limits = lim;
            joint.useLimits = true;
        }
        else
        {
            joint.useLimits = false;
        }
    }

    private GameObject CreateGenChild(Transform parent, string name)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create Collider");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go;
    }

    private void ApplyCommonColliderSettings(Collider c, int layer)
    {
        c.isTrigger = isTrigger;
        if (physicMaterial) c.sharedMaterial = physicMaterial;
        if (layer >= 0) c.gameObject.layer = layer;
    }

    private void SetupHandColliders(Transform handRoot, Transform wrist, Transform palm)
    {
        var hc = handRoot.GetComponent<HandColliders>();
        if (!hc) hc = Undo.AddComponent<HandColliders>(handRoot.gameObject);

        // 新仕様: 子孫の Collider をキャッシュして有効/無効を一括制御するのみ
        hc.RefreshCache();
        EditorUtility.SetDirty(hc);
    }

    private void SetupPhysicalHand(Transform handRoot, Transform wrist)
    {
        var go = handRoot.gameObject;

        // 1) 前提コンポーネントを先に追加
        var rb = go.GetComponent<Rigidbody>();
        if (!rb) rb = Undo.AddComponent<Rigidbody>(go);
        rb.useGravity = false;

        var joint = go.GetComponent<ConfigurableJoint>();
        if (!joint) joint = Undo.AddComponent<ConfigurableJoint>(go);
        joint.connectedBody = null;
        joint.rotationDriveMode = RotationDriveMode.Slerp;

        // 2) Hand Anchor（Wrist 直下に生成して割り当て）
        Transform anchor = null;
        if (createHandAnchorUnderWrist)
        {
            if (wrist) anchor = wrist.Find(GenPrefix + "HandAnchor");
            if (!anchor)
            {
                var anchorGO = new GameObject(GenPrefix + "HandAnchor");
                Undo.RegisterCreatedObjectUndo(anchorGO, "Create Hand Anchor");
                anchor = anchorGO.transform;
                if (wrist) anchor.SetParent(wrist, false); else anchor.SetParent(handRoot, false);
                anchor.localPosition = Vector3.zero;
                anchor.localRotation = Quaternion.identity;
            }
        }

        // 3) CriticallyDampendSpringJoint（存在する場合のみ追加して割り当て）
        Component damp = null;
        if (addDampendSpringJoint)
        {
            var cdsjType = Type.GetType("NorthStar.CriticallyDampendSpringJoint");
            if (cdsjType != null)
            {
                damp = go.GetComponent(cdsjType);
                if (damp == null)
                {
                    damp = go.AddComponent(cdsjType);
                    Undo.RegisterCreatedObjectUndo(damp, "Add CriticallyDampendSpringJoint");
                }
            }
            else
            {
                Debug.LogWarning("HandSkeletonBuilder: CriticallyDampendSpringJoint 型が見つからないため割り当てをスキップしました。");
            }
        }

        // 4) 最後に PhysicalHand を追加（または一番下へ移動）
        var ph = go.GetComponent<PhysicalHand>();
        if (!ph) ph = Undo.AddComponent<PhysicalHand>(go);
        else
        {
            // 既存の場合はコンポーネント順の一番下へ移動
            while (UnityEditorInternal.ComponentUtility.MoveComponentDown(ph)) { }
        }

        // 5) プロパティ割り当て
        var so = new SerializedObject(ph);
        if (anchor) so.FindProperty("m_handAnchor").objectReferenceValue = anchor;
        so.FindProperty("m_boundToParent").boolValue = false;
        so.FindProperty("m_parent").objectReferenceValue = null;
        so.FindProperty("m_jointAnchor").vector3Value = Vector3.zero;
        so.FindProperty("m_jointAnchorRotation").vector3Value = Vector3.zero;
        if (damp) so.FindProperty("DampendSpringJoint").objectReferenceValue = (UnityEngine.Object)damp;
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(ph);
    }

    private void SetupGripAnimator(Transform handRoot)
    {
        var go = handRoot.gameObject;

        // 1) コンポーネント追加（既存なら流用）
        var grip = go.GetComponent<XRGripToHandAnimator>();
        if (!grip) grip = Undo.AddComponent<XRGripToHandAnimator>(go);

        // 2) コンポーネント順の一番下へ移動
        while (UnityEditorInternal.ComponentUtility.MoveComponentDown(grip)) { }

        // 3) 可能なら Animator/handRoot を自動割り当て
        var so = new SerializedObject(grip);
        var spAnimator = so.FindProperty("handAnimator");
        if (spAnimator != null && spAnimator.objectReferenceValue == null)
        {
            var animator = handRoot.GetComponentInChildren<Animator>();
            if (animator != null) spAnimator.objectReferenceValue = animator;
        }
        var spHandRoot = so.FindProperty("handRoot");
        if (spHandRoot != null && spHandRoot.objectReferenceValue == null)
        {
            spHandRoot.objectReferenceValue = handRoot;
        }
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(grip);
    }

    private void SetupGrabController(Transform handRoot)
    {
        var go = handRoot.gameObject;

        // 1) コンポーネント追加（既存なら流用）
        var ctrl = go.GetComponent<XRInteractionNorthStar.PhysicalHandGrabController>();
        if (!ctrl) ctrl = Undo.AddComponent<XRInteractionNorthStar.PhysicalHandGrabController>(go);

        // 2) コンポーネント順の一番下へ移動
        while (UnityEditorInternal.ComponentUtility.MoveComponentDown(ctrl)) { }

        // 3) 主要プロパティの初期セットアップ
        var so = new SerializedObject(ctrl);
        var spAuto = so.FindProperty("autoDiscoverJoints");
        if (spAuto != null) spAuto.boolValue = true;

        var spUseXR = so.FindProperty("useXRInteractor");
        if (spUseXR != null) spUseXR.boolValue = false; // デフォルトはデバッグ操作で確認できるように

        var spDebug = so.FindProperty("debugInput");
        if (spDebug != null) spDebug.boolValue = true;

        var spKeyboard = so.FindProperty("enableKeyboard");
        if (spKeyboard != null) spKeyboard.boolValue = true;

        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(ctrl);
    }

    private void SetupFingerPrimitiveView(Transform handRoot)
    {
        var go = handRoot.gameObject;
        var view = go.GetComponent<FingerPrimitiveView>();
        if (!view)
        {
            view = Undo.AddComponent<FingerPrimitiveView>(go);
        }
        // 即時反映
        if (view != null)
        {
            view.Rebuild();
            EditorUtility.SetDirty(view);
        }
    }

    private static List<Transform> GuessFingerRoots(Transform palm)
    {
        var order = new[] { "Thumb", "Index", "Middle", "Ring", "Pinky" };
        var result = new List<Transform>(5);
        if (palm == null) return result;

        for (int oi = 0; oi < order.Length; oi++)
        {
            string key = order[oi];
            Transform match = null;

            for (int i = 0; i < palm.childCount; i++)
            {
                var c = palm.GetChild(i);
                if (NameLike(c, key))
                {
                    match = c;
                    break;
                }
            }
            if (match == null)
            {
                var all = palm.GetComponentsInChildren<Transform>(true);
                foreach (var t in all)
                {
                    if (t == palm) continue;
                    if (t.parent != null && t.parent != palm) continue;
                    if (NameLike(t, key))
                    {
                        match = t;
                        break;
                    }
                }
            }
            if (match != null) result.Add(match);
        }
        return result;

        static bool NameLike(Transform t, string key)
        {
            var n = t.name.ToLowerInvariant();
            var k = key.ToLowerInvariant();
            if (!n.Contains(k)) return false;
            return n.EndsWith("1") || n.EndsWith("01") || n.Contains("prox");
        }
    }

    private static int CleanGenerated(in UnityEngine.SceneManagement.Scene scene)
    {
        var target = scene;
        if (!target.IsValid())
            target = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();

        int count = 0;
        foreach (var root in target.GetRootGameObjects())
        {
            var gens = root.GetComponentsInChildren<Transform>(true)
                           .Where(t => t && t.name.StartsWith(GenPrefix, StringComparison.Ordinal))
                           .Select(t => t.gameObject)
                           .Distinct()
                           .ToList();
            foreach (var go in gens)
            {
                Undo.RegisterFullObjectHierarchyUndo(go, "Delete Generated");
                UnityEngine.Object.DestroyImmediate(go);
                count++;
            }
        }
        return count;
    }
}
#endif