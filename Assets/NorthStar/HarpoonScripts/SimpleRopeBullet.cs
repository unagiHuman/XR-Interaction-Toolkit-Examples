using System;
using UnityEngine;
using UnityEngine.Events;
using Meta.Utilities.Ropes;
using NorthStar;
using NorthStar.HarpoonScripts; // RopeSystemWrapperの名前空間

/// <summary>
/// 発射される弾の挙動を制御するシンプルなコンポーネント。
/// </summary>
public class SimpleRopeBullet : MonoBehaviour
{
    public UnityEvent OnBulletDisabled = new UnityEvent();

    public Joint HookJoint => m_hookJoint;

    [SerializeField]private Rigidbody m_hookTarget;

    [SerializeField] private Joint m_hookJoint;
   
    private RopeSystemWrapper m_ropeSystem;
    private RopeSystem.Anchor m_createdAnchor;
    private bool m_isFired = false;
    private CollisionEventDispatcher m_collisionEventDispatcher;

    private void Awake()
    {
        m_collisionEventDispatcher = GetComponentInChildren<CollisionEventDispatcher>();
        m_collisionEventDispatcher.OnCollisionEnterEvent = (Collision collision) => { OnCollisionEnter(collision); };
    }

    /// <summary>
    /// 弾を発射し、指定されたロープシステムにアンカーとして接続する。
    /// </summary>
    /// <param name="direction">発射方向</param>
    /// <param name="force">発射の力</param>
    /// <param name="ropeSystem">接続するロープシステム</param>
    /// <param name="anchorIndex">ロープのどのインデックスにアンカーを追加するか</param>
    public void Fire(Vector3 direction, float force, RopeSystemWrapper ropeSystem, Rigidbody pinToBullet, int anchorIndex)
    {
        if (m_isFired) return;
        m_isFired = true;

        m_ropeSystem = ropeSystem;
        // 物理的な力を加えて発射
        transform.forward = direction;
        m_hookJoint.connectedBody = pinToBullet;
        m_hookTarget.AddForce(direction * force, ForceMode.VelocityChange);
    }

    void OnCollisionEnter(Collision collision)
    {
        // 何かに衝突したら物理演算を止める
        if (!m_isFired) return;
        
        m_hookTarget.isKinematic = true;
        // gameObject.SetActive(false); // またはDestroy(gameObject);
    }

    public void ResetBullet()
    {
        this.m_hookJoint.connectedBody = null;
    }

    void OnDisable()
    {
        // 自身が非アクティブになったことをランチャーに通知
        OnBulletDisabled.Invoke();
        
        // 作成したアンカーを破棄（必要に応じて）
       // CleanupAnchor();
    }

    void OnDestroy()
    {
        // オブジェクトが破棄される際にもアンカーをクリーンアップ
      //  CleanupAnchor();
    }

    private void CleanupAnchor()
    {
        if (m_createdAnchor != null && m_ropeSystem != null)
        {
            // 個別のアンカー削除機能がない場合は、必要に応じて実装
            // または、ロープシステム側でアンカーの管理を行う
            m_createdAnchor = null;
           // m_ropeSystem.DestroyAllAnchors();
            m_ropeSystem = null;
        }
    }
}