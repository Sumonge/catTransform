using System.IO.Compression;
using System.Text;
using CatTransform.Conversion;
using CatTransform.Core;
using CatTransform.Documents;
using Xunit;
namespace CatTransform.Tests;
public sealed class EpubTests {
 [Fact]public async Task EpubCheckNavigationAndResources(){
  var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../"));
  var jar=Path.Combine(root,"tools/epubcheck/epubcheck-5.3.0/epubcheck.jar");
  Assert.True(File.Exists(jar),"EPUBCheck dependency missing; run tools/download_dependencies.py");
  var dir=Path.Combine(Path.GetTempPath(),"catTransform-epub-"+Guid.NewGuid());Directory.CreateDirectory(dir);
  try{var source=Path.Combine(dir,"source.epub");var output=Path.Combine(dir,"output.epub");
   using(var zip=ZipFile.Open(source,ZipArchiveMode.Create)){
    void Add(string name,string value,bool stored=false){using var writer=new StreamWriter(zip.CreateEntry(name,stored?CompressionLevel.NoCompression:CompressionLevel.Optimal).Open(),new UTF8Encoding(false));writer.Write(value);}
    Add("mimetype","application/epub+zip",true);
    Add("META-INF/container.xml","<?xml version=\"1.0\"?><container version=\"1.0\" xmlns=\"urn:oasis:names:tc:opendocument:xmlns:container\"><rootfiles><rootfile full-path=\"EPUB/package.opf\" media-type=\"application/oebps-package+xml\"/></rootfiles></container>");
    Add("EPUB/package.opf","<?xml version=\"1.0\"?><package xmlns=\"http://www.idpf.org/2007/opf\" version=\"3.0\" unique-identifier=\"id\"><metadata xmlns:dc=\"http://purl.org/dc/elements/1.1/\"><dc:identifier id=\"id\">urn:uuid:389e88aa-18d2-45ed-b278-fb301927dbbb</dc:identifier><dc:title>汉字</dc:title><dc:language>zh</dc:language><meta property=\"dcterms:modified\">2026-01-01T00:00:00Z</meta></metadata><manifest><item id=\"nav\" href=\"nav.xhtml\" media-type=\"application/xhtml+xml\" properties=\"nav\"/><item id=\"chapter\" href=\"chapter.xhtml\" media-type=\"application/xhtml+xml\"/><item id=\"css\" href=\"style.css\" media-type=\"text/css\"/></manifest><spine><itemref idref=\"chapter\"/></spine></package>");
    Add("EPUB/nav.xhtml","<html xmlns=\"http://www.w3.org/1999/xhtml\" xmlns:epub=\"http://www.idpf.org/2007/ops\"><head><title>目录</title></head><body><nav epub:type=\"toc\"><h1>目录</h1><ol><li><a href=\"chapter.xhtml#chapter\">汉字</a></li></ol></nav></body></html>");
    Add("EPUB/chapter.xhtml","<html xmlns=\"http://www.w3.org/1999/xhtml\"><head><title>汉字</title><link rel=\"stylesheet\" type=\"text/css\" href=\"style.css\"/></head><body><h1 id=\"chapter\">汉字转换</h1><p>软件网络</p><pre>汉字</pre></body></html>");
    Add("EPUB/style.css","/* 汉字 */ body { color: black; }");
   }
   await new DocumentConverter(new TextConverter()).ConvertAsync(source,output,new(){EpubCheckJar=jar,Scope=new(){EpubNavigation=true,EpubMetadata=true}},default);
   using var result=ZipFile.OpenRead(output);Assert.Equal("mimetype",result.Entries[0].FullName);Assert.Equal(result.Entries[0].Length,result.Entries[0].CompressedLength);
   string Read(string name){using var reader=new StreamReader(result.GetEntry(name)!.Open());return reader.ReadToEnd();}
   Assert.Contains("漢字轉換",Read("EPUB/chapter.xhtml"));Assert.Contains("<pre>汉字</pre>",Read("EPUB/chapter.xhtml"));Assert.Contains("chapter.xhtml#chapter",Read("EPUB/nav.xhtml"));Assert.Contains("漢字",Read("EPUB/nav.xhtml"));Assert.Contains("漢字",Read("EPUB/package.opf"));Assert.Equal("/* 汉字 */ body { color: black; }",Read("EPUB/style.css"));
  }finally{Directory.Delete(dir,true);}
 }
}
