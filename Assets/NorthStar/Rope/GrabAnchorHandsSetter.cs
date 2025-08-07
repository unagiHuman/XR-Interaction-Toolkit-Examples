using UnityEngine;

namespace NorthStar
{
    /// <summary>
    /// PhysicsRopeGrabAnchorのHandプロパティに外部のTransformをセットする機能を提供するクラス
    /// </summary>
    public class GrabAnchorHandsSetter : MonoBehaviour
    {
        [Header("Grab Anchors")]
        [SerializeField] private PhysicsRopeGrabAnchor m_grabLeftAnchor;
        [SerializeField] private PhysicsRopeGrabAnchor m_grabRightAnchor;
        
        /// <summary>
        /// 左右のHandTransformを対応するAnchorにセットする
        /// </summary>
        /// <param name="leftHand">左手のTransform</param>
        /// <param name="rightHand">右手のTransform</param>
        public void SetBothHandTransforms(Transform leftHand, Transform rightHand)
        {
            if (m_grabLeftAnchor != null && leftHand != null)
            {
                m_grabLeftAnchor.Hand = leftHand;
            }
            
            if (m_grabRightAnchor != null && rightHand != null)
            {
                m_grabRightAnchor.Hand = rightHand;
            }
        }
    }
}