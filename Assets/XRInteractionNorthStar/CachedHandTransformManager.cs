using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class CachedHandTransformGetter : MonoBehaviour
{
    [SerializeField] private XROrigin selfXrOrigin;
    [SerializeField] private Transform m_leftHandTransform;
    [SerializeField] private Transform m_rightHandTransform;
    private bool m_isInitialized = false;
    
    
    private void InitializeHandTransforms()
    {
        var xrOrigin = this.selfXrOrigin;
        if (xrOrigin != null)
        {
            var controllers = xrOrigin.GetComponentsInChildren<XRController>();
            foreach (var controller in controllers)
            {
                if (controller.controllerNode == UnityEngine.XR.XRNode.LeftHand)
                    m_leftHandTransform = controller.transform;
                else if (controller.controllerNode == UnityEngine.XR.XRNode.RightHand)
                    m_rightHandTransform = controller.transform;
            }
            m_isInitialized = true;
        }
    }
    
    public Transform LeftHandTransform => m_leftHandTransform;
    public Transform RightHandTransform => m_rightHandTransform;
    public bool IsInitialized => m_isInitialized;
}
