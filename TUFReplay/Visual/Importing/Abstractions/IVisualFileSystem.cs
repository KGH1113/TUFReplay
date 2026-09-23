using System.Collections.Generic;

namespace TUFReplay.Visual.Importing.Abstractions;

public interface IVisualFileSystem
{
  bool FileExists(string path);

  long FileLength(string path);

  string ReadAllText(string path);

  byte[] ReadAllBytes(string path);

  IEnumerable<string> EnumerateFiles(string directory, string searchPattern);
}
