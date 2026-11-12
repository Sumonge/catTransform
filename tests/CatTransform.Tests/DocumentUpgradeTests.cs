using CatTransform.Application;
using CatTransform.Conversion;
using CatTransform.Core;
using CatTransform.Documents;
using CatTransform.Infrastructure;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using W=DocumentFormat.OpenXml.Wordprocessing;
using Xunit;

namespace CatTransform.Tests;

public sealed class DocumentUpgradeTests : IDisposable {
 private readonly string dir=Path.Combine(Path.GetTempPath(),"catTransform-upgrade-"+Guid.NewGuid());
 public DocumentUpgradeTests()=>Directory.CreateDirectory(dir);
 public void Dispose(){Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Directory.Delete(dir,true);}

 [Fact] public void PdfPigEmbeddedFontCanRoundTripChinese() {
  var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../"));
  var bytes=File.ReadAllBytes(Path.Combine(root,"LXGWWenKaiMono-Light.ttf"));
  var builder=new PdfDocumentBuilder();
  var font=builder.AddTrueTypeFont(bytes);
  builder.AddPage(PageSize.A4).AddText("漢字",14,new PdfPoint(25,700),font);
  var path=Path.Combine(dir,"chinese.pdf");
  File.WriteAllBytes(path,builder.Build());
  using var document=PdfDocument.Open(path);
  Assert.Contains("漢字",ContentOrderTextExtractor.GetText(document.GetPage(1)));
 }

 [Fact] public async Task PdfTextConversionUsesTxtOutputAndKeepsSource() {
  var source=Path.Combine(dir,"sample.pdf");
  var builder=new PdfDocumentBuilder();
  var font=builder.AddStandard14Font(Standard14Font.Helvetica);
  builder.AddPage(PageSize.A4).AddText("software",12,new PdfPoint(25,700),font);
  var original=builder.Build();
  await File.WriteAllBytesAsync(source,original);
  var text=new TextConverter();
  var documents=new DocumentConverter(text);
  var options=new ConversionOptions{OutputDirectory=Path.Combine(dir,"output"),PdfOutput=PdfOutputKind.Text,Rules=[new(){Enabled=true,Pattern="software",Replacement="软件",Stage=RuleStage.Before}]};
  var batch=new BatchService(text,documents,new HistoryStore(Path.Combine(dir,"history")));
  batch.Add([source],options.OutputDirectory);
  await batch.RunAsync(options);
  var job=Assert.Single(batch.Jobs);
  Assert.Equal(JobState.Succeeded,job.State);
  Assert.Equal("sample.txt",Path.GetFileName(job.OutputPath));
  Assert.Contains("軟件",await File.ReadAllTextAsync(job.OutputPath!));
  Assert.Equal(original,await File.ReadAllBytesAsync(source));
  var preview=await new PreviewService(documents).CreateAsync(source,options,default);
  Assert.Contains("software",preview.Original);
  Assert.Contains("軟件",preview.Converted);
 }

 [Fact] public async Task PdfReflowProducesSearchableChinesePdf() {
  var source=Path.Combine(dir,"reflow.pdf");
  var builder=new PdfDocumentBuilder();
  var font=builder.AddStandard14Font(Standard14Font.Helvetica);
  builder.AddPage(PageSize.A4).AddText("software",12,new PdfPoint(25,700),font);
  await File.WriteAllBytesAsync(source,builder.Build());
  var text=new TextConverter();
  var documents=new DocumentConverter(text);
  var options=new ConversionOptions{OutputDirectory=Path.Combine(dir,"pdf-output"),Rules=[new(){Enabled=true,Pattern="software",Replacement="软件",Stage=RuleStage.Before}]};
  var batch=new BatchService(text,documents,new HistoryStore(Path.Combine(dir,"pdf-history")));
  batch.Add([source],options.OutputDirectory);
  await batch.RunAsync(options);
  var output=Assert.Single(batch.Jobs).OutputPath;
  Assert.Equal("reflow.pdf",Path.GetFileName(output));
  using var pdf=PdfDocument.Open(output!);
  Assert.Contains("軟件",ContentOrderTextExtractor.GetText(pdf.GetPage(1)));
  var preview=await new PreviewService(documents).CreateAsync(source,options,default);
  Assert.Contains("軟件",preview.Converted);
 }

