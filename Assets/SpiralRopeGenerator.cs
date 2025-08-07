using UnityEngine;
using SplineEditor;
using System.Reflection; // リフレクションを使用するために追加
using System;
using Random = UnityEngine.Random; // Typeクラスを使用するために追加

[System.Serializable]
public class SpiralRopeSettings
{
    [Header("螺旋の基本設定")]
    public float radius = 2f;              // 螺旋の半径
    public float height = 5f;              // 全体の高さ
    public int turns = 5;                  // 巻き数
    public int pointsPerTurn = 8;          // 1巻きあたりの制御点数
    
    [Header("ロープの詳細設定")]
    public float ropeThickness = 0.1f;     // ロープの太さ
    public AnimationCurve radiusVariation = AnimationCurve.Linear(0f, 1f, 1f, 1f); // 半径の変化
    public AnimationCurve heightVariation = AnimationCurve.Linear(0f, 0f, 1f, 1f); // 高さの変化
    
    [Header("ランダム要素")]
    public float randomOffset = 0.1f;      // ランダムなオフセット
    public bool useRandomSeed = false;     // ランダムシードを使用
    public int seed = 0;                   // シード値
}

[RequireComponent(typeof(BezierSpline))]
public class SpiralRopeGenerator : MonoBehaviour
{
    [SerializeField] private SpiralRopeSettings settings = new SpiralRopeSettings();
    [SerializeField] private bool autoUpdate = true;
    
    private BezierSpline spline;

    // --- リフレクション用のメンバー情報を保持するフィールド ---
    private MethodInfo m_clearMethod;
    private MethodInfo m_addPointMethod;
    private MethodInfo m_insertPointMethod;
    private PropertyInfo m_pointCountProperty;
    private MethodInfo m_getPointMethod;
    private MethodInfo m_getDirectionMethod;
    private bool m_reflectionInitialized = false;
    // ----------------------------------------------------
    
    /// <summary>
    /// BezierSplineコンポーネントの取得と、リフレクションメンバーの初期化を行います。
    /// </summary>
    private void Initialize()
    {
        if (m_reflectionInitialized && spline != null) return;

        spline = GetComponent<BezierSpline>();
        if (spline == null) return;

        InitializeReflectionMembers();
        m_reflectionInitialized = true;
    }

    /// <summary>
    /// リフレクションを使用してBezierSplineのプライベートメンバー情報を取得します。
    /// </summary>
    private void InitializeReflectionMembers()
    {
        var splineType = typeof(BezierSpline);
        // public/privateを問わずインスタンスメンバーを取得するフラグ
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // メソッドやプロパティの情報を取得
        m_clearMethod = splineType.GetMethod("Clear", flags);
        m_addPointMethod = splineType.GetMethod("AddPoint", flags, null, new Type[] { typeof(Vector3) }, null);
        m_insertPointMethod = splineType.GetMethod("InsertPoint", flags, null, new Type[] { typeof(int), typeof(Vector3) }, null);
        m_pointCountProperty = splineType.GetProperty("PointCount", flags);
        m_getPointMethod = splineType.GetMethod("GetPoint", flags, null, new Type[] { typeof(float) }, null);
        m_getDirectionMethod = splineType.GetMethod("GetDirection", flags, null, new Type[] { typeof(float) }, null);

        // 取得失敗時のエラーログ
        if (m_clearMethod == null || m_addPointMethod == null || m_insertPointMethod == null || m_pointCountProperty == null || m_getPointMethod == null || m_getDirectionMethod == null)
        {
            Debug.LogError("BezierSplineの必要なメンバーの取得に失敗しました。リフレクションの設定を確認してください。");
        }
    }
    
    [ContextMenu("Generate Spiral Rope")]
    public void GenerateSpiralRope()
    {
        Initialize(); // ContextMenuから呼ばれた場合も初期化
        
        if (spline == null)
        {
            Debug.LogError("BezierSplineコンポーネントが見つかりません！");
            return;
        }
        
        // ランダムシードの設定
        if (settings.useRandomSeed)
            Random.InitState(settings.seed);
        
        GenerateSplinePoints();
    }
    
    private void GenerateSplinePoints()
    {
        if (!m_reflectionInitialized) return;

        // 既存の点をクリア (リフレクションで呼び出し)
        m_clearMethod?.Invoke(spline, null);
        
        int totalPoints = settings.turns * settings.pointsPerTurn;
        float angleStep = 360f / settings.pointsPerTurn;
        
        for (int i = 0; i <= totalPoints; i++)
        {
            float progress = (totalPoints > 0) ? (float)i / totalPoints : 0;
            float angle = i * angleStep * Mathf.Deg2Rad;
            
            // 螺旋の基本位置計算
            float currentRadius = settings.radius * settings.radiusVariation.Evaluate(progress);
            float currentHeight = settings.height * settings.heightVariation.Evaluate(progress);
            
            Vector3 position = new Vector3(
                currentRadius * Mathf.Cos(angle),
                currentHeight,
                currentRadius * Mathf.Sin(angle)
            );
            
            // ランダムオフセットの追加
            if (settings.randomOffset > 0f)
            {
                position += new Vector3(
                    Random.Range(-settings.randomOffset, settings.randomOffset),
                    Random.Range(-settings.randomOffset * 0.5f, settings.randomOffset * 0.5f),
                    Random.Range(-settings.randomOffset, settings.randomOffset)
                );
            }
            
            // スプラインに点を追加 (リフレクションで呼び出し)
            if (i == 0)
            {
                m_addPointMethod?.Invoke(spline, new object[] { transform.TransformPoint(position) });
            }
            else
            {
                m_insertPointMethod?.Invoke(spline, new object[] { i, transform.TransformPoint(position) });
            }
        }
    }
    
