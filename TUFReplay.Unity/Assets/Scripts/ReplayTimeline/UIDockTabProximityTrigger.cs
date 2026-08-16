using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace TUFReplay.Unity.ReplayTimeline
{
  [DisallowMultipleComponent]
  [AddComponentMenu("UI/TUFReplay/Dock Tab Proximity Trigger")]
  public sealed class UIDockTabProximityTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
  {
    private UnityAction<bool> proximityChanged;

    public void Configure(UnityAction<bool> callback)
    {
      proximityChanged = callback;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
      proximityChanged?.Invoke(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
      proximityChanged?.Invoke(false);
    }

    private void OnDisable()
    {
      proximityChanged?.Invoke(false);
    }
  }
}
