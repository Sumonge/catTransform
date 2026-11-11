using CatTransform.Conversion;
using CatTransform.Core;
using CatTransform.Documents;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W=DocumentFormat.OpenXml.Wordprocessing;
using Xunit;
namespace CatTransform.Tests;
public sealed class AdvancedDocumentTests {
 [Fact]public async Task DocxHeadersAndCommentsRespectScope(){
  var dir=Path.Combine(Path.GetTempPath(),"catTransform-docx-"+Guid.NewGuid());Directory.CreateDirectory(dir);
  try{var source=Path.Combine(dir,"source.docx");
   using(var doc=WordprocessingDocument.Create(source,WordprocessingDocumentType.Document)){
    var main=doc.AddMainDocumentPart();var header=main.AddNewPart<HeaderPart>();header.Header=new W.Header(new W.Paragraph(new W.Run(new W.Text("页眉汉字"))));
    var comments=main.AddNewPart<WordprocessingCommentsPart>();comments.Comments=new W.Comments(new W.Comment(new W.Paragraph(new W.Run(new W.Text("批注汉字")))){Id="0",Author="test",Initials="t"});
    main.Document=new W.Document(new W.Body(new W.Paragraph(new W.CommentRangeStart{Id="0"},new W.Run(new W.Text("正文汉字")),new W.CommentRangeEnd{Id="0"},new W.Run(new W.CommentReference{Id="0"})),new W.SectionProperties(new W.HeaderReference{Id=main.GetIdOfPart(header),Type=W.HeaderFooterValues.Default})));
   }
   foreach(var advanced in new[]{false,true}){var output=Path.Combine(dir,advanced+".docx");await new DocumentConverter(new TextConverter()).ConvertAsync(source,output,new(){Scope=new(){DocxHeaders=advanced,DocxComments=advanced}},default);using var doc=WordprocessingDocument.Open(output,false);Assert.Contains("正文漢字",doc.MainDocumentPart!.Document.InnerText);Assert.Equal(advanced?"頁眉漢字":"页眉汉字",doc.MainDocumentPart.HeaderParts.Single().Header.InnerText);Assert.Equal(advanced?"批註漢字":"批注汉字",doc.MainDocumentPart.WordprocessingCommentsPart!.Comments.InnerText);}
  }finally{Directory.Delete(dir,true);}
 }
}
