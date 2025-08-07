using Meta.Utilities.Ropes;
using UnityEngine;

public class RopeDistanceController : MonoBehaviour
{
    [Header("接続ポイント")]
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform endPoint;
    
    [Header("アンカー設定")]
    [SerializeField] private RopeSystem.Anchor startAnchor;
    [SerializeField] private RopeSystem.Anchor endAnchor;
    [SerializeField] private bool useExternalAnchors = false; // 外部アンカーを使用するかどうか
    
    [Header("ロープシステム")]
    [SerializeField] private RopeSystem ropeSystem;
    
    [Header("設定")]
    [SerializeField] private bool autoUpdate = true;
    [SerializeField] private float lengthMultiplier = 1.0f;
    [SerializeField] private float minimumLength = 0.5f;
    [SerializeField] private float maximumLength = 50f;
    [SerializeField] private float updateThreshold = 0.01f;
    
    [Header("自動作成アンカー設定")]
    [SerializeField] private RopeSystem.AnchorType startAnchorType = RopeSystem.AnchorType.Dynamic;
    [SerializeField] private RopeSystem.AnchorType endAnchorType = RopeSystem.AnchorType.Dynamic;
    
    private float currentDistance;
    private float previousDistance;
    private bool isInitialized = false;
    private bool managingAnchors = false; // このクラスがアンカーを管理しているかどうか

    void Start()
    {
        InitializeRopeConnection();
    }

    void Update()
    {
        if (autoUpdate && isInitialized)
        {
            UpdateRopeLength();
        }
    }

    /// <summary>
    /// ロープシステムとの接続を初期化
    /// </summary>
    private void InitializeRopeConnection()
    {
        if (ropeSystem == null)
        {
            ropeSystem = GetComponent<RopeSystem>();
            if (ropeSystem == null)
            {
                Debug.LogError("RopeSystem が見つかりません！");
                return;
            }
        }

        if (startPoint == null || endPoint == null)
        {
            Debug.LogError("開始点または終了点が設定されていません！");
            return;
        }

        // 外部アンカーを使用するかどうかで分岐
        if (useExternalAnchors)
        {
            if (startAnchor == null || endAnchor == null)
            {
                Debug.LogError("外部アンカーが設定されていません！");
                return;
            }
            managingAnchors = false;
        }
        else
        {
            CreateAnchors();
            managingAnchors = true;
        }

        SetupConstraints();
        isInitialized = true;
        
        // 初期長さを設定
        UpdateRopeLength();
    }

    /// <summary>
    /// 新しいアンカーを作成
    /// </summary>
    private void CreateAnchors()
    {
        // 開始点のアンカーを作成
        startAnchor = ropeSystem.CreateAnchor(startAnchorType, startPoint.position, 0, startPoint.gameObject);
        
        // 終了点のアンカーを作成
        endAnchor = ropeSystem.CreateAnchor(endAnchorType, endPoint.position, 1, endPoint.gameObject);
    }

    /// <summary>
    /// 制約を設定
    /// </summary>
    private void SetupConstraints()
    {
        if (startAnchor == null || endAnchor == null) return;

        // アンカー間の制約を設定
        float initialDistance = Vector3.Distance(startPoint.position, endPoint.position);
       // ropeSystem.SetupConstraint(startAnchor, endAnchor, initialDistance * lengthMultiplier);
    }

    /// <summary>
    /// ロープの長さを更新
    /// </summary>
    public void UpdateRopeLength()
    {
        if (!isInitialized || startPoint == null || endPoint == null) return;
        if (startAnchor == null || endAnchor == null) return;

        // 2点間の距離を計算
        currentDistance = Vector3.Distance(startPoint.position, endPoint.position);
        
        // 距離の変化が閾値を超えた場合のみ更新
        if (Mathf.Abs(currentDistance - previousDistance) > updateThreshold)
        {
            // 長さを制限内に収める
            float targetLength = Mathf.Clamp(currentDistance * lengthMultiplier, minimumLength, maximumLength);
            
            // アンカーの位置を更新
            UpdateAnchorPositions();
            
            // ロープシステムの長さを更新
            UpdateRopeSystemLength(targetLength);
            
            previousDistance = currentDistance;
        }
    }

