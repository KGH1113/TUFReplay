using System;
using System.Runtime.InteropServices;

namespace TUFReplay.Shared.Native;

// Loads a native library by full path and binds C ABI exports as delegates.
// Windows uses LoadLibraryW; other platforms (the C# tests) use dlopen.
internal sealed class NativeModule
{
  private const int RtldNow = 2;
  private readonly IntPtr _handle;
  private readonly bool _windows;

  private NativeModule(IntPtr handle, bool windows)
  {
    _handle = handle;
    _windows = windows;
  }

  public static NativeModule Open(string path)
  {
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
      IntPtr module = LoadLibraryW(path);
      if (module == IntPtr.Zero)
        throw new DllNotFoundException(path + ": LoadLibrary failed. error=" + Marshal.GetLastWin32Error());
      return new NativeModule(module, true);
    }

    IntPtr handle = dlopen(path, RtldNow);
    if (handle == IntPtr.Zero)
    {
      IntPtr error = dlerror();
      throw new DllNotFoundException(
        path + ": " + (error == IntPtr.Zero ? "dlopen failed" : Marshal.PtrToStringAnsi(error))
      );
    }
    return new NativeModule(handle, false);
  }

  public T Bind<T>(string name)
    where T : class
  {
    IntPtr symbol = _windows ? GetProcAddress(_handle, name) : dlsym(_handle, name);
    if (symbol == IntPtr.Zero)
      throw new EntryPointNotFoundException(name);
    return (T)(object)Marshal.GetDelegateForFunctionPointer(symbol, typeof(T));
  }

  [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
  private static extern IntPtr LoadLibraryW(string path);

  [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
  private static extern IntPtr GetProcAddress(IntPtr module, string name);

  [DllImport("libdl")]
  private static extern IntPtr dlopen(string path, int mode);

  [DllImport("libdl")]
  private static extern IntPtr dlsym(IntPtr handle, string symbol);

  [DllImport("libdl")]
  private static extern IntPtr dlerror();
}
