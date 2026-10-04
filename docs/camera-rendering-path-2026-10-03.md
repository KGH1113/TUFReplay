# 현재 카메라 렌더링 경로

현재 워크트리의 **플레이 중 카메라와 리플레이 카메라 출력은 OnGUI**로 구현되어 있다. `WebcamReplayOverlay.OnGUI()`에서 `GUI.DrawTextureWithTexCoords()`로 영상을 그리고 IMGUI 이벤트로 드래그·리사이즈를 처리한다. 내가 구현한 이 부분이 해당한다. [오버레이 코드](/Users/kgh/dev/src/tuf-replay-webcam/TUFReplay/Webcam/Playback/WebcamReplayOverlay.cs:103).

영상 프레임 읽기와 GPU 업로드는 별도 경로다. 모드의 Update에서 `CameraLivePreview.Tick()`을 호출하고, 공유 메모리에서 **새 프레임을 복사했을 때만** `LoadRawTextureData()`와 `Apply(false, false)`를 실행한다. 텍스처는 최초 프레임이나 크기가 변했을 때 생성하고 나머지 프레임에는 재사용한다. 리플레이는 `VideoPlayer.texture`를 표시한다. [라이브 프레임 처리](/Users/kgh/dev/src/tuf-replay-webcam/TUFReplay/Webcam/Playback/CameraLivePreview.cs:37), [모드 Update 호출](/Users/kgh/dev/src/tuf-replay-webcam/TUFReplay/Composition/Main.cs:153).

첫 run에 나오는 카메라 확인 모달은 이미 uGUI `RawImage`를 사용한다. 현재 상시 표시 오버레이와 모달의 표시 방식이 다르다. [모달의 RawImage](/Users/kgh/dev/src/tuf-replay-webcam/TUFReplay.Unity/Assets/Scripts/ReplayTimeline/CameraSetupView.cs:11).

OnGUI는 이벤트마다 호출되어 한 게임 프레임에 여러 번 실행될 수 있다. 현재 오버레이도 그때마다 위치·크롭·control ID 등의 처리를 수행한다. 다만 Unity의 공개 참조 구현에서는 `DrawTextureWithTexCoords`의 실제 드로우가 Repaint 이벤트에서 실행되므로, 여러 OnGUI 호출을 여러 번의 영상 읽기·업로드 또는 GPU 드로우와 동일하게 계산하면 안 된다. [Unity OnGUI 문서](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.OnGUI.html), [Unity GUI 참조 구현](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Modules/IMGUI/GUI.cs).

OnGUI 자체, 프레임 복사, 텍스처 업로드가 각각 얼마나 프레임 시간을 차지하는지는 현재 실행 환경에서 측정하지 않았다. 따라서 이 구현만 보고 큰 FPS 저하의 원인을 확정할 수는 없다. 상시 카메라 UI를 모달과 같은 uGUI RawImage로 통일하면 표시와 조작을 IMGUI 이벤트 경로에서 분리할 수 있지만, 프레임 업로드 비용은 따로 남는다.

이번 확인에서는 소스 코드를 변경하거나 게임을 실행하지 않았다.
