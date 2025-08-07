// Copyright (c) Meta Platforms, Inc. and affiliates.
using Meta.Utilities;
using Meta.Utilities.Ropes;
using Oculus.Interaction.HandGrab;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.OpenXR.Features.Extensions.PerformanceSettings;

namespace NorthStar
{
    /// <summary>
    /// Exposes physics transformer callbacks as unity events
    /// </summary>
    public class OnGrabEvents : MonoBehaviour
    {
        [SerializeField, AutoSet] private PhysicsTransformer m_physicsTransformer;

        public UnityEvent OnGrab = new();
        public UnityEvent OnRelease = new();

        private void OnEnable()
        {
            m_physicsTransformer.OnInteraction += Grab;
            m_physicsTransformer.OnEndInteraction += EndGrab;
        }

        private void OnDisable()
        {
            m_physicsTransformer.OnInteraction -= Grab;
            m_physicsTransformer.OnEndInteraction -= EndGrab;
        }

        private RopeSystem.Anchor EndGrab(GameObject interactor)
        {
            OnRelease.Invoke();
            return null;
        }

        private RopeSystem.Anchor Grab(GameObject interactor)
        {
            OnGrab.Invoke();
            return null;
        }
    }
}
