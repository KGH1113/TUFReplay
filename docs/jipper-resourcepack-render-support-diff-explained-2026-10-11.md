# JipperResourcePack의 TUFReplay 렌더 지원 diff 전체 해설

## 비교 기준과 읽는 방법

- 조사일: 2026-10-11 (Asia/Seoul).
- checkout: /Users/kgh/dev/src/jipper-resourcepack.
- 브랜치: feat/tufreplay-render-support.
- 비교 기준: 현재 HEAD ac6d6410571ee7d585aedce7f5085de34f615af2 대비 작업 디렉토리의 변경.
- 기존 파일 13개 수정, 새 파일 9개 추가, 총 22개 파일.
- 기존 파일 diff는 +161 / -27행. 새 파일 629행을 합치면 +790 / -27행이다. newline 정리도 포함한 텍스트 수치다.
- Git에 아직 추적되지 않는 새 파일도 전부 포함했다. 기존 파일은 실제 unified diff, 새 파일은 실제 현재 소스 전체를 아래에 넣었다. 임의로 생략한 코드나 의사코드는 없다.
- 이 문서는 조사 시점의 스냅샷이다. 이후 main thread에서 변경하면 실제 파일과 달라질 수 있다. 이 정리 작업은 JRP 코드·git 상태·설치 DLL을 수정하거나 테스트/렌더를 실행하지 않았다.

## 전체 구조

현재 JRP는 키뷰어와 상태 오버레이를 각각 공용 SDK에 등록한다. Renderer가 SDK에 프레임 시간과 held-key 상태를 공급하면 SDK가 JRP의 BeginRender/AdvanceFrame/EndRender를 호출한다. JRP는 기존 UI 로직으로 같은 Unity Canvas를 갱신하고, 픽셀 캡처와 영상 인코딩은 Renderer가 수행한다.

키뷰어는 별도 임시 레이아웃과 카운터를 만든다. 상태 오버레이는 기존 UI를 스냅샷하고 복구한다. 평소의 SkyHook 워커는 유지하되 렌더 중 실제 입력을 복구용 상태에만 보관한다. 변경 전체가 시간 wrapper 교체 한 가지로 끝나지는 않는다.

| 구분 | 내용 | 파일 수 |
| --- | --- | ---: |
| SDK native 연결 | 원래 Unity 시간·입력·환경 값 공급 | 1 |
| 키뷰어 | 등록, 임시 상태, 입력 경계, 저장 차단, 레이닝 | 6 |
| 메인·상태 오버레이 | 자동 갱신 분리, 스냅샷 복구, 플레이 기록 격리, 오류 전달 | 5 |
| 빌드·패키징 | 경로·참조·alias·스크립트·ignore | 6 |
| 문서 | README 사용·빌드 안내 | 1 |
| 테스트 | 운영 Rain 생성·풀 코드의 좌표 회귀 검사 | 3 |

## 파일별 목차

