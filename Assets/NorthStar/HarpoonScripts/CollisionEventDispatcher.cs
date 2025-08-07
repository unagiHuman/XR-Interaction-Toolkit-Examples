using System;
using UnityEngine;

namespace NorthStar.HarpoonScripts
{
    public class CollisionEventDispatcher : MonoBehaviour
    {
        // 衝突開始時のイベント
        public  Action<Collision> OnCollisionEnterEvent;
    
        // 衝突継続中のイベント（オプション）
        public  Action<Collision> OnCollisionStayEvent;
    
        // 衝突終了時のイベント（オプション）
        public  Action<Collision> OnCollisionExitEvent;

        // Unity標準の衝突開始メソッド
        private void OnCollisionEnter(Collision collision)
        {
            // イベントが登録されている場合のみ実行
            OnCollisionEnterEvent?.Invoke(collision);
        }

        // Unity標準の衝突継続メソッド
        private void OnCollisionStay(Collision collision)
        {
            OnCollisionStayEvent?.Invoke(collision);
        }

        // Unity標準の衝突終了メソッド
        private void OnCollisionExit(Collision collision)
        {
            OnCollisionExitEvent?.Invoke(collision);
        }

        // イベントを手動で登録するメソッド
        public void SubscribeToCollisionEnter(Action<Collision> callback)
        {
            OnCollisionEnterEvent += callback;
        }

        // イベントの登録を解除するメソッド
        public void UnsubscribeFromCollisionEnter(Action<Collision> callback)
        {
            OnCollisionEnterEvent -= callback;
        }

        // すべてのイベント登録を解除
        public void ClearAllEvents()
        {
            OnCollisionEnterEvent = null;
            OnCollisionStayEvent = null;
            OnCollisionExitEvent = null;
        }
    }
}
