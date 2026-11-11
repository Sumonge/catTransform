"""Bounded ranged curl downloads of pinned official packages, with ZIP verification."""
import concurrent.futures, pathlib, subprocess, zipfile, hashlib
root=pathlib.Path(__file__).resolve().parents[1]
items=[
 ('https://api.nuget.org/v3-flatcontainer/microsoft.windowsappsdk.winui/1.8.260803003/microsoft.windowsappsdk.winui.1.8.260803003.nupkg',58677153,'microsoft.windowsappsdk.winui.1.8.260803003.nupkg'),
 ('https://api.nuget.org/v3-flatcontainer/microsoft.windowsappsdk.runtime/1.8.260921001/microsoft.windowsappsdk.runtime.1.8.260921001.nupkg',0,'microsoft.windowsappsdk.runtime.1.8.260921001.nupkg'),
 ('https://api.nuget.org/v3-flatcontainer/microsoft.netcore.app.runtime.win-x64/10.0.8/microsoft.netcore.app.runtime.win-x64.10.0.8.nupkg',0,'microsoft.netcore.app.runtime.win-x64.10.0.8.nupkg'),
 ('https://api.nuget.org/v3-flatcontainer/microsoft.aspnetcore.app.runtime.win-x64/10.0.8/microsoft.aspnetcore.app.runtime.win-x64.10.0.8.nupkg',0,'microsoft.aspnetcore.app.runtime.win-x64.10.0.8.nupkg'),
 ('https://api.nuget.org/v3-flatcontainer/microsoft.windows.sdk.net.ref/10.0.26100.57/microsoft.windows.sdk.net.ref.10.0.26100.57.nupkg',0,'microsoft.windows.sdk.net.ref.10.0.26100.57.nupkg')]
for url,size,name in items:
 target=root/'artifacts/nuget'/name
 if target.exists():
  try:
   with zipfile.ZipFile(target) as archive:
    if archive.testzip() is None:continue
  except zipfile.BadZipFile:pass
 if not size:
  headers=subprocess.check_output(['curl.exe','-sSI','--max-time','30',url],text=True)
  size=int(next(line.split(':',1)[1].strip() for line in headers.splitlines() if line.lower().startswith('content-length:')))
 chunks=root/'artifacts/downloads'/name;chunks.mkdir(parents=True,exist_ok=True)
 def download(start):
  end=min(start+1024*1024,size)-1;path=chunks/str(start)
  if path.exists() and path.stat().st_size==end-start+1:return path
  subprocess.run(['curl.exe','-fsSL','--retry','4','--connect-timeout','15','--max-time','80','-r',f'{start}-{end}','-o',str(path),url],check=True)
  if path.stat().st_size!=end-start+1:raise RuntimeError('Wrong range length')
  print(name,start,end,flush=True);return path
 with concurrent.futures.ThreadPoolExecutor(max_workers=8) as pool:paths=list(pool.map(download,range(0,size,1024*1024)))
 target=root/'artifacts/nuget'/name
 with target.open('wb') as stream:
  for path in paths:stream.write(path.read_bytes())
 with zipfile.ZipFile(target) as archive:
  if archive.testzip():raise RuntimeError('Checksum failure')
 print('VERIFIED',name,hashlib.sha256(target.read_bytes()).hexdigest(),flush=True)
