using System.Runtime.InteropServices;
namespace CatTransform.App;
internal sealed class NativeTray : IDisposable {
 private readonly nint hwnd;private readonly SubclassProc callback;
 private NotifyIconData data;
 public event Action<int>? Command;
 public NativeTray(nint hwnd){this.hwnd=hwnd;callback=WindowProc;
  if(!SetWindowSubclass(hwnd,callback,1,0))throw new InvalidOperationException("无法连接托盘窗口");
  data=new(){Size=(uint)Marshal.SizeOf<NotifyIconData>(),Window=hwnd,Id=1,Flags=1|2|4,CallbackMessage=0x8001,Icon=LoadIcon(0,(nint)32512),Tip="catTransform",Info="",InfoTitle=""};
  RefreshIcon(false);
  if(!Shell_NotifyIcon(0,ref data))throw new InvalidOperationException("无法创建托盘图标");
 }
 public bool SetHotkey(uint modifiers,uint key){UnregisterHotKey(hwnd,1);return RegisterHotKey(hwnd,1,modifiers|0x4000,key);}
 public void Notify(){data.Flags|=16;data.InfoTitle="catTransform 仍在运行";data.Info="可从托盘打开主窗口，或选择退出 catTransform 完全退出。";data.InfoFlags=1;Shell_NotifyIcon(1,ref data);data.Flags&=~16u;}
 private nint WindowProc(nint h,uint msg,nuint w,nint l,nuint id,nuint reference){
  if(msg==0x0312){Command?.Invoke(3);return 0;}
  if(msg==0x8001){uint mouse=(uint)((long)l&0xffff);if(mouse==0x0203)Command?.Invoke(1);if(mouse==0x0205){
   var menu=CreatePopupMenu();try{AppendMenu(menu,0,1,"打开主窗口");AppendMenu(menu,0,2,"打开剪贴板");AppendMenu(menu,0,3,"暂停 / 恢复监听");AppendMenu(menu,0,4,"设置");AppendMenu(menu,0,5,"退出 catTransform");GetCursorPos(out var p);SetForegroundWindow(hwnd);int command=TrackPopupMenu(menu,0x100|2,p.X,p.Y,0,hwnd,0);if(command!=0)Command?.Invoke(command);}finally{DestroyMenu(menu);}
  }return 0;}
  var result=DefSubclassProc(h,msg,w,l);
  if(msg==0x02E0||msg==0x001A||msg==0x007E)RefreshIcon(true);
  return result;
 }
 public void Show(){ShowWindow(hwnd,9);SetForegroundWindow(hwnd);}
 public void Hide()=>ShowWindow(hwnd,0);
 private bool customIcon;
 private void RefreshIcon(bool notify){
  var taskbar=FindWindow("Shell_TrayWnd",null);
  uint dpi=WindowDpi.GetDpiForWindow(taskbar!=0?taskbar:hwnd);
  int size=GetSystemMetricsForDpi(49,dpi==0?96:dpi);
  var icon=LoadImage(0,Path.Combine(AppContext.BaseDirectory,"Assets","catTransform.ico"),1,size,size,0x10);
  if(icon==0)return;
  var previous=data.Icon;bool owned=customIcon;data.Icon=icon;customIcon=true;
  if(notify)Shell_NotifyIcon(1,ref data);
  if(owned)DestroyIcon(previous);
 }
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern nint FindWindow(string className,string? title);
 [DllImport("user32.dll")]private static extern int GetSystemMetricsForDpi(int index,uint dpi);
 public void Dispose(){UnregisterHotKey(hwnd,1);Shell_NotifyIcon(2,ref data);RemoveWindowSubclass(hwnd,callback,1);if(customIcon)DestroyIcon(data.Icon);}
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern nint LoadImage(nint instance,string name,uint type,int width,int height,uint flags);
 [DllImport("user32.dll")]private static extern bool DestroyIcon(nint icon);
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]private struct NotifyIconData {public uint Size;public nint Window;public uint Id,Flags,CallbackMessage;public nint Icon;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string Tip;public uint State,StateMask;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)]public string Info;public uint Timeout;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=64)]public string InfoTitle;public uint InfoFlags;public Guid Guid;public nint BalloonIcon;}
 [StructLayout(LayoutKind.Sequential)]private struct Point {public int X,Y;}
 private delegate nint SubclassProc(nint hwnd,uint msg,nuint w,nint l,nuint id,nuint reference);
 [DllImport("comctl32.dll")]private static extern bool SetWindowSubclass(nint h,SubclassProc p,nuint id,nuint data);
 [DllImport("comctl32.dll")]private static extern bool RemoveWindowSubclass(nint h,SubclassProc p,nuint id);
 [DllImport("comctl32.dll")]private static extern nint DefSubclassProc(nint h,uint msg,nuint w,nint l);
 [DllImport("shell32.dll",CharSet=CharSet.Unicode)]private static extern bool Shell_NotifyIcon(uint message,ref NotifyIconData data);
 [DllImport("user32.dll")]private static extern nint LoadIcon(nint instance,nint name);
 [DllImport("user32.dll")]private static extern bool RegisterHotKey(nint h,int id,uint modifiers,uint key);
 [DllImport("user32.dll")]private static extern bool UnregisterHotKey(nint h,int id);
 [DllImport("user32.dll")]private static extern nint CreatePopupMenu();
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern bool AppendMenu(nint menu,uint flags,nuint id,string label);
 [DllImport("user32.dll")]private static extern int TrackPopupMenu(nint menu,uint flags,int x,int y,int reserved,nint h,nint rect);
 [DllImport("user32.dll")]private static extern bool GetCursorPos(out Point p);
 [DllImport("user32.dll")]private static extern bool SetForegroundWindow(nint h);
 [DllImport("user32.dll")]private static extern bool ShowWindow(nint h,int command);
 [DllImport("user32.dll")]private static extern bool DestroyMenu(nint menu);
}
