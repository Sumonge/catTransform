using CatTransform.Infrastructure;
using Microsoft.Win32;
var executable=Path.GetFullPath(args[0]);if(!File.Exists(executable))throw new FileNotFoundException(executable);
using var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
var old=key.GetValue("catTransform");var kind=old is null?RegistryValueKind.String:key.GetValueKind("catTransform");var startup=new StartupService();
try{
 startup.Set(true,false,executable);if(!startup.IsEnabled()||(string?)key.GetValue("catTransform")!="\""+executable+"\"")throw new Exception("Visible startup mismatch");
 startup.Set(true,true,executable);if(!(key.GetValue("catTransform") as string)!.EndsWith(" --tray"))throw new Exception("Silent startup mismatch");
 startup.Set(false,false,executable);if(startup.IsEnabled())throw new Exception("Disable failed");Console.WriteLine("PASS: current-user visible / silent / disabled startup; previous registry value restored in finally.");
}finally{if(old is null)key.DeleteValue("catTransform",false);else key.SetValue("catTransform",old,kind);}
