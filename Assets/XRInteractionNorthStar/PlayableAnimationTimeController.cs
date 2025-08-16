using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

[ExecuteAlways]
[RequireComponent(typeof(Animator))]
public class PlayableAnimationTimeController : MonoBehaviour
{
    [Header("再生するアニメーションクリップ")]
    public AnimationClip clip;

    [Header("表示したい時刻（秒）")]
    [Min(0f)]
    public float time = 0f;

    [Header("time をループさせるか")]
    public bool loop = true;

    private PlayableGraph _graph;
    private AnimationClipPlayable _clipPlayable;
    private AnimationPlayableOutput _output;

    private bool _initialized = false;
    private float _lastAppliedTime = -1f;

    private void OnEnable()
    {
        InitializeGraph();
        ApplyTime(force: true);
    }

    private void OnDisable()
    {
        TeardownGraph();
    }

    private void OnDestroy()
    {
        TeardownGraph();
    }

    private void OnValidate()
    {
        // エディタ上での値変更も即時反映
        if (!isActiveAndEnabled)
            return;

        // クリップや値が変わったときに再初期化/反映
        InitializeGraph();
        ApplyTime(force: true);
    }

    private void Update()
    {
        // 再生はせず、指定 time のポーズを表示
        ApplyTime();
    }

    /// <summary>
    /// 秒で時刻を指定して即時反映
    /// </summary>
    public void SetTime(float seconds)
    {
        time = Mathf.Max(0f, seconds);
        ApplyTime(force: true);
    }

    /// <summary>
    /// 0-1 の正規化時刻で指定して即時反映
    /// </summary>
    public void SetTimeNormalized(float normalized)
    {
        if (clip == null) return;
        SetTime(Mathf.Clamp01(normalized) * clip.length);
    }

    /// <summary>
    /// 再生クリップを差し替え
    /// </summary>
    public void SetClip(AnimationClip newClip)
    {
        if (clip == newClip) return;
        clip = newClip;
        RebuildForNewClip();
    }

    private void RebuildForNewClip()
    {
        TeardownGraph();
        InitializeGraph();
        ApplyTime(force: true);
    }

    private void InitializeGraph()
    {
        var animator = GetComponent<Animator>();

        // 前提が満たせない場合は破棄して抜ける
        if (animator == null || clip == null)
        {
            TeardownGraph();
            return;
        }

        // 既存グラフがあり、同じクリップなら再利用
        if (_initialized && _clipPlayable.IsValid())
        {
            var current = _clipPlayable.GetAnimationClip();
            if (current == clip)
            {
                return;
            }
            else
            {
                TeardownGraph();
            }
        }

        _graph = PlayableGraph.Create($"{name}_AnimationTimeGraph");
        _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

        _clipPlayable = AnimationClipPlayable.Create(_graph, clip);
        _clipPlayable.SetApplyFootIK(true);
        _clipPlayable.SetDuration(clip.length);
        _clipPlayable.SetSpeed(0.0); // 自動で進行しない

        _output = AnimationPlayableOutput.Create(_graph, "Animation", animator);
        _output.SetSourcePlayable(_clipPlayable);

        _graph.Play();
        _initialized = true;
    }

    private void TeardownGraph()
    {
        if (_initialized)
        {
            if (_graph.IsValid())
            {
                _graph.Destroy();
            }
            _initialized = false;
            _lastAppliedTime = -1f;
        }
    }

    private void ApplyTime(bool force = false)
    {
        if (!_initialized)
        {
            InitializeGraph();
            if (!_initialized) return;
        }

        double t = time;

        if (clip != null)
        {
            if (loop && clip.length > 0f)
            {
                t = Mathf.Repeat((float)t, clip.length);
            }
            else
            {
                t = Mathf.Clamp((float)t, 0f, clip.length);
            }
        }

        if (!force && Mathf.Approximately((float)t, _lastAppliedTime))
        {
            return;
        }

        _clipPlayable.SetTime(t);
        // Manual 更新なので Evaluate(0) でサンプルを即時反映
        _graph.Evaluate(0);
        _lastAppliedTime = (float)t;
    }
}
