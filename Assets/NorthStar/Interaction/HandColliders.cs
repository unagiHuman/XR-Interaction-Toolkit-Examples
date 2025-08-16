// Copyright (c) Meta Platforms, Inc. and affiliates.
using System.Collections.Generic;
using UnityEngine;

namespace NorthStar
{
    /// <summary>
    /// 手階層下の全ての Collider をキャッシュし、
    /// 本コンポーネントの有効/無効に合わせて一括で有効化/無効化するユーティリティ。
    /// 生成や配置は行いません（ビルダー機能なし）。
    /// </summary>
    public class HandColliders : MonoBehaviour
    {
        [SerializeField, Tooltip("キャッシュ時に非アクティブの子も含める")]
        private bool m_includeInactive = true;

        [SerializeField, Tooltip("自身に付いている Collider も含める")]
        private bool m_includeSelf = true;

        [SerializeField, Tooltip("現在キャッシュしているコライダー一覧（参照用）")]
        private List<Collider> m_cachedColliders = new();

        /// <summary>
        /// 手動でキャッシュを更新します。階層構成が変わった際に呼び出してください。
        /// </summary>
        public void RefreshCache()
        {
            m_cachedColliders.Clear();

            // 自身を含めて階層下の全 Collider を収集
            var all = GetComponentsInChildren<Collider>(m_includeInactive);
            if (all != null && all.Length > 0)
            {
                for (int i = 0; i < all.Length; i++)
                {
                    var c = all[i];
                    if (c == null) continue;
                    if (!m_includeSelf && c.transform == transform) continue;
                    m_cachedColliders.Add(c);
                }
            }

            RemoveNulls();
        }

        private void Awake()
        {
            RefreshCache();
        }

        // 子の増減・親子変更時に自動でキャッシュを更新
        private void OnTransformChildrenChanged()
        {
            RefreshCache();
        }

        private void OnEnable()
        {
            if (m_cachedColliders == null || m_cachedColliders.Count == 0)
                RefreshCache();

            for (int i = 0; i < m_cachedColliders.Count; i++)
            {
                var c = m_cachedColliders[i];
                if (!c) continue;
                c.enabled = true;
            }
        }

        private void OnDisable()
        {
            for (int i = 0; i < m_cachedColliders.Count; i++)
            {
                var c = m_cachedColliders[i];
                if (!c) continue;
                c.enabled = false;
            }
        }

        private void RemoveNulls()
        {
            for (int i = m_cachedColliders.Count - 1; i >= 0; i--)
            {
                if (!m_cachedColliders[i])
                    m_cachedColliders.RemoveAt(i);
            }
        }
    }
}