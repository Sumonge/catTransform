using System.ComponentModel;
using System.Runtime.CompilerServices;
namespace CatTransform.Core;

public enum JobState { Waiting, Running, Succeeded, Failed, Cancelled, Skipped }
public enum RuleKind { Replace, Protect, Regex }
public enum RuleStage { Before, After }
public enum PdfOutputKind { Pdf, Text }
public sealed record Rule {
 public Guid Id {get;init;} = Guid.NewGuid();
 public string Name {get;init;} = "新规则";
 public bool Enabled {get;init;}
 public RuleKind Kind {get;init;}
 public RuleStage Stage {get;init;}
 public int Order {get;init;}
 public string Pattern {get;init;} = "";
 public string Replacement {get;init;} = "";
 public string? Config {get;init;}
 public string? Direction {get;init;}
 public string? Locale {get;init;}
 public string? Format {get;init;}
 public string? Region {get;init;}
 public bool AllowBacktracking {get;init;}
}
public sealed record Scope {
 public bool MarkdownCode {get;init;}
 public bool CsvHeader {get;init;}
 public string CsvDelimiter {get;init;} = ",";
 public int[] CsvInclude {get;init;} = [];
 public int[] CsvExclude {get;init;} = [];
 public bool JsonKeys {get;init;}
 public string[] JsonPaths {get;init;} = [];
 public bool DocxHeaders {get;init;}
 public bool DocxComments {get;init;}
 public bool DocxTextBoxes {get;init;}
 public bool EpubNavigation {get;init;}
 public bool EpubMetadata {get;init;}
}
public sealed record ConversionOptions {
 public string Config {get;init;} = "s2t";
 public Scope Scope {get;init;} = new();
 public Rule[] Rules {get;init;} = [];
 public int Concurrency {get;init;} = 2;
 public string OutputDirectory {get;init;} = "";
 public string? EpubCheckJar {get;init;}
 public PdfOutputKind PdfOutput {get;init;} = PdfOutputKind.Pdf;
 public string[] Formats {get;init;} = ["txt","md","srt","ass","csv","json","docx","epub","pdf"];
 public long MaxInputBytes {get;init;} = 128 * 1024 * 1024;
}
public sealed record Settings {
 public ConversionOptions Conversion {get;init;} = new();
 public string Theme {get;init;} = "Default";
 public string ClipboardConfig {get;init;} = "s2t";
 public bool Startup {get;init;}
 public bool SilentStartup {get;init;}
 public bool ListenAtStartup {get;init;}
 public uint HotkeyModifiers {get;init;} = 3;
 public uint HotkeyKey {get;init;} = 0x54;
}
public sealed class FileJob : INotifyPropertyChanged {
 public Guid Id {get;init;} = Guid.NewGuid();
 public string SourcePath {get;init;} = "";
 public string RelativePath {get;init;} = "";
 public string Name => Path.GetFileName(SourcePath);
 private JobState state;
 public JobState State {get=>state;set{state=value;Changed();Changed(nameof(Display));}}
 public string? OutputPath {get;set;}
 public string? Error {get;set;}
 public string Display => $"{Name}  ·  {State switch { JobState.Waiting=>"等待中",JobState.Running=>"转换中",JobState.Succeeded=>"成功",JobState.Failed=>"失败",JobState.Cancelled=>"已取消",_=>"已跳过" }}";
 public event PropertyChangedEventHandler? PropertyChanged;
 private void Changed([CallerMemberName]string? name=null)=>PropertyChanged?.Invoke(this,new(name));
}
public sealed record HistoryRecord(Guid BatchId,Guid FileId,DateTimeOffset Started,DateTimeOffset Finished,string Config,string RulesJson,JobState State,string? SourcePath,string? OutputPath,string? Error,string Version="0.2.0");
public sealed record TraceEntry(Guid RuleId,RuleStage Stage,bool Changed);
public interface ITextConverter {
 void Validate(ConversionOptions options);
 string Convert(string text,ConversionOptions options,string format="text",string region="body",ICollection<TraceEntry>? trace=null);
}
public interface IDocumentConverter {
 Task ConvertAsync(string source,string destination,ConversionOptions options,CancellationToken cancellationToken);
 Task<string> ExtractAsync(string source,ConversionOptions options,CancellationToken cancellationToken);
}
public interface IHistoryStore {
 Task SaveAsync(HistoryRecord record);
 Task<IReadOnlyList<HistoryRecord>> QueryAsync(string search="",int page=0,int pageSize=100,JobState? state=null);
 Task DeleteAsync(Guid fileId);
}
