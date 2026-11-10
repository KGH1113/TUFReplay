# 5. Render to video

A render replays the run and writes it to a video. TUFReplay replays the recording itself, and OrbitRender (RenderApi 3 or later) only captures and encodes it. Your provider gets the same calls as in the game, at video time, and a few more that decide what the video shows.

```csharp
internal sealed class ReplayFollower
{
    // … the methods from step 2 …

    // Asked once, as the video starts.
    public IEnumerable<Canvas> GetReplayCanvases()
    {
        yield return KeyViewer.Instance.Canvas;
    }
}
```

| Method | When |
| --- | --- |
| `IEnumerable<Canvas> GetReplayCanvases()` or `Canvas GetReplayCanvases()` | Once, as the video starts: the canvases to put in the video |
| `void OnBeforeFrameCapture()` / `void OnAfterFrameCapture()` | Around each new video frame |
| `void AddReplayOverlayVideos(Action<string, RectInt, RectInt> add)` | Once, after the last frame of a completed render: videos to lay over the finished video |

None of these is called during in-game replays.

```csharp
public static int RenderWidth { get; }      // the video while rendering, otherwise 0
public static int RenderHeight { get; }
public static int RenderFps { get; }
public static bool RenderTakesOverlayVideos { get; } // the renderer lays your videos over the video
public static string RenderFfmpegPath { get; }       // FFmpeg shared with the renderer, or null
```

## A render, step by step

- `OnReplayBegin(render: true)` runs before TUFReplay hands the job to OrbitRender, before the level opens, and `OnReplayEvent` with the start value follows right away.
- `GetReplayCanvases` runs once, after the level is ready and before the first frame. Canvases you create later are not in the video, so create them by then, or in this call. `RenderWidth`, `RenderHeight` and `RenderFps` hold the video's final values from this call on; before it they are the requested values.
- Every frame: `OnReplayClock` with the video time, then `OnReplayEvent` for the values and `OnReplayKey` for the keys of that frame, at their video times. `OnBeforeFrameCapture` and `OnAfterFrameCapture` wrap each new video frame; a repeated frame does not call them.
- `AddReplayOverlayVideos` runs once, after the last frame and before OrbitRender composes the finished video.
- `OnReplayEnd` runs when the render completes, fails or is cancelled, after OrbitRender has restored the game and composed the video. A render that ends before its first frame, such as one cancelled while the level opens, calls `OnReplayEnd` without `GetReplayCanvases`.

## Overlay videos

A mod whose picture is slow to draw, such as a key viewer that runs in another app, can make its own video while the game renders and have OrbitRender lay it over the finished video, the way it adds the webcam. The game then never waits for that mod during the render.

**Whether the renderer takes them**
- `RenderTakesOverlayVideos` is true when the renderer will call `AddReplayOverlayVideos` (OrbitRender with `OverlayVideoVersion` 1 or later). Read it from `OnReplayBegin(render: true)` on.
- `RenderFfmpegPath` is the FFmpeg executable TUFReplay shares with OrbitRender, for writing your video. It is null outside a render and when FFmpeg is not installed.
- When either is missing, the renderer can't use your video: draw a canvas instead, or leave your picture out.

**`AddReplayOverlayVideos(add)`**
- Runs once on Unity's main thread, after the last frame, before OrbitRender composes the video. It may block until your videos are written; the render waits.
- It is not called when the render fails or is cancelled. Clean up in `OnReplayEnd`, which runs after the video is composed.
- Call `add(path, source, target)` once per video, during the call. Videos added later are left out.
  - `path`: an absolute path to a video FFmpeg can read. Use a format with alpha, such as QuickTime Animation (`-c:v qtrle -pix_fmt argb`), for a picture that is not a full rectangle.
  - `source`: the part of each of the video's frames to use, in its pixels.
  - `target`: where that part goes in the rendered video, in pixels of `RenderWidth` × `RenderHeight`. A target of another size scales the part; a target partly outside the video is cut off.
  - Both rectangles count from the top left, with `y` going down.
- The video's time 0 is the rendered video's first frame. Make it at `RenderFps`, one frame per video frame. A video that ends early holds its last frame.
- Videos are drawn in the order added, over the game and its canvases and under the webcam.
- A video whose file is missing, whose path is not absolute, or whose rectangle is empty is left out with a log line.

That is the whole API. The [reference](reference.md) has every member in one place.
