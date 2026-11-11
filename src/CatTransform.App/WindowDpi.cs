using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
namespace CatTransform.App;

// The manifest is the sole DPI-awareness authority. WinUI owns WM_DPICHANGED.
internal sealed class WindowDpi : IDisposable {
 private readonly nint hwnd;
 private readonly AppWindow window;
 private readonly SubclassProc callback;
 private readonly Microsoft.UI.Dispatching.DispatcherQueue dispatcher=Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
 private bool disposed;
 internal WindowDpi(nint hwnd,AppWindow window){
  this.hwnd=hwnd;this.window=window;callback=WindowProc;
  if(!SetWindowSubclass(hwnd,callback,2,0))throw new InvalidOperationException("Cannot install window sizing handler.");
 }
 internal void InitializeSize(){
  var area=DisplayArea.GetFromWindowId(window.Id,DisplayAreaFallback.Primary).WorkArea;
  var size=WindowSizing.Fit(1440,960,GetDpiForWindow(hwnd),area.Width,area.Height);
  window.MoveAndResize(new(area.X+(area.Width-size.Width)/2,area.Y+(area.Height-size.Height)/2,size.Width,size.Height));
 }
 private nint WindowProc(nint h,uint message,nuint w,nint l,nuint id,nuint data){
  // Always forward DPI changes, including their suggested rectangle, to WinUI.
  // WM_GETMINMAXINFO expresses resize constraints without resize-event loops.
  var result=DefSubclassProc(h,message,w,l);
  if(message==0x0024){
   var area=DisplayArea.GetFromWindowId(window.Id,DisplayAreaFallback.Primary).WorkArea;
   var minimum=WindowSizing.Fit(1100,720,GetDpiForWindow(h),area.Width,area.Height);
   var info=Marshal.PtrToStructure<MinMaxInfo>(l);
   info.MinTrackSize=new(){X=minimum.Width,Y=minimum.Height};
   Marshal.StructureToPtr(info,l,false);
  }
  if(message==0x02E0||message==0x007E)dispatcher.TryEnqueue(ClampToWorkArea);
  return result;
 }
 private void ClampToWorkArea(){
  if(disposed||IsIconic(hwnd)||IsZoomed(hwnd))return;
  // WinUI has already consumed the suggested rectangle. Only constrain bounds;
  // never multiply its new size by the DPI ratio a second time.
  var area=DisplayArea.GetFromWindowId(window.Id,DisplayAreaFallback.Primary).WorkArea;
  var size=window.Size;var position=window.Position;
  int width=Math.Min(size.Width,area.Width),height=Math.Min(size.Height,area.Height);
  int x=Math.Clamp(position.X,area.X,area.X+area.Width-width),y=Math.Clamp(position.Y,area.Y,area.Y+area.Height-height);
  if(width!=size.Width||height!=size.Height||x!=position.X||y!=position.Y)window.MoveAndResize(new(x,y,width,height));
 }
 public void Dispose(){disposed=true;RemoveWindowSubclass(hwnd,callback,2);}
 [DllImport("user32.dll")]private static extern bool IsIconic(nint hwnd);
 [DllImport("user32.dll")]private static extern bool IsZoomed(nint hwnd);
 [StructLayout(LayoutKind.Sequential)]private struct Point{public int X,Y;}
 [StructLayout(LayoutKind.Sequential)]private struct MinMaxInfo{public Point Reserved,MaxSize,MaxPosition,MinTrackSize,MaxTrackSize;}
 private delegate nint SubclassProc(nint h,uint msg,nuint w,nint l,nuint id,nuint data);
 [DllImport("user32.dll")]internal static extern uint GetDpiForWindow(nint hwnd);
 [DllImport("comctl32.dll")]private static extern bool SetWindowSubclass(nint h,SubclassProc p,nuint id,nuint data);
 [DllImport("comctl32.dll")]private static extern bool RemoveWindowSubclass(nint h,SubclassProc p,nuint id);
 [DllImport("comctl32.dll")]private static extern nint DefSubclassProc(nint h,uint msg,nuint w,nint l);
}
