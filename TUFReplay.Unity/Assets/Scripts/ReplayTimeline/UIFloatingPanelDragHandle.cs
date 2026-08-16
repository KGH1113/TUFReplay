using UnityEngine;
using UnityEngine.EventSystems;

namespace TUFReplay.Unity.ReplayTimeline
{
  [AddComponentMenu("UI/TUFReplay/Floating Panel Drag Handle")]
  public sealed class UIFloatingPanelDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
  {
    [SerializeField]
    private RectTransform movementTarget;

    [SerializeField]
    private RectTransform[] excludedAreas;

    [SerializeField, Min(0f)]
    private float screenMargin = 16f;

    private RectTransform parentRect;
    private Vector2 pointerOffset;
    private Vector2 lastParentSize = new Vector2(float.NaN, float.NaN);
    private bool isDragging;
    private bool interactionEnabled = true;

    public bool IsDragging => isDragging;

    public bool ContainsScreenPoint(Vector2 screenPoint)
    {
      return movementTarget != null
        && movementTarget.gameObject.activeInHierarchy
        && RectTransformUtility.RectangleContainsScreenPoint(movementTarget, screenPoint);
    }

    public void Configure(RectTransform target, RectTransform[] exclusions, float margin)
    {
      movementTarget = target;
      excludedAreas = exclusions;
      screenMargin = Mathf.Max(0f, margin);
      EnsureInitialized();
      ClampToScreen();
    }

    public void SetInteractionEnabled(bool enabled)
    {
      interactionEnabled = enabled;
      if (!interactionEnabled)
        isDragging = false;
    }

    public Vector2 ClampPosition(Vector2 anchoredPosition)
    {
      if (!EnsureInitialized())
        return anchoredPosition;
      return ClampAnchoredPosition(anchoredPosition);
    }

    public void ClampToScreen()
    {
      if (!EnsureInitialized())
        return;
      movementTarget.anchoredPosition = ClampAnchoredPosition(movementTarget.anchoredPosition);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
      if (!interactionEnabled || !EnsureInitialized())
        return;
      if (excludedAreas != null)
      {
        for (int index = 0; index < excludedAreas.Length; index++)
        {
          RectTransform excludedArea = excludedAreas[index];
          if (
            excludedArea != null
            && RectTransformUtility.RectangleContainsScreenPoint(
              excludedArea,
              eventData.position,
              eventData.pressEventCamera
            )
          )
            return;
        }
      }
      if (
        !RectTransformUtility.ScreenPointToLocalPointInRectangle(
          parentRect,
          eventData.position,
          eventData.pressEventCamera,
          out Vector2 pointerPosition
        )
      )
        return;

      pointerOffset = movementTarget.anchoredPosition - LocalPointToAnchoredPosition(pointerPosition);
      isDragging = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
      if (!interactionEnabled || !isDragging || !EnsureInitialized())
        return;
      if (
        !RectTransformUtility.ScreenPointToLocalPointInRectangle(
          parentRect,
          eventData.position,
          eventData.pressEventCamera,
          out Vector2 pointerPosition
        )
      )
        return;

      movementTarget.anchoredPosition = ClampAnchoredPosition(
        LocalPointToAnchoredPosition(pointerPosition) + pointerOffset
      );
    }

    public void OnEndDrag(PointerEventData eventData)
    {
      isDragging = false;
    }

    private void Awake()
    {
      EnsureInitialized();
    }

    private void OnEnable()
    {
      EnsureInitialized();
    }

    private void OnDisable()
    {
      isDragging = false;
    }

    private void LateUpdate()
    {
      if (!EnsureInitialized())
        return;
      Vector2 parentSize = parentRect.rect.size;
      if (parentSize == lastParentSize)
        return;

      lastParentSize = parentSize;
      if (!interactionEnabled)
        return;
      ClampToScreen();
    }

    private bool EnsureInitialized()
    {
      if (movementTarget == null)
      {
        parentRect = null;
        return false;
      }

      RectTransform currentParent = movementTarget.parent as RectTransform;
      if (currentParent == null)
      {
        parentRect = null;
        return false;
      }

      if (parentRect != currentParent)
      {
        parentRect = currentParent;
        lastParentSize = new Vector2(float.NaN, float.NaN);
      }
      return true;
    }

    private Vector2 LocalPointToAnchoredPosition(Vector2 localPoint)
    {
      Vector2 anchorReference = new Vector2(
        Mathf.Lerp(parentRect.rect.xMin, parentRect.rect.xMax, movementTarget.anchorMin.x),
        Mathf.Lerp(parentRect.rect.yMin, parentRect.rect.yMax, movementTarget.anchorMin.y)
      );
      return localPoint - anchorReference;
    }

    private Vector2 ClampAnchoredPosition(Vector2 anchoredPosition)
    {
      Rect parentBounds = parentRect.rect;
      Rect targetBounds = movementTarget.rect;
      Vector2 anchorReference = new Vector2(
        Mathf.Lerp(parentBounds.xMin, parentBounds.xMax, movementTarget.anchorMin.x),
        Mathf.Lerp(parentBounds.yMin, parentBounds.yMax, movementTarget.anchorMin.y)
      );
      Vector2 pivotPosition = anchorReference + anchoredPosition;

      float minX = parentBounds.xMin + screenMargin - targetBounds.xMin;
      float maxX = parentBounds.xMax - screenMargin - targetBounds.xMax;
      float minY = parentBounds.yMin + screenMargin - targetBounds.yMin;
      float maxY = parentBounds.yMax - screenMargin - targetBounds.yMax;

      pivotPosition.x = minX > maxX ? (minX + maxX) * 0.5f : Mathf.Clamp(pivotPosition.x, minX, maxX);
      pivotPosition.y = minY > maxY ? (minY + maxY) * 0.5f : Mathf.Clamp(pivotPosition.y, minY, maxY);
      return pivotPosition - anchorReference;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
      screenMargin = Mathf.Max(0f, screenMargin);
    }
#endif
  }
}
