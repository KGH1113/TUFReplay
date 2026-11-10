# 5. 영상 렌더

렌더는 판을 리플레이해서 영상으로 씁니다. 리플레이는 TUFReplay가 직접 하고, OrbitRender(RenderApi 3 이상)는 캡처와 인코딩만 합니다. 프로바이더는 게임 안 리플레이와 같은 호출을 영상 시간으로 받고, 영상에 무엇이 나올지 정하는 호출 몇 개를 더 받습니다.

```csharp
internal sealed class ReplayFollower
{
    // … 2단계의 메서드 …

    // 영상이 시작될 때 한 번 묻습니다.
    public IEnumerable<Canvas> GetReplayCanvases()
    {
        yield return KeyViewer.Instance.Canvas;
    }
}
```

| 메서드 | 호출 시점 |
| --- | --- |
| `IEnumerable<Canvas> GetReplayCanvases()` 또는 `Canvas GetReplayCanvases()` | 영상이 시작될 때 한 번: 영상에 넣을 캔버스 |
| `void OnBeforeFrameCapture()` / `void OnAfterFrameCapture()` | 새 영상 프레임마다 앞뒤로 |
| `void AddReplayOverlayVideos(Action<string, RectInt, RectInt> add)` | 렌더가 완료되면 마지막 프레임 뒤에 한 번: 완성된 영상 위에 겹칠 영상 |

게임 안 리플레이에서는 하나도 불리지 않습니다.

```csharp
public static int RenderWidth { get; }      // 렌더 중에는 영상 크기, 아니면 0
public static int RenderHeight { get; }
public static int RenderFps { get; }
public static bool RenderTakesOverlayVideos { get; } // 렌더러가 모드의 영상을 겹쳐 줌
public static string RenderFfmpegPath { get; }       // 렌더러와 같이 쓰는 FFmpeg, 없으면 null
```

## 렌더 순서

- `OnReplayBegin(render: true)`는 TUFReplay가 작업을 OrbitRender에 넘기기 전, 레벨이 열리기 전에 불립니다. 바로 이어서 시작 값과 함께 `OnReplayEvent`가 불립니다.
- `GetReplayCanvases`는 레벨이 준비된 뒤 첫 프레임 전에 한 번 불립니다. 그 뒤에 만든 캔버스는 영상에 들어가지 않으니, 그 전이나 이 호출 안에서 만드세요. `RenderWidth`, `RenderHeight`, `RenderFps`는 이 호출부터 영상의 최종 값이고, 그 전에는 요청한 값입니다.
- 매 프레임: 영상 시간으로 `OnReplayClock`, 그다음 그 프레임의 값에 대한 `OnReplayEvent`와 키에 대한 `OnReplayKey`가 각자의 영상 시간으로 불립니다. `OnBeforeFrameCapture`와 `OnAfterFrameCapture`는 새 영상 프레임의 앞뒤로 불리고, 반복된 프레임에서는 불리지 않습니다.
- `AddReplayOverlayVideos`는 마지막 프레임 뒤, OrbitRender가 완성된 영상을 합치기 전에 한 번 불립니다.
- `OnReplayEnd`는 렌더가 완료되거나, 실패하거나, 취소될 때 불립니다. OrbitRender가 게임을 되돌리고 영상을 합친 뒤입니다. 첫 프레임 전에 끝난 렌더(예: 레벨이 열리는 중에 취소)는 `GetReplayCanvases` 없이 `OnReplayEnd`만 부릅니다.

## 오버레이 영상

다른 앱에서 돌아가는 키뷰어처럼 그리기 느린 모드는, 게임이 렌더하는 동안 자기 영상을 따로 만들 수 있습니다. 그러면 OrbitRender가 웹캠을 넣듯이 그 영상을 완성된 영상 위에 겹치고, 렌더 중에 게임은 그 모드를 기다리지 않습니다.

**렌더러가 받는지**
- 렌더러가 `AddReplayOverlayVideos`를 부를 때(`OverlayVideoVersion` 1 이상인 OrbitRender) `RenderTakesOverlayVideos`가 true입니다. `OnReplayBegin(render: true)`부터 읽을 수 있습니다.
- `RenderFfmpegPath`는 TUFReplay가 OrbitRender와 같이 쓰는 FFmpeg 실행 파일로, 영상을 만들 때 씁니다. 렌더 중이 아니거나 FFmpeg가 설치되지 않았으면 null입니다.
- 둘 중 하나라도 없으면 렌더러가 영상을 쓸 수 없습니다. 대신 캔버스를 그리거나, 그림을 빼세요.

**`AddReplayOverlayVideos(add)`**
- 마지막 프레임 뒤, OrbitRender가 영상을 합치기 전에 유니티 메인 스레드에서 한 번 불립니다. 영상을 다 쓸 때까지 붙잡고 있어도 되고, 렌더는 기다립니다.
- 렌더가 실패하거나 취소되면 불리지 않습니다. 정리는 영상을 합친 뒤에 불리는 `OnReplayEnd`에서 하세요.
- 호출 중에 영상마다 `add(path, source, target)`를 한 번씩 부릅니다. 나중에 추가한 영상은 빠집니다.
  - `path`: FFmpeg가 읽을 수 있는 영상의 절대 경로. 꽉 찬 사각형이 아닌 그림은 QuickTime Animation(`-c:v qtrle -pix_fmt argb`)처럼 알파가 있는 형식을 쓰세요.
  - `source`: 영상의 각 프레임에서 쓸 부분. 그 영상의 픽셀 단위입니다.
  - `target`: 그 부분이 렌더된 영상에서 들어갈 위치. `RenderWidth` × `RenderHeight` 픽셀 단위입니다. 크기가 다르면 늘리거나 줄이고, 영상 밖으로 나간 부분은 잘립니다.
  - 두 사각형 모두 왼쪽 위가 기준이고, `y`는 아래로 커집니다.
- 영상의 0초는 렌더된 영상의 첫 프레임입니다. `RenderFps`로, 영상 프레임 하나에 한 프레임씩 만드세요. 일찍 끝난 영상은 마지막 프레임을 유지합니다.
- 영상은 추가한 순서대로, 게임과 캔버스 위, 웹캠 아래에 그려집니다.
- 파일이 없거나, 경로가 절대 경로가 아니거나, 사각형이 비어 있는 영상은 로그를 남기고 빠집니다.

API는 여기까지입니다. 모든 멤버는 [레퍼런스](reference.md)에 모여 있습니다.
