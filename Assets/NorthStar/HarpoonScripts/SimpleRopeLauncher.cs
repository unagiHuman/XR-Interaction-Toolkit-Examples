using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using Meta.Utilities.Ropes; // RopeTransformBinderとBurstRopeの名前空間

/// <summary>
/// XRITの入力に応じて、ロープ付きの弾を発射するシンプルなランチャー。
/// </summary>
[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
public class SimpleRopeLauncher : MonoBehaviour
{
    [Header("射出設定")]
    [Tooltip("発射される弾のプレハブ（SimpleRopeBulletコンポーネントを持つこと）")]
    [SerializeField] private GameObject m_bulletPrefab;

    [Tooltip("弾が発射される位置と向き")]
    [SerializeField] private Transform m_spawnPoint;

    [Tooltip("弾を発射する際の力の強さ")]
    [SerializeField] private float m_firingForce = 20f;

    [Header("ロープ設定")]
    [Tooltip("接続するBurstRopeコンポーネント")]
    [SerializeField] private BurstRope m_rope;

    [Tooltip("ロープの始点を固定するアンカー（バインド）のインデックス")]
    [SerializeField] private int m_startBindIndex = 0;

    [Tooltip("射出した弾を接続するアンカー（バインド）のインデックス")]
    [SerializeField] private int m_endBindIndex = 1;

    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable m_grabInteractable;
    private SimpleRopeBullet m_currentBullet;

    void Awake()
    {
        m_grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();

        if (m_rope == null || m_bulletPrefab == null || m_spawnPoint == null)
        {
            Debug.LogError("必要なコンポーネントやプレハブが設定されていません。", this);
            enabled = false;
        }
    }

    void OnEnable()
    {
        m_grabInteractable.activated.AddListener(Fire);
        // ロープの始点をランチャー本体に固定
        UpdateStartBind();
    }

    void OnDisable()
    {
        m_grabInteractable.activated.RemoveListener(Fire);
    }

    private void UpdateStartBind()
    {
        if (m_rope.Binds.Count <= m_startBindIndex) return;

        var startBind = m_rope.Binds[m_startBindIndex];
        startBind.Index = 0; // ロープの最初のノード
        startBind.Target = m_rope.ToRopeSpace(transform.position);
        startBind.Bound = true;
        m_rope.Binds[m_startBindIndex] = startBind;
    }

    /// <summary>
    /// 弾を発射する。
    /// </summary>
    public void Fire(ActivateEventArgs args)
    {
        // 既に弾が発射されている場合は何もしない
        if (m_currentBullet != null)
        {
            Debug.Log("リロード待機中です。");
            return;
        }

        // 弾を生成
        GameObject bulletObject = Instantiate(m_bulletPrefab, m_spawnPoint.position, m_spawnPoint.rotation);
        m_currentBullet = bulletObject.GetComponent<SimpleRopeBullet>();

        if (m_currentBullet == null)
        {
            Debug.LogError("発射したプレハブにSimpleRopeBulletコンポーネントがありません。");
            Destroy(bulletObject);
            return;
        }

        // 弾とロープを接続し、発射
       // m_currentBullet.Fire(m_spawnPoint.forward, m_firingForce, m_rope, m_endBindIndex);
        m_currentBullet.OnBulletDisabled.AddListener(OnBulletDisabled);
    }
    
    /// <summary>
    /// 弾が非アクティブになった（何かに当たるなど）時に呼ばれる。
    /// </summary>
    private void OnBulletDisabled()
    {
        if (m_currentBullet != null)
        {
            m_currentBullet.OnBulletDisabled.RemoveListener(OnBulletDisabled);
        }
        m_currentBullet = null;
        Debug.Log("リロード完了。再発射可能です。");
    }
}