 [Fact] public async Task PdfWithoutTextFailsBeforeCreatingOutput() {
  var source=Path.Combine(dir,"scan.pdf");
  var builder=new PdfDocumentBuilder();
  builder.AddPage(PageSize.A4);
  await File.WriteAllBytesAsync(source,builder.Build());
  var output=Path.Combine(dir,"scan.txt");
  await Assert.ThrowsAsync<InvalidDataException>(()=>new DocumentConverter(new TextConverter()).ConvertAsync(source,output,new(),default));
  Assert.False(File.Exists(output));
 }

 [Fact] public async Task PdfReflowWrapsLongTextWithoutLoss() {
  var source=Path.Combine(dir,"long.pdf");
  var builder=new PdfDocumentBuilder();
  var font=builder.AddStandard14Font(Standard14Font.Helvetica);
  builder.AddPage(PageSize.A4).AddText(new string('a',400),12,new PdfPoint(25,700),font);
  await File.WriteAllBytesAsync(source,builder.Build());
  var output=Path.Combine(dir,"long-out.pdf");
  await new DocumentConverter(new TextConverter()).ConvertAsync(source,output,new(),default);
  using var pdf=PdfDocument.Open(output);
  var extracted=string.Concat(pdf.GetPages().Select(page=>ContentOrderTextExtractor.GetText(page)));
  Assert.Equal(new string('a',400),string.Concat(extracted.Where(c=>!char.IsWhiteSpace(c))));
 }

 [Fact] public async Task DocxLengthChangeWithinOneRunKeepsOtherRunAndStyle() {
  var source=Path.Combine(dir,"source.docx");
  using(var doc=WordprocessingDocument.Create(source,WordprocessingDocumentType.Document)){
   var main=doc.AddMainDocumentPart();
   main.Document=new W.Document(new W.Body(new W.Paragraph(
    new W.Run(new W.RunProperties(new W.Bold()),new W.Text("software")),
    new W.Run(new W.Text("汉字")))));
  }
  var destination=Path.Combine(dir,"output.docx");
  var options=new ConversionOptions{Rules=[new(){Enabled=true,Pattern="software",Replacement="软件",Stage=RuleStage.Before}]};
  await new DocumentConverter(new TextConverter()).ConvertAsync(source,destination,options,default);
  using var result=WordprocessingDocument.Open(destination,false);
  var runs=result.MainDocumentPart!.Document.Descendants<W.Run>().ToArray();
  Assert.Equal("軟件",runs[0].InnerText);
  Assert.Equal("漢字",runs[1].InnerText);
  Assert.NotNull(runs[0].RunProperties?.Bold);
  Assert.Null(runs[1].RunProperties?.Bold);
 }

 [Fact] public async Task DocxCrossRunLengthChangeStillFailsSafely() {
  var source=Path.Combine(dir,"split.docx");
  using(var doc=WordprocessingDocument.Create(source,WordprocessingDocumentType.Document)){
   var main=doc.AddMainDocumentPart();
   main.Document=new W.Document(new W.Body(new W.Paragraph(
    new W.Run(new W.RunProperties(new W.Bold()),new W.Text("soft")),
    new W.Run(new W.Text("ware")))));
  }
  var options=new ConversionOptions{Rules=[new(){Enabled=true,Pattern="software",Replacement="软件",Stage=RuleStage.Before}]};
  await Assert.ThrowsAsync<InvalidDataException>(()=>new DocumentConverter(new TextConverter()).ConvertAsync(source,Path.Combine(dir,"split-out.docx"),options,default));
  using var original=WordprocessingDocument.Open(source,false);
  Assert.Equal("software",original.MainDocumentPart!.Document.InnerText);
 }
}
