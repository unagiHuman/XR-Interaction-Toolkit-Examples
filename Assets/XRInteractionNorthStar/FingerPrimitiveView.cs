using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Rendering;

namespace XRInteractionNorthStar
{
    /// <summary>
    /// 指をPrimitive風メッシュ（カプセル/スフィア）で可視化するデバッグビュー（新Mesh API + RenderMesh）。
    /// 手のルートにアタッチすると、子孫の HingeJoint を指ごとに検出して表示します。
    /// </summary>
    [DisallowMultipleComponent]
    public class FingerPrimitiveView : MonoBehaviour
    {
        [Header("Auto-discovery")]
        [SerializeField, Tooltip("子孫の Transform 名（Thumb/Index/Middle/Ring/Pinky + 1..3/Tip）から指ボーンを自動検出します")]
        private bool autoDiscoverJoints = true;

        [SerializeField, Tooltip("指の識別に使う接頭辞（複数候補）")]
        private string[] fingerPrefixes = new[] { "Thumb", "Index", "Middle", "Ring", "Pinky" };

        [SerializeField, Tooltip("親指も含める")]
        private bool includeThumb = true;

        [Header("Appearance")]
        [SerializeField, Tooltip("骨カプセルの半径（m）")]
        private float boneRadius = 0.006f;

        [SerializeField, Tooltip("関節スフィアの半径（m）")]
        private float jointRadius = 0.008f;

        [SerializeField, Tooltip("骨の色")]
        private Color boneColor = new Color(1.0f, 0.45f, 0.75f, 1f);

        [SerializeField, Tooltip("関節の色")]
        private Color jointColor = new Color(0.2f, 1.0f, 1.0f, 1f);

        [SerializeField, Tooltip("骨用マテリアル（未指定なら自動生成）")]
        private Material boneMaterialOverride;

        [SerializeField, Tooltip("関節用マテリアル（未指定なら自動生成）")]
        private Material jointMaterialOverride;

        [SerializeField, Tooltip("シャドウを有効化するか")]
        private bool castShadows = false;

        [SerializeField, Tooltip("受ける影を有効化するか")]
        private bool receiveShadows = false;

        [SerializeField, Tooltip("関節スフィアを表示する（OFF で指(骨)のみ表示）")]
        private bool showJointSpheres = false;

        [Header("Options")]
        [SerializeField, Tooltip("Play中のみ表示（Editor停止中は非表示）")]
        private bool showOnlyWhilePlaying = false;

        [SerializeField, Tooltip("アタッチ/有効化時に自動再構築")]
        private bool rebuildOnEnable = true;

        [Header("Resolution")]
        [SerializeField, Tooltip("スフィア緯度分割")]
        private int sphereLatitude = 12;
        [SerializeField, Tooltip("スフィア経度分割")]
        private int sphereLongitude = 16;
        [SerializeField, Tooltip("カプセルの周方向分割")]
        private int capsuleRadialSegments = 16;
        [SerializeField, Tooltip("カプセルの半球分割")]
        private int capsuleHemisphereSegments = 8;

        private readonly List<FingerRender> _fingers = new();

        // 参照だけ保持（描画はRenderMesh）
        [Serializable]
        private class FingerRender
        {
            public string name;
            public List<Transform> joints = new();
            public List<Segment> segments = new();
        }

        private class Segment
        {
            public Transform a; // 始点
            public Transform b; // 終点（次ボーン or Tip）
        }

        // ランタイム生成資源
        private Material _boneMat, _jointMat;
        private Mesh _capsuleMesh, _sphereMesh;
        private int _lastSphereLat, _lastSphereLon, _lastCapRad, _lastCapHem;

        // JobSystem 用キャッシュ
        private readonly List<Transform> _segATransforms = new();
        private readonly List<Transform> _segBTransforms = new();
        private TransformAccessArray _taaA, _taaB;

        private NativeArray<float3> _posA, _posB;
        private NativeArray<float4x4> _boneMatricesF4, _jointAMatricesF4, _jointBMatricesF4;
        private NativeArray<Matrix4x4> _boneMatrices;       // インスタンシング用
        private NativeArray<Matrix4x4> _jointMatrices;      // A,B を連結した 2N

