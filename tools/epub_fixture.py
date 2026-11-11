import zipfile
def create_epub(path):
 with zipfile.ZipFile(path,'w') as z:
  z.writestr('mimetype','application/epub+zip',compress_type=zipfile.ZIP_STORED)
  z.writestr('META-INF/container.xml','<container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container"><rootfiles><rootfile full-path="EPUB/package.opf" media-type="application/oebps-package+xml"/></rootfiles></container>')
  z.writestr('EPUB/package.opf','<package xmlns="http://www.idpf.org/2007/opf" version="3.0" unique-identifier="id"><metadata xmlns:dc="http://purl.org/dc/elements/1.1/"><dc:identifier id="id">urn:uuid:389e88aa-18d2-45ed-b278-fb301927dbbb</dc:identifier><dc:title>转换验收</dc:title><dc:language>zh</dc:language><meta property="dcterms:modified">2026-01-01T00:00:00Z</meta></metadata><manifest><item id="nav" href="nav.xhtml" media-type="application/xhtml+xml" properties="nav"/><item id="chapter" href="chapter.xhtml" media-type="application/xhtml+xml"/></manifest><spine><itemref idref="chapter"/></spine></package>')
  z.writestr('EPUB/nav.xhtml','<html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops"><head><title>目录</title></head><body><nav epub:type="toc"><h1>目录</h1><ol><li><a href="chapter.xhtml">汉字</a></li></ol></nav></body></html>')
  z.writestr('EPUB/chapter.xhtml','<html xmlns="http://www.w3.org/1999/xhtml"><head><title>汉字</title></head><body><h1>汉字转换</h1><p>软件鼠标。</p></body></html>')
