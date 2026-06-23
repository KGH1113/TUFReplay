using System;
using System.Collections.Generic;
using UnityEngine;

namespace TUFReplay.Shared;

public static class UnityMainThread
{
  private static readonly Queue<Action> Actions = new Queue<Action>();
  private static readonly object Lock = new object();
  private static bool _initialized;

  public static void Initialize()
  {
    if (_initialized) return;

    GameObject gameObject = new GameObject("TUFReplay UnityMainThread");
    UnityEngine.Object.DontDestroyOnLoad(gameObject);
    gameObject.AddComponent<UnityMainThreadRunner>();

    _initialized = true;
  }

  public static void Post(Action action)
  {
    if (action == null) return;

    lock (Lock)
    {
      Actions.Enqueue(action);
    }
  }

  private class UnityMainThreadRunner : MonoBehaviour
  {
    private void Update()
    {
      while (true)
      {
        Action action;

        lock (Lock)
        {
          if (Actions.Count == 0) return;
          action = Actions.Dequeue();
        }

        try
        {
          action();
        }
        catch (Exception e)
        {
          Main.Instance?.LogException(e);
        }
      }
    }
  }
}