    /// <summary>
    /// アンカーの位置を更新
    /// </summary>
    private void UpdateAnchorPositions()
    {
        if (startAnchor != null && startPoint != null)
        {
            Vector3 offset = startPoint.position - startAnchor.Position;
            if (offset.magnitude > updateThreshold)
            {
             //   ropeSystem.NudgeAnchor(startAnchor, offset);
            }
        }
        
        if (endAnchor != null && endPoint != null)
        {
            Vector3 offset = endPoint.position - endAnchor.Position;
            if (offset.magnitude > updateThreshold)
            {
               // ropeSystem.NudgeAnchor(endAnchor, offset);
            }
        }
    }

    /// <summary>
    /// RopeSystemの長さを更新
    /// </summary>
    /// <param name="targetLength">目標長さ</param>
    private void UpdateRopeSystemLength(float targetLength)
    {
        if (ropeSystem == null || startAnchor == null || endAnchor == null) return;

        // 現在の総長と目標長の差を計算
        float currentTotalLength = ropeSystem.TotalLength;
        float lengthDifference = targetLength - currentTotalLength;
        
        // 長さの調整が必要な場合
        if (Mathf.Abs(lengthDifference) > updateThreshold)
        {
            // アンカー間の制約を再設定
            //ropeSystem.SetupConstraint(startAnchor, endAnchor, targetLength);
        }
    }

    /// <summary>
    /// 開始点のアンカーを設定
    /// </summary>
    /// <param name="anchor">開始点のアンカー</param>
    /// <param name="transform">開始点のTransform</param>
    public void SetStartAnchor(RopeSystem.Anchor anchor, Transform transform = null)
    {
        // 既存のアンカーを削除（管理している場合のみ）
        if (managingAnchors && startAnchor != null)
        {
            ropeSystem.DestroyAnchor(startAnchor);
        }

        startAnchor = anchor;
        if (transform != null)
        {
            startPoint = transform;
        }
        
        managingAnchors = false;
        useExternalAnchors = true;
        
        if (isInitialized)
        {
            SetupConstraints();
        }
    }

    /// <summary>
    /// 終了点のアンカーを設定
    /// </summary>
    /// <param name="anchor">終了点のアンカー</param>
    /// <param name="transform">終了点のTransform</param>
    public void SetEndAnchor(RopeSystem.Anchor anchor, Transform transform = null)
    {
        // 既存のアンカーを削除（管理している場合のみ）
        if (managingAnchors && endAnchor != null)
        {
            ropeSystem.DestroyAnchor(endAnchor);
        }

        endAnchor = anchor;
        if (transform != null)
        {
            endPoint = transform;
        }
        
        managingAnchors = false;
        useExternalAnchors = true;
        
        if (isInitialized)
        {
            SetupConstraints();
        }
    }

    /// <summary>
    /// 両方のアンカーを同時に設定
    /// </summary>
    /// <param name="startAnchor">開始点のアンカー</param>
    /// <param name="endAnchor">終了点のアンカー</param>
    /// <param name="startTransform">開始点のTransform</param>
    /// <param name="endTransform">終了点のTransform</param>
    public void SetAnchors(RopeSystem.Anchor startAnchor, RopeSystem.Anchor endAnchor, Transform startTransform = null, Transform endTransform = null)
    {
        // 既存のアンカーを削除（管理している場合のみ）
        if (managingAnchors)
        {
            if (this.startAnchor != null)
            {
                ropeSystem.DestroyAnchor(this.startAnchor);
            }
            if (this.endAnchor != null)
            {
                ropeSystem.DestroyAnchor(this.endAnchor);
            }
        }

        this.startAnchor = startAnchor;
        this.endAnchor = endAnchor;
        
        if (startTransform != null) startPoint = startTransform;
        if (endTransform != null) endPoint = endTransform;
        
        managingAnchors = false;
        useExternalAnchors = true;
        
        if (isInitialized)
        {
            SetupConstraints();
        }
    }

