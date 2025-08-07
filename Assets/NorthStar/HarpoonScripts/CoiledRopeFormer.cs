using UnityEngine;
using SplineEditor;
using Meta.Utilities.Ropes;
using System.Reflection;
using System.Collections.Generic;
using System;
using Random = UnityEngine.Random;

// SpiralRopeGenerator.cs から再利用する設定クラス
[System.Serializable]
public class SpiralRopeSetting
{
    [Header("螺旋の基本設定")]
    public float radius = 1f;              // 螺旋の半径
    public float height = 2f;              // 全体の高さ
    public int turns = 3;                  // 巻き数
    public int pointsPerTurn = 12;         // 1巻きあたりの制御点数
    
    [Header("ロープの詳細設定")]
    public AnimationCurve radiusVariation = AnimationCurve.Linear(0f, 1f, 1f, 1f); // 半径の変化
    public AnimationCurve heightVariation = AnimationCurve.Linear(0f, 0f, 1f, 1f); // 高さの変化
    
    [Header("ランダム要素")]
    public float randomOffset = 0.0f;      // ランダムなオフセット
    public bool useRandomSeed = false;     // ランダムシードを使用
    public int seed = 0;                   // シード値
}

/// <summary>
/// BezierSplineを使用してRopeSystemを動的に螺旋形状に構成するクラス。
/// </summary>
[RequireComponent(typeof(BezierSpline), typeof(RopeSystem))]
public class CoiledRopeFormer : MonoBehaviour
{
    [Tooltip("螺旋の形状を定義する設定")]
    [SerializeField] private SpiralRopeSetting settings = new SpiralRopeSetting();
    
    [Tooltip("Start時に自動的に螺旋を生成するかどうか")]
    [SerializeField] private bool generateOnStart = true;

    private BezierSpline m_spline;
    private RopeSystem m_ropeSystem;

    // --- リフレクション用のメンバー情報 ---
    private FieldInfo m_anchorsField;
    private FieldInfo m_totalLengthField;
    private FieldInfo m_spooledLengthField;
    private MethodInfo m_setupAnchorMethod;
    private MethodInfo m_setupConstraintMethod;
    private MethodInfo m_updateBurstRopeMethod;
    private bool m_isReflectionInitialized = false;
    // ------------------------------------

    void Start()
    {
        if (generateOnStart)
        {
            GenerateCoiledRope();
        }
    }

    /// <summary>
    /// コンポーネントとリフレクションメンバーを初期化します。
    /// </summary>
    private void Initialize()
    {
        if (m_isReflectionInitialized) return;

        m_spline = GetComponent<BezierSpline>();
        m_ropeSystem = GetComponent<RopeSystem>();

        InitializeReflectionMembers();
        m_isReflectionInitialized = true;
    }

    /// <summary>
    /// リフレクションを使用してRopeSystemのプライベートメンバー情報を取得します。
    /// これにより、アンカーリストや内部メソッドにアクセスできます。
    /// </summary>
    private void InitializeReflectionMembers()
    {
        var ropeSystemType = typeof(RopeSystem);
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        m_anchorsField = ropeSystemType.GetField("m_anchors", flags);
        m_totalLengthField = ropeSystemType.GetField("m_totalLength", flags);
        m_spooledLengthField = ropeSystemType.GetField("m_spooledLength", flags);
        m_setupAnchorMethod = ropeSystemType.GetMethod("SetupAnchor", flags);
        m_setupConstraintMethod = ropeSystemType.GetMethod("SetupConstraint", flags, null, new Type[] { typeof(RopeSystem.Anchor), typeof(RopeSystem.Anchor), typeof(float) }, null);
        m_updateBurstRopeMethod = ropeSystemType.GetMethod("UpdateBurstRope", flags);

        if (m_anchorsField == null || m_totalLengthField == null || m_spooledLengthField == null ||
            m_setupAnchorMethod == null || m_setupConstraintMethod == null || m_updateBurstRopeMethod == null)
        {
            Debug.LogError("RopeSystemの必要なメンバーの取得に失敗しました。RopeSystemの実装が変更された可能性があります。");
        }
    }

    /// <summary>
    /// 螺旋状のロープを生成・構成します。
    /// </summary>
    [ContextMenu("Generate Coiled Rope")]
    public void GenerateCoiledRope()
    {
        Initialize();
        if (!m_isReflectionInitialized) return;

        // 1. 設定に基づいて螺旋状のBezierSplineを生成
        var splinePoints = GenerateSpiralSplinePoints();

        // 2. 生成したスプラインをRopeSystemのアンカーに適用
        ApplySplineToRopeSystem(splinePoints);
    }

