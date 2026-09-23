using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace TUFReplay.Visual.Importing.Security;

internal static class VisualPathPolicy
{
  public static bool IsContained(string root, string path)
  {
    if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
      return false;
    string canonicalRoot = Canonical(root);
    string canonicalPath = Canonical(path);
    if (string.IsNullOrWhiteSpace(canonicalRoot) || string.IsNullOrWhiteSpace(canonicalPath))
      return IsLexicallyContained(root, path);

    canonicalRoot = TrimSeparator(canonicalRoot);
    canonicalPath = TrimSeparator(canonicalPath);
    return string.Equals(canonicalRoot, canonicalPath, StringComparison.OrdinalIgnoreCase)
      || canonicalPath.StartsWith(canonicalRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
      || canonicalPath.StartsWith(canonicalRoot + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
  }

  public static bool IsTrustedRoot(string root)
  {
    if (string.IsNullOrWhiteSpace(root))
      return false;
    try
    {
      string fullPath = Path.GetFullPath(root);
      FileAttributes attributes = File.GetAttributes(fullPath);
      if ((attributes & FileAttributes.ReparsePoint) != 0)
        return false;
      if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        return true;
      var buffer = new byte[1];
      return readlink(fullPath, buffer, (ulong)buffer.Length) < 0;
    }
    catch (Exception exception)
      when (exception is ArgumentException || exception is IOException || exception is NotSupportedException)
    {
      return false;
    }
    catch (UnauthorizedAccessException)
    {
      return false;
    }
    catch (DllNotFoundException)
    {
      return true;
    }
    catch (EntryPointNotFoundException)
    {
      return true;
    }
  }

  public static string Canonical(string path)
  {
    if (string.IsNullOrWhiteSpace(path))
      return null;
    try
    {
      string fullPath = Path.GetFullPath(path);
      if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        return fullPath;
      var buffer = new StringBuilder(4096);
      return realpath(fullPath, buffer) == IntPtr.Zero ? fullPath : buffer.ToString();
    }
    catch (Exception exception)
      when (exception is ArgumentException || exception is IOException || exception is NotSupportedException)
    {
      return null;
    }
    catch (DllNotFoundException)
    {
      return Path.GetFullPath(path);
    }
    catch (EntryPointNotFoundException)
    {
      return Path.GetFullPath(path);
    }
  }

  private static bool IsLexicallyContained(string root, string path)
  {
    try
    {
      string fullRoot = TrimSeparator(Path.GetFullPath(root));
      string fullPath = TrimSeparator(Path.GetFullPath(path));
      return string.Equals(fullRoot, fullPath, StringComparison.OrdinalIgnoreCase)
        || fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
        || fullPath.StartsWith(fullRoot + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
    catch (Exception exception)
      when (exception is ArgumentException || exception is IOException || exception is NotSupportedException)
    {
      return false;
    }
  }

  private static string TrimSeparator(string value)
  {
    string root = Path.GetPathRoot(value);
    int minimum = string.IsNullOrEmpty(root) ? 0 : root.Length;
    return value.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length < minimum
      ? root
      : value.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
  }

  [DllImport("libc", EntryPoint = "realpath", CharSet = CharSet.Ansi, SetLastError = true)]
  private static extern IntPtr realpath(string path, StringBuilder resolvedPath);

  [DllImport("libc", EntryPoint = "readlink", SetLastError = true)]
  private static extern long readlink(string path, byte[] buffer, ulong bufferSize);
}
