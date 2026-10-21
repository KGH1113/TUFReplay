using System;
using System.Threading;
using System.Threading.Tasks;

namespace TUFReplay.Shared.Threading;

public sealed class SerialBackgroundQueue
{
  private readonly object _gate = new object();
  private readonly Action<Exception> _onError;
  private Task _tail = Task.CompletedTask;

  public SerialBackgroundQueue(Action<Exception> onError = null)
  {
    _onError = onError;
  }

  public Task Completion
  {
    get
    {
      lock (_gate)
        return _tail;
    }
  }

  public Task Enqueue(Action action)
  {
    if (action == null)
      throw new ArgumentNullException(nameof(action));
    lock (_gate)
    {
      // Always schedule on a worker, including when the previous operation has
      // already completed. A fast retry must never run disk work inline in Unity.
      _tail = _tail.ContinueWith(
        previous =>
        {
          _ = previous.Exception;
          try
          {
            action();
          }
          catch (Exception exception)
          {
            if (_onError == null)
              throw;
            _onError(exception);
          }
        },
        CancellationToken.None,
        TaskContinuationOptions.None,
        TaskScheduler.Default
      );
      return _tail;
    }
  }
}
