using Meta.Utilities.Ropes;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace XRInteractionNorthStar
{
    /// <summary>
    /// Ropeのインスタンス化時のメッセージを受け取るクラス
    /// UnityのEventSystemを使用してメッセージを処理する
    /// </summary>
    public class RopeInitMessageReceiver : MonoBehaviour, IEventSystemHandler
    {
        
        [System.Serializable]
        public class PooledRopeEvent : UnityEvent<RopeSystem>
        {
            
        }

        public PooledRopeEvent OnCreateRopeEvent => onCreateRopeEvent;

        [SerializeField] private PooledRopeEvent onCreateRopeEvent;
        /// <summary>
        /// Ropeがインスタンス化された時に呼び出されるイベント
        /// </summary>
        /// <param name="ropeInstance">インスタンス化されたRopeのGameObject</param>
        public void OnRopeInstantiated(GameObject ropeInstance)
        {
            Debug.Log($"Rope instantiated: {ropeInstance.name}");
            
            // ここでRopeのインスタンス化後の処理を実装
            HandleRopeInstantiation(ropeInstance);
        }
        
        /// <summary>
        /// Ropeインスタンス化時の具体的な処理
        /// </summary>
        /// <param name="ropeInstance">インスタンス化されたRopeのGameObject</param>
        private void HandleRopeInstantiation(GameObject ropeInstance)
        {
            // PooledRopeコンポーネントの取得
            var pooledRope = ropeInstance.GetComponent<RopeSystem>();
            if (pooledRope != null)
            {
                Debug.Log($"PooledRope component found on {ropeInstance.name}");
                // 必要に応じてPooledRopeに対する初期化処理を追加
            }
            
            // その他の初期化処理をここに追加
            // 例: UI更新、サウンド再生、エフェクト処理など
            onCreateRopeEvent.Invoke(pooledRope);
        }
        
        /// <summary>
        /// 複数のRopeが同時にインスタンス化された場合のイベント
        /// </summary>
        /// <param name="ropeInstances">インスタンス化されたRopeのGameObject配列</param>
        public void OnMultipleRopesInstantiated(GameObject[] ropeInstances)
        {
            Debug.Log($"Multiple ropes instantiated: {ropeInstances.Length} ropes");
            
            foreach (var rope in ropeInstances)
            {
                HandleRopeInstantiation(rope);
            }
        }
    }
    
    /// <summary>
    /// Ropeインスタンス化イベントデータ
    /// EventSystemで送信するためのカスタムイベントデータクラス
    /// </summary>
    public class RopeInstantiationEventData : BaseEventData
    {
        public GameObject RopeInstance { get; set; }
        public GameObject[] MultipleRopeInstances { get; set; }
        
        public RopeInstantiationEventData(EventSystem eventSystem) : base(eventSystem)
        {
        }
    }
}