    /// <summary>
    /// 螺旋形状の頂点リストを生成し、BezierSplineを更新します。
    /// </summary>
    /// <returns>生成された螺旋の頂点リスト</returns>
    private List<Vector3> GenerateSpiralSplinePoints()
    {
        // BezierSplineのプライベートメソッドをリフレクションで取得
        var clearMethod = typeof(BezierSpline).GetMethod("Clear", BindingFlags.Instance  | BindingFlags.NonPublic);
        var addPointMethod = typeof(BezierSpline).GetMethod("AddPoint", BindingFlags.Instance  | BindingFlags.NonPublic, null, new Type[] { typeof(Vector3) }, null);

        if (clearMethod == null || addPointMethod == null)
        {
            Debug.LogError("BezierSplineのClearまたはAddPointメソッドが見つかりません。");
            return new List<Vector3>();
        }

        clearMethod.Invoke(m_spline, null);

        if (settings.useRandomSeed)
            Random.InitState(settings.seed);

        int totalPoints = settings.turns * settings.pointsPerTurn;
        float angleStep = 360f / settings.pointsPerTurn;
        var points = new List<Vector3>();

        for (int i = 0; i <= totalPoints; i++)
        {
            float progress = (totalPoints > 0) ? (float)i / totalPoints : 0;
            float angle = i * angleStep * Mathf.Deg2Rad;

            float currentRadius = settings.radius * settings.radiusVariation.Evaluate(progress);
            float currentHeight = settings.height * settings.heightVariation.Evaluate(progress);

            Vector3 position = new Vector3(
                currentRadius * Mathf.Cos(angle),
                currentHeight,
                currentRadius * Mathf.Sin(angle)
            );

            if (settings.randomOffset > 0f)
            {
                position += Random.insideUnitSphere * settings.randomOffset;
            }
            
            // スプラインとリストに点を追加
            // RopeSystemはローカル座標でアンカーを扱うため、TransformPointは不要
            addPointMethod.Invoke(m_spline, new object[] { position });
            points.Add(position);
        }
        return points;
    }

    /// <summary>
    /// スプライン形状をRopeSystemのアンカー構成に変換して適用します。
    /// </summary>
    /// <param name="points">アンカーの基準となる頂点リスト</param>
    private void ApplySplineToRopeSystem(List<Vector3> points)
    {
        if (points.Count < 2)
        {
            Debug.LogWarning("螺旋の生成に必要なポイントが不足しています。");
            return;
        }

        var anchorsList = m_anchorsField.GetValue(m_ropeSystem) as IList<RopeSystem.Anchor>;
        if (anchorsList == null)
        {
            Debug.LogError("RopeSystemからアンカーリストを取得できませんでした。");
            return;
        }

        // 既存のアンカーを全てクリア
        anchorsList.Clear();

        // スプラインの長さを計算し、ロープの全長として設定
        float splineLength = 0f;
        for (int i = 0; i < points.Count - 1; i++)
        {
            splineLength += Vector3.Distance(points[i], points[i + 1]);
        }
        m_totalLengthField.SetValue(m_ropeSystem, splineLength);
        m_spooledLengthField.SetValue(m_ropeSystem, 0f);

        // スプラインの点から新しいアンカーを生成
        // 開始アンカー
        anchorsList.Add(new RopeSystem.Anchor { Type = RopeSystem.AnchorType.Start, Position = points[0] });

        // 中間アンカー（Bend）
        for (int i = 1; i < points.Count - 1; i++)
        {
            anchorsList.Add(new RopeSystem.Anchor { Type = RopeSystem.AnchorType.Bend, Position = points[i] });
        }

        // 終了アンカー
        anchorsList.Add(new RopeSystem.Anchor { Type = RopeSystem.AnchorType.End, Position = points[points.Count - 1], Mass = 0.1f });

        // 各アンカーのセットアップと制約の設定
        int nodeCount = m_ropeSystem.RopeSimulation.NodeCount;
        float accumulatedLength = 0f;

        for (int i = 0; i < anchorsList.Count; i++)
        {
            var anchor = anchorsList[i];
            
            // RopeBindIndexとProportionを設定
            if (i < anchorsList.Count - 1)
            {
                float segmentLength = Vector3.Distance(anchor.Position, anchorsList[i + 1].Position);
                anchor.Proportion = segmentLength / splineLength;
                accumulatedLength += segmentLength;
                anchorsList[i + 1].RopeBindIndex = Mathf.FloorToInt((accumulatedLength / splineLength) * (nodeCount - 1));
            }
            
            // privateメソッドを呼び出してアンカーを初期化
            m_setupAnchorMethod.Invoke(m_ropeSystem, new object[] { anchor });
        }
        
        // アンカー間の制約を設定
        for (int i = 0; i < anchorsList.Count - 1; i++)
        {
            m_setupConstraintMethod.Invoke(m_ropeSystem, new object[] { anchorsList[i], anchorsList[i + 1], -1f });
        }

        // BurstRopeシミュレーションを更新して変更を適用
        m_updateBurstRopeMethod.Invoke(m_ropeSystem, null);
        
        Debug.Log($"Coiled rope generated with {anchorsList.Count} anchors.");
    }
}