    /// <summary>
    /// Transformのみを設定（アンカーは内部で作成）
    /// </summary>
    /// <param name="start">開始点のTransform</param>
    /// <param name="end">終了点のTransform</param>
    public void SetTransforms(Transform start, Transform end)
    {
        startPoint = start;
        endPoint = end;
        useExternalAnchors = false;
        
        if (isInitialized)
        {
            RecreateAnchors();
        }
    }

    /// <summary>
    /// アンカーを再作成
    /// </summary>
    private void RecreateAnchors()
    {
        // 既存のアンカーを削除（管理している場合のみ）
        if (managingAnchors)
        {
            if (startAnchor != null)
            {
                ropeSystem.DestroyAnchor(startAnchor);
            }
            if (endAnchor != null)
            {
                ropeSystem.DestroyAnchor(endAnchor);
            }
        }
        
        // 新しいアンカーを作成
        CreateAnchors();
        managingAnchors = true;
        SetupConstraints();
    }

    /// <summary>
    /// 開始点のアンカーを取得
    /// </summary>
    /// <returns>開始点のアンカー</returns>
    public RopeSystem.Anchor GetStartAnchor()
    {
        return startAnchor;
    }

    /// <summary>
    /// 終了点のアンカーを取得
    /// </summary>
    /// <returns>終了点のアンカー</returns>
    public RopeSystem.Anchor GetEndAnchor()
    {
        return endAnchor;
    }

    /// <summary>
    /// 現在の距離を取得
    /// </summary>
    /// <returns>現在の距離</returns>
    public float GetCurrentDistance()
    {
        return currentDistance;
    }

    /// <summary>
    /// RopeSystemの総長を取得
    /// </summary>
    /// <returns>RopeSystemの総長</returns>
    public float GetRopeSystemLength()
    {
        return ropeSystem != null ? ropeSystem.TotalLength : 0f;
    }

    /// <summary>
    /// 長さの倍率を設定
    /// </summary>
    /// <param name="multiplier">倍率</param>
    public void SetLengthMultiplier(float multiplier)
    {
        lengthMultiplier = Mathf.Max(0.1f, multiplier);
        UpdateRopeLength();
    }

    /// <summary>
    /// 外部アンカー使用モードを設定
    /// </summary>
    /// <param name="useExternal">外部アンカーを使用するかどうか</param>
    public void SetUseExternalAnchors(bool useExternal)
    {
        if (useExternalAnchors != useExternal)
        {
            useExternalAnchors = useExternal;
            if (isInitialized)
            {
                if (useExternal)
                {
                    // 内部アンカーを削除
                    if (managingAnchors)
                    {
                        RecreateAnchors();
                    }
                }
                else
                {
                    // 外部アンカーから内部アンカーに切り替え
                    RecreateAnchors();
                }
            }
        }
    }

    /// <summary>
    /// 手動でロープを更新
    /// </summary>
    public void ManualUpdate()
    {
        UpdateRopeLength();
    }

    void OnDestroy()
    {
        // 管理しているアンカーのみクリーンアップ
        if (managingAnchors && ropeSystem != null)
        {
            if (startAnchor != null)
            {
                ropeSystem.DestroyAnchor(startAnchor);
            }
            if (endAnchor != null)
            {
                ropeSystem.DestroyAnchor(endAnchor);
            }
        }
    }

    // デバッグ用の情報表示
    void OnDrawGizmosSelected()
    {
        if (startPoint != null && endPoint != null)
        {
            Gizmos.color = useExternalAnchors ? Color.blue : Color.green;
            Gizmos.DrawLine(startPoint.position, endPoint.position);
            
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(startPoint.position, 0.15f);
            Gizmos.DrawWireSphere(endPoint.position, 0.15f);
            
            // アンカー情報を表示
            if (startAnchor != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(startAnchor.Position, Vector3.one * 0.1f);
            }
            if (endAnchor != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(endAnchor.Position, Vector3.one * 0.1f);
                Gizmos.DrawWireCube(endAnchor.Position, Vector3.one * 0.1f);
            }
        }
    }
}
