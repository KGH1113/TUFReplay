using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TUFReplay.Features.Ui;

public sealed class MicrophoneRecordingToastPointerRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
  public Action<bool> HoverChanged { get; set; }

  public void OnPointerEnter(PointerEventData eventData)
  {
    HoverChanged?.Invoke(true);
  }

  public void OnPointerExit(PointerEventData eventData)
  {
    HoverChanged?.Invoke(false);
  }

  private void OnDisable()
  {
    HoverChanged?.Invoke(false);
  }
}
