import pathlib,json,subprocess,concurrent.futures,zipfile,hashlib,shutil
root=pathlib.Path(__file__).resolve().parents[1]
(root/'artifacts/downloads').mkdir(parents=True,exist_ok=True)
epubzip=root/'artifacts/downloads/epubcheck-5.3.0.zip'
if not epubzip.exists():subprocess.run(['curl.exe','-fsSL','--retry','3','--connect-timeout','15','--max-time','300','-o',str(epubzip),'https://github.com/w3c/epubcheck/releases/download/v5.3.0/epubcheck-5.3.0.zip'],check=True)
if hashlib.sha256(epubzip.read_bytes()).hexdigest()!='6c07e68584b2e2ce2f89fe06e1246dfead3eb36b46b340e7d93524f29dcff6c5':raise ValueError('EPUBCheck checksum mismatch')
epubtarget=root/'tools/epubcheck';epubtarget.mkdir(parents=True,exist_ok=True)
with zipfile.ZipFile(epubzip) as z:
 for entry in z.infolist():
  if not (epubtarget/entry.filename).resolve().is_relative_to(epubtarget.resolve()):raise ValueError('Unsafe path')
 z.extractall(epubtarget)
metadata=json.loads((root/'tools/temurin-runtime.json').read_text())[0]
package=metadata['binary']['package'];url=package['link'];size=package['size']
assert url.startswith('https://github.com/adoptium/temurin21-binaries/')
parts=root/'artifacts/downloads/temurin-parts';parts.mkdir(exist_ok=True)
def download(start):
 end=min(start+1024*1024,size)-1;path=parts/str(start)
 if not path.exists() or path.stat().st_size!=end-start+1:
  subprocess.run(['curl.exe','-fsSL','--retry','3','--connect-timeout','15','--max-time','90','-r',f'{start}-{end}','-o',str(path),url],check=True)
  if path.stat().st_size!=end-start+1:raise ValueError('Unexpected range')
 return path
with concurrent.futures.ThreadPoolExecutor(max_workers=8) as pool:files=list(pool.map(download,range(0,size,1024*1024)))
archive=root/'artifacts/downloads/temurin-jre.zip'
with archive.open('wb') as stream:
 for file in files:stream.write(file.read_bytes())
assert hashlib.sha256(archive.read_bytes()).hexdigest()==package['checksum']
target=root/'artifacts/runtime';target.mkdir(exist_ok=True)
with zipfile.ZipFile(archive) as z:
 for entry in z.infolist():
  if not (target/entry.filename).resolve().is_relative_to(target.resolve()):raise ValueError('Unsafe path')
 z.extractall(target)
jre=next(p for p in target.iterdir() if p.is_dir() and (p/'bin/java.exe').exists())
publish=root/'artifacts/release'
shutil.copytree(jre,publish/'runtime/java',dirs_exist_ok=True)
shutil.copytree(root/'tools/epubcheck/epubcheck-5.3.0',publish/'runtime/epubcheck',dirs_exist_ok=True)
(publish/'runtime/temurin-source.json').write_text(json.dumps(metadata,indent=2),encoding='utf-8')
print('Bundled verified Temurin',metadata['version']['semver'],'and EPUBCheck 5.3.0',flush=True)
