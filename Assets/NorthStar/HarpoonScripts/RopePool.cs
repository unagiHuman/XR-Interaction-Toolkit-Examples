using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ロープオブジェクトのプーリングを管理するシングルトンクラス。
/// </summary>
public class RopePool : MonoBehaviour
{
    public static RopePool Instance { get; private set; }

    [Header("プール設定")]
    [Tooltip("プーリング対象のローププレハブ（PooledRopeコンポーネントが必要）")]
    [SerializeField] private PooledRope m_ropePrefab;

    [Tooltip("最初に生成しておくロープの数")]
    [SerializeField] private int m_poolSize = 5;

    private List<PooledRope> m_pooledRopes;

    void Awake()
    {
        // シングルトンの設定
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }

        InitializePool();
    }

    /// <summary>
    /// プールを初期化し、指定された数のロープを生成する。
    /// </summary>
    private void InitializePool()
    {
        m_pooledRopes = new List<PooledRope>();
        for (int i = 0; i < m_poolSize; i++)
        {
            CreateNewRope();
        }
    }

    /// <summary>
    /// 新しいロープを生成し、非アクティブ状態でプールに追加する。
    /// </summary>
    /// <returns>生成されたロープのPooledRopeコンポーネント</returns>
    private PooledRope CreateNewRope()
    {
        var ropeObject = Instantiate(m_ropePrefab, transform);
        var pooledRope = ropeObject;
        if (pooledRope == null)
        {
            Debug.LogError("Rope PrefabにPooledRopeコンポーネントがありません！");
            return null;
        }
        ropeObject.gameObject.SetActive(false); // 初期状態では非アクティブ
        m_pooledRopes.Add(pooledRope);
        return pooledRope;
    }

    /// <summary>
    /// プールから利用可能なロープを取得する。
    /// </summary>
    /// <returns>利用可能なロープ。なければnullを返す。</returns>
    public PooledRope GetRope()
    {
        // プール内の非アクティブなロープを探す
        foreach (var rope in m_pooledRopes)
        {
            if (!rope.gameObject.activeInHierarchy)
            {
                return rope;
            }
        }

        // 利用可能なロープがない場合、新しく生成する（オプション）
        Debug.LogWarning("プールが枯渇しました。新しいロープを生成します。");
        return CreateNewRope();
    }

    /// <summary>
    /// 使用済みのロープをプールに戻す。
    /// </summary>
    /// <param name="rope">戻すロープ</param>
    public void ReturnRope(PooledRope rope)
    {
        if (rope != null)
        {
            rope.ResetRope();
            rope.gameObject.SetActive(false);
        }
    }
}
