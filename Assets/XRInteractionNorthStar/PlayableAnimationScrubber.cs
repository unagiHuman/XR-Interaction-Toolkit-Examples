using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

[ExecuteAlways]
[RequireComponent(typeof(Animator))]
public class PlayableAnimationScrubber : MonoBehaviour
{
    [Header("再生するアニメーションクリップ")]
    [SerializeField] private AnimationClip clip;

    [Header("現在時刻（秒）")]
    [SerializeField, Min(0f)] private float time = 0f;

    [Header("ループ（time が長さを超えた場合に繰り返す）")]
    [SerializeField] private bool loop = true;

    private PlayableGraph _graph;
    private AnimationClipPlayable _clipPlayable;
    private AnimationPlayableOutput _output;

    private bool _initialized = false;
    private double _lastSampledTime = -1.0;

    public AnimationClip Clip
    {
        get => clip;
        set => SetClip(value);
    }

    public float TimeSeconds
    {
        get => time;
        set => SetTime(value);
    }

    public bool Loop
    {
        get => loop;
        set => loop = value;
    }

    private void OnEnable()
    {
        InitializeGraph();
        SampleAtCurrentTime(force: true);
    }

    private void OnDisable()
    {
        TeardownGraph();
    }

    private void OnDestroy()
    {
        TeardownGraph();
    }

    private void Update()
    {
        if (!_initialized || clip == null)
            return;

        // 常に「指定した time のポーズ」を表示するだけ（自動再生はしない）
        SampleAtCurrentTime();
    }

    private void InitializeGraph()
    {
        var animator = GetComponent<Animator>();
        if (animator == null || clip == null)
        {
            TeardownGraph();
            return;
        }

        if (_initialized && _clipPlayable.IsValid())
        {
            if (_clipPlayable.GetAnimationClip() == clip)
            {
                return;
            }
            TeardownGraph();
        }

        _graph = PlayableGraph.Create($"{name}_PlayableAnimationScrubber");
        _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

        _clipPlayable = AnimationClipPlayable.Create(_graph, clip);
        _clipPlayable.SetApplyFootIK(true);
        _clipPlayable.SetDuration(clip.length);
        _clipPlayable.SetTime(0.0);
        _clipPlayable.SetSpeed(0.0); // 自動進行を完全抑止

        _output = AnimationPlayableOutput.Create(_graph, "Animation", animator);
        _output.SetSourcePlayable(_clipPlayable);

        _graph.Play();
        _initialized = true;
        _lastSampledTime = -1.0;
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
            _lastSampledTime = -1.0;
        }
    }

    private void SampleAtCurrentTime(bool force = false)
    {
        if (!_initialized || clip == null)
            return;

        double length = Mathf.Max(clip.length, 0.0001f);
        double t = time;

        if (loop)
            t = Mathf.Repeat((float)t, (float)length);
        else
            t = Mathf.Clamp((float)t, 0f, (float)length);

        if (!force && Mathf.Approximately((float)t, (float)_lastSampledTime))
            return;

        _clipPlayable.SetTime(t);
        _graph.Evaluate(0); // Manual 更新で即時サンプル
        _lastSampledTime = t;
    }

    public void SetTime(float seconds)
    {
        time = Mathf.Max(0f, seconds);
        SampleAtCurrentTime(force: true);
    }

    public void SetTimeNormalized(float normalized)
    {
        if (clip == null) return;
        normalized = Mathf.Clamp01(normalized);
        SetTime(normalized * clip.length);
    }

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
        SampleAtCurrentTime(force: true);
    }

    private void OnValidate()
    {
        if (!isActiveAndEnabled) return;
        InitializeGraph();
        SampleAtCurrentTime(force: true);
    }
}
