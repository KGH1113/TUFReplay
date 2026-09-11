using System;
using System.Runtime.InteropServices;

namespace TUFReplay.Submission.Auth;

public interface ICredentialStore
{
  string Load();
  void Save(string token);
  void Delete();
}

public static class CredentialStore
{
  public static ICredentialStore Create(string account)
  {
    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return new MacCredentialStore(account);
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return new WindowsCredentialStore(account);
    throw new PlatformNotSupportedException("secure_credentials_unavailable");
  }
}
