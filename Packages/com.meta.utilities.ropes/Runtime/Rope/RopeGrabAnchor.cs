// Copyright (c) Meta Platforms, Inc. and affiliates.
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using UnityEngine;
using UnityEngine.Events;

namespace Meta.Utilities.Ropes
{
    /// <summary>
    /// Proxy for the rope system to detect and handle grabbing the rope using hands
    /// </summary>
    public class RopeGrabAnchor : MonoBehaviour
    {
        [SerializeField] protected RopeSystem m_ropeSystem;
        [SerializeField, AutoSet] protected Rigidbody m_body;
        [SerializeField] protected Vector3 m_gripAxis;
        [SerializeField] protected float m_gripWidth;
        [SerializeField] private bool isDebugNotFollow = false;
        
        #if UNITY_EDITOR
        [SerializeField]
        GameObject interactorObject;

        [SerializeField] private bool isDebugGrab = false;
        
        // inspectorからテスト用にボタンを作成
        [ContextMenu("Grab Button Event")]
        public void TestButtonEvent()
        {
            m_anchor = m_ropeSystem.CreateAnchorViaRopeSim(transform.position, RopeSystem.AnchorType.Dynamic, gameObject);
            if (m_anchor is not null)
            {
                Grabbed = true;
                m_ropeSystem.OnRopeGrabbed();
                m_invertedGrip = false;

                if (m_ropeSystem.GetPrevAndNextAnchors(m_anchor, out var prevAnchor, out var nextAnchor))
                {
                    var ropeDir = m_ropeSystem.transform.TransformDirection((nextAnchor.Position - prevAnchor.Position).normalized);
                    m_invertedGrip = Vector3.Dot(ropeDir, WorldBindAxis) > 0.0f;
                }

                m_anchor.Position = transform.position;
            }
            else
            {
                Debug.LogWarning("anchor is null");
            }
            m_body.isKinematic = false;
            Debug.Log("Test button pressed!");
        }

        
        #endif
        protected Quaternion m_handRotationOffset;

        protected void Awake()
        {
            var interactable =this.transform;
            m_handRotationOffset = Quaternion.Inverse(transform.rotation) * interactable.transform.rotation;
        }

        public bool Grabbed { get; protected set; } = false;

        public Transform Hand;

        protected RopeSystem.Anchor m_anchor;
        protected bool m_invertedGrip;

        public Vector3 WorldBindAxis => Hand.transform.TransformDirection(m_gripAxis).normalized;

        //SyncXRInteractor Grabとのタイミングが重要
        public RopeSystem.Anchor Grab(GameObject interactor)
        {

            this.interactorObject = interactor;
           
            if (Grabbed) return m_anchor;
            if (isDebugGrab)
            {
                Grabbed = true;
                return m_anchor;
            }
           
            m_anchor = m_ropeSystem.CreateAnchorViaRopeSim(transform.position, RopeSystem.AnchorType.Dynamic, gameObject);
            if (m_anchor is not null)
            {
                Grabbed = true;
                m_ropeSystem.OnRopeGrabbed();
                m_invertedGrip = false;

                if (m_ropeSystem.GetPrevAndNextAnchors(m_anchor, out var prevAnchor, out var nextAnchor))
                {
                    var ropeDir = m_ropeSystem.transform.TransformDirection((nextAnchor.Position - prevAnchor.Position).normalized);
                    m_invertedGrip = Vector3.Dot(ropeDir, WorldBindAxis) > 0.0f;
                }
            }
            else
            {
                Debug.LogWarning("anchor is null");
            }
            m_body.isKinematic = false;
            return m_anchor;
        }

        public RopeSystem.Anchor EndGrab(GameObject interactor)
        {
            if (!Grabbed) return null;
            Grabbed = false;
            if(m_anchor==null) return null;
            m_ropeSystem.DestroyAnchor(m_anchor);
            m_ropeSystem.OnRopeReleased();
            return null;
        }

        protected void Update()
        {
            if(this.Hand==null) return;
            /*
            if (m_anchor == null)
            {
                Grabbed = false;
                m_anchor = null;
            }
            */

            if (Grabbed)
            {
                m_anchor.BindAxis = m_ropeSystem.transform.InverseTransformDirection(m_invertedGrip ? -WorldBindAxis : WorldBindAxis);
                m_anchor.BindDistance = m_gripWidth / m_ropeSystem.TotalLength;
                m_body.isKinematic = false;
            }
            else
            {
                m_body.isKinematic = true;
                if (isDebugNotFollow) return;
                
                transform.position = m_ropeSystem.RopeSimulation.ClosestPointOnRope(Hand.transform.position);
                transform.rotation = Hand.transform.rotation * Quaternion.Inverse(m_handRotationOffset);
            }
        }
    }
}
