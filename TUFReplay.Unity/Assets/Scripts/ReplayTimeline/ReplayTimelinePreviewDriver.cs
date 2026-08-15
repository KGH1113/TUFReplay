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
    }
  }
}