        private int _allocatedSegmentCount = 0;

        private void OnEnable()
        {
            if (rebuildOnEnable)
            {
                Rebuild();
            }
        }

        private void OnDisable()
        {
            // 表示停止。ネイティブバッファは解放しておく
            DisposeNativeBuffers();
            if (_taaA.isCreated) _taaA.Dispose();
            if (_taaB.isCreated) _taaB.Dispose();
        }

        private void OnDestroy()
        {
            // ネイティブ資源を破棄
            DisposeNativeBuffers();
            if (_taaA.isCreated) _taaA.Dispose();
            if (_taaB.isCreated) _taaB.Dispose();

            // 生成メッシュは破棄
            SafeDestroy(_capsuleMesh);
            SafeDestroy(_sphereMesh);
            _capsuleMesh = null;
            _sphereMesh = null;
        }

        private void LateUpdate()
        {
            if (showOnlyWhilePlaying && !Application.isPlaying) return;

            if (_fingers.Count == 0 && autoDiscoverJoints)
            {
                Rebuild();
            }
        }

        [ContextMenu("Rebuild Finger View")]
        public void Rebuild()
        {
            if (showOnlyWhilePlaying && !Application.isPlaying) return;

            ClearGenerated();
            BuildFingerJointMap();
            EnsureMaterials();
            EnsureMeshes();
            BuildSegments();
        }

        [ContextMenu("Clear Finger View")]
        public void ClearGenerated()
        {
            foreach (var f in _fingers)
            {
                f.segments.Clear();
                f.joints.Clear();
            }
            _fingers.Clear();

            _segATransforms.Clear();
            _segBTransforms.Clear();

            if (_taaA.isCreated) _taaA.Dispose();
            if (_taaB.isCreated) _taaB.Dispose();

            DisposeNativeBuffers();
            _allocatedSegmentCount = 0;
        }

