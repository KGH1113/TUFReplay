using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace TUFReplay.Unity.ReplayTimeline
{
  [DisallowMultipleComponent]
  [AddComponentMenu("UI/TUFReplay/Timeline Seek Input")]
  public sealed class UITimelineSeekInput
    : MonoBehaviour,
      IPointerDownHandler,
      IBeginDragHandler,
      IDragHandler,
      IPointerUpHandler,
      ICancelHandler
  {
    [SerializeField]
    private RectTransform inputRect;

    [SerializeField]
    private bool interactable = true;

    private UnityAction onBegin;
    private UnityAction<float> onPreview;
    private UnityAction<float> onCommit;
    private UnityAction onCancel;
    private bool pointerActive;

    public void Configure(RectTransform target)
    {
      inputRect = target;
    }

    public void Bind(
      UnityAction beginCallback,
      UnityAction<float> previewCallback,
      UnityAction<float> commitCallback,
      UnityAction cancelCallback
    )
    {
      onBegin = beginCallback;
      onPreview = previewCallback;
      onCommit = commitCallback;
      onCancel = cancelCallback;
    }

    public void SetInteractable(bool value)
    {
      if (interactable == value)
        return;

      interactable = value;
      if (!interactable)
        CancelPointer();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
      if (!interactable || eventData.button != PointerEventData.InputButton.Left || inputRect == null)
        return;

      pointerActive = true;
      onBegin?.Invoke();
      Preview(eventData);
      eventData.Use();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
      if (pointerActive)
        eventData.Use();
    }

    public void OnDrag(PointerEventData eventData)
    {
      if (!pointerActive)
        return;

      Preview(eventData);
      eventData.Use();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
      if (!pointerActive || eventData.button != PointerEventData.InputButton.Left)
        return;

      float normalized = NormalizedPosition(eventData);
      pointerActive = false;
      onPreview?.Invoke(normalized);
      onCommit?.Invoke(normalized);
      eventData.Use();
    }

    public void OnCancel(BaseEventData eventData)
    {
      CancelPointer();
    }

    private void OnDisable()
    {
      CancelPointer();
    }

    private void Preview(PointerEventData eventData)
    {
      onPreview?.Invoke(NormalizedPosition(eventData));
    }

    private float NormalizedPosition(PointerEventData eventData)
    {
      if (
        inputRect == null
        || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
          inputRect,
          eventData.position,
          eventData.pressEventCamera,
          out Vector2 localPoint
        )
      )
        return 0f;

      Rect rect = inputRect.rect;
      if (rect.width <= 0f)
        return 0f;
      return Mathf.Clamp01((localPoint.x - rect.xMin) / rect.width);
    }

    private void CancelPointer()
    {
      if (!pointerActive)
        return;

      pointerActive = false;
      onCancel?.Invoke();
    }
  }
}
