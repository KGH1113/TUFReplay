using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using TUFReplay.Visual.Importing.Abstractions;

namespace TUFReplay.Visual.Infrastructure.FileSystem;

public sealed class PhysicalVisualFileSystem : IVisualFileSystem
{
  public bool FileExists(string path) => IsSafePath(path) && File.Exists(path);

  public long FileLength(string path)
  {
    EnsureSafePath(path);
    return new FileInfo(path).Length;
  }

  public string ReadAllText(string path)
  {
    EnsureSafePath(path);
    return File.ReadAllText(path);
  }

  public byte[] ReadAllBytes(string path)
  {
    EnsureSafePath(path);
    return File.ReadAllBytes(path);
  }

  public IEnumerable<string> EnumerateFiles(string directory, string searchPattern)
  {
    if (!IsSafePath(directory) || !Directory.Exists(directory))
      return new string[0];
    return EnumerateSafeFiles(directory, searchPattern);
  }

  private static IEnumerable<string> EnumerateSafeFiles(string directory, string searchPattern)
  {
    IEnumerable<string> files;
    try
    {
      files = Directory.EnumerateFiles(directory, searchPattern);
    }
    catch (IOException)
    {
      yield break;
    }
    catch (UnauthorizedAccessException)
    {
      yield break;
    }

    var safeFiles = new List<string>();
    try
    {
      foreach (string file in files)
        if (IsSafePath(file))
          safeFiles.Add(file);
    }
    catch (IOException) { }
    catch (UnauthorizedAccessException) { }

    foreach (string file in safeFiles)
      yield return file;
  }

  private static void EnsureSafePath(string path)
  {
    if (!IsSafePath(path))
      throw new IOException("Visual source paths may not contain symbolic links or reparse points.");
  }

  private static bool IsSafePath(string path)
  {
    if (string.IsNullOrWhiteSpace(path))
      return false;

    string fullPath;
    try
    {
      fullPath = Path.GetFullPath(path);
    }
    catch (Exception exception)
      when (exception is ArgumentException || exception is NotSupportedException || exception is PathTooLongException)
    {
      return false;
    }

    string root = Path.GetPathRoot(fullPath);
    if (string.IsNullOrEmpty(root))
      return false;

    string current = root;
    string remainder = fullPath.Substring(root.Length);
    char[] separators = { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
    foreach (string segment in remainder.Split(separators, StringSplitOptions.RemoveEmptyEntries))
    {
      current = Path.Combine(current, segment);
      // Allow operating-system aliases such as macOS /var -> /private/var;
      // adapters enforce canonical containment against the discovered source
      // root for every path. A reparse point at the requested leaf is still
      // rejected here so a linked config or asset cannot be read directly.
      if (string.Equals(current, fullPath, StringComparison.OrdinalIgnoreCase) && IsReparsePoint(current))
        return false;

      // A missing suffix is safe to inspect. The caller will report it as a
      // missing source or asset; existing ancestors have already been checked.
      if (!ExistsAsFileOrDirectory(current))
        break;
    }
    return true;
  }

  private static bool ExistsAsFileOrDirectory(string path)
  {
    try
    {
      return File.Exists(path) || Directory.Exists(path);
    }
    catch (IOException)
    {
      return false;
    }
    catch (UnauthorizedAccessException)
    {
      return false;
    }
  }

  private static bool IsReparsePoint(string path)
  {
    try
    {
      FileAttributes attributes = File.GetAttributes(path);
      if ((attributes & FileAttributes.ReparsePoint) != 0)
        return true;
    }
    catch (FileNotFoundException)
    {
      return false;
    }
    catch (DirectoryNotFoundException)
    {
      return false;
    }
    catch (IOException)
    {
      return true;
    }
    catch (UnauthorizedAccessException)
    {
      return true;
    }

    // File.GetAttributes follows symbolic links on some Unix runtimes. The
    // readlink probe covers that case while keeping Windows reparse handling
    // above. Failure to invoke readlink is treated as a normal non-link.
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      return false;
    try
    {
      var buffer = new byte[1];
      return readlink(path, buffer, (ulong)buffer.Length) >= 0;
    }
    catch (DllNotFoundException)
    {
      return false;
    }
    catch (EntryPointNotFoundException)
    {
      return false;
    }
    catch (MarshalDirectiveException)
    {
      return false;
    }
  }

  [DllImport("libc", EntryPoint = "readlink", SetLastError = true)]
  private static extern long readlink(string path, byte[] buffer, ulong bufferSize);
}
