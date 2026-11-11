using System.Text;
using System.Globalization;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Writer;

namespace CatTransform.Documents;

internal static class PdfText {
 public static string Extract(string path,CancellationToken ct) {
  using var document=PdfDocument.Open(path);
  var text=new StringBuilder();
  foreach(var page in document.GetPages()) {
   ct.ThrowIfCancellationRequested();
   var content=ContentOrderTextExtractor.GetText(page);
   if(text.Length+content.Length+1>4_000_000)throw new InvalidDataException("PDF 可提取文字超过转换上限");
   if(text.Length>0)text.Append('\f');
   text.Append(content);
  }
  if(string.IsNullOrWhiteSpace(text.ToString()))throw new InvalidDataException("PDF 没有可提取的文字；扫描件需要 OCR");
  return text.ToString();
 }

 public static async Task WritePdfAsync(string path,string text,CancellationToken ct) {
  if(text.Length>200_000)throw new InvalidDataException("PDF 重排版文字超过 20 万字符；请选择 TXT 输出");
  var fontPath=Path.Combine(AppContext.BaseDirectory,"Assets","Fonts","LXGWWenKaiMono-Light.ttf");
  if(!File.Exists(fontPath))throw new InvalidDataException("缺少 PDF 输出字体");
  var builder=new PdfDocumentBuilder();
  var font=builder.AddTrueTypeFont(await File.ReadAllBytesAsync(fontPath,ct));
  PdfPageBuilder page=builder.AddPage(PageSize.A4);
  int pageCount=1;
  double y=800;
  void NewPage(){if(++pageCount>1000)throw new InvalidDataException("PDF 重排版超过 1000 页；请选择 TXT 输出");page=builder.AddPage(PageSize.A4);y=800;}
  void NextLine(){y-=18;}

  var firstPage=true;
  foreach(var originalPage in text.Split('\f')) {
   if(!firstPage)NewPage();
   firstPage=false;
   foreach(var line in originalPage.Replace("\r\n","\n").Replace('\r','\n').Split('\n')) {
    ct.ThrowIfCancellationRequested();
    if(line.Length==0){NextLine();continue;}
    var positions=StringInfo.ParseCombiningCharacters(line);
    int element=0;
    while(element<positions.Length) {
     ct.ThrowIfCancellationRequested();
     int low=1,high=Math.Min(256,positions.Length-element),best=0;
     while(low<=high) {
      int middle=(low+high)/2;
      int end=element+middle==positions.Length?line.Length:positions[element+middle];
      var candidate=line[positions[element]..end];
      var width=page.MeasureText(candidate,12,new PdfPoint(0,0),font).Sum(letter=>letter.Width);
      if(width<=515){best=middle;low=middle+1;}else high=middle-1;
     }
     if(best==0)throw new InvalidDataException("PDF 字形无法在页面宽度内排版；请选择 TXT 输出");
     if(y<40)NewPage();
     int chunkEnd=element+best==positions.Length?line.Length:positions[element+best];
     page.AddText(line[positions[element]..chunkEnd],12,new PdfPoint(40,y),font);
     element+=best;
     NextLine();
    }
   }
  }

  var bytes=builder.Build();
  using(var pdf=PdfDocument.Open(bytes)) {
   var rendered=string.Concat(pdf.GetPages().Select(page=>ContentOrderTextExtractor.GetText(page)));
   static string NonWhitespace(string s)=>string.Concat(s.Where(c=>!char.IsWhiteSpace(c)));
   if(NonWhitespace(rendered)!=NonWhitespace(text))
    throw new InvalidDataException("PDF 字体无法完整呈现转换结果；请选择 TXT 输出");
  }
  ct.ThrowIfCancellationRequested();
  await File.WriteAllBytesAsync(path,bytes,ct);
 }
}
