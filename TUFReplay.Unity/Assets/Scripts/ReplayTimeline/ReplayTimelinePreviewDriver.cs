using UnityEngine;

namespace TUFReplay.Unity.ReplayTimeline
{
  [DefaultExecutionOrder(-200)]
  [DisallowMultipleComponent]
  public sealed class ReplayTimelinePreviewDriver : MonoBehaviour
  {
    [SerializeField]
    private ReplayTimelineView view;

    [SerializeField]
    private double durationSeconds = 228d;

    [SerializeField]
    private double initialElapsedSeconds = 84d;

    [SerializeField]
    private bool animateProgress = true;

    private double elapsedSeconds;

    private static readonly ReplayJudgmentMarker[] SampleMarkers =
    {
      new ReplayJudgmentMarker(0.06f, ReplayJudgmentKind.Overload),
      new ReplayJudgmentMarker(0.12f, ReplayJudgmentKind.TooEarly),
      new ReplayJudgmentMarker(0.18f, ReplayJudgmentKind.Early),
      new ReplayJudgmentMarker(0.24f, ReplayJudgmentKind.EarlyPerfect),
      new ReplayJudgmentMarker(0.29f, ReplayJudgmentKind.Perfect),
      new ReplayJudgmentMarker(0.34f, ReplayJudgmentKind.Perfect),
      new ReplayJudgmentMarker(0.39f, ReplayJudgmentKind.Perfect),
      new ReplayJudgmentMarker(0.45f, ReplayJudgmentKind.LatePerfect),
      new ReplayJudgmentMarker(0.51f, ReplayJudgmentKind.Late),
      new ReplayJudgmentMarker(0.58f, ReplayJudgmentKind.TooLate),
      new ReplayJudgmentMarker(0.64f, ReplayJudgmentKind.Miss),
      new ReplayJudgmentMarker(0.70f, ReplayJudgmentKind.EarlyPerfect),
      new ReplayJudgmentMarker(0.75f, ReplayJudgmentKind.Perfect),
      new ReplayJudgmentMarker(0.80f, ReplayJudgmentKind.LatePerfect),
      new ReplayJudgmentMarker(0.85f, ReplayJudgmentKind.Early),
      new ReplayJudgmentMarker(0.90f, ReplayJudgmentKind.Late),
      new ReplayJudgmentMarker(0.95f, ReplayJudgmentKind.Miss),
    };

    public void Configure(ReplayTimelineView timelineView)
    {
      view = timelineView;
      ApplyInitialState();
    }

    private void OnEnable() => ApplyInitialState();

    private void Update()
    {
      if (!Application.isPlaying || view == null)
        return;

      if (animateProgress && durationSeconds > 0d)
      {
        elapsedSeconds += Time.unscaledDeltaTime;
        if (elapsedSeconds >= durationSeconds)
          elapsedSeconds = 0d;
        view.SetProgress((float)(elapsedSeconds / durationSeconds));
        view.SetTime(elapsedSeconds, durationSeconds);
      }
    }

    private void ApplyInitialState()
    {
      elapsedSeconds = initialElapsedSeconds;
      if (view == null)
        return;

      view.SetProgress(durationSeconds > 0d ? (float)(elapsedSeconds / durationSeconds) : 0f);
      view.SetTime(elapsedSeconds, durationSeconds);
      view.SetPlaying(true);
      view.SetJudgmentMarkers(SampleMarkers);
      view.ResetJudgmentFilter();
    }
  }
}
