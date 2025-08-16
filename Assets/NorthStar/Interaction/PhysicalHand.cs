// Copyright (c) Meta Platforms, Inc. and affiliates.
using Oculus.Interaction;
using Oculus.Interaction.Input;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Events;

namespace NorthStar
{
    /// <summary>
    /// Script that handles moving the hand objects as physical objects
    /// XR Interaction Toolkit 連携のため HandGrabInteractor 依存を排除
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PhysicalHand : MonoBehaviour
    {
        [SerializeField] private Transform m_handAnchor;
        [SerializeField] private bool m_boundToParent;
        [SerializeField] private Rigidbody m_parent;
        private Vector3 m_lastBodyPosition;
        [SerializeField, Interface(typeof(IHand))] private Object m_handObject;

        public HandColliders Colliders { get; private set; }

        public float LocalMovementStrengthModifier = 1;
        public float LocalRotationStrengthModifier = 1;

        private ConfigurableJoint m_joint, m_rotationJoint;
        public CriticallyDampendSpringJoint DampendSpringJoint;
        private bool m_connected;

        public Vector3 JointAnchor => m_jointAnchor;
        [SerializeField] private Vector3 m_jointAnchor;
        public Vector3 JointAnchorRotation => m_jointAnchorRotation;
        [SerializeField] private Vector3 m_jointAnchorRotation;

        // XRIT 側で利用できる「現在つかみ可能か」のフラグ（以前の HandGrabInteractor.enabled 相当）
        public bool IsInteractionAllowed { get; private set; }
        [SerializeField] private UnityEvent<bool> m_onInteractionAllowedChanged;

        public Rigidbody Rigidbody { get; private set; }
        public Rigidbody WristBody { get; private set; }
        private float m_breakTimer;
        public float ExcessBreakTimer = 0;

        private void Awake()
        {
            m_joint = GetComponent<ConfigurableJoint>();
            m_rotationJoint = GetComponent<ConfigurableJoint>(); // 必要なら別のジョイントに差し替え
            Colliders = GetComponent<HandColliders>();
            Rigidbody = GetComponent<Rigidbody>();
            WristBody = m_rotationJoint.GetComponent<Rigidbody>();
            if (DampendSpringJoint == null) this.gameObject.AddComponent<CriticallyDampendSpringJoint>();
        }

        private bool GetHandEnabled()
        {
            // XRIT の手検出に置き換える場合はここで状態を返す
            return true;
        }

        private void OnHandConnected()
        {
            Rigidbody.position = m_handAnchor.position;
            Rigidbody.rotation = m_handAnchor.rotation;
            Rigidbody.isKinematic = false;
            m_connected = true;
            UpdateInteractionAllowed();
        }

        private void OnHandDisconnected()
        {
            Rigidbody.isKinematic = true;
            m_connected = false;
            UpdateInteractionAllowed();
        }

        public void StartInteraction()
        {
            Colliders.enabled = false;
        }

        public void EndInteraction()
        {
            Colliders.enabled = true;
        }

        private void UpdateDrive()
        {
            DampendSpringJoint.PositionSpring = LocalMovementStrengthModifier;
            DampendSpringJoint.RotationSpring = LocalRotationStrengthModifier;
        }

        private void OnValidate()
        {
            m_joint = GetComponent<ConfigurableJoint>();
            m_joint.connectedBody = m_boundToParent ? m_parent : null;
        }

        private void SyncPositions()
        {
            DampendSpringJoint.TargetPoint = m_boundToParent ? (m_handAnchor.position - m_parent.position) : m_handAnchor.position;
            DampendSpringJoint.TargetRotation = m_handAnchor.rotation;

            m_rotationJoint.targetRotation = m_handAnchor.rotation;

            if (GlobalSettings.PlayerSettings.MaxHandDistance != float.PositiveInfinity &&
                Vector3.Distance(transform.position, m_handAnchor.position) > GlobalSettings.PlayerSettings.MaxHandDistance)
            {
                Rigidbody.position = m_handAnchor.position;
                transform.position = Rigidbody.position;
            }

            DampendSpringJoint.AddForce();
        }

        private void Update()
        {
            // 解除タイマー（XRIT 側で参照して強制解除などに利用可能）
            m_breakTimer += Time.deltaTime;
            m_breakTimer = Mathf.Clamp(m_breakTimer, 0.0f, GlobalSettings.PlayerSettings.HandBreakTimeout + ExcessBreakTimer);

            if (!GetHandEnabled())
            {
                Rigidbody.position = m_handAnchor.position;
                Rigidbody.rotation = m_handAnchor.rotation;
                if (m_connected)
                    OnHandDisconnected();
                return;
            }

            if (!m_connected)
                OnHandConnected();

            UpdateInteractionAllowed();
        }

        private void UpdateInteractionAllowed()
        {
            var allowed = m_breakTimer < GlobalSettings.PlayerSettings.HandBreakTimeout + ExcessBreakTimer && m_connected;
            if (allowed != IsInteractionAllowed)
            {
                IsInteractionAllowed = allowed;
                m_onInteractionAllowedChanged?.Invoke(allowed); // XRIT ブリッジ側で受け取り可
            }
        }

        public void ForceDropHeldItem()
        {
            if (!GlobalSettings.PlayerSettings.AllowForcedHandBreak)
                return;

            m_breakTimer = GlobalSettings.PlayerSettings.HandBreakTimeout + ExcessBreakTimer;
            UpdateInteractionAllowed();
        }

        private void FixedUpdate()
        {
            UpdateDrive();
            SyncPositions();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position + transform.rotation * m_jointAnchor, .01f);
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position + transform.rotation * m_jointAnchor, transform.rotation * Quaternion.Euler(m_jointAnchorRotation) * Vector3.forward);
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position + transform.rotation * m_jointAnchor, transform.rotation * Quaternion.Euler(m_jointAnchorRotation) * Vector3.right);
            Gizmos.color = Color.green;
            Gizmos.DrawRay(transform.position + transform.rotation * m_jointAnchor, transform.rotation * Quaternion.Euler(m_jointAnchorRotation) * Vector3.up);
        }
    }
}