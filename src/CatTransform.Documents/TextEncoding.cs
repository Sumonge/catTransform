using System.Text;
namespace CatTransform.Documents;
public sealed record EncodedText(string Text,Encoding Encoding,byte[] Bom) {
 public static async Task<EncodedText> ReadAsync(string path,CancellationToken ct) {
  var b=await File.ReadAllBytesAsync(path,ct);Encoding enc=new UTF8Encoding(false,true);int skip=0;
  if(b.AsSpan().StartsWith(new byte[]{0xff,0xfe,0,0})){enc=new UTF32Encoding(false,false,true);skip=4;}
  else if(b.AsSpan().StartsWith(new byte[]{0,0,0xfe,0xff})){enc=new UTF32Encoding(true,false,true);skip=4;}
  else if(b.AsSpan().StartsWith(new byte[]{0xef,0xbb,0xbf})){skip=3;}
  else if(b.AsSpan().StartsWith(new byte[]{0xff,0xfe})){enc=new UnicodeEncoding(false,false,true);skip=2;}
  else if(b.AsSpan().StartsWith(new byte[]{0xfe,0xff})){enc=new UnicodeEncoding(true,false,true);skip=2;}
  var text=enc.GetString(b,skip,b.Length-skip);
  if(text.Contains('\0'))throw new InvalidDataException("无法可靠识别文本编码；请先明确转换为 UTF-8 或带 BOM 的 Unicode");
  return new(text,enc,b[..skip]);
 }
 public async Task WriteAsync(string path,string text,CancellationToken ct) {
  await using var s=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);
  await s.WriteAsync(Bom,ct);await s.WriteAsync(Encoding.GetBytes(text),ct);await s.FlushAsync(ct);
 }
}
