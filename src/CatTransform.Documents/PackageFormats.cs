using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using CatTransform.Core;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using W=DocumentFormat.OpenXml.Wordprocessing;
namespace CatTransform.Documents;

internal static class PackageFormats {
 private static string BundledRuntime=>Path.Combine(AppContext.BaseDirectory,"runtime");
 private static string OptionalRuntime=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"catTransform","components","epub","runtime");
 private static string? CompleteRuntime=>new[]{BundledRuntime,OptionalRuntime}.FirstOrDefault(root=>File.Exists(Path.Combine(root,"epubcheck","epubcheck.jar"))&&File.Exists(Path.Combine(root,"java","bin","java.exe")));
 private static string CheckerPath=>Path.Combine(CompleteRuntime??BundledRuntime,"epubcheck","epubcheck.jar");
 private static string JavaPath=>CompleteRuntime is {} root?Path.Combine(root,"java","bin","java.exe"):"java";
 public static bool HasLocalEpubRuntime=>CompleteRuntime is not null;
 public static void Docx(string source,string destination,Func<string,string,string> convert,Scope scope,CancellationToken ct) {
  using(var archive=ZipFile.OpenRead(source))CheckArchive(archive);
  File.Copy(source,destination,false);
  using var doc=WordprocessingDocument.Open(destination,true);
  var main=doc.MainDocumentPart??throw new InvalidDataException("DOCX 缺少主文档");
  var roots=new List<(OpenXmlPartRootElement Root,string Region)>{(main.Document,"body")};
  if(scope.DocxHeaders){roots.AddRange(main.HeaderParts.Select(p=>((OpenXmlPartRootElement)p.Header,"header")));roots.AddRange(main.FooterParts.Select(p=>((OpenXmlPartRootElement)p.Footer,"footer")));}
  if(scope.DocxComments&&main.WordprocessingCommentsPart?.Comments is {} comments)roots.Add((comments,"comments"));
  foreach(var (root,region) in roots){foreach(var paragraph in root.Descendants<W.Paragraph>()){
   ct.ThrowIfCancellationRequested();
   // Merge only consecutive plain runs. Fields, hyperlinks, breaks, drawings are boundaries.
   var group=new List<W.Text>();
   void Flush(){if(group.Count==0)return;
    var parts=SegmentedText.Convert(group.Select(t=>t.Text).ToArray(),s=>convert(s,region),"DOCX");
    for(int k=0;k<group.Count;k++){group[k].Text=parts[k];group[k].Space=SpaceProcessingModeValues.Preserve;}group.Clear();}
   foreach(var child in paragraph.ChildElements){
    if(child is W.Run run&&run.ChildElements.All(e=>e is W.RunProperties or W.Text)&&!run.Ancestors<W.DeletedRun>().Any()){
     if(!scope.DocxTextBoxes&&run.Ancestors<W.TextBoxContent>().Any()){Flush();continue;}group.AddRange(run.Elements<W.Text>());
    }else{Flush();if(child is W.Hyperlink link)foreach(var t in link.Descendants<W.Text>())t.Text=convert(t.Text,region);}
   }Flush();
  }root.Save();}
  var errors=new OpenXmlValidator(FileFormatVersions.Office2019).Validate(doc).Take(5).ToArray();
  if(errors.Length>0)throw new InvalidDataException("DOCX 结构验证失败："+string.Join("；",errors.Select(e=>e.Description)));
 }
 private static XDocument Xml(Stream stream){using var reader=XmlReader.Create(stream,new(){DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=32_000_000});return XDocument.Load(reader,LoadOptions.PreserveWhitespace);}
 private static XDocument Xml(ZipArchiveEntry e){using var s=e.Open();return Xml(s);}
 private static void CheckArchive(ZipArchive zip){
  if(zip.Entries.Count>10000||zip.Entries.Sum(e=>e.Length)>512L*1024*1024)throw new InvalidDataException("压缩包展开大小或条目数超限");
  var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  foreach(var e in zip.Entries)if(e.FullName.Contains('\\')||e.FullName.StartsWith('/')||e.FullName.Split('/').Contains("..")||!names.Add(e.FullName))throw new InvalidDataException("压缩包含危险或重复路径");
 }
 public static async Task EpubAsync(string source,string destination,Func<string,string,string> convert,ConversionOptions options,CancellationToken ct){
  var checker=options.EpubCheckJar??CheckerPath;
  if(!File.Exists(checker))throw new InvalidDataException("EPUB 转换需要本地 EPUBCheck JAR；请在设置中配置，未验证时不提交输出");
  using(var input=ZipFile.OpenRead(source)){
   CheckArchive(input);
   if(input.GetEntry("META-INF/encryption.xml") is not null)throw new InvalidDataException("不支持带加密声明的 EPUB（包括字体混淆）");
   var mime=input.GetEntry("mimetype")??throw new InvalidDataException("EPUB 缺少 mimetype");using(var r=new StreamReader(mime.Open()))if(await r.ReadToEndAsync(ct)!="application/epub+zip")throw new InvalidDataException("EPUB mimetype 无效");
   var container=Xml(input.GetEntry("META-INF/container.xml")??throw new InvalidDataException("缺少 container.xml"));
   var packagePath=container.Descendants().FirstOrDefault(x=>x.Name.LocalName=="rootfile")?.Attribute("full-path")?.Value??throw new InvalidDataException("缺少 rootfile");
   var package=Xml(input.GetEntry(packagePath)??throw new InvalidDataException("缺少 OPF"));
   var targets=new Dictionary<string,bool>(StringComparer.Ordinal);
   foreach(var item in package.Descendants().Where(e=>e.Name.LocalName=="item")){
    var type=(string?)item.Attribute("media-type");if(type is not ("application/xhtml+xml" or "application/x-dtbncx+xml"))continue;
    var href=(string?)item.Attribute("href")??throw new InvalidDataException("清单 href 为空");
    var resolved=new Uri(new Uri("https://epub.invalid/"+packagePath),href);
    if(resolved.Host!="epub.invalid")throw new InvalidDataException("不支持远程 EPUB 内容");
    var name=Uri.UnescapeDataString(resolved.AbsolutePath.TrimStart('/'));
    if(input.GetEntry(name) is null)throw new InvalidDataException("清单资源缺失");
    targets[name]=type=="application/x-dtbncx+xml"||((string?)item.Attribute("properties"))?.Split(' ').Contains("nav")==true;
   }
   using var output=ZipFile.Open(destination,ZipArchiveMode.Create);
   foreach(var entry in input.Entries.OrderBy(e=>e.FullName=="mimetype"?0:1)){
    ct.ThrowIfCancellationRequested();var created=output.CreateEntry(entry.FullName,entry.FullName=="mimetype"?CompressionLevel.NoCompression:CompressionLevel.Optimal);using var dest=created.Open();
    bool isPackage=entry.FullName==packagePath&&options.Scope.EpubMetadata;
    var targeted=targets.TryGetValue(entry.FullName,out var nav);
    if(isPackage||targeted&&(!nav||options.Scope.EpubNavigation)){
     var xml=Xml(entry);
     ConvertEpubXml(xml,isPackage,nav,convert);
     using var writer=XmlWriter.Create(dest,new(){Encoding=new UTF8Encoding(false),CloseOutput=false,Indent=false});xml.Save(writer);
    }else{using var src=entry.Open();await src.CopyToAsync(dest,ct);}
   }
  }
  await ValidateEpubAsync(destination,checker,ct);
 }
 private static void ConvertEpubXml(XDocument xml,bool isPackage,bool navigation,Func<string,string,string> convert){
  var region=isPackage?"metadata":navigation?"navigation":"body";
  bool Eligible(XText text){
   if(text.Ancestors().Any(e=>e.Name.LocalName is "script" or "style" or "code" or "pre"))return false;
   return isPackage?text.Parent?.Name.NamespaceName=="http://purl.org/dc/elements/1.1/"&&text.Parent.Name.LocalName is "title" or "creator" or "description" or "subject" or "publisher":text.Ancestors().Any(e=>e.Name.LocalName is "body" or "navLabel" or "docTitle");
  }
  var done=new HashSet<XText>();
  void Apply(XText[] nodes){
   if(nodes.Length==0||nodes.All(n=>string.IsNullOrWhiteSpace(n.Value)))return;
   var parts=SegmentedText.Convert(nodes.Select(n=>n.Value).ToArray(),s=>convert(s,region),"EPUB");
   for(int i=0;i<nodes.Length;i++){nodes[i].Value=parts[i];done.Add(nodes[i]);}
  }
  if(!isPackage){
   var blocks=new HashSet<string>(StringComparer.Ordinal){"p","h1","h2","h3","h4","h5","h6","li","td","th","blockquote","figcaption","navLabel","docTitle"};
   foreach(var block in xml.Descendants().Where(e=>blocks.Contains(e.Name.LocalName)).OrderByDescending(e=>e.Ancestors().Count()))
    Apply(block.DescendantNodes().OfType<XText>().Where(t=>!done.Contains(t)&&Eligible(t)).ToArray());
  }
  foreach(var text in xml.DescendantNodes().OfType<XText>().Where(t=>!done.Contains(t)&&Eligible(t)).ToArray())Apply([text]);
 }
 public static async Task ValidateEpubAsync(string path,string jar,CancellationToken ct){
  var start=new ProcessStartInfo(JavaPath){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};start.ArgumentList.Add("-Xmx512m");start.ArgumentList.Add("-jar");start.ArgumentList.Add(jar);start.ArgumentList.Add(path);
  using var process=Process.Start(start)??throw new IOException("无法启动 EPUBCheck");
  var stdout=process.StandardOutput.ReadToEndAsync(ct);var stderr=process.StandardError.ReadToEndAsync(ct);
  using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromMinutes(2));
  try{await process.WaitForExitAsync(timeout.Token);await Task.WhenAll(stdout,stderr);}catch{if(!process.HasExited)process.Kill(true);throw;}
  if(process.ExitCode!=0)throw new InvalidDataException("EPUBCheck 规范验证失败；输出未提交");
 }
 public static string Extract(string path){
  using var zip=ZipFile.OpenRead(path);CheckArchive(zip);var result=new StringBuilder();
  foreach(var entry in zip.Entries.Where(e=>path.EndsWith(".docx",StringComparison.OrdinalIgnoreCase)?e.FullName=="word/document.xml":e.FullName.EndsWith(".xhtml",StringComparison.OrdinalIgnoreCase)||e.FullName.EndsWith(".html",StringComparison.OrdinalIgnoreCase))){var xml=Xml(entry);foreach(var text in xml.DescendantNodes().OfType<XText>().Where(t=>!t.Ancestors().Any(e=>e.Name.LocalName is "style" or "script")))result.AppendLine(text.Value);}
  return result.ToString();
 }
}
