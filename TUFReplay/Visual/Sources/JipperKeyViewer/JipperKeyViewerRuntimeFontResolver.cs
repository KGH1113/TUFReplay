using System;
using System.Collections;
using System.Reflection;
using System.Threading.Tasks;
using TUFReplay.Shared.Unity;

namespace TUFReplay.Visual.Sources.JipperKeyViewer;

/// <summary>Reads the font list built by the installed Jipper KeyViewer, without touching Unity objects off-thread.</summary>
internal static class JipperKeyViewerRuntimeFontResolver
{
  private const string ViewerTypeName = "JipperKeyViewer.KeyViewer.KeyViewer";

  public static string Resolve(int index)
  {
    if (index < 0)
      return null;
    Type viewer = null;
    foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
    {
      viewer = assembly.GetType(ViewerTypeName, throwOnError: false);
      if (viewer != null)
        break;
    }
    if (viewer == null)
      return null;

    var completed = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
    UnityMainThread.Post(() =>
    {
      string name = null;
      try
      {
        var list = viewer.GetField("fontList", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) as IList;
        if (list != null && index < list.Count)
        {
          object entry = list[index];
          name =
            entry?.GetType().GetField("name", BindingFlags.Instance | BindingFlags.Public)?.GetValue(entry) as string;
        }
      }
      catch (Exception)
      {
        // A different mod version or a changing font list can still be handled by manual attachment.
      }
      finally
      {
        completed.TrySetResult(name);
      }
    });
    return completed.Task.Wait(TimeSpan.FromSeconds(2)) && !string.IsNullOrWhiteSpace(completed.Task.Result)
      ? completed.Task.Result
      : null;
  }
}