        private void BuildFingerJointMap()
        {
            _fingers.Clear();

            if (!autoDiscoverJoints)
                return;

            // 子孫の Transform 名から指ボーンを抽出（Index1/2/3/Tip 等）
            var all = GetComponentsInChildren<Transform>(true)
                .Where(t => t != null && t != transform)
                .ToList();

            foreach (var prefix in fingerPrefixes)
            {
                if (!includeThumb && prefix.Equals("Thumb", StringComparison.OrdinalIgnoreCase))
                    continue;

                var finger = new FingerRender { name = prefix };

                // 対象: 名前に prefix を含み、末尾の数字（1..3 等）または "Tip" を持つものを優先
                var matches = all
                    .Where(t => t.name.IndexOf(prefix, StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderBy(t => NameOrderKey(t.name))
                    .ToList();

                foreach (var t in matches)
                {
                    finger.joints.Add(t);
                }

                if (finger.joints.Count > 0)
                    _fingers.Add(finger);
            }

            // どれも見つからなければ「All」グループとして子孫のうち名前に数値や Tip を含むものを並べる
            if (_fingers.Count == 0)
            {
                var fallback = new FingerRender { name = "All" };
                var numberedOrTip = all
                    .Where(t => HasDigits(t.name) || t.name.IndexOf("Tip", StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderBy(t => NameOrderKey(t.name))
                    .ToList();
                if (numberedOrTip.Count == 0)
                {
                    // それでも無ければ直下子を順序で
                    numberedOrTip = transform.GetComponentsInChildren<Transform>(true)
                        .Where(t => t != null && t != transform)
                        .OrderBy(t => t.GetSiblingIndex())
                        .ToList();
                }
                fallback.joints.AddRange(numberedOrTip);
                if (fallback.joints.Count > 0)
                    _fingers.Add(fallback);
            }

            static int NameOrderKey(string n)
            {
                // 数字を抽出し、"Index12" -> 12、"Tip" を末尾扱い（大きめのキー）に
                int acc = 0;
                bool hasDigit = false;
                foreach (char c in n)
                {
                    if (char.IsDigit(c)) { acc = acc * 10 + (c - '0'); hasDigit = true; }
                }
                if (!hasDigit && n.IndexOf("Tip", StringComparison.OrdinalIgnoreCase) >= 0)
                    acc = 999;
                return acc;
            }

            static bool HasDigits(string n)
            {
                foreach (char c in n) if (char.IsDigit(c)) return true;
                return false;
            }
        }

        private void BuildSegments()
        {
            foreach (var f in _fingers)
            {
                for (int i = 0; i < f.joints.Count; i++)
                {
                    var a = f.joints[i];
                    var b = (i + 1 < f.joints.Count) ? f.joints[i + 1] : FindTipTransform(a);
                    if (a == null || b == null || a == b) continue;

                    f.segments.Add(new Segment { a = a, b = b });
                }
            }

            // TransformAccessArray を再構築
            _segATransforms.Clear();
            _segBTransforms.Clear();
            foreach (var f in _fingers)
            {
                foreach (var s in f.segments)
                {
                    _segATransforms.Add(s.a);
                    _segBTransforms.Add(s.b);
                }
            }

            if (_taaA.isCreated) _taaA.Dispose();
            if (_taaB.isCreated) _taaB.Dispose();

            int segCount = _segATransforms.Count;
            if (segCount > 0)
            {
                _taaA = new TransformAccessArray(segCount);
                _taaB = new TransformAccessArray(segCount);
                for (int i = 0; i < segCount; i++)
                {
                    _taaA.Add(_segATransforms[i]);
                    _taaB.Add(_segBTransforms[i]);
                }
            }

            EnsureNativeBuffersForSegments(segCount);
        }

        private void EnsureMaterials()
        {
            // Bone material
            if (boneMaterialOverride != null)
            {
                // アセットを汚さないようランタイム複製し、インスタンシングを有効化
                if (_boneMat == null || _boneMat.shader != boneMaterialOverride.shader)
                {
                    SafeDestroy(_boneMat);
                    _boneMat = new Material(boneMaterialOverride) { name = boneMaterialOverride.name + " (Instanced Runtime)" };
                }
                _boneMat.enableInstancing = true;
            }
            else
            {
                if (_boneMat == null)
                    _boneMat = CreateLitMaterial("FingerBoneRuntimeMat", boneColor);
                _boneMat.enableInstancing = true;
                ApplyColor(_boneMat, boneColor);
            }

            // Joint material
            if (jointMaterialOverride != null)
            {
                if (_jointMat == null || _jointMat.shader != jointMaterialOverride.shader)
                {
                    SafeDestroy(_jointMat);
                    _jointMat = new Material(jointMaterialOverride) { name = jointMaterialOverride.name + " (Instanced Runtime)" };
                }
                _jointMat.enableInstancing = true;
            }
            else
            {
                if (_jointMat == null)
                    _jointMat = CreateLitMaterial("FingerJointRuntimeMat", jointColor);
                _jointMat.enableInstancing = true;
                ApplyColor(_jointMat, jointColor);
            }
        }

        private Material CreateLitMaterial(string name, Color color)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (!sh) sh = Shader.Find("Standard");
            var m = new Material(sh) { name = name };
            ApplyColor(m, color);
            m.enableInstancing = true; // RenderMeshInstanced の必須条件
            return m;
        }

        private void EnsureMeshes()
        {
            // スフィア
            if (_sphereMesh == null || _lastSphereLat != sphereLatitude || _lastSphereLon != sphereLongitude)
            {
                SafeDestroy(_sphereMesh);
                _sphereMesh = BuildSphereMesh(Mathf.Max(3, sphereLatitude), Mathf.Max(3, sphereLongitude), 0.5f);
                _lastSphereLat = sphereLatitude;
                _lastSphereLon = sphereLongitude;
            }
            // カプセル（Y軸、半径0.5、全長2.0）
            if (_capsuleMesh == null || _lastCapRad != capsuleRadialSegments || _lastCapHem != capsuleHemisphereSegments)
            {
                SafeDestroy(_capsuleMesh);
                _capsuleMesh = BuildCapsuleMesh(Mathf.Max(6, capsuleRadialSegments), Mathf.Max(2, capsuleHemisphereSegments), 0.5f, 1.0f);
                _lastCapRad = capsuleRadialSegments;
                _lastCapHem = capsuleHemisphereSegments;
            }
        }

        private void OnRenderObject()
        {
            if (showOnlyWhilePlaying && !Application.isPlaying) return;
            // 指（骨）描画に必要なものだけを必須にする。関節スフィアは任意。
            if (_fingers.Count == 0 || _capsuleMesh == null || _boneMat == null)
                return;

            int segCount = _segATransforms.Count;
            if (segCount <= 0) return;

            // セグメント数が変わっていたら配列を確保し直す
            if (segCount != _allocatedSegmentCount)
            {
                EnsureNativeBuffersForSegments(segCount);
            }

            // 位置読み取りジョブ
            var jobA = new WritePositionsJob { outPos = _posA };
            var jobB = new WritePositionsJob { outPos = _posB };
            JobHandle hA = jobA.Schedule(_taaA);
            JobHandle hB = jobB.Schedule(_taaB);

            // 行列組み立てジョブ
            var build = new BuildMatricesJob
            {
                posA = _posA,
                posB = _posB,
                boneRadius = boneRadius,
                jointRadius = jointRadius,
                boneOut = _boneMatricesF4,
                jointAOut = _jointAMatricesF4,
                jointBOut = _jointBMatricesF4
            };
            JobHandle hM = build.Schedule(segCount, 32, JobHandle.CombineDependencies(hA, hB));
            hM.Complete();

            // float4x4 -> Matrix4x4 へ変換（軽量）
            for (int i = 0; i < segCount; i++)
            {
                _boneMatrices[i] = (Matrix4x4)_boneMatricesF4[i];
            }
            if (showJointSpheres)
            {
                for (int i = 0; i < segCount; i++)
                {
                    _jointMatrices[i] = (Matrix4x4)_jointAMatricesF4[i];
                    _jointMatrices[i + segCount] = (Matrix4x4)_jointBMatricesF4[i];
                }
            }

#if UNITY_6000_0_OR_NEWER
            var rpBone = new RenderParams(_boneMat)
            {
                layer = gameObject.layer,
                shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                receiveShadows = receiveShadows
            };

            // バッチ描画（1023制限考慮）
            RenderBatchedInstanced(rpBone, _capsuleMesh, _boneMatrices);

            if (showJointSpheres)
            {
                var rpJoint = new RenderParams(_jointMat)
                {
                    layer = gameObject.layer,
                    shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                    receiveShadows = receiveShadows
                };
                RenderBatchedInstanced(rpJoint, _sphereMesh, _jointMatrices);
            }
#else
            // フォールバック（旧API）
            RenderBatchedInstancedFallback(_boneMat, _capsuleMesh, _boneMatrices);
            if (showJointSpheres)
            {
                RenderBatchedInstancedFallback(_jointMat, _sphereMesh, _jointMatrices);
            }
#endif
        }

        // ---------- Jobs & Helpers ----------

        [BurstCompile]
        private struct WritePositionsJob : IJobParallelForTransform
        {
            public NativeArray<float3> outPos; // [WriteOnly] は配列再利用のため付けない
            public void Execute(int index, TransformAccess transform)
            {
                outPos[index] = transform.position;
            }
        }

        [BurstCompile]
        private struct BuildMatricesJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float3> posA;
            [ReadOnly] public NativeArray<float3> posB;

            public float boneRadius;
            public float jointRadius;

            [WriteOnly] public NativeArray<float4x4> boneOut;
            [WriteOnly] public NativeArray<float4x4> jointAOut;
            [WriteOnly] public NativeArray<float4x4> jointBOut;

            public void Execute(int index)
            {
                float3 a = posA[index];
                float3 b = posB[index];
                float3 d = b - a;
                float len = math.length(d);
                if (len <= 1e-6f)
                {
                    // 退避：無効な行列を入れておく（描画側でスキップしないため単位行列で極小スケール）
                    boneOut[index] = float4x4.TRS((a + b) * 0.5f, quaternion.identity, new float3(1e-6f));
                    jointAOut[index] = float4x4.TRS(a, quaternion.identity, new float3(jointRadius * 2f));
                    jointBOut[index] = float4x4.TRS(b, quaternion.identity, new float3(jointRadius * 2f));
                    return;
                }

                float3 up = new float3(0, 1, 0);
                float3 dirN = d / len;

                // up -> dirN の最短回転
                float dot = math.dot(up, dirN);
                quaternion rot;
                const float eps = 1e-6f;
                if (dot > 1f - 1e-6f)
                {
                    rot = quaternion.identity;
                }
                else if (dot < -1f + 1e-6f)
                {
                    // 180度回転：X軸周りに回す（任意だが安定）
                    rot = quaternion.AxisAngle(new float3(1, 0, 0), math.PI);
                }
                else
                {
                    float3 axis = math.normalize(math.cross(up, dirN));
                    float angle = math.acos(math.clamp(dot, -1f, 1f));
                    rot = quaternion.AxisAngle(axis, angle);
                }

                float3 pos = (a + b) * 0.5f;

                float3 boneScale = new float3(boneRadius * 2f, len * 0.5f, boneRadius * 2f);
                float3 jointScale = new float3(jointRadius * 2f);

                boneOut[index]    = float4x4.TRS(pos, rot, boneScale);
                jointAOut[index]  = float4x4.TRS(a, quaternion.identity, jointScale);
                jointBOut[index]  = float4x4.TRS(b, quaternion.identity, jointScale);
            }
        }

        private void EnsureNativeBuffersForSegments(int segCount)
        {
            if (segCount == _allocatedSegmentCount) return;

            DisposeNativeBuffers();

            if (segCount > 0)
            {
                _posA = new NativeArray<float3>(segCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                _posB = new NativeArray<float3>(segCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

                _boneMatricesF4  = new NativeArray<float4x4>(segCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                _jointAMatricesF4 = new NativeArray<float4x4>(segCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                _jointBMatricesF4 = new NativeArray<float4x4>(segCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

                _boneMatrices = new NativeArray<Matrix4x4>(segCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                _jointMatrices = new NativeArray<Matrix4x4>(segCount * 2, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            }

            _allocatedSegmentCount = segCount;
        }

        private void DisposeNativeBuffers()
        {
            if (_posA.IsCreated) _posA.Dispose();
            if (_posB.IsCreated) _posB.Dispose();

            if (_boneMatricesF4.IsCreated) _boneMatricesF4.Dispose();
            if (_jointAMatricesF4.IsCreated) _jointAMatricesF4.Dispose();
            if (_jointBMatricesF4.IsCreated) _jointBMatricesF4.Dispose();

            if (_boneMatrices.IsCreated) _boneMatrices.Dispose();
            if (_jointMatrices.IsCreated) _jointMatrices.Dispose();
        }

        #if UNITY_6000_0_OR_NEWER
        private static void RenderBatchedInstanced(RenderParams rp, Mesh mesh, NativeArray<Matrix4x4> matrices)
        {
            const int maxBatch = 1023;
            int total = matrices.Length;
            int offset = 0;
            while (offset < total)
            {
                int count = math.min(maxBatch, total - offset);
                var slice = matrices.GetSubArray(offset, count);
                Graphics.RenderMeshInstanced(rp, mesh, 0, slice);
                offset += count;
            }
        }
        #else
        private void RenderBatchedInstancedFallback(Material mat, Mesh mesh, NativeArray<Matrix4x4> matrices)
        {
            const int maxBatch = 1023;
            int total = matrices.Length;
            int offset = 0;

            while (offset < total)
            {
                int count = Mathf.Min(maxBatch, total - offset);
                var temp = new Matrix4x4[count];
                for (int i = 0; i < count; i++) temp[i] = matrices[offset + i];
                Graphics.DrawMeshInstanced(mesh, 0, mat, temp, count, null, ShadowCastingMode.On, true, gameObject.layer);
                offset += count;
            }
        }
        #endif

        // ---------- Mesh Generators (新Mesh API) ----------

        private Mesh BuildSphereMesh(int lat, int lon, float radius)
        {
            int vertCount = (lat + 1) * (lon + 1);
            int triCount = lat * lon * 2;
            int indexCount = triCount * 3;

            var mdArr = Mesh.AllocateWritableMeshData(1);
            var md = mdArr[0];

            var vLayout = new[]
            {
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Normal,  VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32, 2),
            };
            md.SetVertexBufferParams(vertCount, vLayout);
            md.SetIndexBufferParams(indexCount, IndexFormat.UInt32);

            var vtx = md.GetVertexData<VertexPNuV>(0);
            var idx = md.GetIndexData<uint>();

            int v = 0;
            for (int i = 0; i <= lat; i++)
            {
                float t = (float)i / lat;           // 0..1
                float theta = t * Mathf.PI;         // 0..PI
                float st = Mathf.Sin(theta);
                float ct = Mathf.Cos(theta);

                for (int j = 0; j <= lon; j++)
                {
                    float u = (float)j / lon;       // 0..1
                    float phi = u * Mathf.PI * 2f;  // 0..2PI
                    float sp = Mathf.Sin(phi);
                    float cp = Mathf.Cos(phi);

                    Vector3 n = new Vector3(st * cp, ct, st * sp);
                    vtx[v] = new VertexPNuV
                    {
                        pos = n * radius,
                        normal = n,
                        uv = new Vector2(u, t)
                    };
                    v++;
                }
            }

            int k = 0;
            for (int i = 0; i < lat; i++)
            {
                for (int j = 0; j < lon; j++)
                {
                    uint a = (uint)(i * (lon + 1) + j);
                    uint b = (uint)((i + 1) * (lon + 1) + j);
                    uint a1 = a + 1;
                    uint b1 = b + 1;

                    idx[k++] = a;  idx[k++] = b;  idx[k++] = b1;
                    idx[k++] = a;  idx[k++] = b1; idx[k++] = a1;
                }
            }

            md.subMeshCount = 1;
            md.SetSubMesh(0, new SubMeshDescriptor(0, indexCount, MeshTopology.Triangles), MeshUpdateFlags.DontRecalculateBounds);

            var mesh = new Mesh { name = "Sphere_UnitR0.5" };
            Mesh.ApplyAndDisposeWritableMeshData(mdArr, new[] { mesh });
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }

        // 半径r=0.5、シリンダ半長=cy=1.0-0.5=0.5 → 全長2.0
        private Mesh BuildCapsuleMesh(int radial, int hemiSeg, float radius, float halfLength)
        {
            // 半球中心は ±cy（cy = halfLength - radius = 0.5）
            float cy = Mathf.Max(0f, halfLength - radius);

            // リング列（下端→上端）
            var rings = new List<Ring>();

            // 底半球（半径→0）: t: [pi/2 .. 0]
            for (int h = hemiSeg; h >= 1; h--)
            {
                float t = (h / (float)hemiSeg) * (Mathf.PI * 0.5f);
                rings.Add(new Ring
                {
                    y = -cy - radius * Mathf.Sin(t),
                    r = radius * Mathf.Cos(t),
                    type = RingType.Hemisphere,
                    hemiCenterY = -cy
                });
            }
            // シリンダ下縁
            rings.Add(new Ring { y = -cy, r = radius, type = RingType.Cylinder });
            // シリンダ上縁
            rings.Add(new Ring { y = cy, r = radius, type = RingType.Cylinder });
            // 上半球（0→半径0）: t: [0 .. pi/2]
            for (int h = 1; h <= hemiSeg; h++)
            {
                float t = (h / (float)hemiSeg) * (Mathf.PI * 0.5f);
                rings.Add(new Ring
                {
                    y = cy + radius * Mathf.Sin(t),
                    r = radius * Mathf.Cos(t),
                    type = RingType.Hemisphere,
                    hemiCenterY = cy
                });
            }

            int ringCount = rings.Count;
            int vertsPerRing = radial + 1;
            int vertCount = ringCount * vertsPerRing;
            int quadCount = (ringCount - 1) * radial;
            int indexCount = quadCount * 6;

            var mdArr = Mesh.AllocateWritableMeshData(1);
            var md = mdArr[0];

            var vLayout = new[]
            {
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Normal,  VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32, 2),
            };
            md.SetVertexBufferParams(vertCount, vLayout);
            md.SetIndexBufferParams(indexCount, IndexFormat.UInt32);

            var vtx = md.GetVertexData<VertexPNuV>(0);
            var idx = md.GetIndexData<uint>();

            int v = 0;
            for (int i = 0; i < ringCount; i++)
            {
                var ring = rings[i];
                for (int j = 0; j <= radial; j++)
                {
                    float u = (float)j / radial;
                    float phi = u * Mathf.PI * 2f;
                    float cp = Mathf.Cos(phi);
                    float sp = Mathf.Sin(phi);

                    Vector3 pos = new Vector3(ring.r * cp, ring.y, ring.r * sp);
                    Vector3 normal;
                    if (ring.type == RingType.Cylinder)
                    {
                        normal = new Vector3(cp, 0f, sp);
                    }
                    else
                    {
                        // 半球中心からの方向
                        Vector3 local = new Vector3(ring.r * cp, ring.y - ring.hemiCenterY, ring.r * sp);
                        normal = local.normalized;
                    }

                    vtx[v++] = new VertexPNuV
                    {
                        pos = pos,
                        normal = normal,
                        uv = new Vector2(u, Mathf.InverseLerp(-halfLength, halfLength, ring.y))
                    };
                }
            }

            int k = 0;
            for (int i = 0; i < ringCount - 1; i++)
            {
                int row = i * vertsPerRing;
                int next = (i + 1) * vertsPerRing;

                for (int j = 0; j < radial; j++)
                {
                    uint a = (uint)(row + j);
                    uint b = (uint)(next + j);
                    uint a1 = a + 1;
                    uint b1 = b + 1;

                    idx[k++] = a;  idx[k++] = b;  idx[k++] = b1;
                    idx[k++] = a;  idx[k++] = b1; idx[k++] = a1;
                }
            }

            md.subMeshCount = 1;
            md.SetSubMesh(0, new SubMeshDescriptor(0, indexCount, MeshTopology.Triangles), MeshUpdateFlags.DontRecalculateBounds);

            var mesh = new Mesh { name = "Capsule_UnitR0.5_YLen2" };
            Mesh.ApplyAndDisposeWritableMeshData(mdArr, new[] { mesh });
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }

        private struct VertexPNuV
        {
            public Vector3 pos;
            public Vector3 normal;
            public Vector2 uv;
        }

        private enum RingType { Cylinder, Hemisphere }
        private struct Ring
        {
            public float y;
            public float r;
            public RingType type;
            public float hemiCenterY;
        }

        private void ApplyColor(Material m, Color c)
        {
            if (!m) return;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        private static void SafeDestroy(UnityEngine.Object o)
        {
            if (!o) return;
#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEngine.Object.DestroyImmediate(o);
            else UnityEngine.Object.Destroy(o);
#else
            UnityEngine.Object.Destroy(o);
#endif
        }

        private Transform FindTipTransform(Transform start)
        {
            if (start == null) return null;

            foreach (var t in start.GetComponentsInChildren<Transform>(true))
            {
                if (t == start) continue;
                if (t.name.IndexOf("Tip", StringComparison.OrdinalIgnoreCase) >= 0)
                    return t;
            }
            if (start.childCount > 0)
                return start.GetChild(start.childCount - 1);
            return null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // 色/解像度変更適用
            if (_boneMat) ApplyColor(_boneMat, boneColor);
            if (_jointMat) ApplyColor(_jointMat, jointColor);

            // 解像度変更時に再生成
            if (_lastSphereLat != sphereLatitude || _lastSphereLon != sphereLongitude)
            {
                if (_sphereMesh) { SafeDestroy(_sphereMesh); _sphereMesh = null; }
            }
            if (_lastCapRad != capsuleRadialSegments || _lastCapHem != capsuleHemisphereSegments)
            {
                if (_capsuleMesh) { SafeDestroy(_capsuleMesh); _capsuleMesh = null; }
            }

            if (isActiveAndEnabled)
            {
                EnsureMaterials();
                EnsureMeshes();
            }
        }
#endif
    }
}