1. [JipperResourcePack/UnityNativeServices.cs](#file-1) — SDK에 평소의 Unity 값 제공
2. [JipperResourcePack/KeyViewerContents/KeyViewer.cs](#file-2) — Stopwatch 교체와 키뷰어 등록·해제
3. [JipperResourcePack/KeyViewerContents/KeyViewer.Render.cs](#file-3) — 키뷰어의 렌더 수명주기·입력 전달·복구
4. [JipperResourcePack/KeyViewerContents/KeyViewer.Update.cs](#file-4) — 실제 입력과 렌더 입력의 경계 분리
5. [JipperResourcePack/KeyViewerContents/KeyCountData.cs](#file-5) — 렌더용 키 카운터의 저장 차단
6. [JipperResourcePack/KeyViewerContents/RainManager.cs](#file-6) — 레이닝 갱신을 프레임 콜백에서 호출 가능하게 분리
7. [JipperResourcePack/KeyViewerContents/Rain.cs](#file-7) — 동적으로 만든 레이닝을 부모 UI 평면에 배치
8. [JipperResourcePack/Main.cs](#file-8) — 평소 자동 갱신과 렌더 프레임 갱신의 중복 방지
9. [JipperResourcePack/OverlayContents/Overlay.cs](#file-9) — 상태 오버레이 등록·가상 콤보 시간·오류 전달
10. [JipperResourcePack/OverlayContents/Overlay.Render.cs](#file-10) — 상태 오버레이의 스냅샷·초기화·복구
11. [JipperResourcePack/OverlayContents/PlayCount.cs](#file-11) — 렌더용 플레이 기록을 별도 메모리에 격리
12. [JipperResourcePack/OverlayContents/Status.cs](#file-12) — Harmony using 정리
13. [Directory.Build.props](#file-13) — 게임·모드·공용 SDK 경로 설정
14. [Directory.Build.targets](#file-14) — 설치 환경의 참조 Assembly 해결
15. [JipperResourcePack/JipperResourcePack.csproj](#file-15) — SDK DLL 포함과 참조 경로·Harmony alias
16. [JipperResourcePack/HarmonyAlias.cs](#file-16) — Harmony에서 CodeInstruction만 명시적으로 노출
17. [scripts/run.sh](#file-17) — 빌드·패키징·검사·설치 진입점
18. [.gitignore](#file-18) — 로컬 패키지 결과 제외
19. [README.md](#file-19) — 포크의 렌더 동작·빌드·설치·제한 문서화
20. [tests/RainLayout/RainLayout.csproj](#file-20) — 운영 Rain 코드와 풀을 직접 연결하는 검사 프로젝트
21. [tests/RainLayout/Program.cs](#file-21) — 일반·고스트·풀 재사용의 UI 좌표 회귀 검사
22. [tests/RainLayout/UnityStubs.cs](#file-22) — 부모 변환 계약을 모델링하는 최소 Unity 대체 타입

## 동작을 이해할 때 구분할 점

- 렌더 입력은 프레임의 최종 눌림 상태다. 중간 press/release 이벤트를 추가로 재현하는 계약은 아니다.
- RenderStopwatch 기반 KPS·레이닝·콤보 애니메이션 시간은 SDK를 따른다. 진행도·판정·음악/맵 시간 표시값은 기존 게임 상태와 콜백 경로를 사용한다. 모든 Time 함수가 전역적으로 교체된 것은 아니다.
- 프레임 입력은 frame.IsPressed로 직접 읽는다. UnityNativeServices.GetKey가 SkyHook 워커를 대체하는 것은 아니다.
- 오류 시 제외는 렌더 UI를 숨기는 작업이다. live 상태 복구는 세션 종료 시 EndRender에서 한다. 키뷰어와 상태 오버레이 ID가 달라 별도로 제외할 수 있다.
- 입력 세대·native 타임스탬프·lock은 렌더 전후 비동기 물리 입력이 임시 카운터에 섞이는 것을 막기 위한 경계다. 이 부분이 새 코드의 복잡도를 늘린다.
- 바인딩 배열·UI 스냅샷·임시 레이아웃은 시작 시 만든다. 프레임에서는 기존 Work와 Refresh를 사용한다. 다만 기존 텍스트/큐/UI 생성에서 일어나는 할당이 없어지는 것은 아니며, 이 문서화에서는 성능을 측정하지 않았다.
- SDK 구현은 TUFReplay-Renderer monorepo에 있으므로 JRP diff에는 SDK 전체 소스가 들어 있지 않다. JRP는 빌드된 DLL을 참조·동봉한다.

<a id="file-1"></a>

## 1. JipperResourcePack/UnityNativeServices.cs

[JipperResourcePack/UnityNativeServices.cs](/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/UnityNativeServices.cs) — SDK에 평소의 Unity 값 제공 (새 파일).

`IOverlayNativeServices`를 구현한 singleton이다. 시간은 Unity의 `Time`에서, 포커스·화면 크기는 `Application`/`Screen`에서, 키 조회는 `Input.GetKey/GetKeyDown/GetKeyUp`에서 읽는다. JRP의 키뷰어·오버레이 등록 시 SDK에 이 인스턴스를 전달한다.

SDK가 렌더 중이 아닐 때 원래 값을 반환할 수 있게 하는 연결이다. 이 파일 자체가 SkyHook 입력 워커를 교체하거나 CSV를 읽지는 않는다. JRP 렌더 키뷰어는 뒤에서 설명하는 `frame.IsPressed()`를 사용한다.

### 실제 코드 전체

```csharp
using TUFReplay.Overlay;
using UnityEngine;

namespace JipperResourcePack;

internal sealed class UnityNativeServices : IOverlayNativeServices {
    internal static readonly UnityNativeServices Instance = new();
    private UnityNativeServices() { }

    public float Time => UnityEngine.Time.time;
    public float DeltaTime => UnityEngine.Time.deltaTime;
    public float UnscaledTime => UnityEngine.Time.unscaledTime;
    public float UnscaledDeltaTime => UnityEngine.Time.unscaledDeltaTime;
    public float RealtimeSinceStartup => UnityEngine.Time.realtimeSinceStartup;
    public bool IsFocused => Application.isFocused;
    public int Width => Screen.width;
    public int Height => Screen.height;
    public bool GetKey(int key) => Input.GetKey((KeyCode) key);
    public bool GetKeyDown(int key) => Input.GetKeyDown((KeyCode) key);
    public bool GetKeyUp(int key) => Input.GetKeyUp((KeyCode) key);
}
```

<a id="file-2"></a>

## 2. JipperResourcePack/KeyViewerContents/KeyViewer.cs

[JipperResourcePack/KeyViewerContents/KeyViewer.cs](/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/KeyViewerContents/KeyViewer.cs) — Stopwatch 교체와 키뷰어 등록·해제 (기존 파일 수정).

`Stopwatch` 별칭을 `TUFReplay.Overlay.RenderStopwatch`로 바꾼다. 기존 Stopwatch 호출 형태와 `CurrentTicks`를 유지하면서, 렌더에서는 가상 경과 시간, 평소에는 실제 Stopwatch 시간을 사용하게 한다. 기존 `System.Diagnostics.Stopwatch.Frequency`와 TimeSpan 틱 변환 코드는 그대로다.

초기화가 끝난 뒤 `OverlayRuntime.Register(this, UnityNativeServices.Instance)`로 키뷰어를 등록한다. 비활성화 시 등록을 해제하고 `EndRender()`로 렌더용 상태를 정리한 뒤 기존 워커·UI를 종료한다. 재활성화 때 오래된 물리 입력 복구 정보가 남지 않도록 `_nativeResumeState`, `_nativeResumeTimes`도 비운다.

### 실제 diff

```diff
diff --git a/JipperResourcePack/KeyViewerContents/KeyViewer.cs b/JipperResourcePack/KeyViewerContents/KeyViewer.cs
index b4bca35..9870177 100644
--- a/JipperResourcePack/KeyViewerContents/KeyViewer.cs
+++ b/JipperResourcePack/KeyViewerContents/KeyViewer.cs
@@ -17,6 +17,7 @@ using TMPro;
 using UnityEngine;
 using UnityEngine.UI;
 using UnityModManagerNet;
+using Stopwatch = TUFReplay.Overlay.RenderStopwatch;
 using Application = UnityEngine.Application;
 using Object = UnityEngine.Object;
 using Unsafe = System.Runtime.CompilerServices.Unsafe;
@@ -110,10 +111,14 @@ public partial class KeyViewer : Feature {
         RebuildKeyBinding();
         StartEventListener();
         UpdateKeyLimit();
+        _renderRegistration = TUFReplay.Overlay.OverlayRuntime.Register(this, UnityNativeServices.Instance);
     }
 
     protected override void OnDisable() {
         if(!KeyViewerObject) return;
+        _renderRegistration?.Dispose();
+        _renderRegistration = null;
+        EndRender();
         StopEventListener();
         AsyncInputHook.Release();
         Object.Destroy(KeyViewerObject);
@@ -122,6 +127,8 @@ public partial class KeyViewer : Feature {
         Keys = null;
         _keyBinding = null;
         _pressTimes = null;
+        _nativeResumeState = null;
+        _nativeResumeTimes = null;
         Array.Clear(_keyState, 0, _keyState.Length);
     }
 
@@ -1193,4 +1200,4 @@ public partial class KeyViewer : Feature {
     
     [JAPatch(typeof(SettingsMenu), "StartEditingKeys", PatchType.Prefix, false, TryingCatch = false)]
     private static bool StartEditingKeys() => !Settings.AutoSetupKeyLimit;
-}
\ No newline at end of file
+}
```

<a id="file-3"></a>

## 3. JipperResourcePack/KeyViewerContents/KeyViewer.Render.cs

[JipperResourcePack/KeyViewerContents/KeyViewer.Render.cs](/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/KeyViewerContents/KeyViewer.Render.cs) — 키뷰어의 렌더 수명주기·입력 전달·복구 (새 파일).

이번 변경에서 가장 큰 런타임 파일이다. 기존 `KeyViewer`의 partial 구현으로 `IOverlayParticipant`를 추가하고 ID를 `JipperResourcePack.KeyViewer`로 지정한다.

### 시작: BeginRender

`_inputSync` 잠금 아래에서 기존 상태를 `LiveView`에 보관한다. `_renderActive`를 켜고 입력 세대 `_inputGeneration`을 증가시킨다. 대기 중인 실제 입력은 복구용 상태에 반영한 뒤 원래 레이아웃을 숨긴다.

렌더 전용 `KeyCountData(persistent: false)`, KPS 큐, Stopwatch, SizeObject, RainManager, Key 배열을 만든다. 새 Canvas를 만들지 않고 기존 `KeyViewerObject` 아래에 임시 SizeObject를 붙인다. 실제 키 박스·텍스트·레이닝 레이아웃은 기존 `InitializeKeyViewer()`와 `InitializeFootKeyViewer()`로 생성한다. RainManager의 자동 Update는 꺼 두고 프레임 콜백에서 직접 갱신한다.

`AddBindings()`는 손·발·고스트 키 설정을 배열로 저장한다. 키별 `[offset + i]` 인덱스 배열은 시작할 때 만들며 매 프레임 재생성하지 않는다.

### 매 프레임: AdvanceFrame

각 설정 키에 대해 `frame.IsPressed((int) key)`를 읽고 기존 `Work(indexes, pressed, CurrentTicks)`에 전달한다. `Work()`의 상태 변화 판정, 색상, 카운터, KPS, 레이닝 생성·종료 로직은 새로 만들지 않았다. 그 뒤 `Updater.Refresh()`와 `RainManager.Refresh()`를 호출한다.

이것은 프레임 시점의 눌림 상태를 샘플링하는 방식이다. 프레임 사이에 press/release가 모두 끝난 입력을 추가로 복원하지 않는다. CSV의 Unity 키 이름으로 표현할 수 없는 native-only 바인딩도 이 경로에서는 재현할 수 없다.

### 물리 입력 보관과 재개

`InitializeNativeResumeState()`는 기존 눌림 상태를 복사한다. `RememberNativeInput()`은 실제 SkyHook 이벤트를 기존 label/native 바인딩으로 매핑해 복구용 bool 배열에만 저장한다. 키별 native 타임스탬프를 비교해 뒤늦게 도착한 더 오래된 이벤트가 최신 상태를 덮어쓰지 않게 한다. `DrainNativeInput()`은 이미 큐에 들어 있던 이벤트도 이 보관 경로로 옮긴다.

`ResumeNativeInput()`은 SDK와 키뷰어의 렌더 상태가 모두 끝난 뒤 기존 상태와 보관 상태를 맞춘다. 기존 키 박스의 하이라이트를 바꾸고, release가 확인된 기존 일반/고스트 레이닝은 종료한다. 이 재개 과정에서는 `Work()`를 호출하지 않으므로 새 카운트·KPS·레이닝 press를 만들지 않는다.

### 제외·종료

`SetPresentationEnabled(false)`는 렌더 SizeObject를 숨긴다. 이 함수가 곧바로 원래 live 키뷰어를 복구하는 것은 아니다. 원래 상태는 세션 종료 시 `EndRender()`에서 복구한다.

`EndRender()`는 먼저 SizeObject, RainManager, Keys, KPS/Total, 카운터, 큐, Stopwatch, 카운트 캐시, 눌림 상태를 원래 참조로 돌린다. 원래 가시성과 RainManager/Updater.enabled도 복구한다. `_renderRoot`로 자신이 만든 오브젝트만 식별하고 `finally`에서 파괴하여, 시작 중 일부 단계가 실패하더라도 live 루트를 잘못 지우지 않도록 한다. 이미 끝난 상태에서 다시 호출하면 바로 반환한다.

`LiveView`는 보존할 참조·enabled·가시성·캐시를 모으고 키 상태 bool 배열만 복사한다. 물리 입력 워커와 렌더 수명주기의 경계 처리는 이 파일과 다음 Update 파일에 집중돼 있다.

### 실제 코드 전체

```csharp
using System;
using System.Collections.Concurrent;
using TUFReplay.Overlay;
using UnityEngine;
using Object = UnityEngine.Object;

namespace JipperResourcePack.KeyViewerContents;

public partial class KeyViewer : IOverlayParticipant {
    private IDisposable _renderRegistration;
    private LiveView _liveView;
    private GameObject _renderRoot;
    private KeyCode[] _renderKeys;
    private int[][] _renderIndexes;
    private bool _renderVisible;
    private bool[] _nativeResumeState;
    private long[] _nativeResumeTimes;

    public string Id => "JipperResourcePack.KeyViewer";
    public string DisplayName => "JipperResourcePack KeyViewer";

    public void BeginRender(OverlayRenderContext context) {
        lock(_inputSync) {
            if(_liveView != null) throw new InvalidOperationException("A key viewer render is already active.");
            _liveView = new LiveView(this);
            _renderActive = true;
            _inputGeneration++;
            InitializeNativeResumeState();
            DrainNativeInput();
            KeyViewerSizeObject.SetActive(false);
            RainManager.enabled = false;
            KeyCountData.Instance = new KeyCountData(persistent: false);
            _pressTimes = new ConcurrentQueue<long>();
            Array.Clear(_keyState, 0, _keyState.Length);
            Stopwatch = RenderStopwatch.StartNew();
            _renderRoot = new GameObject("Render SizeObject");
            KeyViewerSizeObject = _renderRoot;
            RectTransform transform = KeyViewerSizeObject.AddComponent<RectTransform>();
            transform.SetParent(KeyViewerObject.transform);
            transform.localScale = new Vector3(Settings.Size, Settings.Size, 1);
            transform.anchorMin = transform.anchorMax = transform.offsetMin = transform.offsetMax = Vector2.zero;
            RainManager = KeyViewerSizeObject.AddComponent<RainManager>();
            RainManager.enabled = false;
            Keys = new Key[FootOutIndex];
            Kps = Total = null;
            InitializeKeyViewer();
            InitializeFootKeyViewer();
            _renderKeys = new KeyCode[GhostOutIndex];
            _renderIndexes = new int[GhostOutIndex][];
            AddBindings(GetKeyCode(), 0);
            AddBindings(GetFootKeyCode(), HandOutIndex);
            AddBindings(GetGhostKeyCode(), FootOutIndex);
            _renderVisible = true;
        }
    }

    private void AddBindings(KeyCode[] keys, int offset) {
        for(int i = 0; i < keys.Length; i++) {
            _renderKeys[offset + i] = keys[i];
            _renderIndexes[offset + i] = [offset + i];
        }
    }

    public void AdvanceFrame(OverlayFrame frame) {
        if(_liveView == null || !_renderVisible) return;
        long ticks = CurrentTicks;
        for(int i = 0; i < _renderKeys.Length; i++) {
            KeyCode key = _renderKeys[i];
            if(key == KeyCode.None || _renderIndexes[i] == null) continue;
            Work(_renderIndexes[i], frame.IsPressed((int) key), ticks);
        }
        Updater.Refresh();
        if(Settings.useRain) RainManager.Refresh();
    }

    public void SetPresentationEnabled(bool visible) {
        _renderVisible = visible;
        if(_liveView != null && KeyViewerSizeObject) KeyViewerSizeObject.SetActive(visible);
    }

    private void InitializeNativeResumeState() {
        if(_nativeResumeState != null) return;
        _nativeResumeState = (bool[]) _keyState.Clone();
        if(_nativeResumeTimes != null) return;
        _nativeResumeTimes = new long[GhostOutIndex];
        for(int i = 0; i < _nativeResumeTimes.Length; i++) _nativeResumeTimes[i] = long.MinValue;
    }

    private void DrainNativeInput() {
        ConcurrentQueue<KeyEvent> queue = _eventQueue;
        if(queue == null) return;
        while(queue.TryDequeue(out KeyEvent keyEvent))
            RememberNativeInput(keyEvent.Label, keyEvent.Key, keyEvent.Pressed, keyEvent.NativeTicks);
    }

    private void RememberNativeInput(SkyHook.KeyLabel label, ushort key, bool pressed, long nativeTicks) {
        lock(_inputSync) {
            KeyBinding binding = _keyBinding;
            if(binding == null) return;
            InitializeNativeResumeState();
            if(binding.LabelMap.TryGetValue(label, out int[] indexes)) SetState(indexes);
            if(binding.NativeMap.TryGetValue(key, out indexes)) SetState(indexes);
            return;

            void SetState(int[] keys) {
                foreach(int index in keys) {
                    if(nativeTicks < _nativeResumeTimes[index]) continue;
                    _nativeResumeTimes[index] = nativeTicks;
                    _nativeResumeState[index] = pressed;
                }
            }
        }
    }

    internal void ResumeNativeInput() {
        if(_nativeResumeState == null || OverlayRuntime.IsRendering) return;
        lock(_inputSync) {
            bool[] state = _nativeResumeState;
            if(state == null || _renderActive || OverlayRuntime.IsRendering) return;
            _nativeResumeState = null;
            if(Keys == null) return;
            long ticks = CurrentTicks;
            for(int i = 0; i < state.Length; i++) {
                if(_keyState[i] == state[i]) continue;
                _keyState[i] = state[i];
                if(i >= FootOutIndex) {
                    if(!state[i]) Keys[i - FootOutIndex]?.LastGhostRain?.Finish(ticks);
                    continue;
                }
                Key key = Keys[i];
                if(key == null) continue;
                key.UpdateRequestKey(state[i]);
                if(!state[i] && i < HandOutIndex) key.LastRain?.Finish(ticks);
            }
        }
    }

    public void EndRender() {
        lock(_inputSync) {
            LiveView live = _liveView;
            if(live == null) return;
            GameObject renderRoot = _renderRoot;
            KeyViewerSizeObject = live.Root;
            RainManager = live.RainManager;
            Keys = live.Keys;
            Kps = live.Kps;
            Total = live.Total;
            KeyCountData.Instance = live.CountData;
            _pressTimes = live.PressTimes;
            Stopwatch = live.Stopwatch;
            _lastKpsCount = live.LastKpsCount;
            _lastTotalCount = live.LastTotalCount;
            Array.Copy(live.KeyState, _keyState, _keyState.Length);
            DrainNativeInput();
            _eventOffsetTicks = NoEventOffset;
            _renderKeys = null;
            _renderIndexes = null;
            _renderRoot = null;
            _liveView = null;
            _renderActive = false;
            try {
                if(renderRoot) renderRoot.SetActive(false);
                if(KeyViewerSizeObject) KeyViewerSizeObject.SetActive(live.Visible);
                if(RainManager) RainManager.enabled = live.RainEnabled;
                if(Updater) Updater.enabled = live.UpdaterEnabled;
            } finally {
                if(renderRoot) Object.Destroy(renderRoot);
            }
        }
    }

    private sealed class LiveView(KeyViewer viewer) {
        internal readonly GameObject Root = viewer.KeyViewerSizeObject;
        internal readonly RainManager RainManager = KeyViewer.RainManager;
        internal readonly bool RainEnabled = KeyViewer.RainManager.enabled;
        internal readonly bool UpdaterEnabled = viewer.Updater.enabled;
        internal readonly bool Visible = viewer.KeyViewerSizeObject.activeSelf;
        internal readonly Key[] Keys = viewer.Keys;
        internal readonly Key Kps = viewer.Kps;
        internal readonly Key Total = viewer.Total;
        internal readonly KeyCountData CountData = KeyCountData.Instance;
        internal readonly ConcurrentQueue<long> PressTimes = viewer._pressTimes;
        internal readonly RenderStopwatch Stopwatch = KeyViewer.Stopwatch;
        internal readonly int LastKpsCount = viewer._lastKpsCount;
        internal readonly int LastTotalCount = viewer._lastTotalCount;
        internal readonly bool[] KeyState = (bool[]) viewer._keyState.Clone();
    }
}
```

<a id="file-4"></a>

## 4. JipperResourcePack/KeyViewerContents/KeyViewer.Update.cs

[JipperResourcePack/KeyViewerContents/KeyViewer.Update.cs](/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/KeyViewerContents/KeyViewer.Update.cs) — 실제 입력과 렌더 입력의 경계 분리 (기존 파일 수정).

`_inputSync`, `_renderActive`, `_inputGeneration`을 추가한다. 일반 SkyHook 이벤트에는 원래 native 타임스탬프와 입력 세대를 함께 기록한다.

렌더 중 들어온 실제 입력은 `OnKeyEvent()`에서 `RememberNativeInput()`에만 전달하고 정상 처리 큐에 넣지 않는다. 렌더 직전에 이미 큐에 들어온 이벤트도 워커에서 세대를 검사하여, 오래된 이벤트가 렌더용 카운터와 UI에 섞이지 않게 한다. 렌더가 끝난 뒤에는 `ResumeNativeInput()`을 먼저 실행하고 새 정상 이벤트를 처리한다.

`KeyViewerUpdater.Update()`의 기존 KPS·Total 처리 본문을 `Refresh()`로 추출한다. 평소에는 Update에서 실행하고 렌더 중에는 자동 Update를 건너뛰어 `AdvanceFrame()`이 호출하도록 한다. `ProcessKeyEvent()`, `Work()`, rain/KPS 계산식은 이 diff에서 새로 구현하지 않았다.

`using HarmonyLib` 삭제는 입력 정책 변경과 무관하다. 아래 `HarmonyAlias.cs`의 전역 CodeInstruction 별칭을 사용하도록 빌드 참조를 정리한 것이다.

### 실제 diff

```diff
diff --git a/JipperResourcePack/KeyViewerContents/KeyViewer.Update.cs b/JipperResourcePack/KeyViewerContents/KeyViewer.Update.cs
index 2defe2a..02a4acc 100644
--- a/JipperResourcePack/KeyViewerContents/KeyViewer.Update.cs
+++ b/JipperResourcePack/KeyViewerContents/KeyViewer.Update.cs
@@ -5,7 +5,6 @@ using System.Reflection;
 using System.Reflection.Emit;
 using System.Runtime.CompilerServices;
 using System.Threading;
-using HarmonyLib;
 using JALib.Core.Patch;
 using SkyHook;
 using UnityEngine;
@@ -27,6 +26,9 @@ public partial class KeyViewer {
     private ConcurrentQueue<KeyEvent> _eventQueue;
     private SemaphoreSlim _eventSignal;
     private volatile bool _listening;
+    private readonly object _inputSync = new();
+    private volatile bool _renderActive;
+    private volatile int _inputGeneration;
     private KeyBinding _keyBinding;
     private long _eventOffsetTicks = NoEventOffset;
     private volatile int _capturedKeyCode;
@@ -148,10 +150,16 @@ public partial class KeyViewer {
     }
 
     private void OnKeyEvent(SkyHookEvent hookEvent) {
+        long nativeTicks = hookEvent.GetTimeInTicks();
+        int generation = _inputGeneration;
+        if(_renderActive || TUFReplay.Overlay.OverlayRuntime.IsRendering) {
+            RememberNativeInput(hookEvent.Label, hookEvent.Key, hookEvent.Type == EventType.KeyPressed, nativeTicks);
+            return;
+        }
         ConcurrentQueue<KeyEvent> queue = _eventQueue;
         SemaphoreSlim signal = _eventSignal;
         if(queue == null || signal == null) return;
-        queue.Enqueue(new KeyEvent(hookEvent.Label, hookEvent.Key, hookEvent.Type == EventType.KeyPressed, ToStopwatchTicks(hookEvent.GetTimeInTicks())));
+        queue.Enqueue(new KeyEvent(hookEvent.Label, hookEvent.Key, hookEvent.Type == EventType.KeyPressed, ToStopwatchTicks(nativeTicks), nativeTicks, generation));
         signal.Release();
     }
 
@@ -161,7 +169,16 @@ public partial class KeyViewer {
         while(_listening) {
             try {
                 signal.Wait();
-                while(_listening && queue.TryDequeue(out KeyEvent keyEvent)) ProcessKeyEvent(keyEvent);
+                while(_listening && queue.TryDequeue(out KeyEvent keyEvent)) {
+                    lock(_inputSync) {
+                        if(keyEvent.Generation != _inputGeneration || _renderActive || TUFReplay.Overlay.OverlayRuntime.IsRendering) {
+                            RememberNativeInput(keyEvent.Label, keyEvent.Key, keyEvent.Pressed, keyEvent.NativeTicks);
+                        } else {
+                            if(_nativeResumeState != null) ResumeNativeInput();
+                            ProcessKeyEvent(keyEvent);
+                        }
+                    }
+                }
             } catch (ThreadAbortException) {
                 // Handle thread abort if necessary
             } catch (Exception e) {
@@ -246,11 +263,13 @@ public partial class KeyViewer {
         return counted;
     }
 
-    private readonly struct KeyEvent(KeyLabel label, ushort key, bool pressed, long ticks) {
+    private readonly struct KeyEvent(KeyLabel label, ushort key, bool pressed, long ticks, long nativeTicks, int generation) {
         public readonly KeyLabel Label = label;
         public readonly ushort Key = key;
         public readonly bool Pressed = pressed;
         public readonly long Ticks = ticks;
+        public readonly long NativeTicks = nativeTicks;
+        public readonly int Generation = generation;
     }
 
     private sealed class KeyBinding(Dictionary<KeyLabel, int[]> labelMap, Dictionary<ushort, int[]> nativeMap) {
@@ -260,6 +279,12 @@ public partial class KeyViewer {
 
     public class KeyViewerUpdater : MonoBehaviour {
         private void Update() {
+            if(TUFReplay.Overlay.OverlayRuntime.IsRendering) return;
+            Instance.ResumeNativeInput();
+            Refresh();
+        }
+
+        internal void Refresh() {
             KeyViewer instance = Instance;
             ConcurrentQueue<long> pressTimes = instance._pressTimes;
             if(pressTimes == null) return;
```

<a id="file-5"></a>

## 5. JipperResourcePack/KeyViewerContents/KeyCountData.cs

[JipperResourcePack/KeyViewerContents/KeyCountData.cs](/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/KeyViewerContents/KeyCountData.cs) — 렌더용 키 카운터의 저장 차단 (기존 파일 수정).

생성자에 `persistent = true`를 추가한다. 기존 `Load()`는 기본 생성자를 계속 사용하므로 평소 저장 동작은 유지된다. 렌더 시작 시 만드는 `persistent: false` 인스턴스는 `Save()`에서 바로 반환한다.

따라서 기존 카운터 배열과 집계 로직을 그대로 사용할 수 있으면서 렌더용 카운트가 `KeyCount.dat`로 저장되지 않는다. live 인스턴스는 종료 시 다시 복구된다. 기존 live 인스턴스에 이미 예약된 비동기 저장 작업까지 취소하는 코드가 추가된 것은 아니다.

### 실제 diff

```diff
diff --git a/JipperResourcePack/KeyViewerContents/KeyCountData.cs b/JipperResourcePack/KeyViewerContents/KeyCountData.cs
index bf78b33..6bf1966 100644
--- a/JipperResourcePack/KeyViewerContents/KeyCountData.cs
+++ b/JipperResourcePack/KeyViewerContents/KeyCountData.cs
@@ -11,8 +11,11 @@ public class KeyCountData {
     public static KeyCountData Instance;
     public readonly int[] Count = new int[KeyViewer.FootOutIndex];
     public int TotalCount;
+    private readonly bool _persistent;
     private int _saveDirtyVersion;
 
+    public KeyCountData(bool persistent = true) => _persistent = persistent;
+
     public static void Load() {
         if(Instance != null) return;
         Instance = new KeyCountData();
@@ -42,6 +45,7 @@ public class KeyCountData {
     }
 
     public void Save() {
+        if(!_persistent) return;
         if(Interlocked.Increment(ref _saveDirtyVersion) == 1) Task.Run(SaveData);
     }
 
@@ -67,4 +71,4 @@ public class KeyCountData {
             _saveDirtyVersion = 0;
         }
     }
-}
\ No newline at end of file
+}
```

<a id="file-6"></a>

## 6. JipperResourcePack/KeyViewerContents/RainManager.cs

[JipperResourcePack/KeyViewerContents/RainManager.cs](/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/KeyViewerContents/RainManager.cs) — 레이닝 갱신을 프레임 콜백에서 호출 가능하게 분리 (기존 파일 수정).

기존 Update의 본문을 `internal Refresh()`로 옮겼다. 평소에는 `Update → Refresh`, 렌더에서는 `AdvanceFrame → Refresh`로 들어온다. 렌더 중에는 자동 Update가 반환하므로 동일 UI를 두 경로에서 중복 갱신하지 않는다.

큐에서 RawRain을 꺼내 풀에서 Graphic을 얻는 부분, `CurrentTicks - StartTime`으로 위치·길이를 계산하는 부분, 높이 제한과 풀 반환은 그대로다. 가상 Stopwatch를 통해 시간이 바뀌므로 레이닝 공식을 교체할 필요가 없다.

### 실제 diff

```diff
diff --git a/JipperResourcePack/KeyViewerContents/RainManager.cs b/JipperResourcePack/KeyViewerContents/RainManager.cs
index 9dad5db..f8dd302 100644
--- a/JipperResourcePack/KeyViewerContents/RainManager.cs
+++ b/JipperResourcePack/KeyViewerContents/RainManager.cs
@@ -9,6 +9,11 @@ public class RainManager : MonoBehaviour {
     public readonly ConcurrentQueue<RawRain> RawRainQueue = new();
 
     private void Update() {
+        if(TUFReplay.Overlay.OverlayRuntime.IsRendering) return;
+        Refresh();
+    }
+
+    internal void Refresh() {
         while(RawRainQueue.TryDequeue(out RawRain rawRain)) {
             Rain rainComponent = rawRain.Key.RainPool.GetOrNewRain(rawRain.IsGhost);
             rainComponent.Image.color = rawRain.Key.Color switch {
@@ -71,4 +76,4 @@ public class RainManager : MonoBehaviour {
             }
         }
     }
-}
\ No newline at end of file
+}
```

<a id="file-7"></a>

## 7. JipperResourcePack/KeyViewerContents/Rain.cs

[JipperResourcePack/KeyViewerContents/Rain.cs](/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/KeyViewerContents/Rain.cs) — 동적으로 만든 레이닝을 부모 UI 평면에 배치 (기존 파일 수정).

실질적인 수정은 `SetParent(pool.Transform)`를 `SetParent(pool.Transform, false)`로 바꾸는 한 줄이다. 한 인자 SetParent는 월드 위치·회전을 유지한다. Renderer가 Canvas를 카메라 앞의 평면으로 이동한 뒤 새 Rain을 만들면, anchoredPosition의 XY만 설정하는 기존 코드에서는 Z가 그 평면을 따르지 않아 레이닝이 캡처되지 않을 수 있었다.

`false`는 새 UI가 부모의 로컬 좌표를 따르게 한다. 일반 레이닝과 고스트 레이닝이 같은 생성자를 사용하므로 둘 다 적용된다. 시간 계산, 색상, 풀 구조, 움직임에는 변경이 없다. 파일 마지막 newline도 정리됐다.

### 실제 diff

```diff
diff --git a/JipperResourcePack/KeyViewerContents/Rain.cs b/JipperResourcePack/KeyViewerContents/Rain.cs
index 8bf877c..da150c8 100644
--- a/JipperResourcePack/KeyViewerContents/Rain.cs
+++ b/JipperResourcePack/KeyViewerContents/Rain.cs
@@ -15,7 +15,7 @@ public class Rain {
         Pool = pool;
         GameObject rainPrefab = GameObject = new GameObject("Rain");
         RectTransform rainTransform = Transform = rainPrefab.AddComponent<RectTransform>();
-        rainTransform.SetParent(pool.Transform);
+        rainTransform.SetParent(pool.Transform, false);
         rainTransform.anchorMin = rainTransform.anchorMax = rainTransform.pivot = new Vector2(0.5f, 1);
         rainTransform.anchoredPosition = rainTransform.sizeDelta = Vector2.zero;
         rainTransform.localScale = Vector3.one;
@@ -27,4 +27,4 @@ public class Rain {
         } else Image = rainPrefab.AddComponent<RawImage>();
         IsGhost = isGhost;
     }
-}
\ No newline at end of file
+}
```

<a id="file-8"></a>

## 8. JipperResourcePack/Main.cs

[JipperResourcePack/Main.cs](/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/Main.cs) — 평소 자동 갱신과 렌더 프레임 갱신의 중복 방지 (기존 파일 수정).

평소에는 기존 Overlay 시간·콤보 갱신을 유지하고 live 입력 복구도 실행한다. SDK 렌더 중에는 이 자동 시간·콤보 갱신을 건너뛰어 Overlay의 `AdvanceFrame()`이 담당하게 한다.

`JMain.Update(deltaTime)` 호출 자체는 유지하지만 렌더 중에는 인자로 `OverlayTime.UnscaledDeltaTime`을 사용한다. SDK가 캡처용 refresh 단계에 들어가면 해당 delta는 0이므로, UI가 정리되는 추가 Unity 프레임에서 이 인자의 시간이 더 진행되지 않는다. JRP의 모든 시간 API 호출을 일괄 패치한 것은 아니다.

### 실제 diff

```diff
diff --git a/JipperResourcePack/Main.cs b/JipperResourcePack/Main.cs
index 3d65e47..0f0a002 100644
--- a/JipperResourcePack/Main.cs
+++ b/JipperResourcePack/Main.cs
@@ -100,8 +100,12 @@ public class Main() : JAMod(typeof(ResourcePackSetting)) {
     }
     
     protected override void OnUpdate(float deltaTime) {
-        Overlay.Instance.UpdateTime();
-        Overlay.Instance.UpdateComboSize();
+        if(!TUFReplay.Overlay.OverlayRuntime.IsRendering) {
+            KeyViewer.Instance?.ResumeNativeInput();
+            Overlay.Instance.UpdateTime();
+            Overlay.Instance.UpdateComboSize();
+        }
+        if(TUFReplay.Overlay.OverlayRuntime.IsRendering) deltaTime = TUFReplay.Overlay.OverlayTime.UnscaledDeltaTime;
         JMain.Update(deltaTime);
     }
 
```

<a id="file-9"></a>

## 9. JipperResourcePack/OverlayContents/Overlay.cs

[JipperResourcePack/OverlayContents/Overlay.cs](/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/OverlayContents/Overlay.cs) — 상태 오버레이 등록·가상 콤보 시간·오류 전달 (기존 파일 수정).

클래스를 partial로 바꿔 렌더 전용 수명주기를 다른 파일로 분리하고 Stopwatch 별칭을 공용 RenderStopwatch로 교체한다. 초기화 끝에 등록하고 Destroy 시 등록 해제와 EndRender를 수행한다.

기존 progress-bar 갱신 및 콤보 위치 갱신에서 잡힌 예외는 렌더 중 `OverlayRuntime.ReportFailure(Id, e)`로 보고한다. 평소에는 원래 로그 경로를 유지한다. SDK가 이 실패를 Renderer에 전달하면 사용자의 제외/취소 선택으로 이어질 수 있다.

콤보 글자 크기를 바꾼 뒤 평소의 `Task.Yield().OnCompleted(...)` 방식은 유지한다. 렌더에서는 `ForceMeshUpdate()` 뒤 위치를 즉시 계산하여, 비동기 위치 갱신이 캡처 뒤로 밀리지 않게 한다. 제외된 렌더 오버레이가 게임의 Show 콜백으로 다시 나타나지 않도록 Show 진입에도 가드를 추가한다.

게임 진행도·판정·음악/맵 표시값을 새 데이터 모델로 교체하지 않았다. 기존 게임 콜백과 텍스트 매니저가 계속 값을 읽고 표시한다.

### 실제 diff

```diff
diff --git a/JipperResourcePack/OverlayContents/Overlay.cs b/JipperResourcePack/OverlayContents/Overlay.cs
index 3c4560b..b5a8957 100644
--- a/JipperResourcePack/OverlayContents/Overlay.cs
+++ b/JipperResourcePack/OverlayContents/Overlay.cs
@@ -9,10 +9,11 @@ using TMPro;
 using UnityEngine;
 using UnityEngine.UI;
 using Object = UnityEngine.Object;
+using Stopwatch = TUFReplay.Overlay.RenderStopwatch;
 
 namespace JipperResourcePack.OverlayContents;
 
-public class Overlay {
+public partial class Overlay {
     public static Overlay Instance;
     public IOverlayTextManager OverlayTextManager;
     public readonly GameObject GameObject;
@@ -85,6 +86,7 @@ public class Overlay {
         UpdateSize();
         Object.DontDestroyOnLoad(GameObject);
         if(ADOBase.controller is { paused: false } && ADOBase.conductor is { isGameWorld: true }) Show(0);
+        _renderRegistration = TUFReplay.Overlay.OverlayRuntime.Register(this, UnityNativeServices.Instance);
     }
 
     public void OnChangePlayers() {
@@ -365,7 +367,8 @@ public class Overlay {
             if(!ProgressBar.LineTransform) return;
             OverlayTextManager.UpdateProgressBar(this);
         } catch (Exception e) {
-            Main.Instance.LogException(e);
+            if(TUFReplay.Overlay.OverlayRuntime.IsRendering) TUFReplay.Overlay.OverlayRuntime.ReportFailure(Id, e);
+            else Main.Instance.LogException(e);
         }
     }
 
@@ -498,14 +501,18 @@ public class Overlay {
             _stopwatch.Stop();
         }
         ComboText.fontSize = 30 * OutExpoChange(t) + 78;
-        Task.Yield().OnCompleted(UpdateComboLocation);
+        if(TUFReplay.Overlay.OverlayRuntime.IsRendering) {
+            ComboText.ForceMeshUpdate();
+            UpdateComboLocation();
+        } else Task.Yield().OnCompleted(UpdateComboLocation);
     }
 
     private void UpdateComboLocation() {
         try {
             ComboTransform.anchoredPosition = new Vector2(0, ComboTextTransform.sizeDelta.y / 2);
         } catch (Exception e) {
-            Main.Instance.LogReportException("Failed to update combo location", e);
+            if(TUFReplay.Overlay.OverlayRuntime.IsRendering) TUFReplay.Overlay.OverlayRuntime.ReportFailure(Id, e);
+            else Main.Instance.LogReportException("Failed to update combo location", e);
         }
     }
     
@@ -572,6 +579,7 @@ public class Overlay {
     }
     
     public virtual void Show(int floor) {
+        if(TUFReplay.Overlay.OverlayRuntime.IsRendering && _liveOverlay != null && !_renderVisible) return;
         // ReSharper disable once CompareOfFloatsByEqualityOperator
         if(_lastSavedStartProgress != -1) {
             if(!AutoOnceEnabled) PlayCount.SetBest(LastHash, _lastSavedStartProgress, OverlayTextManager.GetProgress(), LastMultiplier);
@@ -662,6 +670,9 @@ public class Overlay {
     }
 
     public void Destroy() {
+        _renderRegistration?.Dispose();
+        _renderRegistration = null;
+        EndRender();
         Object.Destroy(GameObject);
         GC.SuppressFinalize(this);
     }
```

<a id="file-10"></a>

## 10. JipperResourcePack/OverlayContents/Overlay.Render.cs

[JipperResourcePack/OverlayContents/Overlay.Render.cs](/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/OverlayContents/Overlay.Render.cs) — 상태 오버레이의 스냅샷·초기화·복구 (새 파일).

`IOverlayParticipant`를 구현하고 ID를 `JipperResourcePack.Overlay`로 지정한다. 키뷰어와 별개 참가자여서 어느 한쪽의 실패를 별도로 식별할 수 있다.

`BeginRender()`는 기존 캐시·진행 정보·매니저와 UI 상태를 `LiveOverlay`에 저장한다. 렌더용 Stopwatch 상태와 시간 캐시, 맵 캐시, 저장 시작 진행도, 체크포인트·해시 등을 초기화하고 새 텍스트 매니저를 설정한다. 콤보 글자 크기도 초기화한다.

키뷰어와 달리 별도 UI 트리를 새로 만들지는 않는다. 기존 GameObject의 텍스트·RectTransform·활성 상태를 스냅샷하고 같은 UI를 갱신한다. `AdvanceFrame()`은 기존 `UpdateTime()`과 `UpdateComboSize()`를 호출한다.

`SetPresentationEnabled(false)`는 루트의 자식들을 숨긴다. 현재 구현에서 true 호출은 자식을 다시 켜는 로직을 포함하지 않으며, 정상 상태 복구는 EndRender의 ObjectView.Restore에 맡긴다. 현재 사용 경로는 실패 시 제외와 세션 종료다.

`EndRender()`는 저장한 필드와 매니저를 복구하고 `TextView`로 text/color/fontSize, `RectView`로 anchoredPosition/sizeDelta/localScale, `ObjectView`로 activeSelf를 복구한다. `Hit`, Checkpoints 등의 배열은 전체 내용의 deep copy가 아니라 원래 참조를 저장·복구한다. 게임 전체 상태를 스냅샷하는 기능은 아니다.

### 실제 코드 전체

```csharp
using System;
using TUFReplay.Overlay;
using TMPro;
using UnityEngine;

namespace JipperResourcePack.OverlayContents;

public partial class Overlay : IOverlayParticipant {
    private IDisposable _renderRegistration;
    private LiveOverlay _liveOverlay;
    private bool _renderVisible;

    public string Id => "JipperResourcePack.Overlay";
    public string DisplayName => "JipperResourcePack Overlay";

    public void BeginRender(OverlayRenderContext context) {
        if(_liveOverlay != null) throw new InvalidOperationException("An overlay render is already active.");
        _liveOverlay = new LiveOverlay(this);
        _renderVisible = true;
        _stopwatch.Reset();
        _lastTime = _lastMapTime = -1;
        _lastSavedStartProgress = -1;
        MusicTimeCache = MapTimeCache = null;
        MapTotalTime = -1;
        _scorableTiles = null;
        Checkpoints = null;
        LastHash = default;
        SetupTextManager();
        SongPlaying = IsDeath = false;
        ComboText.fontSize = 78;
    }

    public void AdvanceFrame(OverlayFrame frame) {
        if(_liveOverlay == null || !_renderVisible) return;
        UpdateTime();
        UpdateComboSize();
    }

    public void SetPresentationEnabled(bool visible) {
        _renderVisible = visible;
        if(_liveOverlay == null || visible) return;
        for(int i = 0; i < GameObject.transform.childCount; i++) GameObject.transform.GetChild(i).gameObject.SetActive(false);
    }

    public void EndRender() {
        LiveOverlay live = _liveOverlay;
        if(live == null) return;
        _lastTime = live.LastTime;
        _lastMapTime = live.LastMapTime;
        StartTile = live.StartTile;
        NoCheckStartTile = live.NoCheckStartTile;
        Checkpoints = live.Checkpoints;
        LastTileBpm = live.LastTileBpm;
        LastCurBpm = live.LastCurBpm;
        SongPlaying = live.SongPlaying;
        StartProgress = live.StartProgress;
        AutoOnceEnabled = live.AutoOnceEnabled;
        IsDeath = live.IsDeath;
        MusicTimeCache = live.MusicTimeCache;
        MapTimeCache = live.MapTimeCache;
        MapTotalTime = live.MapTotalTime;
        _scorableTiles = live.ScorableTiles;
        LastHash = live.LastHash;
        _lastSavedStartProgress = live.LastSavedStartProgress;
        LastMultiplier = live.LastMultiplier;
        _current = live.Current;
        OverlayTextManager = live.TextManager;
        Hit = live.Hit;
        foreach(TextView text in live.Texts) text.Restore();
        foreach(RectView rect in live.Rects) rect.Restore();
        foreach(ObjectView obj in live.Objects) obj.Restore();
        _liveOverlay = null;
    }

    private sealed class LiveOverlay {
        internal readonly int LastTime, LastMapTime, StartTile, NoCheckStartTile;
        internal readonly int[] Checkpoints, ScorableTiles;
        internal readonly int[] Hit;
        internal readonly IOverlayTextManager TextManager;
        internal readonly float LastTileBpm, LastCurBpm, StartProgress, MapTotalTime, LastSavedStartProgress, LastMultiplier;
        internal readonly bool SongPlaying, AutoOnceEnabled, IsDeath;
        internal readonly string MusicTimeCache, MapTimeCache;
        internal readonly PlayCount.Hash LastHash;
        internal readonly ComboTier Current;
        internal readonly TextView[] Texts;
        internal readonly RectView[] Rects;
        internal readonly ObjectView[] Objects;

        internal LiveOverlay(Overlay overlay) {
            LastTime = overlay._lastTime;
            LastMapTime = overlay._lastMapTime;
            StartTile = overlay.StartTile;
            NoCheckStartTile = overlay.NoCheckStartTile;
            Checkpoints = overlay.Checkpoints;
            ScorableTiles = overlay._scorableTiles;
            LastTileBpm = overlay.LastTileBpm;
            LastCurBpm = overlay.LastCurBpm;
            StartProgress = overlay.StartProgress;
            MapTotalTime = overlay.MapTotalTime;
            LastSavedStartProgress = overlay._lastSavedStartProgress;
            LastMultiplier = overlay.LastMultiplier;
            SongPlaying = overlay.SongPlaying;
            AutoOnceEnabled = overlay.AutoOnceEnabled;
            IsDeath = overlay.IsDeath;
            MusicTimeCache = overlay.MusicTimeCache;
            MapTimeCache = overlay.MapTimeCache;
            LastHash = overlay.LastHash;
            Current = overlay._current;
            TextManager = overlay.OverlayTextManager;
            Hit = overlay.Hit;
            TextMeshProUGUI[] texts = overlay.GameObject.GetComponentsInChildren<TextMeshProUGUI>(true);
            Texts = Array.ConvertAll(texts, text => new TextView(text));
            RectTransform[] rects = overlay.GameObject.GetComponentsInChildren<RectTransform>(true);
            Rects = Array.ConvertAll(rects, rect => new RectView(rect));
            Transform[] objects = overlay.GameObject.GetComponentsInChildren<Transform>(true);
            Objects = Array.ConvertAll(objects, obj => new ObjectView(obj.gameObject));
        }
    }

    private sealed class TextView(TextMeshProUGUI text) {
        private readonly string _text = text.text;
        private readonly Color _color = text.color;
        private readonly float _fontSize = text.fontSize;
        internal void Restore() {
            if(!text) return;
            text.text = _text;
            text.color = _color;
            text.fontSize = _fontSize;
        }
    }

    private sealed class RectView(RectTransform rect) {
        private readonly Vector2 _position = rect.anchoredPosition;
        private readonly Vector2 _size = rect.sizeDelta;
        private readonly Vector3 _scale = rect.localScale;
        internal void Restore() {
            if(!rect) return;
            rect.anchoredPosition = _position;
            rect.sizeDelta = _size;
            rect.localScale = _scale;
        }
    }

    private sealed class ObjectView(GameObject obj) {
        private readonly bool _active = obj.activeSelf;
        internal void Restore() {
            if(obj) obj.SetActive(_active);
        }
    }
}
```

<a id="file-11"></a>

## 11. JipperResourcePack/OverlayContents/PlayCount.cs

[JipperResourcePack/OverlayContents/PlayCount.cs](/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/OverlayContents/PlayCount.cs) — 렌더용 플레이 기록을 별도 메모리에 격리 (기존 파일 수정).

렌더용 Dictionary와 세션 ID를 추가한다. `GetData()`는 평소에는 `_datas`, 렌더에서는 현재 세션 전용 `_renderDatas`를 선택한다. 새 세션이면 빈 Dictionary를 만들어 반복 렌더의 기록이 이전 렌더와 섞이지 않게 한다. 렌더 중 `Save()`는 반환하여 `Plays.dat`에 기록하지 않는다.

기존 정상 플레이 데이터는 수정하지 않고 보관한다. 렌더용 Dictionary는 빈 상태에서 시작하므로 기존 live 최고 기록을 복사해서 보여주는 구조는 아니다. Dispose 시 렌더 Dictionary도 비운다.

`Hash.ToString()`의 Harmony 확장 `.Join(...)`을 LINQ와 `string.Concat`로 바꾸었다. 바이트를 소문자 2자리 hex 문자열로 붙이는 결과는 유지하며 Harmony 별칭 정리로 생기는 확장 메서드 의존을 없애는 빌드 변경이다. 해시 계산 알고리즘은 바꾸지 않았다.

### 실제 diff

```diff
diff --git a/JipperResourcePack/OverlayContents/PlayCount.cs b/JipperResourcePack/OverlayContents/PlayCount.cs
index fff29d3..f0d4f63 100644
--- a/JipperResourcePack/OverlayContents/PlayCount.cs
+++ b/JipperResourcePack/OverlayContents/PlayCount.cs
@@ -6,13 +6,15 @@ using System.Runtime.CompilerServices;
 using System.Security.Cryptography;
 using System.Text;
 using ADOFAI;
-using HarmonyLib;
 using JALib.Tools.ByteTool;
+using TUFReplay.Overlay;
 
 namespace JipperResourcePack.OverlayContents;
 
 public static class PlayCount {
     private static Dictionary<Hash, PlayData> _datas;
+    private static Dictionary<Hash, PlayData> _renderDatas;
+    private static long _renderSessionId;
     private static string FilePath => Path.Combine(Main.Instance.Path, "Plays.dat");
 
     public static void Load() {
@@ -48,6 +50,7 @@ public static class PlayCount {
 
     public static void Dispose() {
         _datas = null;
+        _renderDatas = null;
     }
 
     public static void AddAttempts(Hash hash, float progress, float multiplier) => GetData(hash).AddAttempts(progress, multiplier);
@@ -62,6 +65,7 @@ public static class PlayCount {
 
 
     public static void Save() {
+        if(OverlayRuntime.IsRendering) return;
         try {
             string path = FilePath;
             if(File.Exists(path)) File.Copy(path, path + ".bak", true);
@@ -81,8 +85,16 @@ public static class PlayCount {
     }
 
     public static PlayData GetData(Hash hash) {
-        if(!_datas.ContainsKey(hash)) _datas[hash] = new PlayData();
-        return _datas[hash];
+        Dictionary<Hash, PlayData> datas = _datas;
+        if(OverlayRuntime.IsRendering) {
+            if(_renderDatas == null || _renderSessionId != OverlayRuntime.SessionId) {
+                _renderSessionId = OverlayRuntime.SessionId;
+                _renderDatas = new Dictionary<Hash, PlayData>();
+            }
+            datas = _renderDatas;
+        }
+        if(!datas.TryGetValue(hash, out PlayData data)) datas[hash] = data = new PlayData();
+        return data;
     }
 
     public class PlayData {
@@ -243,8 +255,8 @@ public static class PlayCount {
         public static implicit operator Hash(byte[] hash) => new(hash);
         public static implicit operator byte[](Hash hash) => hash.Data;
 
-        public override string ToString() => Data.Join(b => b.ToString("x2"), "");
+        public override string ToString() => string.Concat(Data.Select(b => b.ToString("x2")));
     }
 
     #endregion
-}
\ No newline at end of file
+}
```

<a id="file-12"></a>

## 12. JipperResourcePack/OverlayContents/Status.cs

[JipperResourcePack/OverlayContents/Status.cs](/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/OverlayContents/Status.cs) — Harmony using 정리 (기존 파일 수정).

`using HarmonyLib`를 제거하고 전역 CodeInstruction 별칭을 사용한다. Status의 기존 게임 패치·판정 갱신·표시 로직은 이 diff에서 바뀌지 않았다. 마지막 newline 정리도 포함된다.

### 실제 diff

```diff
diff --git a/JipperResourcePack/OverlayContents/Status.cs b/JipperResourcePack/OverlayContents/Status.cs
index 2ce9b61..09c1a5f 100644
--- a/JipperResourcePack/OverlayContents/Status.cs
+++ b/JipperResourcePack/OverlayContents/Status.cs
@@ -3,7 +3,6 @@ using System.Collections.Generic;
 using System.Linq;
 using System.Reflection;
 using System.Reflection.Emit;
-using HarmonyLib;
 using JALib.Core;
 using JALib.Core.Patch;
 using JALib.Core.Setting;
@@ -311,4 +310,4 @@ public class Status : Feature {
         });
     }
     // ReSharper restore UnusedMember.Local
-}
\ No newline at end of file
+}
```

<a id="file-13"></a>

## 13. Directory.Build.props

[Directory.Build.props](/Users/kgh/dev/src/jipper-resourcepack/Directory.Build.props) — 게임·모드·공용 SDK 경로 설정 (기존 파일 수정).

`GameModsPath`, `OverlaySdkPath`, `KeyboardChatterBlockerPath`의 기본값을 추가한다. 모두 외부 MSBuild property로 지정하면 그 값을 우선한다. SDK 기본 경로는 sibling TUFReplay-Renderer checkout의 netstandard2.0 빌드 결과다.

원래 Windows Managed 기본 경로는 유지된다. 새 run.sh가 macOS 게임 경로를 MSBuild 인자로 넘기므로 같은 프로젝트를 다른 설치 구조에서 빌드할 수 있다.

### 실제 diff

```diff
diff --git a/Directory.Build.props b/Directory.Build.props
index 689f70e..942ef4e 100644
--- a/Directory.Build.props
+++ b/Directory.Build.props
@@ -3,5 +3,8 @@
 
     <PropertyGroup>
         <GameManagedPath Condition="'$(GameManagedPath)' == ''">C:/Program Files (x86)/Steam/steamapps/common/A Dance of Fire and Ice/A Dance of Fire and Ice_Data/Managed</GameManagedPath>
+        <GameModsPath Condition="'$(GameModsPath)' == ''">$(GameManagedPath)/../../Mods</GameModsPath>
+        <OverlaySdkPath Condition="'$(OverlaySdkPath)' == ''">$(MSBuildThisFileDirectory)../TUFReplay-Renderer/sdk/TUFReplay.Overlay/bin/Release/netstandard2.0/TUFReplay.Overlay.dll</OverlaySdkPath>
+        <KeyboardChatterBlockerPath Condition="'$(KeyboardChatterBlockerPath)' == ''">$(GameModsPath)/KeyboardChatterBlocker/KeyboardChatterBlocker.dll</KeyboardChatterBlockerPath>
     </PropertyGroup>
-</Project>
\ No newline at end of file
+</Project>
```

<a id="file-14"></a>

## 14. Directory.Build.targets

[Directory.Build.targets](/Users/kgh/dev/src/jipper-resourcepack/Directory.Build.targets) — 설치 환경의 참조 Assembly 해결 (새 파일).

이 파일은 오버레이 프레임 프로토콜과 무관한 로컬 빌드 지원이다. `DisableImplicitFrameworkReferences == true`인 프로젝트에서, 존재하지 않는 HintPath를 가진 명시적 참조를 ResolveAssemblyReferences 전에 제거한다. `TUFReplay.Overlay`, `JALib`, `KeyboardChatterBlocker`는 그 제거 대상에서 제외해 누락이 드러나게 한다.

Unity Managed에 mscorlib.dll이 있으면 SDK target 뒤에 GetReferenceAssemblyPaths를 정의하여 해당 Managed 디렉토리를 framework 참조 경로로 사용한다. 버전별 하위 프로젝트 빌드도 설치 게임의 framework Assembly를 사용하게 하는 조정이다. 존재하지 않는 참조를 제거하는 조건은 파일명별 플랫폼 whitelist가 아니라 위의 일반 조건임을 실제 코드에서 확인할 수 있다.

### 실제 코드 전체

```xml
<Project>
    <Target Name="RemoveUnavailablePlatformReferences" BeforeTargets="ResolveAssemblyReferences" Condition="'$(DisableImplicitFrameworkReferences)' == 'true'">
        <ItemGroup>
            <Reference Remove="@(Reference)" Condition="'%(Reference.HintPath)' != '' and !Exists('%(Reference.HintPath)') and '%(Reference.Identity)' != 'TUFReplay.Overlay' and '%(Reference.Identity)' != 'JALib' and '%(Reference.Identity)' != 'KeyboardChatterBlocker'"/>
        </ItemGroup>
    </Target>
    <!-- Unity supplies the framework assemblies for these explicit-reference mod
         projects. Define this after the SDK targets so nested builds use them too. -->
    <Target Name="GetReferenceAssemblyPaths" Condition="'$(DisableImplicitFrameworkReferences)' == 'true' and Exists('$(GameManagedPath)/mscorlib.dll')">
        <PropertyGroup>
            <_TargetFrameworkDirectories>$(GameManagedPath)</_TargetFrameworkDirectories>
            <_FullFrameworkReferenceAssemblyPaths>$(GameManagedPath)</_FullFrameworkReferenceAssemblyPaths>
            <TargetFrameworkDirectory>$(GameManagedPath)</TargetFrameworkDirectory>
        </PropertyGroup>
    </Target>
</Project>
```

<a id="file-15"></a>

## 15. JipperResourcePack/JipperResourcePack.csproj

[JipperResourcePack/JipperResourcePack.csproj](/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/JipperResourcePack.csproj) — SDK DLL 포함과 참조 경로·Harmony alias (기존 파일 수정).

공용 SDK Reference를 추가하고 Private=true로 설정한다. 기존 모드/릴리스 복사 target에도 SDK 복사를 추가하여 배포물에 DLL을 함께 넣는다. 별도 UMM 의존성 모드를 추가하는 변경은 없다.

AdofaiTweaks/JALib 경로는 `GameModsPath`, KeyboardChatterBlocker는 지정 가능한 별도 property를 사용한다. macOS의 앱 내부 Managed 위치에서 Mods를 단순히 ../../로 추정하지 않게 한다.

0Harmony에는 `HarmonyLibrary` alias를 지정한다. 다음 HarmonyAlias.cs에서 필요한 CodeInstruction만 꺼내 쓰도록 하여 embedded framework helper와 기존 System/Unsafe 타입의 충돌을 피하는 빌드 조정이다. 이 변경은 새 Harmony 런타임 패치를 추가하지 않는다.

### 실제 diff

```diff
diff --git a/JipperResourcePack/JipperResourcePack.csproj b/JipperResourcePack/JipperResourcePack.csproj
index acd99b0..9751d86 100644
--- a/JipperResourcePack/JipperResourcePack.csproj
+++ b/JipperResourcePack/JipperResourcePack.csproj
@@ -29,14 +29,19 @@
     </PropertyGroup>
 
     <ItemGroup>
+        <Reference Include="TUFReplay.Overlay">
+            <HintPath>$(OverlaySdkPath)</HintPath>
+            <Private>true</Private>
+        </Reference>
         <Reference Include="0Harmony">
             <HintPath>$(GameManagedPath)/UnityModManager/0Harmony.dll</HintPath>
+            <Aliases>HarmonyLibrary</Aliases>
         </Reference>
         <Reference Include="Accessibility">
             <HintPath>$(GameManagedPath)/Accessibility.dll</HintPath>
         </Reference>
         <Reference Include="AdofaiTweaks">
-            <HintPath>$(GameManagedPath)/../../Mods/AdofaiTweaks/AdofaiTweaks.dll</HintPath>
+            <HintPath>$(GameModsPath)/AdofaiTweaks/AdofaiTweaks.dll</HintPath>
         </Reference>
         <Reference Include="Assembly-CSharp">
             <HintPath>$(GameManagedPath)/Assembly-CSharp.dll</HintPath>
@@ -78,10 +83,10 @@
             <HintPath>$(GameManagedPath)/I18N.West.dll</HintPath>
         </Reference>
         <Reference Include="JALib">
-            <HintPath>$(GameManagedPath)/../../Mods/JALib/JALib.dll</HintPath>
+            <HintPath>$(GameModsPath)/JALib/JALib.dll</HintPath>
         </Reference>
         <Reference Include="KeyboardChatterBlocker">
-            <HintPath>$(GameManagedPath)/../../Mods/KeyboardChatterBlocker/KeyboardChatterBlocker.dll</HintPath>
+            <HintPath>$(KeyboardChatterBlockerPath)</HintPath>
         </Reference>
         <Reference Include="Mono.Data.Sqlite">
             <HintPath>$(GameManagedPath)/Mono.Data.Sqlite.dll</HintPath>
@@ -541,9 +546,11 @@
     <Target Name="CopyToModPath" AfterTargets="Build" Condition="'$(ModPath)' != ''">
         <Copy SourceFiles="$(OutputPath)JipperResourcePack.dll" DestinationFolder="$(ModPath)/JipperResourcePack" SkipUnchangedFiles="true"/>
         <Copy SourceFiles="$(OutputPath)JipperResourcePack.pdb" DestinationFolder="$(ModPath)/JipperResourcePack" Condition="Exists('$(OutputPath)JipperResourcePack.pdb')" SkipUnchangedFiles="true"/>
+        <Copy SourceFiles="$(OverlaySdkPath)" DestinationFolder="$(ModPath)/JipperResourcePack" SkipUnchangedFiles="true"/>
     </Target>
 
     <Target Name="CopyToReleasePath" AfterTargets="Build" Condition="'$(ReleasePath)' != '' and '$(Configuration)' == 'Release'">
         <Copy SourceFiles="$(OutputPath)JipperResourcePack.dll" DestinationFolder="$(ReleasePath)/JipperResourcePack" SkipUnchangedFiles="true"/>
+        <Copy SourceFiles="$(OverlaySdkPath)" DestinationFolder="$(ReleasePath)/JipperResourcePack" SkipUnchangedFiles="true"/>
     </Target>
 </Project>
```

<a id="file-16"></a>

## 16. JipperResourcePack/HarmonyAlias.cs

[JipperResourcePack/HarmonyAlias.cs](/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/HarmonyAlias.cs) — Harmony에서 CodeInstruction만 명시적으로 노출 (새 파일).

extern alias와 global using으로 Harmony의 CodeInstruction을 기존 소스에서 같은 이름으로 사용하게 한다. 기존 세 파일의 `using HarmonyLib` 제거와 짝을 이루는 변경이다. 렌더 전용 분기나 실행 코드는 없다.

### 실제 코드 전체

```csharp
extern alias HarmonyLibrary;

// Harmony may embed framework helpers; expose only the API this mod uses.
global using CodeInstruction = HarmonyLibrary::HarmonyLib.CodeInstruction;
```

<a id="file-17"></a>

## 17. scripts/run.sh

[scripts/run.sh](/Users/kgh/dev/src/jipper-resourcepack/scripts/run.sh) — 빌드·패키징·검사·설치 진입점 (새 파일).

`build`, `package`, `install`, `check` 네 명령을 제공한다. build는 게임 Managed/JALib/SDK/선택적 upstream KeyboardChatterBlocker의 컴파일 참조를 검증한 뒤 dotnet build를 실행한다. 원래 CopyToModPath/CopyToReleasePath target의 경로를 빈 값으로 넘겨 단순 build가 게임을 수정하지 않도록 한다.

package는 JipperResourcePack.dll, TUFReplay.Overlay.dll, R136/R141/R146/R149 VersionSafe DLL만 출력한다. bootstrap, Unity 번들, 폰트, localization을 모두 포함하는 완전한 신규 설치 zip이 아니라 기존 JRP 패키지 위에 덮을 프로그램 payload다.

install은 Info.json이 있는 기존 설치를 요구한다. 처음 있던 파일을 백업하고, 없던 파일은 missing marker로 기록한다. 이미 백업된 파일은 덮어쓰지 않는다. SHA-256 매니페스트를 기록하고 위 DLL 6개만 복사한다. 기존 settings/카운터/assets는 복사 대상에 없다.

check는 git diff --check, bash -n, 실제 Rain/RainPool을 연결한 회귀 테스트를 실행한다. 이 설명 작업에서는 build/check/install을 다시 실행하지 않았다.

### 실제 코드 전체

```bash
#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
command_name="${1:-build}"
game_root="${ADOFAI_PATH:-$HOME/Library/Application Support/Steam/steamapps/common/A Dance of Fire and Ice}"
managed_path="${ADOFAI_MANAGED_PATH:-$game_root/ADanceOfFireAndIce.app/Contents/Resources/Data/Managed}"
mods_path="${ADOFAI_MODS_PATH:-$game_root/Mods}"
sdk_path="${OVERLAY_SDK_PATH:-$repo_root/../TUFReplay-Renderer/sdk/TUFReplay.Overlay/bin/Release/netstandard2.0/TUFReplay.Overlay.dll}"
keyboard_chatter_path="${KEYBOARD_CHATTER_BLOCKER_DLL:-$mods_path/KeyboardChatterBlocker/KeyboardChatterBlocker.dll}"
configuration="${CONFIGURATION:-Release}"
dotnet_command="${DOTNET:-dotnet}"

if [[ "$command_name" == check ]]; then
    git -C "$repo_root" diff --check
    bash -n "$repo_root/scripts/run.sh"
    "$dotnet_command" run --project "$repo_root/tests/RainLayout/RainLayout.csproj" -c "$configuration"
    exit 0
fi
if [[ "$command_name" != build && "$command_name" != package && "$command_name" != install ]]; then
    printf 'Usage: bash scripts/run.sh [build|package|install|check]\n' >&2
    exit 2
fi
if [[ ! -f "$managed_path/Assembly-CSharp.dll" || ! -f "$mods_path/JALib/JALib.dll" ]]; then
    printf 'Set ADOFAI_MANAGED_PATH and ADOFAI_MODS_PATH to an ADOFAI installation with JALib.\n' >&2
    exit 1
fi
if [[ ! -f "$sdk_path" ]]; then
    printf 'Build the common SDK with TUFReplay-Renderer/scripts/run.sh overlay-sdk-build, or set OVERLAY_SDK_PATH.\n' >&2
    exit 1
fi
if [[ ! -f "$keyboard_chatter_path" ]]; then
    printf 'Set KEYBOARD_CHATTER_BLOCKER_DLL to the optional KeyboardChatterBlocker DLL used as a compile-time reference.\n' >&2
    exit 1
fi

"$dotnet_command" build "$repo_root/JipperResourcePack/JipperResourcePack.csproj" -c "$configuration" \
    -p:GameManagedPath="$managed_path" -p:GameModsPath="$mods_path" -p:OverlaySdkPath="$sdk_path" \
    -p:KeyboardChatterBlockerPath="$keyboard_chatter_path" \
    -p:ModPath= -p:ReleasePath= -v:minimal

if [[ "$command_name" == package || "$command_name" == install ]]; then
    package_root="${PACKAGE_PATH:-$repo_root/artifacts/render-support/JipperResourcePack}"
    mkdir -p "$package_root/VersionSafe"
    cp "$repo_root/JipperResourcePack/bin/$configuration/net481/JipperResourcePack.dll" "$package_root/"
    cp "$sdk_path" "$package_root/"
    for release in R136 R141 R146 R149; do
        cp "$repo_root/JipperResourcePack.VersionSafe/bin/$configuration/$release/JipperResourcePack.VersionSafe.dll" "$package_root/VersionSafe/JipperResourcePack.VersionSafe.$release.dll"
    done
    printf 'Render support files: %s\nCopy these files over an existing JipperResourcePack package; keep its bootstrap, bundle, fonts, and localization.\n' "$package_root"
fi

if [[ "$command_name" == install ]]; then
    installed_root="$mods_path/JipperResourcePack"
    backup_root="${VALIDATION_BACKUP_PATH:-/private/tmp/tuf-overlay-validation-originals/jipper}"
    if [[ ! -f "$installed_root/Info.json" ]]; then
        printf 'install requires an existing JipperResourcePack installation at %s.\n' "$installed_root" >&2
        exit 1
    fi
    payload=(JipperResourcePack.dll TUFReplay.Overlay.dll)
    for release in R136 R141 R146 R149; do
        payload+=("VersionSafe/JipperResourcePack.VersionSafe.$release.dll")
    done
    mkdir -p "$backup_root"
    if [[ ! -f "$backup_root/original-sha256.tsv" ]]; then
        printf 'sha256\tpath\n' > "$backup_root/original-sha256.tsv"
    fi
    for relative_path in "${payload[@]}"; do
        if [[ -e "$backup_root/$relative_path" || -e "$backup_root/missing/$relative_path" ]]; then
            continue
        fi
        if [[ -f "$installed_root/$relative_path" ]]; then
            mkdir -p "$(dirname "$backup_root/$relative_path")"
            cp -p "$installed_root/$relative_path" "$backup_root/$relative_path"
            digest="$(shasum -a 256 "$backup_root/$relative_path")"
            printf '%s\t%s\n' "${digest%% *}" "$relative_path" >> "$backup_root/original-sha256.tsv"
        else
            mkdir -p "$(dirname "$backup_root/missing/$relative_path")"
            touch "$backup_root/missing/$relative_path"
            printf 'absent\t%s\n' "$relative_path" >> "$backup_root/original-sha256.tsv"
        fi
    done
    printf 'sha256\tpath\n' > "$backup_root/installed-sha256.tsv"
    for relative_path in "${payload[@]}"; do
        mkdir -p "$(dirname "$installed_root/$relative_path")"
        cp -p "$package_root/$relative_path" "$installed_root/$relative_path"
        digest="$(shasum -a 256 "$installed_root/$relative_path")"
        printf '%s\t%s\n' "${digest%% *}" "$relative_path" >> "$backup_root/installed-sha256.tsv"
    done
    printf 'Installed program DLLs at %s\nOriginal program files and hashes: %s\n' "$installed_root" "$backup_root"
fi
```

<a id="file-18"></a>

## 18. .gitignore

[.gitignore](/Users/kgh/dev/src/jipper-resourcepack/.gitignore) — 로컬 패키지 결과 제외 (기존 파일 수정).

프로그램 패키징 결과가 Git 변경 목록에 섞이지 않도록 `artifacts/`를 추가한다. 기존 ignore 항목은 유지되며 마지막 newline도 정리됐다.

### 실제 diff

```diff
diff --git a/.gitignore b/.gitignore
index 14b4cc1..c04d1b0 100644
--- a/.gitignore
+++ b/.gitignore
@@ -10,4 +10,7 @@ obj/
 packages/
 
 # User Settings
-Directory.Build.props.user
\ No newline at end of file
+Directory.Build.props.user
+
+# Local render-support package
+artifacts/
```

<a id="file-19"></a>

## 19. README.md

[README.md](/Users/kgh/dev/src/jipper-resourcepack/README.md) — 포크의 렌더 동작·빌드·설치·제한 문서화 (기존 파일 수정).

기존 upstream README 뒤에 TUFReplay rendering 설명을 추가한다. SDK 동봉, 프레임별 held-state 샘플링, live 상태 복구, 물리 입력 보관, 저장 차단, 빌드/패키지/설치 경로와 환경 변수, native-only 키 바인딩 제한, Rain 회귀 테스트 범위를 설명한다.

읽을 때 한 가지 구분이 필요하다. README의 excluded 때 복구된다는 문구는 최종적으로 복구한다는 정책 설명이다. 현재 실제 SetPresentationEnabled(false)는 렌더 UI만 숨기며, 원래 live UI와 카운터 복구는 세션 종료 시 EndRender에서 수행한다. 이번 정리에서는 README를 수정하지 않았다.

### 실제 diff

```diff
diff --git a/README.md b/README.md
index 2136ea8..c9b3b4b 100644
--- a/README.md
+++ b/README.md
@@ -83,3 +83,57 @@
 ---
 # [Join Jipper's Discord!](https://discord.gg/qTbnPhY7YA)
 # [Join My Discord!](https://discord.jongyeol.kr)
+
+## TUFReplay rendering
+
+This fork bundles `TUFReplay.Overlay.dll` with the mod. It preserves the normal
+SkyHook input backend when no render is running. During an offline render, the
+key viewer samples the recorded held keys once per simulation frame and updates
+its existing key, counter, KPS, and rain UI on the game thread. A press and release
+that both happen between sampled frames may be invisible, as with a state-based
+viewer.
+
+The render uses a separate viewer layout and temporary counter data on the same
+Canvas. The live layout and counters are restored when rendering ends, is
+cancelled, or the viewer is excluded. Render-only counts and play records are
+not written to `KeyCount.dat` or `Plays.dat`. The status overlay also uses the
+render clock for combo animation and restores its previous UI state afterward.
+Physical keys pressed or released while rendering only update a held-state
+snapshot for resuming the live viewer. Resuming reconciles highlights without
+adding counts, KPS entries, or new rain effects.
+No additional TUFReplay dependency mod is required; JALib remains required.
+
+Build the common SDK in the sibling `TUFReplay-Renderer` checkout first:
+
+```sh
+bash ../TUFReplay-Renderer/scripts/run.sh overlay-sdk-build
+bash scripts/run.sh build
+bash scripts/run.sh package
+bash scripts/run.sh check
+```
+
+The build finds the standard macOS Steam installation by default. Set
+`ADOFAI_MANAGED_PATH`, `ADOFAI_MODS_PATH`, and optionally `OVERLAY_SDK_PATH` for
+other installations. `package` creates an overlay of the compiled DLLs in
+`artifacts/render-support/JipperResourcePack`; combine it with an existing
+JipperResourcePack package so its bootstrap, Unity bundle, fonts, and localization
+remain available. Building or packaging does not modify the installed game.
+
+For local game verification, `bash scripts/run.sh install` backs up the program
+DLLs to `/private/tmp/tuf-overlay-validation-originals/jipper`, records their
+original and installed SHA-256 hashes, and copies only the main DLL, common SDK,
+and four VersionSafe DLLs into the existing mod installation. It preserves the
+bootstrap, `Info.json`, assets, settings, and counter files. Set
+`VALIDATION_BACKUP_PATH` to use another backup directory.
+
+The upstream optional KeyboardChatterBlocker integration needs its DLL as a
+compile-time reference. If that mod is not installed, set
+`KEYBOARD_CHATTER_BLOCKER_DLL` to a separately downloaded or backed-up DLL;
+the build neither installs nor bundles it. Existing native-only bindings encoded
+by this mod cannot be reproduced by the current recording format, which contains
+Unity key names. The render samples the ordinary Unity key bindings.
+
+`check` also compiles the production rain constructor and pool in a small
+standalone regression test. It checks that new normal/ghost rain and pooled
+graphics stay on the parent Canvas plane after the Canvas has been moved, scaled,
+and rotated for camera capture. This does not replace an in-game visual check.
```

<a id="file-20"></a>

## 20. tests/RainLayout/RainLayout.csproj

[tests/RainLayout/RainLayout.csproj](/Users/kgh/dev/src/jipper-resourcepack/tests/RainLayout/RainLayout.csproj) — 운영 Rain 코드와 풀을 직접 연결하는 검사 프로젝트 (새 파일).

net10.0의 standalone 실행 프로젝트다. production Rain.cs와 RainPool.cs를 Compile Include로 직접 넣는다. 수정된 코드의 별도 복제본을 검증하는 대신 실제 생성자와 재사용 로직을 테스트한다. Unity 런타임 대신 다음 UnityStubs.cs의 필요한 타입만 사용하므로 이 테스트는 실제 카메라 렌더를 실행하지 않는다. 모드 본체의 target framework를 net10으로 바꾸는 변경은 아니다.

### 실제 코드 전체

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>disable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="../../JipperResourcePack/KeyViewerContents/Rain.cs" Link="Rain.cs" />
    <Compile Include="../../JipperResourcePack/KeyViewerContents/RainPool.cs" Link="RainPool.cs" />
  </ItemGroup>
</Project>
```

<a id="file-21"></a>

## 21. tests/RainLayout/Program.cs

[tests/RainLayout/Program.cs](/Users/kgh/dev/src/jipper-resourcepack/tests/RainLayout/Program.cs) — 일반·고스트·풀 재사용의 UI 좌표 회귀 검사 (새 파일).

부모 RectTransform에 이동·회전·스케일을 적용한 상태에서 normal/ghost Rain을 만들고 local Z=0, world Z=parent Z, 부모를 따르는 회전·크기·앵커·활성 상태를 검사한다. 일반/고스트 Graphic 종류와 고스트 스프라이트/타일 설정도 확인한다.

이후 풀에 반환하여 숨겨지는지 확인하고, 같은 객체를 재사용하는지 및 크기가 초기화되며 평면을 유지하는지 검사한다. 앞선 구현 작업에서는 수정 전 Rain.cs에서 local depth 검사 실패, 수정 후 통과를 확인했다. 이번 문서화 작업에서는 실행 결과를 새로 만들지 않았다.

### 실제 코드 전체

```csharp
using JipperResourcePack.KeyViewerContents;
using UnityEngine;
using UnityEngine.UI;

// A camera-space Canvas moves/scales its descendants away from the world origin.
// Link the production constructor and pool so this checks dynamic creation,
// rather than an independent copy of the parenting code.
RectTransform parent = new GameObject("Camera-space rain line").AddComponent<RectTransform>();
parent.localPosition = new Vector3(12, -4, -9);
parent.localScale = new Vector3(0.01f, 0.01f, 0.01f);
parent.localRotation = Quaternion.EulerZ(25);
RainPool pool = new(parent);

foreach(bool ghost in new[] { false, true }) {
    Rain rain = pool.GetOrNewRain(ghost);
    CheckLayout(rain, ghost, "new");
    Check(ghost ? rain.Image is Image : rain.Image is RawImage, "Graphic type");
    if(ghost) {
        Image image = (Image) rain.Image;
        Check(ReferenceEquals(image.sprite, JipperResourcePack.BundleLoader.GhostRain), "Ghost sprite");
        Check(image.type == Image.Type.Tiled, "Ghost tiling");
    }

    rain.Transform.sizeDelta = new Vector2(50, 100);
    pool.AddPool(rain, ghost);
    Check(!rain.GameObject.activeSelf, "Pooled rain hidden");
    Rain reused = pool.GetOrNewRain(ghost);
    Check(ReferenceEquals(rain, reused), "Reuse existing rain");
    CheckLayout(reused, ghost, "reused");
}

Console.WriteLine("Rain layout regression passed: normal, ghost, and pooled graphics stay on the translated/scaled/rotated parent plane.");

void CheckLayout(Rain rain, bool ghost, string stage) {
    string kind = (ghost ? "ghost" : "normal") + " " + stage;
    Check(ReferenceEquals(rain.Transform.parent, parent), kind + " parent");
    Check(Near(rain.Transform.localPosition.z, 0), kind + " local depth must remain on its Canvas plane");
    Check(Near(rain.Transform.position.z, parent.position.z), kind + " world depth follows the parent");
    Check(rain.Transform.localRotation.IsIdentity, kind + " local rotation follows the parent");
    Check(rain.Transform.localScale == Vector3.one, kind + " local scale");
    Check(rain.Transform.anchoredPosition == Vector2.zero, kind + " anchor position");
    Check(rain.Transform.sizeDelta == Vector2.zero, kind + " fresh size");
    Check(rain.Transform.anchorMin == new Vector2(0.5f, 1) && rain.Transform.anchorMax == rain.Transform.anchorMin,
        kind + " anchors");
    Check(rain.Transform.pivot == new Vector2(0.5f, 1), kind + " pivot");
    Check(rain.GameObject.activeSelf, kind + " visible");
    Check(rain.IsGhost == ghost, kind + " ghost flag");
}

static bool Near(float value, float expected) => Math.Abs(value - expected) < 0.0001f;
static void Check(bool success, string message) {
    if(!success) throw new InvalidOperationException("Rain layout regression: " + message);
}
```

<a id="file-22"></a>

## 22. tests/RainLayout/UnityStubs.cs

[tests/RainLayout/UnityStubs.cs](/Users/kgh/dev/src/jipper-resourcepack/tests/RainLayout/UnityStubs.cs) — 부모 변환 계약을 모델링하는 최소 Unity 대체 타입 (새 파일).

System.Numerics의 행렬·Quaternion으로 부모의 이동·회전·스케일과 SetParent(worldPositionStays)를 모델링한다. RectTransform의 2D anchoredPosition setter가 local Z를 유지하도록 하여 기존 생성 코드의 깊이 문제를 재현한다.

나머지는 Rain/RainPool이 사용하는 최소 GameObject, Graphic, Image, sprite, RawRain 선언이다. Graphic의 실제 메시 생성·클리핑·카메라·GPU·영상 픽셀은 구현하지 않으므로 좌표/풀 회귀 확인과 실제 게임 영상 검증은 구분해야 한다.

### 실제 코드 전체

```csharp
using NumericsMatrix = System.Numerics.Matrix4x4;
using NumericsQuaternion = System.Numerics.Quaternion;
using NumericsVector = System.Numerics.Vector3;

// Only the Unity surface used by Rain/RainPool is modeled. Affine parenting
// follows Transform.SetParent's worldPositionStays contract; no rendering is
// simulated. RectTransform's 2D setter deliberately preserves local depth.
namespace UnityEngine {
    public readonly record struct Vector2(float x, float y) {
        public static Vector2 zero => new(0, 0);
    }

    public readonly record struct Vector3(float x, float y, float z) {
        public static Vector3 zero => new(0, 0, 0);
        public static Vector3 one => new(1, 1, 1);
        internal NumericsVector Native => new(x, y, z);
        internal static Vector3 From(NumericsVector value) => new(value.X, value.Y, value.Z);
    }

    public readonly record struct Quaternion(float x, float y, float z, float w) {
        public static Quaternion identity => new(0, 0, 0, 1);
        public bool IsIdentity => Math.Abs(x) + Math.Abs(y) + Math.Abs(z) + Math.Abs(w - 1) < 0.0001f;
        internal NumericsQuaternion Native => new(x, y, z, w);
        internal static Quaternion From(NumericsQuaternion value) => new(value.X, value.Y, value.Z, value.W);
        public static Quaternion EulerZ(float degrees) => From(NumericsQuaternion.CreateFromAxisAngle(
            NumericsVector.UnitZ, degrees * MathF.PI / 180));
    }

    public class Component {
        public GameObject gameObject;
    }

    public class Transform : Component {
        public Transform parent { get; private set; }
        public Vector3 localPosition = Vector3.zero;
        public Vector3 localScale = Vector3.one;
        public Quaternion localRotation = Quaternion.identity;
        private NumericsMatrix WorldMatrix => NumericsMatrix.CreateScale(localScale.Native)
            * NumericsMatrix.CreateFromQuaternion(localRotation.Native)
            * NumericsMatrix.CreateTranslation(localPosition.Native)
            * (parent?.WorldMatrix ?? NumericsMatrix.Identity);
        public Vector3 position => Vector3.From(NumericsVector.Transform(NumericsVector.Zero, WorldMatrix));

        public void SetParent(Transform target, bool worldPositionStays = true) {
            NumericsMatrix world = WorldMatrix;
            parent = target;
            if(!worldPositionStays) return;
            if(!NumericsMatrix.Invert(target?.WorldMatrix ?? NumericsMatrix.Identity, out NumericsMatrix inverse)
                || !NumericsMatrix.Decompose(world * inverse, out NumericsVector scale,
                    out NumericsQuaternion rotation, out NumericsVector position))
                throw new InvalidOperationException("Invalid test transform.");
            localPosition = Vector3.From(position);
            localScale = Vector3.From(scale);
            localRotation = Quaternion.From(rotation);
        }
    }

    public class RectTransform : Transform {
        public Vector2 anchorMin, anchorMax, pivot, sizeDelta;
        public Vector2 anchoredPosition {
            get => new(localPosition.x, localPosition.y);
            set => localPosition = new Vector3(value.x, value.y, localPosition.z);
        }
    }

    public class GameObject {
        public bool activeSelf { get; private set; } = true;
        public GameObject(string name) { }
        public T AddComponent<T>() where T : Component, new() => new T { gameObject = this };
        public void SetActive(bool active) => activeSelf = active;
    }

    public class Sprite { }
}

namespace UnityEngine.UI {
    public class Graphic : UnityEngine.Component { }
    public class RawImage : Graphic { }
    public class Image : Graphic {
        public enum Type { Simple, Tiled }
        public UnityEngine.Sprite sprite;
        public Type type;
    }
}

namespace JipperResourcePack {
    public static class BundleLoader {
        public static readonly UnityEngine.Sprite GhostRain = new();
    }
}

namespace JipperResourcePack.KeyViewerContents {
    public class RawRain { }
}
```

## 검증 기록의 범위

앞선 구현 작업의 [전체 구현 결과](/Users/kgh/dev/src/tuf-replay-dev/docs/overlay-runtime-implementation-result-2026-10-11.md)와 [레이닝 수정 결과](/Users/kgh/dev/src/tuf-replay-dev/docs/jipper-rain-render-fix-2026-10-11.md)에 빌드·테스트·설치 기록이 있다. 그 기록에서 Rain 회귀 테스트는 수정 전 실패/수정 후 통과, 최종 JRP 빌드는 경고 0개·오류 0개로 보고됐다. 이 문서를 만든 side conversation에서는 코드 읽기와 문서 작성만 수행했다. 실제 영상에서 수정 효과를 새로 검증했다는 뜻은 아니다.

## 스냅샷 식별 정보

후속 변경과 구분할 수 있도록 각 설명 대상 파일의 SHA-256을 기록한다. 이는 소스 파일의 해시이며 설치된 DLL 해시와 다르다.

```text
f134f4a27810fb0008a9fd3b80df1985e1a61f3124fbbe428c6d88112a2bd9f6  .gitignore
29ea8870a189ec1382e411ff73fc4dc3b706d8a2bf6a1d4ec0887da46ff84c9e  Directory.Build.props
32436803eb105c549170b4cf4b233bbe3debda9d95d750cb803a3cc9cac71557  Directory.Build.targets
f22967b286ebd0a43d3d8ffe6b4ca3194e9400deaf1c6dd7332830244a391327  JipperResourcePack/HarmonyAlias.cs
2577f29a9c45175232680521cb1e30f30b1d9f933781a797586d2329215009ba  JipperResourcePack/JipperResourcePack.csproj
21d6ebbb07a135c15731d3646cbea01afa7435e060a281a87fbb9cc48b8b8667  JipperResourcePack/KeyViewerContents/KeyCountData.cs
50d16153c7e48c03a4e0a5068743f70e561f69661599d71a895a15f26956aaf6  JipperResourcePack/KeyViewerContents/KeyViewer.Render.cs
86ec7c7d1bcc11e42751a1071fe3c83f7e1c7e2ea497e38c25aaf2f6e1f37ec2  JipperResourcePack/KeyViewerContents/KeyViewer.Update.cs
61264ab655c7af6baadd06c9cad36228e13ea47601fc79657bb4d78a966a3e3c  JipperResourcePack/KeyViewerContents/KeyViewer.cs
e3e7857a66e3e3cd558c8b215cf573851537831c598c27cd273d4eeae080c1be  JipperResourcePack/KeyViewerContents/Rain.cs
50fc48e6c737d137a9b4062c82c80e5f66d39254ddeb039c13e75abf9d1a1336  JipperResourcePack/KeyViewerContents/RainManager.cs
e64248be5245a64937cca9f14c9881d851d6cb6d71a178d9f5f49048bc17a5a8  JipperResourcePack/Main.cs
bc8efe54c21fbbc99db6490ec075c4ee35ef4c3ca45a88cd6c46ae9f0fc0d5dd  JipperResourcePack/OverlayContents/Overlay.Render.cs
450b9ba0d08207a4436e9993fa7ee37a4f3b11e57725be0baadcd902f6f91285  JipperResourcePack/OverlayContents/Overlay.cs
fe5016cea9fbc3288efc3820ee8b219bf4527f8a11a926eaf6c12b606df0154e  JipperResourcePack/OverlayContents/PlayCount.cs
334816e01412eba8c37b2974bd5f0094b2ae9ba358dba883dd14413551f02a6a  JipperResourcePack/OverlayContents/Status.cs
5fe6bf6573f9183df62af960c3bf6f48eee8017fd8d3f9f91ba526182ca845ad  JipperResourcePack/UnityNativeServices.cs
64bbebbb15ba6e9c208ffce9554b7e1ea8f2c55a1391953ba7cf4d797b91d8e4  README.md
8c44b942d8210aa7e871327d5458483214cca75b35311581e2f518b1c01d786f  scripts/run.sh
a2c1e72a8dddedc65f5c320cc07d47bd036f4f5d5c955de37f6f0308396e28c7  tests/RainLayout/Program.cs
8f23282cea420148d6947f39d1baa9dbc3bc0a1988cc82ce3fcaee558f0fc786  tests/RainLayout/RainLayout.csproj
10e34972d90a9f167575e4b5fdefdfe402650b9ef0ecec849addb095a4d2e217  tests/RainLayout/UnityStubs.cs
```
