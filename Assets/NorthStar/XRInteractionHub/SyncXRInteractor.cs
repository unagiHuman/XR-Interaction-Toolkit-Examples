using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace NorthStar.XRInteractionHub
{
    
    [RequireComponent(typeof(Rigidbody))]
    public class SyncXRInteractor:MonoBehaviour
    {
        
        public XRBaseInteractor SyncGroup => syncGroup;
        
        [SerializeField] private XRBaseInteractor syncGroup;
        
        [SerializeField] PhysicsRopeGrabAnchor anchor;
        
        [SerializeField] PhysicsTransformer physicsTransformer;
        
        private Rigidbody rigidbody;

        private void Awake()
        {
            this.anchor.Hand = this.transform;
        }

        private IEnumerator Start()
        {
            this.syncGroup.selectEntered.AddListener((
                arg0 =>
                {
                    Grab();
                }));
            
            this.syncGroup.selectExited.AddListener((
                arg0 =>
                {
                    Release();
                }));
            this.rigidbody = GetComponent<Rigidbody>();
            this.rigidbody.isKinematic = true;
            this.rigidbody.useGravity = false;
            var wait = new WaitForFixedUpdate();
            while (true)
            {
                var diffpos = this.rigidbody.transform.position - syncGroup.transform.position;
                //this.rigidbody.linearVelocity = diffpos / Time.fixedDeltaTime;
                this.rigidbody.MovePosition(this.syncGroup.transform.position);
                this.rigidbody.MoveRotation(this.syncGroup.transform.rotation);
              //  this.rigidbody.transform.SetPositionAndRotation(syncGroup.transform.position, syncGroup.transform.rotation);
                yield return wait;
            }
        }
        
        private void Grab()
        {
            rigidbody.isKinematic = false;
            // PhysicsTransformerのAddInteractorを呼び出してグラブを開始します。
            // 第1引数: インタラクター（この手オブジェクト）
            // 第2引数: インタラクタブル（掴まれるオブジェクト）
            physicsTransformer.AddInteractor(this.gameObject, physicsTransformer.gameObject);

            Debug.Log($"'{physicsTransformer.name}' を PhysicsTransformer を使ってグラブしました。");
        }

        private void Release()
        {
            this.rigidbody.isKinematic = true;
            // PhysicsTransformerのRemoveInteractorを呼び出してグラブを終了します。
            physicsTransformer.RemoveInteractor(this.gameObject);

            Debug.Log($"'{physicsTransformer.name}' を解放しました。");
        }
    }
}