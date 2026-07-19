using System;
using System.IO;
using System.Security.Cryptography;

namespace TUFReplay.Infrastructure.Adofai;

public static class AdofaiLevelFileHash
{
  public const int Size = 32;

  public static bool TryCompute(string levelPath, out byte[] hash)
  {
    hash = null;
    try
    {
      if (string.IsNullOrWhiteSpace(levelPath) || !File.Exists(levelPath))
        return false;

      using FileStream stream = new FileStream(levelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
      using SHA256 sha256 = SHA256.Create();
      hash = sha256.ComputeHash(stream);
      return hash.Length == Size;
    }
    catch (Exception exception)
      when (exception is IOException || exception is UnauthorizedAccessException || exception is NotSupportedException)
    {
      return false;
    }
  }

  public static bool Equals(byte[] left, byte[] right)
  {
    if (ReferenceEquals(left, right))
      return true;
    if (left == null || right == null || left.Length != right.Length)
      return false;

    int difference = 0;
    for (int index = 0; index < left.Length; index++)
      difference |= left[index] ^ right[index];
    return difference == 0;
  }
}
