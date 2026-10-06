using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TUFReplay.Unity.CameraSetup
{
  public sealed class CameraSetupView : MonoBehaviour
  {
    [SerializeField]
    private RawImage preview;

    [SerializeField]
    private TMP_Text heading;

    [SerializeField]
    private TMP_Text description;

    [SerializeField]
    private TMP_Text previewHint;

    [SerializeField]
    private TMP_Text deviceName;

    [SerializeField]
    private TMP_Text cameraLabel;

    [SerializeField]
    private TMP_Text liveLabel;

    [SerializeField]
    private TMP_Text privacyHint;

    [SerializeField]
    private TMP_Text errorText;

    [SerializeField]
    private Toggle cameraToggle;

    [SerializeField]
    private Toggle liveToggle;

    [SerializeField]
    private Button continueButton;

    [SerializeField]
    private Button withoutCameraButton;

    [SerializeField]
    private Button cancelButton;

    [SerializeField]
    private Button retryButton;

    [SerializeField]
    private Button revealButton;
    private Action<bool> _cameraChanged;
    private Action<bool> _liveChanged;
    private Action _continue;
    private Action _withoutCamera;
    private Action _cancel;
    private Action _retry;
    private bool _bound;
    private bool _korean;
    private bool _cameraEnabled;
    private bool _previewRevealed;
    private Texture _previewTexture;
    public bool IsConfigured =>
      preview != null
      && heading != null
      && description != null
      && previewHint != null
      && deviceName != null
      && cameraLabel != null
      && liveLabel != null
      && privacyHint != null
      && errorText != null
      && cameraToggle != null
      && liveToggle != null
      && continueButton != null
      && withoutCameraButton != null
      && cancelButton != null
      && retryButton != null
      && revealButton != null;

    public void Configure(
      RawImage image,
      TMP_Text title,
      TMP_Text detail,
      TMP_Text placeholder,
      TMP_Text device,
      TMP_Text camera,
      TMP_Text live,
      TMP_Text privacy,
      TMP_Text error,
      Toggle cameraSwitch,
      Toggle liveSwitch,
      Button start,
      Button without,
      Button cancel,
      Button retry,
      Button reveal
    )
    {
      preview = image;
      heading = title;
      description = detail;
      previewHint = placeholder;
      deviceName = device;
      cameraLabel = camera;
      liveLabel = live;
      privacyHint = privacy;
      errorText = error;
      cameraToggle = cameraSwitch;
      liveToggle = liveSwitch;
      continueButton = start;
      withoutCameraButton = without;
      cancelButton = cancel;
      retryButton = retry;
      revealButton = reveal;
    }

    public void Show(
      bool korean,
      bool canCancel,
      Action<bool> cameraChanged,
      Action<bool> liveChanged,
      Action start,
      Action without,
      Action cancel,
      Action retry
    )
    {
      _korean = korean;
      _cameraChanged = cameraChanged;
      _liveChanged = liveChanged;
      _continue = start;
      _withoutCamera = without;
      _cancel = cancel;
      _retry = retry;
      ConcealPreview();
      if (!_bound)
      {
        cameraToggle.onValueChanged.AddListener(value => _cameraChanged?.Invoke(value));
        liveToggle.onValueChanged.AddListener(value => _liveChanged?.Invoke(value));
        continueButton.onClick.AddListener(() => _continue?.Invoke());
        withoutCameraButton.onClick.AddListener(() => _withoutCamera?.Invoke());
        cancelButton.onClick.AddListener(() => _cancel?.Invoke());
        retryButton.onClick.AddListener(() => _retry?.Invoke());
        revealButton.onClick.AddListener(RevealPreview);
        _bound = true;
      }
      heading.text = korean ? "플레이 전에 카메라를 확인해요" : "Check your camera before playing";
      description.text = korean
        ? "게임을 실행한 뒤 첫 플레이에서만 확인해요."
        : "You’ll see this once each time you open the game.";
      cameraLabel.text = korean ? "카메라 켜기" : "Camera on";
      liveLabel.text = korean ? "플레이 중 실시간으로 표시" : "Show live camera while playing";
      privacyHint.text = korean
        ? "카메라를 켜면 게임이 열린 동안 계속 읽어요. 영상은 플레이 중에만 저장하고, 리플레이에서 함께 재생해요.\n\n위치와 크기는 게임 안에서 드래그해 조절할 수 있어요."
        : "When on, your camera stays active while the game is open. Video is saved only during runs and plays with your replay.\n\nDrag the camera in the game to move or resize it.";
      continueButton.GetComponentInChildren<TMP_Text>().text = korean ? "플레이 시작" : "Start run";
      withoutCameraButton.GetComponentInChildren<TMP_Text>().text = korean
        ? "카메라 없이 시작"
        : "Start without camera";
      cancelButton.GetComponentInChildren<TMP_Text>().text = korean ? "돌아가기" : "Back";
      retryButton.GetComponentInChildren<TMP_Text>().text = korean ? "다시 연결" : "Reconnect";
      revealButton.GetComponentInChildren<TMP_Text>().text = korean ? "클릭해서 보기" : "Click to reveal";
      cancelButton.gameObject.SetActive(canCancel);
      gameObject.SetActive(true);
    }

    public void SetState(
      bool enabled,
      bool liveVisible,
      bool ready,
      bool supported,
      string device,
      string error,
      Texture texture,
      bool mirror,
      Rect? crop = null,
      bool flipVertical = false
    )
    {
      cameraToggle.SetIsOnWithoutNotify(enabled);
      liveToggle.SetIsOnWithoutNotify(liveVisible);
      cameraToggle.interactable = supported;
      liveToggle.interactable = supported;
      continueButton.interactable = !enabled || ready;
      deviceName.text = device ?? (_korean ? "기본 카메라" : "Default camera");
      _cameraEnabled = enabled;
      _previewTexture = enabled ? texture : null;
      if (!enabled)
        _previewRevealed = false;
      revealButton.interactable = supported;
      Rect area = crop ?? new Rect(0, 0, 1, 1);
      preview.uvRect = new Rect(
        mirror ? area.x + area.width : area.x,
        flipVertical ? 1 - area.y : 1 - area.y - area.height,
        mirror ? -area.width : area.width,
        flipVertical ? -area.height : area.height
      );
      AspectRatioFitter fit = preview.GetComponent<AspectRatioFitter>();
      if (fit != null && texture != null)
        fit.aspectRatio = (float)texture.width / texture.height * area.width / area.height;
      UpdatePreviewVisibility();
      previewHint.text = enabled
        ? (
          _korean
            ? "카메라 접근을 허용하면\n여기에서 영상을 볼 수 있어요."
            : "Allow camera access\nto see your preview here."
        )
        : (_korean ? "카메라가 꺼져 있어요" : "Camera is off");
      errorText.text =
        error == null
          ? ""
          : (
            _korean
              ? "카메라를 읽지 못했어요. 접근 권한과 연결을 확인한 뒤 다시 연결해 주세요. 카메라 없이도 플레이할 수 있어요."
              : "The camera could not start. Check permission and the connection, then reconnect. You can also start without a camera."
          );
      errorText.gameObject.SetActive(error != null);
      retryButton.gameObject.SetActive(error != null);
    }

    public void Hide()
    {
      ConcealPreview();
      gameObject.SetActive(false);
    }

    private void RevealPreview()
    {
      if (!_cameraEnabled)
        return;
      _previewRevealed = true;
      UpdatePreviewVisibility();
    }

    private void ConcealPreview()
    {
      _previewRevealed = false;
      _previewTexture = null;
      UpdatePreviewVisibility();
    }

    private void UpdatePreviewVisibility()
    {
      bool visible = _cameraEnabled && _previewRevealed && _previewTexture != null;
      // Keep the image unbound as well as disabled so a camera frame cannot
      // appear before the user reveals it, even while the UI is refreshing.
      preview.enabled = visible;
      preview.texture = visible ? _previewTexture : null;
      preview.color = visible ? Color.white : Color.clear;
      revealButton.gameObject.SetActive(_cameraEnabled && !_previewRevealed);
      previewHint.gameObject.SetActive(!_cameraEnabled || (_previewRevealed && _previewTexture == null));
    }
  }
}
