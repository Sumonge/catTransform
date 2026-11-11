import ctypes as c,json,pathlib,subprocess,sys,time,xml.etree.ElementTree as ET
root=pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0,str(root/'tools/pythonlibs'))
u=c.windll.user32;k=c.windll.kernel32
u.SetThreadDpiAwarenessContext.argtypes=[c.c_void_p];u.SetThreadDpiAwarenessContext.restype=c.c_void_p
u.SetThreadDpiAwarenessContext(c.c_void_p(-4))
u.GetWindowDpiAwarenessContext.argtypes=[c.c_void_p];u.GetWindowDpiAwarenessContext.restype=c.c_void_p
u.AreDpiAwarenessContextsEqual.argtypes=[c.c_void_p,c.c_void_p]
u.GetDpiForWindow.argtypes=[c.c_void_p]
from pywinauto import Desktop
import win32gui,win32ui
from PIL import Image
exe=root/'artifacts/release/catTransform.exe'
# Inspect the executable's actual RT_MANIFEST, not just the source file.
k.LoadLibraryExW.argtypes=[c.c_wchar_p,c.c_void_p,c.c_ulong];k.LoadLibraryExW.restype=c.c_void_p
k.FindResourceW.argtypes=[c.c_void_p,c.c_void_p,c.c_void_p];k.FindResourceW.restype=c.c_void_p
k.LoadResource.argtypes=[c.c_void_p,c.c_void_p];k.LoadResource.restype=c.c_void_p
k.LockResource.argtypes=[c.c_void_p];k.LockResource.restype=c.c_void_p
k.SizeofResource.argtypes=[c.c_void_p,c.c_void_p];k.SizeofResource.restype=c.c_ulong
k.FreeLibrary.argtypes=[c.c_void_p]
module=k.LoadLibraryExW(str(exe),None,2)
try:
 resource=k.FindResourceW(module,c.c_void_p(1),c.c_void_p(24));assert resource
 raw=c.string_at(k.LockResource(k.LoadResource(module,resource)),k.SizeofResource(module,resource))
 manifest=ET.fromstring(raw)
 declarations=manifest.findall('.//{http://schemas.microsoft.com/SMI/2016/WindowsSettings}dpiAwareness')
 assert len(declarations)==1 and declarations[0].text=='PerMonitorV2'
 (root/'artifacts/dpi-embedded.manifest').write_bytes(raw)
finally:k.FreeLibrary(module)
p=subprocess.Popen([str(exe)])
info={'embedded_manifest':'PerMonitorV2','actual_dpi_switch_tested':False,'monitors':[{'monitor':list(__import__('win32api').GetMonitorInfo(m[0])['Monitor']),'work':list(__import__('win32api').GetMonitorInfo(m[0])['Work'])} for m in __import__('win32api').EnumDisplayMonitors()]}
try:
 w=Desktop(backend='uia').window(title='catTransform',process=p.pid);w.wait('visible',timeout=25)
 wrapper=w.wrapper_object();h=wrapper.handle
 context=u.GetWindowDpiAwarenessContext(h);dpi=u.GetDpiForWindow(h)
 info.update(dpi=dpi,scale_percent=round(dpi/96*100),pmv2=bool(u.AreDpiAwarenessContextsEqual(context,c.c_void_p(-4))),rect=win32gui.GetWindowRect(h))
 assert info['pmv2'],info
 (root/'artifacts/release-pid.txt').write_text(str(p.pid))
 # Existing functional UI regression runs against this same live process.
 subprocess.run([sys.executable,str(root/'tools/ui_probe.py')],check=True,timeout=120)
 info['five_page_functional_regression']=True
 def capture(name):
  rect=win32gui.GetWindowRect(h);width=rect[2]-rect[0];height=rect[3]-rect[1]
  hdc=win32gui.GetWindowDC(h);src=win32ui.CreateDCFromHandle(hdc);dst=src.CreateCompatibleDC();bmp=win32ui.CreateBitmap();bmp.CreateCompatibleBitmap(src,width,height);dst.SelectObject(bmp)
  try:
   assert c.windll.user32.PrintWindow(c.c_void_p(h),c.c_void_p(dst.GetSafeHdc()),2)
   Image.frombuffer('RGB',(width,height),bmp.GetBitmapBits(True),'raw','BGRX',0,1).save(str(root/'artifacts'/name))
  finally:
   win32gui.DeleteObject(bmp.GetHandle());dst.DeleteDC();src.DeleteDC();win32gui.ReleaseDC(h,hdc)
 capture('dpi-native-window.png')
 # Read rendered text attributes when the UIA provider exposes them.
 fonts=[]
 for control in wrapper.descendants(control_type='Text'):
  try:
   font=control.iface_text.DocumentRange.GetAttributeValue(40005)
   if isinstance(font,str):fonts.append({'text':control.window_text(),'font':font})
  except Exception:pass
 info['uia_fonts']=fonts
 # Smaller physical window = less available logical space. This tests layout,
 # not a change of monitor DPI or rasterization scale.
 win32gui.SetWindowPos(h,0,info['rect'][0],info['rect'][1],round(720*dpi/96),round(500*dpi/96),0x14)
 time.sleep(.8);capture('dpi-small-viewport.png')
 info['requested_viewport_dips']=[720,500]
 info['minimum_window_dips']=[1100,720]
 info['small_viewport_rect']=win32gui.GetWindowRect(h)
 info['small_viewport_scrollbars']=len(wrapper.descendants(control_type='ScrollBar'))
 info['other_scales']='Window-size arithmetic tested at 100/125/150/175/200/250/300; physical monitor DPI changes not performed.'
 (root/'artifacts/dpi-verification.json').write_text(json.dumps(info,ensure_ascii=True,indent=2),encoding='utf-8')
 print(json.dumps(info,ensure_ascii=True),flush=True)
finally:
 try:w.close();p.wait(timeout=5)
 except Exception:
  if p.poll() is None:p.terminate()
