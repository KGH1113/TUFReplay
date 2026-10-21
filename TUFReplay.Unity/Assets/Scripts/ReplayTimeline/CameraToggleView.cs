using UnityEngine;
using UnityEngine.UI;

namespace TUFReplay.Unity.CameraSetup
{
  [ExecuteAlways]
  public sealed class CameraToggleView : MonoBehaviour
  {
    [SerializeField]
    private Toggle toggle;

    [SerializeField]
    private Graphic track;

    [SerializeField]
    private RectTransform thumb;
    private float _position;
    private float _velocity;
    private static readonly Color OffColor = new Color32(65, 69, 78, 255);
    private static readonly Color OnColor = new Color32(124, 207, 0, 255);

    public void Configure(Toggle value, Graphic surface, RectTransform knob)
    {
      toggle = value;
      track = surface;
      thumb = knob;
      _position = toggle.isOn ? 1 : 0;
      Refresh();
    }

    private void OnEnable()
    {
      if (toggle == null)
        return;
      _position = toggle.isOn ? 1 : 0;
      _velocity = 0;
      Refresh();
    }

    private void Update()
    {
      if (toggle == null || track == null || thumb == null)
        return;
      float target = toggle.isOn ? 1 : 0;
      _position = Application.isPlaying
        ? Mathf.SmoothDamp(_position, target, ref _velocity, 0.08f, Mathf.Infinity, Time.unscaledDeltaTime)
        : target;
      Refresh();
    }

    private void Refresh()
    {
      if (track == null || thumb == null)
        return;
      track.color = Color.Lerp(OffColor, OnColor, _position);
      thumb.anchoredPosition = new Vector2(Mathf.Lerp(-10, 10, _position), 0);
    }
  }
}
