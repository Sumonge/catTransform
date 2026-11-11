using CatTransform.Core;
namespace CatTransform.Documents;
public sealed class DocumentConverter(ITextConverter converter) : IDocumentConverter {
 public static readonly string[] Extensions=[".txt",".md",".srt",".ass",".csv",".json",".docx",".epub"];
 public async Task ConvertAsync(string source,string destination,ConversionOptions options,CancellationToken ct){
  ct.ThrowIfCancellationRequested();if(new FileInfo(source).Length>options.MaxInputBytes)throw new InvalidDataException("文件超过配置的大小限制");
  string format=Path.GetExtension(source).ToLowerInvariant().TrimStart('.');
  string Convert(string s,string region){ct.ThrowIfCancellationRequested();var result=converter.Convert(s,options,format,region);
   string controls=format switch{"md"=>"`*_[]()<>!#|~\\\r\n","ass"=>"{}\\\r\n","srt"=>"<>{}\\\r\n",_=>""};
   if(controls.Length>0&&!s.Where(controls.Contains).SequenceEqual(result.Where(controls.Contains)))throw new InvalidDataException("规则修改了受保护的文档控制字符");
   return result;}
  if(format=="docx"){PackageFormats.Docx(source,destination,Convert,options.Scope,ct);return;}
  if(format=="epub"){await PackageFormats.EpubAsync(source,destination,Convert,options,ct);return;}
  var input=await EncodedText.ReadAsync(source,ct);
  var result=format switch{
   "txt"=>Convert(input.Text,"body"),"md"=>PlainFormats.MarkdownText(input.Text,Convert,options.Scope),
   "srt"=>PlainFormats.Srt(input.Text,Convert),"ass"=>PlainFormats.Ass(input.Text,Convert),
   "csv"=>PlainFormats.Csv(input.Text,Convert,options.Scope),"json"=>PlainFormats.Json(input.Text,Convert,options.Scope),
   _=>throw new NotSupportedException("不支持的文件格式")};
  ct.ThrowIfCancellationRequested();await input.WriteAsync(destination,result,ct);
 }
 public async Task<string> ExtractAsync(string source,ConversionOptions options,CancellationToken ct){
  if(new FileInfo(source).Length>options.MaxInputBytes)throw new InvalidDataException("预览文件超过大小限制");
  if(Path.GetExtension(source).ToLowerInvariant() is ".docx" or ".epub")return await Task.Run(()=>PackageFormats.Extract(source),ct);
  return (await EncodedText.ReadAsync(source,ct)).Text;
 }
}
