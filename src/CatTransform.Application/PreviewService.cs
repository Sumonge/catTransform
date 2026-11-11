using System.Globalization;
using CatTransform.Core;
namespace CatTransform.Application;
public sealed record Preview(string Original,string Converted);
public sealed class PreviewService(IDocumentConverter documents) {
 public async Task<Preview> CreateAsync(string path,ConversionOptions options,CancellationToken ct){
  var temp=Path.Combine(Path.GetTempPath(),"catTransform-preview-"+Guid.NewGuid()+Path.GetExtension(path));
  try{await documents.ConvertAsync(path,temp,options,ct);return new(Sample(await documents.ExtractAsync(path,options,ct)),Sample(await documents.ExtractAsync(temp,options,ct)));}
  finally{if(File.Exists(temp))File.Delete(temp);}
 }
 public static string Sample(string text){var indices=StringInfo.ParseCombiningCharacters(text);if(indices.Length<=9000)return text;
  string Segment(int start){int end=Math.Min(start+3000,indices.Length);return text[indices[start]..(end==indices.Length?text.Length:indices[end])];}
  return Segment(0)+"\n\n……中段……\n\n"+Segment((indices.Length-3000)/2)+"\n\n……结尾……\n\n"+Segment(indices.Length-3000);
 }
}
