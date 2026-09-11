using System;
using System.Runtime.InteropServices;
using System.Text;

namespace TUFReplay.Submission.Auth;

public sealed class WindowsCredentialStore : ICredentialStore
{
  private readonly string _target;
  public WindowsCredentialStore(string account) => _target = "TUFReplay:OAuth:" + account;

  public string Load()
  {
    if (!CredRead(_target, 1, 0, out var pointer))
    {
      if (Marshal.GetLastWin32Error() == 1168) return null;
      throw new InvalidOperationException("secure_credentials_unavailable");
    }
    try
    {
      var credential = Marshal.PtrToStructure<Credential>(pointer);
      if (credential.BlobSize > 4096) throw new InvalidOperationException("invalid_stored_credential");
      var bytes = new byte[credential.BlobSize];
      Marshal.Copy(credential.Blob, bytes, 0, bytes.Length);
      return Encoding.UTF8.GetString(bytes);
    }
    finally { CredFree(pointer); }
  }

  public void Save(string token)
  {
    var bytes = Encoding.UTF8.GetBytes(token);
    var pointer = Marshal.AllocHGlobal(bytes.Length);
    try
    {
      Marshal.Copy(bytes, 0, pointer, bytes.Length);
      var credential = new Credential {
        Type = 1, TargetName = _target, BlobSize = (uint)bytes.Length,
        Blob = pointer, Persist = 2, UserName = "TUFReplay",
      };
      if (!CredWrite(ref credential, 0)) throw new InvalidOperationException("secure_credentials_unavailable");
    }
    finally
    {
      for (int i = 0; i < bytes.Length; i++) Marshal.WriteByte(pointer, i, 0);
      Marshal.FreeHGlobal(pointer);
    }
  }

  public void Delete()
  {
    if (!CredDelete(_target, 1, 0) && Marshal.GetLastWin32Error() != 1168)
      throw new InvalidOperationException("secure_credentials_unavailable");
  }

  [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
  private struct Credential
  {
    public uint Flags, Type;
    public string TargetName, Comment;
    public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
    public uint BlobSize;
    public IntPtr Blob;
    public uint Persist, AttributeCount;
    public IntPtr Attributes;
    public string TargetAlias, UserName;
  }
  [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
  private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);
  [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
  private static extern bool CredWrite(ref Credential credential, uint flags);
  [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
  private static extern bool CredDelete(string target, uint type, uint flags);
  [DllImport("advapi32.dll")] private static extern void CredFree(IntPtr credential);
}
