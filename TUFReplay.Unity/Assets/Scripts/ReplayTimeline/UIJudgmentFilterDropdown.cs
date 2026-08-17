using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TUFReplay.Unity.ReplayTimeline
{
  [DisallowMultipleComponent]
  [AddComponentMenu("UI/TUFReplay/Judgment Filter Dropdown")]
  public sealed class UIJudgmentFilterDropdown : MonoBehaviour, ICancelHandler
  {
    private const float ScreenMargin = 16f;
    private const float PanelGap = 6f;

    [SerializeField]
    private RectTransform canvasRoot;

    [SerializeField]
    private RectTransform floatingPanel;

    [SerializeField]
    private RectTransform popup;

    [SerializeField]
    private GameObject blocker;

    [SerializeField]
    private Button openButton;

    [SerializeField]
    private Button blockerButton;

    [SerializeField]
    private TMP_Text buttonLabel;

    [SerializeField]
    private TMP_Text countLabel;

    [SerializeField]
    private RectTransform chevron;

    [SerializeField]
    private Toggle[] categoryToggles;

    [SerializeField]
    private ReplayJudgmentKind[] categories;

    [SerializeField]
    private UIJudgmentMarkerGraphic markerGraphic;

    private bool interactionsBound;
    private bool isOpen;

    public void Configure(
      RectTransform root,
      RectTransform panel,
      RectTransform popupRect,
      GameObject outsideBlocker,
      Button dropdownButton,
      Button outsideButton,
      TMP_Text label,
      TMP_Text selectedCountLabel,
      RectTransform dropdownChevron,
      Toggle[] toggles,
      ReplayJudgmentKind[] judgmentCategories,
      UIJudgmentMarkerGraphic markers
    )
    {
      canvasRoot = root;
      floatingPanel = panel;
      popup = popupRect;
      blocker = outsideBlocker;
      openButton = dropdownButton;
      blockerButton = outsideButton;
      buttonLabel = label;
      countLabel = selectedCountLabel;
      if (buttonLabel != null)
        buttonLabel.text = "Judgements";
      chevron = dropdownChevron;
      categoryToggles = toggles;
      categories = judgmentCategories;
      markerGraphic = markers;
      ResetSelection();
    }

    public void SetMarkers(ReplayJudgmentMarker[] markers)
    {
      markerGraphic?.SetMarkers(markers);
    }

    public void ResetSelection()
    {
      if (categoryToggles != null)
      {
        foreach (Toggle toggle in categoryToggles)
          toggle?.SetIsOnWithoutNotify(false);
      }
      if (markerGraphic != null)
        markerGraphic.VisibleMask = 0;
      UpdateButtonCount(0);
      CloseDropdown();
    }

    public void CloseDropdown()
    {
      isOpen = false;
      if (popup != null)
        popup.gameObject.SetActive(false);
      if (blocker != null)
        blocker.SetActive(false);
      if (chevron != null)
        chevron.localRotation = Quaternion.identity;
    }

    private void Awake()
    {
      BindInteractions();
      CloseDropdown();
      RefreshFilter();
    }

    private void OnEnable()
    {
      BindInteractions();
      RefreshFilter();
    }

    private void OnDisable()
    {
      CloseDropdown();
    }

    public void OnCancel(BaseEventData eventData)
    {
      if (isOpen)
      {
        CloseDropdown();
        eventData?.Use();
      }
    }

    private void BindInteractions()
    {
      if (interactionsBound)
        return;

      openButton?.onClick.AddListener(ToggleDropdown);
      blockerButton?.onClick.AddListener(CloseDropdown);
      if (categoryToggles != null)
      {
        foreach (Toggle toggle in categoryToggles)
          toggle?.onValueChanged.AddListener(OnSelectionChanged);
      }
      interactionsBound = true;
    }

    private void ToggleDropdown()
    {
      if (isOpen)
      {
        CloseDropdown();
        return;
      }

      OpenDropdown();
    }

    private void OpenDropdown()
    {
      if (popup == null || blocker == null || canvasRoot == null || floatingPanel == null)
        return;

      blocker.SetActive(true);
      popup.gameObject.SetActive(true);
      blocker.transform.SetAsLastSibling();
      popup.SetAsLastSibling();
      PositionPopup();
      isOpen = true;
      if (chevron != null)
        chevron.localRotation = Quaternion.Euler(0f, 0f, 180f);
    }

    private void PositionPopup()
    {
      Canvas.ForceUpdateCanvases();
      RectTransform buttonRect = openButton != null ? openButton.transform as RectTransform : null;
      if (buttonRect == null)
        return;

      Vector3[] corners = new Vector3[4];
      buttonRect.GetWorldCorners(corners);
      Vector2 buttonBottomLeft = canvasRoot.InverseTransformPoint(corners[0]);
      Vector2 buttonTopLeft = canvasRoot.InverseTransformPoint(corners[1]);
      Vector2 buttonTopRight = canvasRoot.InverseTransformPoint(corners[2]);

      Rect canvasBounds = canvasRoot.rect;
      Vector2 size = popup.rect.size;
      float availableAbove = canvasBounds.yMax - ScreenMargin - buttonTopLeft.y - PanelGap;
      float availableBelow = buttonBottomLeft.y - PanelGap - (canvasBounds.yMin + ScreenMargin);
      bool openBelow = availableBelow >= size.y || availableBelow >= availableAbove;

      // Keep the popup attached to the trigger instead of to the movable panel's old left edge.
      float x = buttonTopRight.x - size.x * 0.5f;
      float y = openBelow
        ? buttonBottomLeft.y - PanelGap - size.y * 0.5f
        : buttonTopLeft.y + PanelGap + size.y * 0.5f;
      x = Mathf.Clamp(x, canvasBounds.xMin + ScreenMargin + size.x * 0.5f, canvasBounds.xMax - ScreenMargin - size.x * 0.5f);
      y = Mathf.Clamp(y, canvasBounds.yMin + ScreenMargin + size.y * 0.5f, canvasBounds.yMax - ScreenMargin - size.y * 0.5f);
      popup.anchoredPosition = new Vector2(x, y);
    }

    private void OnSelectionChanged(bool _)
    {
      RefreshFilter();
    }

    private void RefreshFilter()
    {
      int count = 0;
      int mask = 0;
      int length = Mathf.Min(categoryToggles?.Length ?? 0, categories?.Length ?? 0);
      for (int index = 0; index < length; index++)
      {
        if (categoryToggles[index] == null || !categoryToggles[index].isOn)
          continue;
        count++;
        mask |= 1 << (int)categories[index];
      }
      if (markerGraphic != null)
        markerGraphic.VisibleMask = mask;
      UpdateButtonCount(count);
    }

    private void UpdateButtonCount(int count)
    {
      if (countLabel != null)
        countLabel.text = count.ToString();
    }
  }
}
