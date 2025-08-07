using System;
using Meta.Utilities.Ropes;
using NorthStar;
using UnityEngine;

namespace XRInteractionNorthStar
{
    [DefaultExecutionOrder(-32000)]  // 最も早い実行順序
    public class XRRopeDependencyResolver:MonoBehaviour
    {
        [SerializeField] private RopeInitMessageReceiver ropeInitMessageReceiver;
      //  [SerializeField] private GrabAnchorHandsSetter grabAnchorHandsSetter;
        [SerializeField] private CachedHandTransformGetter cachedHandTransformGetter;
        private void Awake()
        {
            ropeInitMessageReceiver.OnCreateRopeEvent.AddListener(OnInitRope);
        }

        private void OnInitRope(RopeSystem rope)
        {
            var grabAnchorHandsSetter = rope.GetComponent<GrabAnchorHandsSetter>();
            if(grabAnchorHandsSetter==null) return;
            var left = cachedHandTransformGetter.LeftHandTransform;
            var right = cachedHandTransformGetter.RightHandTransform;
            grabAnchorHandsSetter.SetBothHandTransforms(left,right);
        }

        private void OnDestroy()
        {
            ropeInitMessageReceiver.OnCreateRopeEvent.RemoveListener(OnInitRope);
        }
    }
}