    // ロープメッシュの生成（オプション）
    public Mesh GenerateRopeMesh(int segments = 16)
    {
        Initialize();
        if (!m_reflectionInitialized || spline == null) return null;

        int pointCount = (int)(m_pointCountProperty?.GetValue(spline) ?? 0);
        if (pointCount < 2) return null;
        
        Mesh mesh = new Mesh();
        
        int splineSegments = pointCount * 10; // スプライン上の分割数
        int vertexCount = splineSegments * segments;
        int triangleCount = (splineSegments - 1) * segments * 6;
        
        Vector3[] vertices = new Vector3[vertexCount];
        Vector3[] normals = new Vector3[vertexCount];
        Vector2[] uv = new Vector2[vertexCount];
        int[] triangles = new int[triangleCount];
        
        // 円形断面の生成
        Vector3[] circle = new Vector3[segments];
        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            circle[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * settings.ropeThickness;
        }
        
        // スプライン上の各点でメッシュを生成 (リフレクションで呼び出し)
        for (int i = 0; i < splineSegments; i++)
        {
            float t = (splineSegments > 1) ? (float)i / (splineSegments - 1) : 0;
            Vector3 point = (Vector3)(m_getPointMethod?.Invoke(spline, new object[] { t }) ?? Vector3.zero);
            Vector3 direction = ((Vector3)(m_getDirectionMethod?.Invoke(spline, new object[] { t }) ?? Vector3.forward)).normalized;
            
            Vector3 up = Vector3.up;
            // 方向が真上や真下を向いている場合の対策
            if (Vector3.Dot(direction, up) > 0.99f || Vector3.Dot(direction, up) < -0.99f)
            {
                up = Vector3.forward;
            }
            Vector3 right = Vector3.Cross(direction, up).normalized;
            up = Vector3.Cross(right, direction).normalized;
            
            for (int j = 0; j < segments; j++)
            {
                int vertIndex = i * segments + j;
                Vector3 localPos = circle[j];
                vertices[vertIndex] = point + right * localPos.x + up * localPos.y;
                normals[vertIndex] = (right * localPos.x + up * localPos.y).normalized;
                uv[vertIndex] = new Vector2((float)j / segments, t);
            }
        }
        
        // 三角形の生成
        int triIndex = 0;
        for (int i = 0; i < splineSegments - 1; i++)
        {
            for (int j = 0; j < segments; j++)
            {
                int current = i * segments + j;
                int next = i * segments + (j + 1) % segments;
                int currentNext = (i + 1) * segments + j;
                int nextNext = (i + 1) * segments + (j + 1) % segments;
                
                triangles[triIndex++] = current;
                triangles[triIndex++] = currentNext;
                triangles[triIndex++] = next;
                
                triangles[triIndex++] = next;
                triangles[triIndex++] = currentNext;
                triangles[triIndex++] = nextNext;
            }
        }
        
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        
        return mesh;
    }
    
    // Gizmosで螺旋を可視化
    void OnDrawGizmos()
    {
        Initialize(); // Gizmos描画時にも初期化
        if (!m_reflectionInitialized || spline == null) return;

        int pointCount = (int)(m_pointCountProperty?.GetValue(spline) ?? 0);
        if (pointCount > 1)
        {
            Gizmos.color = Color.yellow;
            
            for (int i = 0; i < 100; i++)
            {
                float t1 = (float)i / 99f;
                float t2 = (float)(i + 1) / 99f;
                
                if (t2 <= 1f)
                {
                    Vector3 p1 = (Vector3)(m_getPointMethod?.Invoke(spline, new object[] { t1 }) ?? Vector3.zero);
                    Vector3 p2 = (Vector3)(m_getPointMethod?.Invoke(spline, new object[] { t2 }) ?? Vector3.zero);
                    Gizmos.DrawLine(p1, p2);
                }
            }
            
            // 制御点の表示
            Gizmos.color = Color.red;
            for (int i = 0; i < pointCount; i++)
            {
                float t = (pointCount > 1) ? (float)i / (pointCount - 1) : 0;
                Vector3 point = (Vector3)(m_getPointMethod?.Invoke(spline, new object[] { t }) ?? Vector3.zero);
                Gizmos.DrawWireSphere(point, 0.1f);
            }
        }
    }
}