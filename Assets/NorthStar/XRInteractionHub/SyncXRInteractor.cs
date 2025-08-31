using System;
using System.Collections;
using Meta.Utilities.Ropes;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace NorthStar.XRInteractionHub
{
    
    [RequireComponent(typeof(Rigidbody))]
    public class SyncXRInteractor:MonoBehaviour
    {
        
        public XRBaseInteractable SyncGroup => syncGroup;
        
        public float LocalMovementStrengthModifier = 1;
        public float LocalRotationStrengthModifier = 1;
        
        [SerializeField] private XRBaseInteractable syncGroup;
        
        [SerializeField] PhysicsRopeGrabAnchor anchor;
        
        [SerializeField] PhysicsTransformer physicsTransformer;

        [SerializeField] private CriticallyDampendSpringJoint criticallyDampendSpringJoint;
        
        [SerializeField] RopeSystem parentRopeSystem;

        [SerializeField] private float maxJointPower;
        
        private Rigidbody rigidbody;
        
        private IXRInteractor interactor;
        
        private RopeSystem.Anchor ropeGrabAnchor;
        
        private IEnumerator Start()
        {
            var handref = GetComponent<PhysicalHandRef>();
            this.syncGroup.selectEntered.AddListener((
                arg0 =>
                {
                    handref.Hand.StartInteraction();
                    Grab(arg0.interactorObject);
                }));
            
            this.syncGroup.selectExited.AddListener((
                arg0 =>
                {
                    handref.Hand.EndInteraction();
                    Release(arg0.interactorObject);
                }));
            this.rigidbody = GetComponent<Rigidbody>();
            this.rigidbody.isKinematic = true;
            this.rigidbody.useGravity = false;
            yield return null;
        }

        private void FixedUpdate()
        {
            UpdateDrive();
          
            if (this.ropeGrabAnchor != null)
            {
                var isLimitedByTension = false;
                if (this.parentRopeSystem.GetPrevAndNextAnchors(this.ropeGrabAnchor, out var prevAnchor,
                        out var nextAnchor))
                {
                    if (prevAnchor != null && nextAnchor != null)
                    {
                        isLimitedByTension = (IsMaxJointPower(prevAnchor) | IsMaxJointPower(nextAnchor));
                    }
                    else if (prevAnchor != null)
                    {
                        isLimitedByTension = IsMaxJointPower(prevAnchor);
                    }
                    else if (nextAnchor != null)
                    {
                        isLimitedByTension = IsMaxJointPower(nextAnchor);
                    }
                }
                if (isLimitedByTension)
                {
                    Debug.Log("isLimitedByTension");
                    //SyncPositions(this.syncGroup.transform.position, this.syncGroup.transform.rotation);
                    SyncPositions(this.interactor.transform.position, this.interactor.transform.rotation);
                }
                else
                {
                    SyncPositions(this.interactor.transform.position, this.interactor.transform.rotation);
                }
            }
            else
            {
                SyncPositions(this.syncGroup.transform.position, this.syncGroup.transform.rotation);
            }
        }

        private bool IsMaxJointPower(RopeSystem.Anchor anchor)
        {
            return anchor.CurrentTensionForce > this.maxJointPower;
        }

        private void Grab(IXRSelectInteractor interactor)
        {
            this.interactor = interactor;
            rigidbody.isKinematic = false;
            // PhysicsTransformerのAddInteractorを呼び出してグラブを開始します。
            // 第1引数: インタラクター（この手オブジェクト）
            // 第2引数: インタラクタブル（掴まれるオブジェクト）
            this.ropeGrabAnchor = physicsTransformer.AddInteractor(this.gameObject, physicsTransformer.gameObject);

            Debug.Log($"'{physicsTransformer.name}' を PhysicsTransformer を使ってグラブしました。");
        }

        private void Release(IXRSelectInteractor interactor)
        {
            this.interactor = null;
            this.rigidbody.isKinematic = true;
            // PhysicsTransformerのRemoveInteractorを呼び出してグラブを終了します。
            physicsTransformer.RemoveInteractor(this.gameObject);
            this.ropeGrabAnchor = null;
            Debug.Log($"'{physicsTransformer.name}' を解放しました。");
        }

        /// <summary>
        /// Synchronizes the position and rotation of the object with the specified target position and rotation values.
        /// </summary>
        /// <param name="targetPosition">The target position to synchronize to.</param>
        /// <param name="targetRotation">The target rotation to synchronize to.</param>
        private void SyncPositions(Vector3 targetPosition, Quaternion targetRotation)
        {
            criticallyDampendSpringJoint.TargetPoint = targetPosition;
            criticallyDampendSpringJoint.TargetRotation = targetRotation;
            
            if (GlobalSettings.PlayerSettings.MaxHandDistance != float.PositiveInfinity &&
                Vector3.Distance(transform.position, targetPosition) > GlobalSettings.PlayerSettings.MaxHandDistance)
            {
                this.rigidbody.position =targetPosition;
                transform.position = this.rigidbody.position;
            }

            criticallyDampendSpringJoint.AddForce();
        }
        
        private void UpdateDrive()
        {
            criticallyDampendSpringJoint.PositionSpring = LocalMovementStrengthModifier;
            criticallyDampendSpringJoint.RotationSpring = LocalRotationStrengthModifier;
        }
    }
}