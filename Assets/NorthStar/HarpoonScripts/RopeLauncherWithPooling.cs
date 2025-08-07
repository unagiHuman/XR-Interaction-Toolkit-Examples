using Meta.Utilities.Ropes;
using NorthStar;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// オブジェクトプーリングを利用してロープ付きの弾を発射するランチャー。
/// </summary>
public class RopeLauncherWithPooling : MonoBehaviour
{
    [Header("射出設定")]
    [Tooltip("発射される弾のプレハブ（SimpleRopeBulletコンポーネントを持つこと）")]
    [SerializeField] private GameObject m_bulletPrefab;

    [Tooltip("弾が発射される位置と向き")]
    [SerializeField] private Transform m_targetPoint;

    [Tooltip("弾を発射する際の力の強さ")]
    [SerializeField] private float m_firingForce = 20f;

    [Header("ロープ設定")]
    [Tooltip("ロープの始点を固定するアンカー（バインド）のインデックス")]
    [SerializeField] private int m_startBindIndex = 0;

    [Tooltip("射出した弾を接続するアンカー（バインド）のインデックス")]
    [SerializeField] private int m_endBindIndex = 1;

    [Tooltip("壁との距離に対するロープ長の倍率")]
    [SerializeField, Range(1f, 10.0f)] private float m_ropeLengthMultiplier = 2.5f;

    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable m_grabInteractable;
   
    private SimpleRopeBullet m_currentBullet;
    private PooledRope m_currentRope;
   
    void Awake()
    {
        m_grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
    }

    void OnEnable()
    {
        m_grabInteractable.selectEntered.AddListener(Fire);
    }

    void OnDisable()
    {
        m_grabInteractable.selectEntered.RemoveListener(Fire);
    }
    
    public void Fire(SelectEnterEventArgs args)
    {
        if (m_currentBullet != null) return;

        // 1. プールからロープを取得
        m_currentRope = RopePool.Instance.GetRope();
        if (m_currentRope == null)
        {
            Debug.LogError("ローププールからロープを取得できませんでした。");
            return;
        }

        // ロープをランチャーの位置に移動させ、アクティブにする
        m_currentRope.transform.position = m_targetPoint.position;
        m_currentRope.transform.rotation = m_targetPoint.rotation;
        m_currentRope.gameObject.SetActive(true);
        

        // ロープの始点をRopeTransformBinderでバインド
        UpdateStartBindWithTransformBinder(m_currentRope.GetComponent<RopeSystemWrapper>());

        // 2. 弾を生成
        GameObject bulletObject = Instantiate(m_bulletPrefab, m_targetPoint.position, m_targetPoint.rotation);
        m_currentBullet = bulletObject.GetComponent<SimpleRopeBullet>();

        // 3. 弾と取得したロープを接続して発射
        var rope = m_currentRope.GetComponent<RopeSystemWrapper>();
        rope.SetRopeLength(GetDistanceToWall()*m_ropeLengthMultiplier);
        rope.InitRopeSettings();
        m_currentBullet.Fire(m_targetPoint.forward, m_firingForce, rope,m_currentRope.HookBody, m_endBindIndex);
        m_currentBullet.OnBulletDisabled.AddListener(OnBulletDisabled);
    }

    // クラスのフィールドとして追加（再利用のため）
    private RaycastHit[] m_raycastHits = new RaycastHit[1];

    /// <summary>
    /// 前方にRaycastを行い、衝突した地点までの距離を取得する（Nonallocバージョン）
    /// </summary>
    /// <param name="maxDistance">最大検出距離</param>
    /// <returns>衝突地点までの距離。衝突しなかった場合は-1を返す</returns>
    public float GetDistanceToWall(float maxDistance = 100f)
    {
        // Nonallocバージョンを使用してRaycastを実行
        int hitCount = Physics.RaycastNonAlloc(m_targetPoint.position, m_targetPoint.forward, m_raycastHits, maxDistance);
    
        if (hitCount > 0)
        {
            return m_raycastHits[0].distance;
        }
    
        // 衝突がなかった場合は-1を返す
        return 1f;
    }

