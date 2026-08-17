using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TUFReplay.Unity.ReplayTimeline
{
  [DisallowMultipleComponent]
  public sealed class ReplayTimelineView : MonoBehaviour
  {
    [SerializeField]
    private UILinearTimelineGraphic track;

    [SerializeField]
    private RectTransform playhead;

    [SerializeField]
    private UITimelineSeekInput seekInput;

    [SerializeField]
    private UIPlayPauseGraphic playPauseGraphic;

    [SerializeField]
    private Button playPauseButton;

    [SerializeField]
    private Button seekBackwardButton;

    [SerializeField]
    private Button seekForwardButton;

    [SerializeField]
    private TMP_Text elapsedTimeText;

    [SerializeField]
    private TMP_Text durationTimeText;

    [SerializeField]
    private UIFilledCircleGraphic playbackStatusDot;

    [SerializeField]
    private TMP_Text playbackStatusText;

    [SerializeField]
    private UIFloatingPanelDockController dockController;

    [SerializeField]
    private UIJudgmentFilterDropdown judgmentFilter;

    [SerializeField, Range(0f, 1f)]
    private float progress = 84f / 228f;

    [SerializeField]
    private double elapsedSeconds = 84d;

    [SerializeField]
    private double durationSeconds = 228d;

    [SerializeField]
    private bool playing = true;

    public void Configure(
      UILinearTimelineGraphic timelineTrack,
      RectTransform playheadTransform,
      UITimelineSeekInput timelineSeekInput,
      UIPlayPauseGraphic playPause,
      Button playbackButton,
      Button backwardButton,
      Button forwardButton,
      TMP_Text elapsedLabel,
      TMP_Text durationLabel,
      UIFloatingPanelDockController floatingDockController,
      UIJudgmentFilterDropdown timelineJudgmentFilter
    )
    {
      track = timelineTrack;
      playhead = playheadTransform;
      seekInput = timelineSeekInput;
      playPauseGraphic = playPause;
      playPauseButton = playbackButton;
      seekBackwardButton = backwardButton;
      seekForwardButton = forwardButton;
      elapsedTimeText = elapsedLabel;
      durationTimeText = durationLabel;
      dockController = floatingDockController;
      judgmentFilter = timelineJudgmentFilter;
      ResolveStatusReferences();
      BindJudgmentInteractions();
      ApplyAll();
    }

    public void SetProgress(float normalized)
    {
      progress = Mathf.Clamp01(normalized);
      ApplyProgress();
    }

    public void SetTime(double elapsed, double duration)
    {
      elapsedSeconds = Math.Max(0d, elapsed);
      durationSeconds = Math.Max(0d, duration);
      if (elapsedTimeText != null)
        elapsedTimeText.text = FormatTime(elapsedSeconds);
      if (durationTimeText != null)
        durationTimeText.text = FormatTime(durationSeconds);
    }

    public void SetPlaying(bool isPlaying)
    {
      playing = isPlaying;
      if (playPauseGraphic != null)
        playPauseGraphic.SetPlaying(playing);
      if (playbackStatusDot != null)
        playbackStatusDot.color = playing ? new Color32(124, 207, 0, 255) : new Color32(161, 161, 161, 255);
      if (playbackStatusText != null)
      {
        playbackStatusText.text = playing ? "PLAYING" : "PAUSED";
        playbackStatusText.color = playing ? new Color32(229, 229, 229, 255) : new Color32(161, 161, 161, 255);
      }
    }

    public void SetPlaybackControlInteractable(bool interactable)
    {
      if (playPauseButton != null)
        playPauseButton.interactable = interactable;
    }

    public void SetSeekControlsInteractable(bool interactable)
    {
      if (seekBackwardButton != null)
        seekBackwardButton.interactable = interactable;
      if (seekForwardButton != null)
        seekForwardButton.interactable = interactable;
      if (seekInput != null)
        seekInput.SetInteractable(interactable);
    }

    public void BindTogglePlayback(UnityAction callback)
    {
      if (playPauseButton == null)
        return;

      playPauseButton.onClick.RemoveAllListeners();
      if (callback != null)
        playPauseButton.onClick.AddListener(callback);
    }

    public void BindSeekRelative(UnityAction<int> callback)
    {
      if (seekBackwardButton != null)
        seekBackwardButton.onClick.RemoveAllListeners();
      if (seekForwardButton != null)
        seekForwardButton.onClick.RemoveAllListeners();
      if (callback == null)
        return;

      if (seekBackwardButton != null)
        seekBackwardButton.onClick.AddListener(() => callback(-5));
      if (seekForwardButton != null)
        seekForwardButton.onClick.AddListener(() => callback(5));
    }

    public void BindScrub(
      UnityAction onBegin,
      UnityAction<float> onPreview,
      UnityAction<float> onCommit,
      UnityAction onCancel
    )
    {
      seekInput?.Bind(onBegin, onPreview, onCommit, onCancel);
    }

    public void BindExpandDocked(UnityAction callback)
    {
      dockController?.BindExpandRequest(callback);
    }

    public void SetJudgmentMarkers(ReplayJudgmentMarker[] markers)
    {
      judgmentFilter?.SetMarkers(markers);
    }

    public void ResetJudgmentFilter()
    {
      judgmentFilter?.ResetSelection();
    }

    public void CloseJudgmentDropdown()
    {
      judgmentFilter?.CloseDropdown();
    }

    public void ResetPlacement(Rect nativeControlsScreenRect, bool hasNativeControlsScreenRect)
    {
      dockController?.ResetExpanded(nativeControlsScreenRect, hasNativeControlsScreenRect);
    }

    public void ResetPlacementDocked(Rect nativeControlsScreenRect, bool hasNativeControlsScreenRect)
    {
      dockController?.ResetDocked(nativeControlsScreenRect, hasNativeControlsScreenRect);
    }

    public void UpdatePlacementReference(Rect nativeControlsScreenRect, bool hasNativeControlsScreenRect)
    {
      dockController?.UpdatePlacementReference(nativeControlsScreenRect, hasNativeControlsScreenRect);
    }

    public void ExpandDockedToDefault()
    {
      dockController?.ExpandToDefault();
    }

    private void Awake()
    {
      ResolveStatusReferences();
      BindJudgmentInteractions();
      ApplyAll();
    }

    private void OnEnable()
    {
      ApplyAll();
    }

    private void OnValidate()
    {
      progress = Mathf.Clamp01(progress);
      if (UnityEngine.Application.isPlaying)
        ApplyAll();
    }

    private void ApplyAll()
    {
      SetProgress(progress);
      SetTime(elapsedSeconds, durationSeconds);
      SetPlaying(playing);
    }

    private void BindJudgmentInteractions()
    {
      dockController?.BindDockRequest(judgmentFilter != null ? judgmentFilter.ResetSelection : null);
    }

    private void ResolveStatusReferences()
    {
      Transform status = transform.Find("FloatingPanel/MainPanel/PlaybackStatus");
      if (status == null)
        return;
      if (playbackStatusDot == null)
        playbackStatusDot = status.Find("StatusDot")?.GetComponent<UIFilledCircleGraphic>();
      if (playbackStatusText == null)
        playbackStatusText = status.Find("StatusLabel")?.GetComponent<TMP_Text>();
    }

    private void ApplyProgress()
    {
      if (track != null)
        track.Progress = progress;
      if (playhead == null || track == null)
        return;

      Vector2 anchor = new Vector2(progress, 0.5f);
      playhead.anchorMin = anchor;
      playhead.anchorMax = anchor;
      playhead.anchoredPosition = Vector2.zero;
    }

    private static string FormatTime(double seconds)
    {
      long totalSeconds = Math.Max(0L, (long)Math.Floor(seconds));
      long minutes = totalSeconds / 60L;
      long remainder = totalSeconds % 60L;
      return $"{minutes:00}:{remainder:00}";
    }
  }
}
