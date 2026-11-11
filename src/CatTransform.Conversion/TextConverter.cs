using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using CatTransform.Core;
using OpenccNetLib;
namespace CatTransform.Conversion;

public sealed class TextConverter : ITextConverter {
 private const int Limit=4_000_000;
 private static string ReplaceBounded(string input,Rule rule){
  var result=new System.Text.StringBuilder();int offset=0;
  void Append(string value){if(result.Length+value.Length>Limit)throw new InvalidDataException("规则输出超过限制");result.Append(value);}
  if(rule.Kind!=RuleKind.Regex){while(true){var index=input.IndexOf(rule.Pattern,offset,StringComparison.Ordinal);if(index<0)break;Append(input[offset..index]);Append(rule.Replacement);offset=index+rule.Pattern.Length;}}
  else foreach(Match match in Compile(rule).Matches(input)){
   Append(input[offset..match.Index]);
   // Deliberately bounded replacement syntax: $$, $&, $1..$99, ${name}.
   var replacement=rule.Replacement;
   for(int i=0;i<replacement.Length;i++){
    if(replacement[i]!='$'||i+1==replacement.Length){Append(replacement[i].ToString());continue;}
    char next=replacement[++i];
    if(next=='$')Append("$");else if(next=='&')Append(match.Value);
    else if(char.IsDigit(next)){var name=next.ToString();if(i+1<replacement.Length&&char.IsDigit(replacement[i+1]))name+=replacement[++i];Append(match.Groups[name].Value);}
    else if(next=='{'){int end=replacement.IndexOf('}',i+1);if(end<0)throw new ArgumentException("正则替换分组未闭合");Append(match.Groups[replacement[(i+1)..end]].Value);i=end;}
    else throw new ArgumentException("正则替换仅支持 $$、$&、$1 和 ${name}");
   }
   offset=match.Index+match.Length;
  }
  Append(input[offset..]);return result.ToString();
 }
 public static readonly string[] Configurations = ["s2t","t2s","s2tw","s2twp","s2hk","tw2s","tw2sp","hk2s"];
 private readonly ConcurrentDictionary<string,Opencc> engines = new();
 private static Regex Compile(Rule r) {
  try { return new(r.Pattern,RegexOptions.CultureInvariant|RegexOptions.NonBacktracking,TimeSpan.FromMilliseconds(150)); }
  catch(NotSupportedException) when(r.AllowBacktracking) {return new(r.Pattern,RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(150));}
 }
 public void Validate(ConversionOptions o) {
  if(o.Scope is null||o.Rules is null||o.Scope.CsvInclude is null||o.Scope.CsvExclude is null||o.Scope.JsonPaths is null)throw new ArgumentException("范围或规则配置不能为空");
  if(o.Formats is null||o.Formats.Any(f=>f is not ("txt" or "md" or "srt" or "ass" or "csv" or "json" or "docx" or "epub")))throw new ArgumentException("文件格式范围无效");
  if(o.Scope.CsvDelimiter is null||o.Scope.CsvDelimiter.Length!=1||o.Scope.CsvDelimiter[0] is '\r' or '\n' or '"')throw new ArgumentException("CSV 分隔符无效");
  if(o.Scope.CsvInclude.Concat(o.Scope.CsvExclude).Any(i=>i<0))throw new ArgumentException("CSV 列号不能为负数");
  if(o.Scope.JsonPaths.Any(p=>p is null||p.Length>2048||p.Length>0&&!p.StartsWith('/')))throw new ArgumentException("JSON 路径必须使用 JSON Pointer");
  if(o.MaxInputBytes is <1 or >536870912)throw new ArgumentException("输入大小限制必须为 1–512 MiB 范围内的字节数");
  if(!Configurations.Contains(o.Config))throw new ArgumentException("不支持的转换配置");
  if(o.Concurrency is <1 or >16)throw new ArgumentException("并发数须为 1–16");
  if(o.Rules.Length>1000)throw new ArgumentException("规则数量超过 1000");
  if(o.Rules.Select(r=>r.Id).Distinct().Count()!=o.Rules.Length)throw new ArgumentException("规则 UUID 重复");
  foreach(var r in o.Rules) {
   if(r is null||r.Id==Guid.Empty || r.Pattern is null||r.Replacement is null||r.Pattern.Length is 0 or >4096 || r.Replacement.Length>16384)throw new ArgumentException("规则内容无效");
   if(!Enum.IsDefined(r.Kind)||!Enum.IsDefined(r.Stage))throw new ArgumentException("未知规则类型或阶段");
   if(r.Config is not null&&!Configurations.Contains(r.Config))throw new ArgumentException("规则转换条件无效");
   if(r.Direction is not null&&r.Direction is not ("s2t" or "t2s"))throw new ArgumentException("规则方向条件无效");
   if(r.Locale is not null&&r.Locale is not ("standard" or "tw" or "hk"))throw new ArgumentException("规则地区条件无效");
   if(r.Kind==RuleKind.Regex){var regex=Compile(r);var groups=regex.GetGroupNames();
    for(int i=0;i<r.Replacement.Length;i++){if(r.Replacement[i]!='$'||i+1==r.Replacement.Length)continue;char c=r.Replacement[++i];if(c is '$' or '&')continue;string name;
     if(char.IsDigit(c)){name=c.ToString();if(i+1<r.Replacement.Length&&char.IsDigit(r.Replacement[i+1]))name+=r.Replacement[++i];}
     else if(c=='{'){int end=r.Replacement.IndexOf('}',i+1);if(end<0)throw new ArgumentException("替换分组未闭合");name=r.Replacement[(i+1)..end];i=end;}
     else throw new ArgumentException("正则替换仅支持 $$、$&、$1 和 ${name}");
     if(!groups.Contains(name))throw new ArgumentException("正则替换引用了不存在的分组");
    }
   }
  }
  var duplicates=o.Rules.Where(r=>r.Enabled&&r.Kind!=RuleKind.Protect).GroupBy(r=>(r.Stage,r.Order,r.Pattern,r.Config,r.Direction,r.Locale,r.Format,r.Region));
  if(duplicates.Any(g=>g.Count()>1))throw new ArgumentException("同阶段、顺序和条件存在冲突规则");
 }
 public string Convert(string text,ConversionOptions o,string format="text",string region="body",ICollection<TraceEntry>? trace=null) {
  if(text.Length>4_000_000)throw new InvalidDataException("文本片段超过 400 万 UTF-16 字符限制");
  var direction=o.Config.StartsWith("s2")?"s2t":"t2s";var locale=o.Config.Contains("tw")?"tw":o.Config.Contains("hk")?"hk":"standard";
  var active=o.Rules.Where(r=>r.Enabled&&(r.Config is null||r.Config==o.Config)&&(r.Direction is null||r.Direction==direction)&&(r.Locale is null||r.Locale==locale)&&(r.Format is null||r.Format.Equals(format,StringComparison.OrdinalIgnoreCase))&&(r.Region is null||r.Region==region)).OrderBy(r=>r.Order).ThenBy(r=>r.Id).ToArray();
  // Split protection spans out of the pipeline; no sentinel can collide with user text or regex.
  var protect=active.Where(r=>r.Kind==RuleKind.Protect).ToArray();
  var engine=engines.GetOrAdd(o.Config,c=>new Opencc(c));
  string Process(string part) {
   foreach(var stage in new[]{RuleStage.Before,RuleStage.After}) {
    if(stage==RuleStage.After) {lock(engine)part=engine.Convert(part);}
    foreach(var r in active.Where(r=>r.Kind!=RuleKind.Protect&&r.Stage==stage)) {
     var result=ReplaceBounded(part,r);
     trace?.Add(new(r.Id,stage,result!=part));part=result;
     if(part.Length>4_000_000)throw new InvalidDataException("规则输出超过限制");
    }
   }
   return part;
  }
  if(protect.Length==0)return Process(text);
  var output=new System.Text.StringBuilder();var offset=0;
  while(offset<text.Length) {
   var match=protect.Select(r=>(r,index:text.IndexOf(r.Pattern,offset,StringComparison.Ordinal))).Where(x=>x.index>=0).OrderBy(x=>x.index).ThenByDescending(x=>x.r.Pattern.Length).FirstOrDefault();
   if(match.r is null){output.Append(Process(text[offset..]));break;}
   output.Append(Process(text[offset..match.index]));output.Append(match.r.Pattern);offset=match.index+match.r.Pattern.Length;
  }
  return output.ToString();
 }
}
public sealed record RulePackage(int schema_version,Rule[] rules);
public static class RulePackages {
 public static Rule[] Import(string json,Rule[] existing,bool replace,ITextConverter converter) {
  if(System.Text.Encoding.UTF8.GetByteCount(json)>2_000_000)throw new InvalidDataException("规则包过大");
  var p=JsonSerializer.Deserialize<RulePackage>(json)??throw new InvalidDataException("规则包为空");
  if(p.schema_version!=1||p.rules is null)throw new InvalidDataException("规则包版本不支持");
  var rules=replace?p.rules:existing.Concat(p.rules).ToArray();
  converter.Validate(new(){Rules=rules});return rules;
 }
 public static string Export(Rule[] rules)=>JsonSerializer.Serialize(new RulePackage(1,rules),new JsonSerializerOptions{WriteIndented=true});
}