    /// <summary>
    /// 指定したレイヤーマスクで前方にRaycastを行い、衝突した地点までの距離を取得する（Nonallocバージョン）
    /// </summary>
    /// <param name="layerMask">検出対象のレイヤーマスク</param>
    /// <param name="maxDistance">最大検出距離</param>
    /// <returns>衝突地点までの距離。衝突しなかった場合は-1を返す</returns>
    public float GetDistanceToWall(LayerMask layerMask, float maxDistance = 100f)
    {
        // 指定レイヤーでNonallocバージョンを使用してRaycastを実行
        int hitCount = Physics.RaycastNonAlloc(m_targetPoint.position, m_targetPoint.forward, m_raycastHits, maxDistance, layerMask);
    
        if (hitCount > 0)
        {
            return m_raycastHits[0].distance;
        }
    
        // 衝突がなかった場合は-1を返す
        return 1f;
    }

    /// <summary>
    /// 前方にRaycastを行い、ヒット情報を含む詳細な結果を取得する（Nonallocバージョン）
    /// </summary>
    /// <param name="hitInfo">衝突情報</param>
    /// <param name="maxDistance">最大検出距離</param>
    /// <returns>衝突があった場合はtrue、なかった場合はfalse</returns>
    public bool GetRaycastHitInfo(out RaycastHit hitInfo, float maxDistance = 100f)
    {
        int hitCount = Physics.RaycastNonAlloc(m_targetPoint.position, m_targetPoint.forward, m_raycastHits, maxDistance);
    
        if (hitCount > 0)
        {
            hitInfo = m_raycastHits[0];
            return true;
        }
    
        hitInfo = default(RaycastHit);
        return false;
    }

    /// <summary>
    /// 複数のヒット結果を取得したい場合のメソッド（Nonallocバージョン）
    /// </summary>
    /// <param name="hits">ヒット結果を格納する配列</param>
    /// <param name="maxDistance">最大検出距離</param>
    /// <returns>ヒットした数</returns>
    public int GetRaycastHitsNonAlloc(RaycastHit[] hits, float maxDistance = 100f)
    {
        return Physics.RaycastNonAlloc(m_targetPoint.position, m_targetPoint.forward, hits, maxDistance);
    }

    /// <summary>
    /// 複数のヒット結果を取得したい場合のメソッド（レイヤーマスク指定、Nonallocバージョン）
    /// </summary>
    /// <param name="hits">ヒット結果を格納する配列</param>
    /// <param name="layerMask">検出対象のレイヤーマスク</param>
    /// <param name="maxDistance">最大検出距離</param>
    /// <returns>ヒットした数</returns>
    public int GetRaycastHitsNonAlloc(RaycastHit[] hits, LayerMask layerMask, float maxDistance = 100f)
    {
        return Physics.RaycastNonAlloc(m_targetPoint.position, m_targetPoint.forward, hits, maxDistance, layerMask);
    }

    float GetDistanceFromToWall()
    {
        return GetDistanceToWall();
    }

    private void UpdateStartBindWithTransformBinder(RopeSystemWrapper rope)
    {
        // RopeTransformBinderを設定してロープの始点と接続
        //rope.InitCreateAnchorViaRopeSimWithPin(this.m_targetPoint.position, RopeSystem.AnchorType.Dynamic, this.GetComponent<Rigidbody>(), 0, null);
    }

    private void OnBulletDisabled()
    {
        if (m_currentBullet != null)
        {
            m_currentBullet.OnBulletDisabled.RemoveListener(OnBulletDisabled);
        }
        
        // 4. 使用済みのロープをプールに戻す
        if (m_currentRope != null)
        {
            RopePool.Instance.ReturnRope(m_currentRope);
        }

        m_currentBullet = null;
        m_currentRope = null;
    }
}