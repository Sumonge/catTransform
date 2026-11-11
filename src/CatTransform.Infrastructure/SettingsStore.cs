using System.Runtime.InteropServices;
using System.Text.Json;
using CatTransform.Core;
namespace CatTransform.Infrastructure;
public static class UserPaths {
 public static string Data=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"catTransform");
 public static string DefaultOutput {
  get{if(!OperatingSystem.IsWindows())return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"catTransform","Output");
   var id=new Guid("FDD39AD0-238F-46AF-ADB4-6C85480369C7");Marshal.ThrowExceptionForHR(SHGetKnownFolderPath(ref id,0,0,out var ptr));try{return Path.Combine(Marshal.PtrToStringUni(ptr)!,"catTransform","Output");}finally{Marshal.FreeCoTaskMem(ptr);}}
 }
 [DllImport("shell32.dll")]private static extern int SHGetKnownFolderPath(ref Guid id,uint flags,nint token,out nint path);
}
public sealed class SettingsStore {
 private readonly string path;
 public SettingsStore(string? directory=null){var dir=directory??UserPaths.Data;Directory.CreateDirectory(dir);path=Path.Combine(dir,"settings.json");}
 public Settings Load(){if(!File.Exists(path))return Defaults();return JsonSerializer.Deserialize<Settings>(File.ReadAllText(path))??throw new InvalidDataException("设置文件为空");}
 public static Settings Defaults()=>new(){Conversion=new(){OutputDirectory=UserPaths.DefaultOutput}};
 public void Save(Settings settings){var temp=path+"."+Guid.NewGuid()+".tmp";try{File.WriteAllText(temp,JsonSerializer.Serialize(settings,new JsonSerializerOptions{WriteIndented=true}));if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}}
}
