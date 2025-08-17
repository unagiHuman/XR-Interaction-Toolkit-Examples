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
        
        private Rigidbody rigidbody;
        
        private IXRInteractor interactor;
        
        private RopeSystem.Anchor ropeGrabAnchor;
        
        private IEnumerator Start()
        {
            this.syncGroup.selectEntered.AddListener((
                arg0 =>
                {
                    Grab(arg0.interactorObject);
                }));
            
            this.syncGroup.selectExited.AddListener((
                arg0 =>
                {
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
                if (this.ropeGrabAnchor.IsLimitedByTension)
                {
                    SyncPositions(this.syncGroup.transform.position, this.syncGroup.transform.rotation);
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