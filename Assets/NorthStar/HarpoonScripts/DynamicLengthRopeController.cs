using UnityEngine;
using Meta.Utilities.Ropes;
using System.Reflection;
using System.Collections.Generic;
using System;

namespace NorthStar.HarpoonScripts
{
    /// <summary>
    /// RopeSystemの両端を2つのRigidbodyに接続し、それらの距離に応じてロープの全長を動的に変化させるコントローラー。
    /// </summary>
    [RequireComponent(typeof(RopeSystem))]
    public class DynamicLengthRopeController : MonoBehaviour
    {
        public enum RopeState
        {
            ShotWait,
            InShot,
            WallConnect,
        }
        [Header("アンカー設定")]
        [Tooltip("ロープの始点となるオブジェクト。Rigidbodyコンポーネントが必要です。")]
        [SerializeField] private Rigidbody m_startObject;

        [Tooltip("ロープの終点となるオブジェクト。Rigidbodyコンポーネントが必要です。")]
        [SerializeField] private Rigidbody m_endObject;

        [Header("ロープ設定")]
        [Tooltip("ロープのたるみ具合。0の場合、オブジェクト間の距離がそのままロープの長さになります。")]
        [SerializeField] private float m_ropeSlack = 1.0f;

        [Header("追加のピン設定")]
        [Tooltip("ロープの終点オブジェクト (m_endObject) を、このRigidbodyに固定します。")]
        [SerializeField] private Rigidbody m_pinEndObjectTo;

        private RopeSystem m_ropeSystem;
        private bool m_isInitialized = false;
        private FixedJoint m_endObjectPinJoint; // 終点オブジェクトに動的に追加するFixedJoint
        [SerializeField] private RopeState current = RopeState.ShotWait;

        private Dictionary<Rigidbody, int> m_anchorHash = new Dictionary<Rigidbody, int>();

        // --- リフレクション用のメンバー情報 ---
        private FieldInfo m_anchorsField;
        private FieldInfo m_totalLengthField;
        private FieldInfo m_spooledLengthField;
        private MethodInfo m_setupAnchorMethod;
        private MethodInfo m_setupConstraintMethod;
        private MethodInfo m_updateBurstRopeMethod;
        // ------------------------------------
        

        void Awake()
        {
            this.current = RopeState.ShotWait;
            m_anchorHash.Add(this.m_endObject,1);
            m_anchorHash.Add(this.m_startObject,0);
            Initialize();
           
          //  UpdateRope();
        }

        private void Start()
        {
            // privateメソッドを呼び出してアンカーを初期化
            
        }

        void FixedUpdate()
        { 
            if (!m_isInitialized) return;
           // return;
            UpdateRope();
        }

        void UpdateRope()
        {
            if (this.current != RopeState.InShot) return;
            // 2. 2つのオブジェクト間の現在の距離を計算
            float currentDistance = Vector3.Distance(m_startObject.transform.position, m_endObject.transform.position);

            // 3. ロープの全長を「現在の距離 + たるみ」で更新
            float newTotalLength = currentDistance + m_ropeSlack;
            
            // 5. アンカーと物理制約を更新
            UpdateAnchorsAndConstraints(newTotalLength);
            
            m_totalLengthField.SetValue(m_ropeSystem, newTotalLength);
            
            // 4. スプール（巻き取り）されている長さを0に設定
            m_spooledLengthField.SetValue(m_ropeSystem, 0f);

            // 6. BurstRopeシミュレーションに長さの変更を適用
            m_updateBurstRopeMethod.Invoke(m_ropeSystem, null);
        }

        /// <summary>
        /// RopeSystemを初期化し、アンカーを動的に設定します。
        /// </summary>
        [ContextMenu("Initialize Dynamic Length Rope")]
        public void Initialize()
        {
            if (!ValidateAndGetComponents()) return;

            InitializeReflectionMembers();
            m_isInitialized = true;
            Debug.Log("DynamicLengthRopeControllerが正常に初期化されました。");
        }

        /// <summary>
        /// 終点アンカーの位置と、アンカー間の物理的な制約（Joint）を更新します。
        /// </summary>
        private void UpdateAnchorsAndConstraints(float newTotalLength)
        {
            var anchorsList = m_anchorsField.GetValue(m_ropeSystem) as IList<RopeSystem.Anchor>;
            if (anchorsList == null || anchorsList.Count != 2) return;
            foreach (var anchor in anchorsList)
            {
                // 始点アンカーに接続されたConfigurableJointの距離制限を更新
                if (anchor.Constraint != null)
                {
                    var limit = anchor.Constraint.linearLimit;
                    limit.limit = newTotalLength;
                    anchor.Constraint.linearLimit = limit;
                }
            }
        }

        private void OnDrawGizmos()
        {
            var posA = this.transform.InverseTransformPoint(m_startObject.transform.position);
            var posB = this.transform.InverseTransformPoint(m_endObject.transform.position);
            
            Gizmos.DrawSphere(posA, 0.2f);
            Gizmos.DrawSphere(posB, 0.2f);
        }

        #region Helper Methods (Validation and Reflection)
        private bool ValidateAndGetComponents()
        {
            m_ropeSystem = GetComponent<RopeSystem>();
            if (m_startObject == null || m_endObject == null)
            {
                Debug.LogError("Start ObjectまたはEnd Objectが設定されていません。", this);
                return false;
            }
            if (m_startObject.GetComponent<Rigidbody>() == null || m_endObject.GetComponent<Rigidbody>() == null)
            {
                Debug.LogError("両端のオブジェクトにはRigidbodyが必要です。", this);
                return false;
            }
            return true;
        }

        private void InitializeReflectionMembers()
        {
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
        }
        
        #endregion
    }	
}
