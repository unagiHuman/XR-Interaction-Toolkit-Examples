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
        
        [SerializeField] private XRBaseInteractable syncGroup;
        
        [SerializeField] PhysicsRopeGrabAnchor anchor;
        
        [SerializeField] PhysicsTransformer physicsTransformer;
        
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
            var wait = new WaitForFixedUpdate();
            while (true)
            {
                if (this.ropeGrabAnchor != null)
                {
                    if (this.ropeGrabAnchor.IsLimitedByTension)
                    {
                        this.rigidbody.MovePosition(this.syncGroup.transform.position);
                        this.rigidbody.MoveRotation(this.syncGroup.transform.rotation); 
                       
                    }
                    else
                    {
                        this.rigidbody.MovePosition(this.interactor.transform.position);
                        this.rigidbody.MoveRotation(this.interactor.transform.rotation); 
                    }
                   
                }
                else
                {
                    this.rigidbody.MovePosition(this.syncGroup.transform.position);
                    this.rigidbody.MoveRotation(this.syncGroup.transform.rotation); 
                }
                
                yield return wait;
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
    }
}