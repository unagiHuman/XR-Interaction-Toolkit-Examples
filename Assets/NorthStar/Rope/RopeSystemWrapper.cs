using System;
using System.Reflection;
using System.Collections.Generic;
using Meta.Utilities.Ropes;
using UnityEngine;
using UnityEngine.EventSystems;
using XRInteractionNorthStar;
using Object = UnityEngine.Object;

namespace NorthStar
{
    public class RopeSystemWrapper:RopeSystem
    {
        // --- リフレクション用のメンバー情報 ---
        private FieldInfo m_anchorsField;
        private FieldInfo m_totalLengthField;
        private FieldInfo m_spooledLengthField;
        private MethodInfo m_setupAnchorMethod;
        private MethodInfo m_setupConstraintMethod;
        private MethodInfo m_updateBurstRopeMethod;
        
        private bool m_isInitialized = false;
        
        private bool m_isEnabled = false;

        private Vector3 m_pinbodyPos, m_bulletPos;

        private void Awake()
        {
            InitializeReflectionMembers();
           
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (m_isEnabled) return;
            SendRopeInstantiationEvent();
        }

        /// <summary>
        /// RopeInitMessageReceiverに対してRopeインスタンス化イベントを送信
        /// </summary>
        protected void SendRopeInstantiationEvent()
        {
            m_isEnabled = true;
            // シーン内のすべてのRopeInitMessageReceiverを取得
            var receivers = Object.FindObjectsByType<RopeInitMessageReceiver>(FindObjectsSortMode.None);
            
            if (receivers.Length == 0)
            {
                Debug.LogWarning("RopeInitMessageReceiverが見つかりませんでした。");
                return;
            }

            // 各ReceiverにEventSystemを通してメッセージを送信
            foreach (var receiver in receivers)
            {
                try
                {
                    // EventSystemが存在することを確認
                    if (EventSystem.current == null)
                    {
                        Debug.LogWarning("EventSystemが見つかりません。直接メソッド呼び出しを行います。");
                        receiver.OnRopeInstantiated(this.gameObject);
                        continue;
                    }

                    // カスタムイベントデータを作成
                    var eventData = new RopeInstantiationEventData(EventSystem.current)
                    {
                        RopeInstance = this.gameObject
                    };

                    // EventSystemを通してイベントを実行
                    ExecuteEvents.Execute<RopeInitMessageReceiver>(
                        receiver.gameObject,
                        eventData,
                        (handler, data) => handler.OnRopeInstantiated(((RopeInstantiationEventData)data).RopeInstance)
                    );

                    Debug.Log($"Ropeインスタンス化イベントを送信しました: {this.gameObject.name} -> {receiver.gameObject.name}");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"イベント送信中にエラーが発生しました: {ex.Message}");
                    // フォールバック: 直接メソッド呼び出し
                    receiver.OnRopeInstantiated(this.gameObject);
                }
            }
        }

        public void InitializeReflectionMembers()
        {
            if (m_isInitialized) return;
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
                m_isInitialized = false;
            }
            else
            {
                m_isInitialized = true;
            }
        }
        
        /// <summary>
        /// m_anchorsフィールドに直接Anchorを追加する
        /// </summary>
        /// <param name="anchor">追加するAnchor</param>
        public void AddAnchorToField(RopeSystem.Anchor anchor)
        {
            if (!m_isInitialized)
            {
                Debug.LogError("RopeSystemWrapperが初期化されていません。");
                return;
            }

            // m_anchorsフィールドの値を取得
            var anchorsList = m_anchorsField.GetValue(this) as IList<RopeSystem.Anchor>;
    
            if (anchorsList == null)
            {
                Debug.LogError("m_anchorsフィールドの取得に失敗しました。");
                return;
            }

            // リストにanchorを追加
            anchorsList.Add(anchor);
            m_anchorsField.SetValue(this, anchorsList);

            Debug.Log($"Anchorが追加されました。現在のAnchor数: {anchorsList.Count}");
        }


        public void SetAnchors(Rigidbody pinToBody, Rigidbody bullet)
        {
            m_pinbodyPos = pinToBody.transform.position;
            m_bulletPos = bullet.transform.position;
            DestroyAnchors();

            CreateAnchor(pinToBody);
            CreateAnchor(bullet);
        }

        public void DestroyAnchors()
        {
            var anchorsList = m_anchorsField.GetValue(this) as IList<RopeSystem.Anchor>;
            if (anchorsList != null)
            {
                anchorsList.Clear();
                m_anchorsField.SetValue(this, anchorsList);
            }
        }

        public void ResetPos(Rigidbody pinToBody, Rigidbody bullet)
        {
            pinToBody.transform.position = m_pinbodyPos;
            pinToBody.transform.localRotation = Quaternion.identity;
            bullet.transform.position = m_bulletPos;
            bullet.transform.localRotation = Quaternion.identity;
        }

        private void CreateAnchor(Rigidbody body)
        {
            var anchor = new Anchor
            {
                Type = AnchorType.Dynamic,
                Position = this.transform.InverseTransformPoint(body.position),
                Proportion = 0.5f,
                PinToRigidbody = body
            };
            AddAnchorToField(anchor);
            m_setupAnchorMethod.Invoke(this, new object[] { anchor });
        }
    }
}