using UnityEngine;
using Meta.Utilities.Ropes;
using NorthStar; // BurstRopeの名前空間

/// <summary>
/// プールされる各ロープにアタッチされるコンポーネント。
/// 自身の状態リセットなどを管理する。
/// </summary>
[RequireComponent(typeof(RopeSystem))]
public class PooledRope : MonoBehaviour
{
    public Rigidbody PinToBody { get => m_pinToBody; }
    public Rigidbody HookBody { get => m_HookBody; }

    public RopeSystem Rope => this.ropeSystem;
    [SerializeField] private Rigidbody m_pinToBody;
    [SerializeField] private Rigidbody m_HookBody;
    [SerializeField] private RopeSystemWrapper ropeSystem;
    private Vector3 m_initialPosition;

  
    void Awake()
    {
        if(ropeSystem==null) ropeSystem = GetComponent<RopeSystemWrapper>();
        
        ropeSystem.InitializeReflectionMembers();
        m_initialPosition = transform.position;
        //ropeSystem.DestroyAnchors();
        ///ropeSystem.SetAnchors(m_pinToBody, m_HookBody);
        //ropeSystem.RopeSimulation.Simulate(1);
    }
    
    /// <summary>
    /// このロープをプールに戻す際に呼び出される。
    /// </summary>
    public void ResetRope()
    {
        // ロープの位置を初期位置に戻す
        transform.position = m_initialPosition;
        transform.rotation = Quaternion.identity;
        ropeSystem.DestroyAnchors();
        ropeSystem.ResetPos(m_pinToBody,m_HookBody);
        // 全てのバインドを解除
        if (ropeSystem.RopeSimulation != null)
        {
            for (int i = 0; i < ropeSystem.RopeSimulation.Binds.Count; i++)
            {
                var bind = ropeSystem.RopeSimulation.Binds[i];
                bind.Bound = false;
                ropeSystem.RopeSimulation.Binds[i] = bind;
            }
        }
        ropeSystem.SetAnchors(m_pinToBody, m_HookBody);
       // ropeSystem.RopeSimulation.Simulate(1);
       // ropeSystem.RopeSimulation.Simulate(1);
        // 必要に応じて、物理シミュレーションのリセット処理などをここに追加する
        // 例: m_burstRope.Simulate(1); // 1フレームシミュレーションして状態をリセット
    }
}
