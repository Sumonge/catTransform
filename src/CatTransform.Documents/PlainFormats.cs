using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CatTransform.Core;
using CsvHelper;
using CsvHelper.Configuration;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
namespace CatTransform.Documents;

internal static class PlainFormats {
 public static string MarkdownText(string text,Func<string,string,string> convert,Scope scope) {
  var doc=Markdown.Parse(text,new MarkdownPipelineBuilder().UsePreciseSourceLocation().Build());
  var edits=new List<(int Start,int Length,string Value)>();
  foreach(var leaf in doc.Descendants<LeafBlock>()) {
   if(leaf.Inline is not null)foreach(var literal in leaf.Inline.Descendants<LiteralInline>()) {
    var span=literal.Span;
    if(span.Start>=0&&span.End<text.Length)edits.Add((span.Start,span.Length,convert(text.Substring(span.Start,span.Length),"body")));
   }
   if(scope.MarkdownCode&&leaf is CodeBlock code)foreach(var line in code.Lines.Lines.Take(code.Lines.Count)) {
    var slice=line.Slice;if(slice.Length>0)edits.Add((slice.Start,slice.Length,convert(text.Substring(slice.Start,slice.Length),"code")));
   }
  }
  var result=new StringBuilder(text);
  foreach(var e in edits.OrderByDescending(e=>e.Start))result.Remove(e.Start,e.Length).Insert(e.Start,e.Value);
  return result.ToString();
 }
 public static string Srt(string text,Func<string,string,string> convert) {
  var lines=Regex.Split(text,"(\r\n|\n|\r)");int state=0;bool any=false;
  for(int i=0;i<lines.Length;i+=2){var line=lines[i];
   if(string.IsNullOrWhiteSpace(line)){state=0;continue;}
   if(state==0){if(!int.TryParse(line,out _))throw new InvalidDataException("SRT 序号无效");state=1;}
   else if(state==1){if(!Regex.IsMatch(line,@"^\d{2,}:\d{2}:\d{2},\d{3} --> \d{2,}:\d{2}:\d{2},\d{3}(?: .*)?$"))throw new InvalidDataException("SRT 时间轴无效");state=2;any=true;}
   else lines[i]=Tagged(line,convert);
  }
  if(!any||state==1)throw new InvalidDataException("SRT 不完整");return string.Concat(lines);
 }
 private static string Tagged(string s,Func<string,string,string> convert)=>string.Concat(Regex.Split(s,"(<[^>]*>|\\{[^}]*\\}|\\\\[Nnh])").Select(x=>x.StartsWith('<')||x.StartsWith('{')||x is @"\N" or @"\n" or @"\h"?x:convert(x,"body")));
 public static string Ass(string text,Func<string,string,string> convert) {
  var lines=Regex.Split(text,"(\r\n|\n|\r)");bool events=false;string[]? fields=null;
  for(int i=0;i<lines.Length;i+=2){var s=lines[i];
   if(s.StartsWith('[')){events=s.Trim().Equals("[Events]",StringComparison.OrdinalIgnoreCase);continue;}
   if(!events)continue;
   if(s.StartsWith("Format:",StringComparison.OrdinalIgnoreCase)){fields=s[7..].Split(',').Select(x=>x.Trim()).ToArray();continue;}
   if(!s.StartsWith("Dialogue:",StringComparison.OrdinalIgnoreCase))continue;
   if(fields is null||fields[^1]!="Text")throw new InvalidDataException("ASS 需要 Text 为最后一列的 Events Format");
   var values=s[9..].Split(',',fields.Length);if(values.Length!=fields.Length)throw new InvalidDataException("ASS Dialogue 列数无效");
   bool drawing=false;var chunks=Regex.Split(values[^1],"(\\{[^}]*\\})");
   for(var j=0;j<chunks.Length;j++){if(chunks[j].StartsWith('{')){foreach(Match m in Regex.Matches(chunks[j],@"\\p(\d+)"))drawing=m.Groups[1].Value!="0";}else if(!drawing)chunks[j]=Tagged(chunks[j],convert);}
   values[^1]=string.Concat(chunks);lines[i]="Dialogue:"+string.Join(',',values);
  }
  return string.Concat(lines);
 }
 public static string Csv(string text,Func<string,string,string> convert,Scope scope) {
  if(scope.CsvDelimiter.Length!=1)throw new InvalidDataException("CSV 分隔符必须为单字符");
  var config=new CsvConfiguration(CultureInfo.InvariantCulture){HasHeaderRecord=false,Delimiter=scope.CsvDelimiter,NewLine=text.Contains("\r\n")?"\r\n":"\n",Mode=CsvMode.RFC4180};
  using var reader=new CsvReader(new StringReader(text),config);using var output=new StringWriter();using var writer=new CsvWriter(output,config);
  int row=0;int? width=null;
  while(reader.Read()){width??=reader.Parser.Count;if(reader.Parser.Count!=width)throw new InvalidDataException("CSV 各行列数不一致");
   for(int col=0;col<reader.Parser.Count;col++){var value=reader.GetField(col)??"";if((row>0||scope.CsvHeader)&&(scope.CsvInclude.Length==0||scope.CsvInclude.Contains(col))&&!scope.CsvExclude.Contains(col))value=convert(value,row==0?"header":"body");writer.WriteField(value);}writer.NextRecord();row++;
  }
  return output.ToString();
 }
 public static string Json(string text,Func<string,string,string> convert,Scope scope) {
  using var doc=JsonDocument.Parse(text,new JsonDocumentOptions{MaxDepth=128});using var stream=new MemoryStream();using var writer=new Utf8JsonWriter(stream,new(){Indented=true});Write(doc.RootElement,"");writer.Flush();
  return Encoding.UTF8.GetString(stream.ToArray());
  void Write(JsonElement e,string path){switch(e.ValueKind){
   case JsonValueKind.Object:
    writer.WriteStartObject();var keys=new HashSet<string>(StringComparer.Ordinal);
    foreach(var p in e.EnumerateObject()){var key=scope.JsonKeys?convert(p.Name,"key"):p.Name;if(!keys.Add(key))throw new InvalidDataException("JSON 属性名冲突");writer.WritePropertyName(key);Write(p.Value,path+"/"+p.Name.Replace("~","~0").Replace("/","~1"));}writer.WriteEndObject();break;
   case JsonValueKind.Array:writer.WriteStartArray();int i=0;foreach(var v in e.EnumerateArray())Write(v,path+"/"+i++);writer.WriteEndArray();break;
   case JsonValueKind.String:writer.WriteStringValue(scope.JsonPaths.Length==0||scope.JsonPaths.Contains(path)?convert(e.GetString()!,"body"):e.GetString());break;
   default:e.WriteTo(writer);break;
  }}
 }
}
