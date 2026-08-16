using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TUFReplay.Unity.ReplayTimeline
{
  [DisallowMultipleComponent]
  [AddComponentMenu("UI/TUFReplay/Floating Panel Dock Controller")]
  public sealed class UIFloatingPanelDockController : MonoBehaviour
  {
    private enum DockState
    {
      Expanded,
      Docking,
      Docked,
      Expanding,
    }

    [SerializeField]
    private RectTransform canvasRoot;

    [SerializeField]
    private RectTransform movementTarget;

    [SerializeField]
    private UIFloatingPanelDragHandle dragHandle;

    [SerializeField]
    private Button dockButton;

    [SerializeField]
    private RectTransform dockTab;

    [SerializeField]
    private CanvasGroup dockTabCanvasGroup;

    [SerializeField]
    private Button dockTabButton;

    [SerializeField, Min(0f)]
    private float screenMargin = 16f;

    [SerializeField, Min(0f)]
    private float nativeControlsGap = 16f;

    [SerializeField, Min(0.01f)]
    private float panelAnimationDuration = 0.26f;

    [SerializeField, Min(0.01f)]
    private float tabRevealDuration = 0.12f;

    private DockState state;
    private Rect nativeControlsScreenRect;
    private bool hasNativeControlsScreenRect;
    private Vector2 defaultExpandedPosition;
    private Vector2 fixedDockedPosition;
    private Vector2 tabShownPosition;
    private Vector2 tabHiddenPosition;
    private Vector2 animationStart;
    private Vector2 animationTarget;
    private Vector2 lastCanvasSize = new Vector2(float.NaN, float.NaN);
    private float animationElapsed;
    private float tabReveal;
    private UnityAction expandRequested;
    private UnityAction dockRequested;

    public void Configure(
      RectTransform root,
      RectTransform target,
      UIFloatingPanelDragHandle panelDragHandle,
      Button collapseButton,
      RectTransform edgeTab,
      CanvasGroup edgeTabCanvasGroup,
      Button edgeTabButton
    )
    {
      canvasRoot = root;
      movementTarget = target;
      dragHandle = panelDragHandle;
      dockButton = collapseButton;
      dockTab = edgeTab;
      dockTabCanvasGroup = edgeTabCanvasGroup;
      dockTabButton = edgeTabButton;
      BindButtons();
      TryRecalculatePositions();
      ApplyTabVisual();
    }

    public void BindExpandRequest(UnityAction callback)
    {
      expandRequested = callback;
    }

    public void BindDockRequest(UnityAction callback)
    {
      dockRequested = callback;
    }

    public void ResetExpanded(Rect controlsScreenRect, bool hasControlsScreenRect)
    {
      if (!SetPlacementReference(controlsScreenRect, hasControlsScreenRect))
        return;
      state = DockState.Expanded;
      animationElapsed = 0f;
      tabReveal = 0f;
      if (movementTarget != null)
        movementTarget.anchoredPosition = defaultExpandedPosition;
      SetExpandedInteraction(true);
      ApplyTabVisual();
    }

    public void UpdatePlacementReference(Rect controlsScreenRect, bool hasControlsScreenRect)
    {
      if (!SetPlacementReference(controlsScreenRect, hasControlsScreenRect))
        return;
      if (state == DockState.Docked)
      {
        movementTarget.anchoredPosition = fixedDockedPosition;
        ApplyTabVisual();
      }
      else if (state == DockState.Expanded)
      {
        dragHandle?.ClampToScreen();
      }
      else
      {
        FinishAnimation();
      }
    }

    public void Dock()
    {
      if (state != DockState.Expanded || movementTarget == null)
        return;

      if (!TryRecalculatePositions())
        return;
      dockRequested?.Invoke();
      state = DockState.Docking;
      animationStart = movementTarget.anchoredPosition;
      animationTarget = fixedDockedPosition;
      animationElapsed = 0f;
      tabReveal = 0f;
      SetExpandedInteraction(false);
      ApplyTabVisual();
    }

    public void ExpandToDefault()
    {
      if (state != DockState.Docked || movementTarget == null)
        return;

      if (!TryRecalculatePositions())
        return;
      state = DockState.Expanding;
      animationStart = movementTarget.anchoredPosition;
      animationTarget = defaultExpandedPosition;
      animationElapsed = 0f;
      tabReveal = 0f;
      SetExpandedInteraction(false);
      ApplyTabVisual();
    }

    private void Awake()
    {
      BindButtons();
      ApplyTabVisual();
    }

    private void OnEnable()
    {
      lastCanvasSize = new Vector2(float.NaN, float.NaN);
      TryRecalculatePositions();
      ApplyTabVisual();
    }

    private void OnDisable()
    {
      FinishAnimation();
      tabReveal = 0f;
      ApplyTabVisual();
    }

    private void Update()
    {
      UpdatePanelAnimation();
      UpdateTabReveal();
    }

    private void LateUpdate()
    {
      if (canvasRoot == null)
        return;

      Vector2 canvasSize = canvasRoot.rect.size;
      if (canvasSize == lastCanvasSize)
        return;

      lastCanvasSize = canvasSize;
      if (!TryRecalculatePositions())
        return;
      if (state == DockState.Docked)
        movementTarget.anchoredPosition = fixedDockedPosition;
      else if (state == DockState.Expanded)
        dragHandle?.ClampToScreen();
      else
        FinishAnimation();
      ApplyTabVisual();
    }

    private void BindButtons()
    {
      if (dockButton != null)
      {
        dockButton.onClick.RemoveListener(Dock);
        dockButton.onClick.AddListener(Dock);
      }
      if (dockTabButton != null)
      {
        dockTabButton.onClick.RemoveListener(RequestExpand);
        dockTabButton.onClick.AddListener(RequestExpand);
      }
    }

    private void RequestExpand()
    {
      if (state != DockState.Docked)
        return;
      if (expandRequested != null)
        expandRequested.Invoke();
      else
        ExpandToDefault();
    }

    private bool SetPlacementReference(Rect controlsScreenRect, bool hasControlsScreenRect)
    {
      nativeControlsScreenRect = controlsScreenRect;
      hasNativeControlsScreenRect = hasControlsScreenRect;
      return TryRecalculatePositions();
    }

    private bool TryRecalculatePositions()
    {
      if (canvasRoot == null || movementTarget == null || dockTab == null)
        return false;

      Rect canvasBounds = canvasRoot.rect;
      Rect panelBounds = movementTarget.rect;
      float desiredRight = canvasBounds.xMax - screenMargin;
      float desiredBottom = canvasBounds.yMin + 112f;
      if (
        hasNativeControlsScreenRect
        && RectTransformUtility.ScreenPointToLocalPointInRectangle(
          canvasRoot,
          new Vector2(nativeControlsScreenRect.xMax, nativeControlsScreenRect.yMax),
          null,
          out Vector2 nativeTopRight
        )
      )
      {
        desiredRight = nativeTopRight.x;
        desiredBottom = nativeTopRight.y + nativeControlsGap;
      }

      Vector2 desiredCenter = new Vector2(desiredRight - panelBounds.xMax, desiredBottom - panelBounds.yMin);
      defaultExpandedPosition = dragHandle != null ? dragHandle.ClampPosition(desiredCenter) : desiredCenter;

      fixedDockedPosition = new Vector2(canvasBounds.xMax - panelBounds.xMin + 1f, defaultExpandedPosition.y);

      Rect tabBounds = dockTab.rect;
      tabShownPosition = new Vector2(canvasBounds.xMax - tabBounds.xMax, defaultExpandedPosition.y);
      tabHiddenPosition = new Vector2(canvasBounds.xMax - tabBounds.xMin + 1f, defaultExpandedPosition.y);
      return true;
    }

    private void UpdatePanelAnimation()
    {
      if (state != DockState.Docking && state != DockState.Expanding)
        return;

      animationElapsed += Time.unscaledDeltaTime;
      float normalized = Mathf.Clamp01(animationElapsed / panelAnimationDuration);
      float eased = normalized * normalized * (3f - 2f * normalized);
      movementTarget.anchoredPosition = Vector2.LerpUnclamped(animationStart, animationTarget, eased);
      if (normalized >= 1f)
        FinishAnimation();
    }

    private void FinishAnimation()
    {
      if (state == DockState.Docking)
      {
        state = DockState.Docked;
        if (movementTarget != null)
          movementTarget.anchoredPosition = fixedDockedPosition;
        SetExpandedInteraction(false);
      }
      else if (state == DockState.Expanding)
      {
        state = DockState.Expanded;
        if (movementTarget != null)
          movementTarget.anchoredPosition = defaultExpandedPosition;
        SetExpandedInteraction(true);
      }
      animationElapsed = 0f;
    }

    private void UpdateTabReveal()
    {
      bool shouldShow = state == DockState.Docked;
      float target = shouldShow ? 1f : 0f;
      float step = Time.unscaledDeltaTime / tabRevealDuration;
      float nextReveal = Mathf.MoveTowards(tabReveal, target, step);
      if (Mathf.Approximately(nextReveal, tabReveal))
        return;

      tabReveal = nextReveal;
      ApplyTabVisual();
    }

    private void ApplyTabVisual()
    {
      if (dockTab != null)
        dockTab.anchoredPosition = Vector2.LerpUnclamped(tabHiddenPosition, tabShownPosition, tabReveal);
      if (dockTabCanvasGroup == null)
        return;

      dockTabCanvasGroup.alpha = tabReveal;
      bool receivesInput = state == DockState.Docked && tabReveal >= 0.95f;
      dockTabCanvasGroup.interactable = receivesInput;
      dockTabCanvasGroup.blocksRaycasts = receivesInput;
    }

    private void SetExpandedInteraction(bool interactable)
    {
      dragHandle?.SetInteractionEnabled(interactable);
      if (dockButton != null)
        dockButton.interactable = interactable;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
      screenMargin = Mathf.Max(0f, screenMargin);
      nativeControlsGap = Mathf.Max(0f, nativeControlsGap);
      panelAnimationDuration = Mathf.Max(0.01f, panelAnimationDuration);
      tabRevealDuration = Mathf.Max(0.01f, tabRevealDuration);
    }
#endif
  }
}
