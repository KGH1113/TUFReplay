using System.Reflection;
using TUFReplay.Webcam.Capture;
using TUFReplay.Webcam.Models;
using static TestFixture;

internal static class CameraFramePipelineSuite
{
  public static void RunAll()
  {
    SharedFramesReturnOnlyAfterEveryConsumerReleases();
    BurstOverloadKeepsRecentFramesWithoutOverwritingConsumer();
    DisposingQueuePreservesAnActiveConsumerLease();
    CompletingQueueWakesItsConsumerAndRejectsNewFrames();
    PreviewNeverWaitsForTheSharedDisplayBuffer();
    PreviewDisposalReleasesItsFrames();
    Console.WriteLine("TUFReplay camera frame pipeline tests passed.");
  }

  private static void SharedFramesReturnOnlyAfterEveryConsumerReleases()
  {
    var pool = new CameraFramePool(24, 2);
    CameraFrame shared = pool.TryRent();
    shared.Pixels[0] = 77;
    shared.TimestampTicks = 456;
    shared.Retain();
    shared.Retain();
    CameraFrame other = pool.TryRent();
    shared.Release();
    Assert(pool.TryRent() == null, "Capture reused a frame still held by preview and encoder.");
    shared.Release();
    Assert(pool.TryRent() == null, "Capture reused a frame before the last consumer finished.");
    Assert(shared.Pixels[0] == 77 && shared.TimestampTicks == 456, "An active frame was reset.");
    shared.Release();
    CameraFrame reused = pool.TryRent();
    Assert(ReferenceEquals(shared, reused), "The frame pool allocated instead of reusing a released buffer.");
    Assert(reused.TimestampTicks == 0, "A new capture inherited the previous frame timestamp.");
    reused.Release();
    other.Release();
  }

  private static void BurstOverloadKeepsRecentFramesWithoutOverwritingConsumer()
  {
    var pool = new CameraFramePool(24, 5);
    using var queue = new CameraFrameQueue(3);
    CameraFrame initial = pool.TryRent();
    initial.Pixels[0] = 0xDA;
    queue.Submit(initial);
    initial.Release();
    CameraFrame held = queue.Take();
    for (int index = 1; index <= 1000; index++)
    {
      CameraFrame frame = pool.TryRent();
      Assert(frame != null, "Replacing stale frames exhausted the bounded pool.");
      frame.TimestampTicks = index * 17_000L;
      frame.Pixels[0] = (byte)index;
      Assert(queue.Submit(frame), "Overload aborted the recording queue.");
      frame.Release();
    }
    Assert(held.Pixels[0] == 0xDA, "Overload overwrote the encoder's active frame.");
    queue.Complete();
    var timestamps = new List<long>();
    CameraFrame next;
    while ((next = queue.Take()) != null)
    {
      timestamps.Add(next.TimestampTicks);
      next.Release();
    }
    Assert(
      timestamps.SequenceEqual(new[] { 998 * 17_000L, 999 * 17_000L, 1000 * 17_000L }),
      "Overload failed to retain the latest capture timestamps."
    );
    Assert(queue.Dropped == 997, "Dropped-frame diagnostics lost replaced frames.");
    held.Release();
    AssertAllBuffersAvailable(pool, 5);
  }

  private static void DisposingQueuePreservesAnActiveConsumerLease()
  {
    var pool = new CameraFramePool(24, 3);
    using var queue = new CameraFrameQueue(3);
    for (int index = 0; index < 3; index++)
    {
      CameraFrame frame = pool.TryRent();
      queue.Submit(frame);
      frame.Release();
    }
    CameraFrame active = queue.Take();
    queue.Dispose();
    CameraFrame first = pool.TryRent();
    CameraFrame second = pool.TryRent();
    Assert(first != null && second != null, "Queue disposal leaked pending frames.");
    Assert(pool.TryRent() == null, "Queue disposal returned a frame still being encoded.");
    active.Release();
    CameraFrame last = pool.TryRent();
    Assert(ReferenceEquals(active, last), "The active frame did not return after encoding stopped.");
    first.Release();
    second.Release();
    last.Release();
  }

  private static void CompletingQueueWakesItsConsumerAndRejectsNewFrames()
  {
    var pool = new CameraFramePool(24, 1);
    using var queue = new CameraFrameQueue(1);
    using var started = new ManualResetEventSlim();
    Task<CameraFrame> waiting = Task.Run(() =>
    {
      started.Set();
      return queue.Take();
    });
    Assert(started.Wait(TimeSpan.FromSeconds(2)), "Frame consumer did not start.");
    queue.Complete();
    Assert(
      waiting.Wait(TimeSpan.FromSeconds(2)) && waiting.Result == null,
      "Completing capture left its consumer asleep."
    );
    CameraFrame late = pool.TryRent();
    Assert(!queue.Submit(late), "A completed recording accepted another frame.");
    late.Release();
    AssertAllBuffersAvailable(pool, 1);
  }

  private static void PreviewNeverWaitsForTheSharedDisplayBuffer()
  {
    using var preview = new CameraPreviewBuffer(4, 4);
    using var worker = new CameraPreviewWorker(preview, new CameraFrameSize(4, 4));
    var pool = new CameraFramePool(24, 1);
    CameraFrame frame = pool.TryRent();
    object gate = typeof(CameraPreviewBuffer)
      .GetField("_gate", BindingFlags.Instance | BindingFlags.NonPublic)
      .GetValue(preview);
    using var started = new ManualResetEventSlim();
    Task submit;
    bool returned;
    lock (gate)
    {
      submit = Task.Run(() =>
      {
        started.Set();
        worker.Submit(frame);
      });
      Assert(started.Wait(TimeSpan.FromSeconds(2)), "Preview submit worker did not start.");
      returned = submit.Wait(TimeSpan.FromMilliseconds(500));
    }
    Assert(submit.Wait(TimeSpan.FromSeconds(2)), "Preview submit did not finish after releasing the display.");
    frame.Release();
    worker.Dispose();
    WaitForPreviewWorker(worker);
    Assert(returned, "A busy display buffer blocked camera capture through preview submission.");
    AssertAllBuffersAvailable(pool, 1);
  }

  private static void PreviewDisposalReleasesItsFrames()
  {
    using var preview = new CameraPreviewBuffer(4, 4);
    using var worker = new CameraPreviewWorker(preview, new CameraFrameSize(4, 4));
    var pool = new CameraFramePool(24, 3);
    for (int index = 0; index < 100; index++)
    {
      CameraFrame frame = pool.TryRent();
      Assert(frame != null, "Preview retained more than its bounded frame allowance.");
      Array.Fill(frame.Pixels, (byte)128);
      worker.Submit(frame);
      frame.Release();
    }
    worker.Dispose();
    WaitForPreviewWorker(worker);
    AssertAllBuffersAvailable(pool, 3);
  }

  private static void WaitForPreviewWorker(CameraPreviewWorker worker)
  {
    var task = (Task)
      typeof(CameraPreviewWorker).GetField("_worker", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(worker);
    Assert(task.Wait(TimeSpan.FromSeconds(2)), "Disposed preview retained an active conversion worker.");
  }

  private static void AssertAllBuffersAvailable(CameraFramePool pool, int capacity)
  {
    var frames = new List<CameraFrame>();
    for (int index = 0; index < capacity; index++)
    {
      CameraFrame frame = pool.TryRent();
      Assert(frame != null, "A consumer leaked a camera frame.");
      frames.Add(frame);
    }
    Assert(pool.TryRent() == null, "The camera frame pool exceeded its memory bound.");
    foreach (CameraFrame frame in frames)
      frame.Release();
  }
}
