"""Download the pinned Temurin build with verified resumable ranges."""
import concurrent.futures, hashlib, json, pathlib, subprocess
root=pathlib.Path(__file__).resolve().parents[1]
metadata_path=root/'tools/temurin-jdk-runtime.json'
metadata=json.loads(metadata_path.read_text())[0]
package=metadata['binary']['package']
assert metadata['version']['semver']=='21.0.13+9.0.LTS'
assert metadata['binary']['package']['checksum']=='a4dbaa05fca4bd89d84584e58c459bd732efebb1722af80ab13c6516832004f9'
assert package['link'].startswith('https://github.com/adoptium/temurin21-binaries/releases/download/jdk-21.0.13%2B9/')
size=package['size'];url=package['link'];target=root/'artifacts/downloads/temurin-jdk-21.0.13+9.zip'
chunks=root/'artifacts/downloads/temurin-jdk-parts';chunks.mkdir(parents=True,exist_ok=True)
if target.exists() and target.stat().st_size==size:
 with target.open('rb') as stream:existing_hash=hashlib.file_digest(stream,'sha256').hexdigest()
 if existing_hash==package['checksum']:
  print('Verified cached Temurin JDK',flush=True)
  raise SystemExit(0)
 target.unlink()
initial=target.stat().st_size if target.exists() else 0
assert initial<=size
step=4*1024*1024
starts=list(range(initial,size,step))
def get(start):
 end=min(start+step,size)-1
 path=chunks/str(start)
 if path.exists() and path.stat().st_size==end-start+1:return path
 result=subprocess.run(['curl.exe','-fsSL','--retry','3','--connect-timeout','15','--max-time','180','-r',f'{start}-{end}','-o',str(path),url],capture_output=True,text=True)
 if result.returncode or path.stat().st_size!=end-start+1:raise RuntimeError(f'Range {start}-{end} failed: {result.stderr[-250:]}')
 return path
print(f'Resuming {initial}/{size} with {len(starts)} verified ranges',flush=True)
with concurrent.futures.ThreadPoolExecutor(max_workers=8) as pool:
 results=list(pool.map(get,starts))
 print(f'Downloaded {len(results)} ranges',flush=True)
partial=target.with_suffix('.complete.tmp')
with partial.open('wb') as output:
 if initial:
  with target.open('rb') as source:
   for block in iter(lambda:source.read(1024*1024),b''):output.write(block)
 for path in results:
  with path.open('rb') as source:
   for block in iter(lambda:source.read(1024*1024),b''):output.write(block)
assert partial.stat().st_size==size
h=hashlib.file_digest(partial.open('rb'),'sha256').hexdigest()
assert h==package['checksum'],(h,package['checksum'])
partial.replace(target)
print(f'Verified SHA-256: {h}',flush=True)
