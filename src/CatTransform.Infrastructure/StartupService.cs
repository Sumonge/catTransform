using Microsoft.Win32;
using System.Runtime.Versioning;
namespace CatTransform.Infrastructure;
[SupportedOSPlatform("windows")]
public sealed class StartupService {
 private const string Key=@"Software\Microsoft\Windows\CurrentVersion\Run";
 public bool IsEnabled(){using var key=Registry.CurrentUser.OpenSubKey(Key);return key?.GetValue("catTransform") is string;}
 public void Set(bool enabled,bool silent,string executable){using var key=Registry.CurrentUser.CreateSubKey(Key);if(enabled)key.SetValue("catTransform","\""+Path.GetFullPath(executable)+"\""+(silent?" --tray":""));else key.DeleteValue("catTransform",false);}
}
