import sys,pathlib,time,json,winreg
root=pathlib.Path(__file__).resolve().parents[1];sys.path.insert(0,str(root/'tools/pythonlibs'))
from pywinauto import Desktop
import win32clipboard,win32con,win32gui
w=Desktop(backend='uia').window(title='catTransform',process=int((root/'artifacts/release-pid.txt').read_text().strip()));w.wait('visible',timeout=15);w.set_focus()
def page(name):w.child_window(title=name,control_type='ListItem').select();time.sleep(.3)
def button(name):w.child_window(title=name,control_type='Button').invoke();time.sleep(.3)
def text():return [e.get_value() for e in w.descendants(control_type='Edit')]
def waitfor(predicate):
 for _ in range(100):
  if predicate():return
  time.sleep(.1)
 raise AssertionError('Timed out')
if '--tray-only' in sys.argv:
 page('剪贴板');button('开启监听');button('确认')
 try:
  button('暂停 / 恢复');hwnd=w.handle;win32gui.PostMessage(hwnd,win32con.WM_CLOSE,0,0);waitfor(lambda:not win32gui.IsWindowVisible(hwnd))
  win32gui.PostMessage(hwnd,0x8001,1,win32con.WM_LBUTTONDBLCLK);waitfor(lambda:win32gui.IsWindowVisible(hwnd))
 finally:
  if not win32gui.IsWindowVisible(w.handle):win32gui.ShowWindow(w.handle,win32con.SW_RESTORE)
  button('停止并清空')
 assert all(v=='' for v in text())
 (root/'artifacts/tray-ui-results.json').write_text(json.dumps({'paused_close_hides':True,'tray_restores_window':True,'stop_clears_cache':True}),encoding='utf-8')
 page('批量转换');print('Tray lifecycle assertions passed');sys.exit(0)
def set_clipboard(value):
 for _ in range(20):
  try:
   win32clipboard.OpenClipboard();win32clipboard.EmptyClipboard();win32clipboard.SetClipboardText(value,win32con.CF_UNICODETEXT);win32clipboard.CloseClipboard();return
  except Exception:
   try:win32clipboard.CloseClipboard()
   except:pass
   time.sleep(.1)
 raise RuntimeError('Clipboard busy')
win32clipboard.OpenClipboard()
try:
 formats=[];f=0
 while f:=win32clipboard.EnumClipboardFormats(f):formats.append(f)
 # Do not replace a rich-text/image/file clipboard owned by the user.
 allowed={1,7,13,16}
 if not set(formats).issubset(allowed):raise RuntimeError('Non-plain-text clipboard; automatic test refused to replace it')
 previous=win32clipboard.GetClipboardData(win32con.CF_UNICODETEXT) if 13 in formats else None
finally:win32clipboard.CloseClipboard()
results={}
try:
 page('剪贴板');button('开启监听');button('确认')
 set_clipboard('汉字转换测试');waitfor(lambda:'漢字轉換測試' in text())
 win32clipboard.OpenClipboard()
 try:assert win32clipboard.GetClipboardData(win32con.CF_UNICODETEXT)=='汉字转换测试'
 finally:win32clipboard.CloseClipboard()
 results['event_conversion_without_overwrite']=True
 button('暂停 / 恢复');set_clipboard('软件鼠标');time.sleep(.7);assert '漢字轉換測試' in text();results['pause_retains_cache']=True
 hwnd=w.handle;win32gui.PostMessage(hwnd,win32con.WM_CLOSE,0,0);waitfor(lambda:not win32gui.IsWindowVisible(hwnd));results['paused_close_hides_to_tray']=True
 win32gui.PostMessage(hwnd,0x8001,1,win32con.WM_LBUTTONDBLCLK);waitfor(lambda:win32gui.IsWindowVisible(hwnd))
 button('停止并清空');assert all(v=='' for v in text());results['stop_clears_cache']=True
 page('批量转换')
finally:
 if previous is not None:set_clipboard(previous)
 else:
  win32clipboard.OpenClipboard();win32clipboard.EmptyClipboard();win32clipboard.CloseClipboard()
(root/'artifacts/background-ui-results.json').write_text(json.dumps(results,indent=2),encoding='utf-8')
print(results)
