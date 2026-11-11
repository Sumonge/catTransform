import pathlib,sys,time,json,uuid,zipfile
import atexit
root=pathlib.Path(__file__).resolve().parents[1];sys.path.insert(0,str(root/'tools/pythonlibs'))
from pywinauto import Desktop
w=Desktop(backend='uia').window(title='catTransform',process=int((root/'artifacts/release-pid.txt').read_text().strip()));w.set_focus()
from win32com.shell import shell,shellcon
settings_path=pathlib.Path(shell.SHGetFolderPath(0,shellcon.CSIDL_LOCAL_APPDATA,None,0))/'catTransform/settings.json'
old_config=json.loads(settings_path.read_text(encoding='utf-8'))['Conversion']['Config'] if settings_path.exists() else 's2t'
configs=['s2t','t2s','s2tw','s2twp','s2hk','tw2s','tw2sp','hk2s']
def restore_config():
 try:
  w.child_window(title='批量转换',control_type='ListItem').select();w.descendants(control_type='ComboBox')[0].select(configs.index(old_config))
 except Exception:pass
atexit.register(restore_config)
w.child_window(title='批量转换',control_type='ListItem').select();w.descendants(control_type='ComboBox')[0].select(0)
def waitfor(predicate):
 for _ in range(600):
  if predicate():return
  time.sleep(.1)
 raise AssertionError('Timed out')
fixture=root/'artifacts/ui-fixtures';fixture.mkdir(exist_ok=True)
name='ui-proof-'+uuid.uuid4().hex[:8]
source=fixture/(name+'.txt');source.write_text('汉字转换，软件鼠标。',encoding='utf-8')
structured=fixture/(name+'.json');structured.write_text('{"汉字":"汉字"}',encoding='utf-8')
from epub_fixture import create_epub
epub=fixture/(name+'.epub');create_epub(epub)
picker=w.child_window(title='打开',control_type='Window')
if not picker.exists():
 w.child_window(title='批量转换',control_type='ListItem').select();w.child_window(title='添加文件',control_type='Button').invoke();picker.wait('visible',timeout=15)
picker.child_window(auto_id='1148',control_type='Edit').set_edit_text(f'"{source}" "{structured}" "{epub}"')
picker.child_window(auto_id='1',control_type='SplitButton').invoke()
waitfor(lambda:not picker.exists())
def queue_names():return [x.window_text() for x in w.descendants(control_type='ListItem') if name in x.window_text()]
waitfor(lambda:len(queue_names())==3)
assert all('等待中' in x for x in queue_names()),queue_names()
w.child_window(title='开始转换',control_type='Button').invoke()
waitfor(lambda:len(queue_names())==3 and all('成功' in x for x in queue_names()))
from win32com.shell import shell,shellcon
settings_path=pathlib.Path(shell.SHGetFolderPath(0,shellcon.CSIDL_LOCAL_APPDATA,None,0))/'catTransform/settings.json'
out=pathlib.Path(json.loads(settings_path.read_text(encoding='utf-8'))['Conversion']['OutputDirectory']) if settings_path.exists() else pathlib.Path(shell.SHGetFolderPath(0,shellcon.CSIDL_PERSONAL,None,0))/'catTransform/Output'
assert (out/source.name).read_text(encoding='utf-8')=='漢字轉換，軟件鼠標。'
assert json.loads((out/structured.name).read_text(encoding='utf-8'))=={'汉字':'漢字'}
with zipfile.ZipFile(out/epub.name) as z:assert '漢字轉換' in z.read('EPUB/chapter.xhtml').decode('utf-8')
w.child_window(title='开始转换',control_type='Button').invoke();time.sleep(.5)
assert not (out/(source.stem+' (1).txt')).exists()
w.child_window(title='历史记录',control_type='ListItem').select();time.sleep(.5)
assert any(name in x.window_text() for x in w.descendants(control_type='ListItem'))
(root/'artifacts/batch-ui-results.json').write_text(json.dumps({'txt_output':True,'json_output':True,'epub_with_bundled_java':True,'manual_start':True,'completed_not_repeated':True,'history_visible':True,'fixture':name},ensure_ascii=False,indent=2),encoding='utf-8')
print('Batch UI assertions passed',name)
w.child_window(title='批量转换',control_type='ListItem').select()
