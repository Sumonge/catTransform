using System.Globalization;

namespace CatTransform.Documents;

internal static class SegmentedText {
 public static string[] Convert(IReadOnlyList<string> source,Func<string,string> convert,string format) {
  if(source.Count==0)return [];
  var joined=string.Concat(source);
  if(joined.Length==0)return source.ToArray();
  var whole=convert(joined);
  if(source.Count==1)return [whole];

  // If each segment can be converted independently, preserve its exact styling.
  var separate=source.Select(convert).ToArray();
  if(string.Concat(separate)==whole)return separate;

  // A cross-segment change can be mapped by position only when text-element counts agree.
  var sourceCounts=source.Select(s=>StringInfo.ParseCombiningCharacters(s).Length).ToArray();
  var joinedCount=StringInfo.ParseCombiningCharacters(joined).Length;
  var positions=StringInfo.ParseCombiningCharacters(whole);
  if(sourceCounts.Sum()!=joinedCount||positions.Length!=joinedCount)
   throw new InvalidDataException($"{format} 跨格式片段的转换改变字符数量，无法可靠保留原格式");
  var result=new string[source.Count];
  int element=0;
  for(int i=0;i<source.Count;i++) {
   var start=element==positions.Length?whole.Length:positions[element];
   element+=sourceCounts[i];
   var end=element==positions.Length?whole.Length:positions[element];
   result[i]=whole[start..end];
  }
  return result;
 }
}
