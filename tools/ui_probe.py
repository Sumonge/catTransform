import sys,pathlib,time,json
root=pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0,str(root/'tools/pythonlibs'))
from pywinauto import Desktop
if '--close' in sys.argv:
 current=Desktop(backend='uia').window(title='catTransform',process=int((root/'artifacts/release-pid.txt').read_text().strip()))
 if current.exists():current.close()
 print('Closed current test window');sys.exit(0)
if '--close-old' in sys.argv:
 for pid in [55132,74348]:
  old=Desktop(backend='uia').window(title='catTransform',process=pid)
  if not old.exists():continue
  picker=old.child_window(title='打开',control_type='Window')
  if picker.exists():picker.close();time.sleep(.2)
  old.close()
 print('Closed owned test windows');sys.exit(0)
if '--inspect' in sys.argv:
 import win32gui,win32process
 handles=[]
 win32gui.EnumWindows(lambda h,_:handles.append(h) if win32process.GetWindowThreadProcessId(h)[1] in [55132,75428] else None,None)
 for h in handles:
  print(win32process.GetWindowThreadProcessId(h)[1],h,win32gui.GetWindowText(h),win32gui.IsWindowVisible(h),flush=True)
  if win32gui.IsWindowVisible(h):Desktop(backend='uia').window(handle=h).print_control_identifiers(filename=str(root/f'artifacts/inspect-{h}.txt'))
 sys.exit(0)
window=Desktop(backend='uia').window(title='catTransform',process=int((root/'artifacts/release-pid.txt').read_text().strip()))
window.wait('visible',timeout=20)
window.print_control_identifiers(filename=str(root/'artifacts/ui-tree.txt'))
print('UI tree saved',flush=True)
window.set_focus()
time.sleep(.5)
window.capture_as_image().save(str(root/'artifacts/batch-window.png'))
window.child_window(title='文本转换',control_type='ListItem').select()
time.sleep(.5)
window.descendants(control_type='ComboBox')[0].select(0)
edits=window.descendants(control_type='Edit')
assert len(edits)==2, len(edits)
edits[0].set_edit_text('汉字转换，软件和鼠标。')
for _ in range(50):
 if edits[1].get_value()=='漢字轉換，軟件和鼠標。':break
 time.sleep(.1)
assert edits[1].get_value()=='漢字轉換，軟件和鼠標。',edits[1].get_value()
window.capture_as_image().save(str(root/'artifacts/text-window.png'))
window.child_window(title='批量转换',control_type='ListItem').select()
window.child_window(title='文本转换',control_type='ListItem').select()
assert window.descendants(control_type='Edit')[0].get_value()=='汉字转换，软件和鼠标。'
for page in ['剪贴板','历史记录','设置','批量转换']:
 window.child_window(title=page,control_type='ListItem').select();time.sleep(.3)
assert window.child_window(title='开始转换',control_type='Button').exists()
(root/'artifacts/ui-results.json').write_text(json.dumps({'real_time_text':True,'session_retention':True,'five_pages':True},ensure_ascii=False,indent=2),encoding='utf-8')
print('UI assertions passed',flush=True)
