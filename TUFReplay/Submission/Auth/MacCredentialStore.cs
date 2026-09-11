using System;
using System.Runtime.InteropServices;
using System.Text;

namespace TUFReplay.Submission.Auth;

/// <summary>Keychain access runs off the Unity thread. No secrets in process arguments or files.</summary>
public sealed class MacCredentialStore : ICredentialStore
{
  private const string Security = "/System/Library/Frameworks/Security.framework/Security";
  private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
  private readonly byte[] _service = Encoding.UTF8.GetBytes("dev.impl1113.TUFReplay.OAuth");
  private readonly byte[] _account;

  public MacCredentialStore(string account) => _account = Encoding.UTF8.GetBytes(account);

  public string Load()
  {
    int status = Find(out uint length, out IntPtr data, out IntPtr item);
    if (status == -25300) return null;
    Check(status);
    try
    {
      if (length > 4096) throw new InvalidOperationException("invalid_stored_credential");
      var bytes = new byte[length];
      Marshal.Copy(data, bytes, 0, bytes.Length);
      return Encoding.UTF8.GetString(bytes);
    }
    finally { SecKeychainItemFreeContent(IntPtr.Zero, data); CFRelease(item); }
  }

  public void Save(string token)
  {
    byte[] bytes = Encoding.UTF8.GetBytes(token);
    int status = Find(out _, out IntPtr data, out IntPtr item);
    if (status == -25300)
    {
      Check(SecKeychainAddGenericPassword(IntPtr.Zero, (uint)_service.Length, _service,
        (uint)_account.Length, _account, (uint)bytes.Length, bytes, out item));
      CFRelease(item);
      return;
    }
    Check(status);
    try { Check(SecKeychainItemModifyAttributesAndData(item, IntPtr.Zero, (uint)bytes.Length, bytes)); }
    finally { SecKeychainItemFreeContent(IntPtr.Zero, data); CFRelease(item); }
  }

  public void Delete()
  {
    int status = Find(out _, out IntPtr data, out IntPtr item);
    if (status == -25300) return;
    Check(status);
    try { Check(SecKeychainItemDelete(item)); }
    finally { SecKeychainItemFreeContent(IntPtr.Zero, data); CFRelease(item); }
  }

  private int Find(out uint length, out IntPtr data, out IntPtr item) =>
    SecKeychainFindGenericPassword(IntPtr.Zero, (uint)_service.Length, _service,
      (uint)_account.Length, _account, out length, out data, out item);
  private static void Check(int status)
  {
    if (status != 0) throw new InvalidOperationException("secure_credentials_unavailable");
  }

  [DllImport(Security)] private static extern int SecKeychainFindGenericPassword(IntPtr keychain,
    uint serviceLength, byte[] service, uint accountLength, byte[] account,
    out uint passwordLength, out IntPtr password, out IntPtr item);
  [DllImport(Security)] private static extern int SecKeychainAddGenericPassword(IntPtr keychain,
    uint serviceLength, byte[] service, uint accountLength, byte[] account,
    uint passwordLength, byte[] password, out IntPtr item);
  [DllImport(Security)] private static extern int SecKeychainItemModifyAttributesAndData(IntPtr item,
    IntPtr attributes, uint length, byte[] data);
  [DllImport(Security)] private static extern int SecKeychainItemDelete(IntPtr item);
  [DllImport(Security)] private static extern int SecKeychainItemFreeContent(IntPtr attributes, IntPtr data);
  [DllImport(CoreFoundation)] private static extern void CFRelease(IntPtr value);
}
