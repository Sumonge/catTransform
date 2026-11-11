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
# Build a reduced Java 21 image from the pinned Temurin JDK. A full JRE is
# intentionally not copied; jlink preserves the licenses for included modules.
jdk_archive=root/'artifacts/downloads/temurin-jdk-21.0.13+9.zip'
subprocess.run(['python',str(root/'tools/download_temurin_jdk.py')],check=True)
expected='a4dbaa05fca4bd89d84584e58c459bd732efebb1722af80ab13c6516832004f9'
with jdk_archive.open('rb') as stream:
 if hashlib.file_digest(stream,'sha256').hexdigest()!=expected:raise ValueError('Temurin JDK checksum mismatch')
source=root/'artifacts/runtime/temurin-jdk-source';source.mkdir(parents=True,exist_ok=True)
if not any(p.is_dir() and (p/'bin/jlink.exe').exists() for p in source.iterdir()):
 with zipfile.ZipFile(jdk_archive) as z:
  for entry in z.infolist():
   if not (source/entry.filename).resolve().is_relative_to(source.resolve()):raise ValueError('Unsafe JDK ZIP path')
  z.extractall(source)
jdk=next(p for p in source.iterdir() if p.is_dir() and (p/'bin/jlink.exe').exists())
modules='java.base,java.compiler,java.desktop,java.security.jgss,java.sql,jdk.unsupported,jdk.xml.dom,jdk.charsets,jdk.localedata,jdk.crypto.ec,java.net.http,java.logging,java.naming,java.xml.crypto'
slim=root/'artifacts/runtime/java-slim'
if slim.exists():
 assert slim.resolve().is_relative_to((root/'artifacts/runtime').resolve())
 shutil.rmtree(slim)
subprocess.run([str(jdk/'bin/jlink.exe'),'--module-path',str(jdk/'jmods'),'--add-modules',modules,'--strip-debug','--no-header-files','--no-man-pages','--compress=0','--output',str(slim)],check=True)
for name in ('NOTICE','LICENSE'):
 if (jdk/name).exists():shutil.copy2(jdk/name,slim/name)
if not (slim/'bin/java.exe').exists() or not (slim/'legal').is_dir():raise RuntimeError('Incomplete Java image')
publish=root/'artifacts/release';runtime=publish/'runtime';runtime.mkdir(parents=True,exist_ok=True)
java_target=runtime/'java'
if java_target.exists():
 assert java_target.resolve().is_relative_to(runtime.resolve())
 shutil.rmtree(java_target)
shutil.copytree(slim,java_target)
shutil.copytree(root/'tools/epubcheck/epubcheck-5.3.0',runtime/'epubcheck',dirs_exist_ok=True)
metadata=json.loads((root/'tools/temurin-jdk-runtime.json').read_text())[0]
(runtime/'temurin-source.json').write_text(json.dumps(metadata,indent=2),encoding='utf-8')
print('Bundled verified minimal Temurin',metadata['version']['semver'],'and EPUBCheck 5.3.0',flush=True)

