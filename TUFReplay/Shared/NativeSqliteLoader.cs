using System;
using System.IO;
using System.Runtime.InteropServices;
using SQLitePCL;

namespace TUFReplay.Shared;

public static class NativeSqliteLoader
{
  private static bool _initialized;

  public static void Initialize()
  {
    if (_initialized) return;

    string path = GetNativeLibraryPath();
    IGetFunctionPointer functionPointer = CreateFunctionPointerProvider(path);

    SQLite3Provider_dynamic_cdecl.Setup(path, functionPointer);
    raw.SetProvider(new SQLite3Provider_dynamic_cdecl());
    _initialized = true;
  }

  private static IGetFunctionPointer CreateFunctionPointerProvider(string libraryPath)
  {
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return new WindowsFunctionPointer(libraryPath);
    return new DlOpenFunctionPointer(libraryPath);
  }

  private static string GetNativeLibraryPath()
  {
    string configuredPath = Environment.GetEnvironmentVariable("TUFREPLAY_SQLITE_NATIVE_LIBRARY");
    if (!string.IsNullOrEmpty(configuredPath) && File.Exists(configuredPath)) return configuredPath;

    string bundledPath = Path.Combine(Main.Instance.Path, GetBundledLibraryFileName());
    if (File.Exists(bundledPath)) return bundledPath;

    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "/usr/lib/libsqlite3.dylib";
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return "libsqlite3.so";
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "sqlite3.dll";

    return GetBundledLibraryFileName();
  }

  private static string GetBundledLibraryFileName()
  {
    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
      return "libe_sqlite3.dylib";
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      return "e_sqlite3.dll";

    return "libe_sqlite3.so";
  }

  private sealed class WindowsFunctionPointer : IGetFunctionPointer
  {
    private readonly IntPtr _handle;

    public WindowsFunctionPointer(string libraryPath)
    {
      _handle = LoadLibrary(libraryPath);
      if (_handle == IntPtr.Zero)
      {
        int error = Marshal.GetLastWin32Error();
        throw new DllNotFoundException(libraryPath + ": LoadLibrary failed with Win32 error " + error);
      }
    }

    public IntPtr GetFunctionPointer(string name)
    {
      IntPtr pointer = GetProcAddress(_handle, name);
      if (pointer == IntPtr.Zero)
      {
        int error = Marshal.GetLastWin32Error();
        throw new EntryPointNotFoundException(name + "GetProcAddress failed with Win32 error " + error);
      }

      return pointer;
    }

    [DllImport("kernel32.dll", EntryPoint = "LoadLibraryW", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("kernel32.dll", EntryPoint = "GetProcAddress", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
  }

  private sealed class DlOpenFunctionPointer : IGetFunctionPointer
  {
    private const int RtldNow = 2;
    private readonly IntPtr _handle;

    public DlOpenFunctionPointer(string libraryPath)
    {
      _handle = dlopen(libraryPath, RtldNow);
      if (_handle == IntPtr.Zero)
      {
        IntPtr error = dlerror();
        string message = error == IntPtr.Zero ? "unknown dlopen error" : Marshal.PtrToStringAnsi(error);
        throw new DllNotFoundException(libraryPath + ": " + message);
      }
    }

    public IntPtr GetFunctionPointer(string name)
    {
      return dlsym(_handle, name);
    }

    [DllImport("libdl")]
    private static extern IntPtr dlopen(string path, int mode);

    [DllImport("libdl")]
    private static extern IntPtr dlsym(IntPtr handle, string symbol);

    [DllImport("libdl")]
    private static extern IntPtr dlerror();
  }
}
