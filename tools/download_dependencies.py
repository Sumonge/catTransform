"""Resume official dependency downloads into the workspace; never used at app runtime."""
import pathlib, urllib.request, time, zipfile
root=pathlib.Path(__file__).resolve().parents[1]
items=[
 ('https://api.nuget.org/v3-flatcontainer/microsoft.windowsappsdk.winui/1.8.260803003/microsoft.windowsappsdk.winui.1.8.260803003.nupkg',root/'artifacts/nuget/microsoft.windowsappsdk.winui.1.8.260803003.nupkg'),
 ('https://api.nuget.org/v3-flatcontainer/microsoft.windowsappsdk.runtime/1.8.260921001/microsoft.windowsappsdk.runtime.1.8.260921001.nupkg',root/'artifacts/nuget/microsoft.windowsappsdk.runtime.1.8.260921001.nupkg'),
 ('https://github.com/w3c/epubcheck/releases/download/v5.3.0/epubcheck-5.3.0.zip',root/'artifacts/downloads/epubcheck-5.3.0.zip')]
for url,target in items:
 target.parent.mkdir(parents=True,exist_ok=True)
 part=target.with_suffix(target.suffix+'.partial')
 if target.exists(): continue
 for attempt in range(30):
  offset=part.stat().st_size if part.exists() else 0
  try:
   req=urllib.request.Request(url,headers={'Range':f'bytes={offset}-','User-Agent':'catTransform-build/0.1'})
   with urllib.request.urlopen(req,timeout=45) as response:
    append=response.status==206
    expected=int(response.headers.get('Content-Length','0'))
    received=0
    with part.open('ab' if append else 'wb') as stream:
     while block:=response.read(1024*1024):stream.write(block);received+=len(block)
    if expected and received!=expected:raise OSError('truncated download')
   with zipfile.ZipFile(part) as archive:
    if archive.testzip():raise OSError('ZIP checksum mismatch')
   part.rename(target);print('Verified',target.name,target.stat().st_size,flush=True);break
  except Exception as e:
   print('Retry',attempt+1,target.name,part.stat().st_size if part.exists() else 0,type(e).__name__,str(e),flush=True);time.sleep(2)
 else:raise RuntimeError('Download failed: '+url)
epub=root/'tools/epubcheck'
epub.mkdir(parents=True,exist_ok=True)
with zipfile.ZipFile(root/'artifacts/downloads/epubcheck-5.3.0.zip') as archive:
 for info in archive.infolist():
  resolved=(epub/info.filename).resolve()
  if not resolved.is_relative_to(epub.resolve()):raise ValueError('unsafe ZIP path')
 archive.extractall(epub)
