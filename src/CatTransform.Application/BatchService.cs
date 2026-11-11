using System.Collections.ObjectModel;
using System.Text.Json;
using CatTransform.Core;
namespace CatTransform.Application;

public static class SafePaths {
 public static string Normalize(string path)=>Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
 public static bool IsWithin(string path,string parent)=>Normalize(path).Equals(Normalize(parent),StringComparison.OrdinalIgnoreCase)||Normalize(path).StartsWith(Normalize(parent)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
 public static void RejectLinks(string path){
  var full=Path.GetFullPath(path);
  for(string? current=full;!string.IsNullOrEmpty(current);current=Path.GetDirectoryName(current))
   if((File.Exists(current)||Directory.Exists(current))&&(File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new IOException("不处理符号链接或目录联接");
 }
}
public sealed class SafeOutput {
 public async Task<string> WriteAsync(string source,string relative,string output,Func<string,Task> write,CancellationToken ct){
  output=Path.GetFullPath(output);var desired=Path.GetFullPath(Path.Combine(output,relative));
  if(!SafePaths.IsWithin(desired,output)||Path.IsPathRooted(relative))throw new IOException("不安全的输出路径");
  SafePaths.RejectLinks(source);SafePaths.RejectLinks(desired);
  var dir=Path.GetDirectoryName(desired)!;Directory.CreateDirectory(dir);SafePaths.RejectLinks(dir);
  var temporary=Path.Combine(dir,".catTransform-"+Guid.NewGuid().ToString("N")+".tmp"+Path.GetExtension(desired));
  try{
   await write(temporary);ct.ThrowIfCancellationRequested();
   if(!File.Exists(temporary))throw new IOException("转换器没有产生结果");
   using(var stream=new FileStream(temporary,FileMode.Open,FileAccess.Write,FileShare.None))stream.Flush(true);
   for(int i=0;i<100000;i++){
    ct.ThrowIfCancellationRequested();SafePaths.RejectLinks(dir);
    var target=i==0?desired:Path.Combine(dir,Path.GetFileNameWithoutExtension(desired)+$" ({i})"+Path.GetExtension(desired));
    if(target.Equals(Path.GetFullPath(source),StringComparison.OrdinalIgnoreCase))continue;
    try{File.Move(temporary,target,false);return target;}catch(IOException)when(File.Exists(target)||Directory.Exists(target)){ }
   }
   throw new IOException("输出名称冲突过多");
  }finally{if(File.Exists(temporary))File.Delete(temporary);}
 }
}
public sealed class BatchService(ITextConverter text,IDocumentConverter documents,IHistoryStore history) {
 public ObservableCollection<FileJob> Jobs {get;}=[];
 public bool IsRunning {get;private set;}
 private readonly HashSet<string> paths=new(StringComparer.OrdinalIgnoreCase);
 private CancellationTokenSource? cancellation;
 public static readonly string[] Extensions=[".txt",".md",".srt",".ass",".csv",".json",".docx",".epub"];
 public int Add(IEnumerable<string> inputs,string output){int count=0;
  foreach(var path in inputs){SafePaths.RejectLinks(path);
   if(Directory.Exists(path)){var root=SafePaths.Normalize(path);if(SafePaths.IsWithin(root,output))continue;
    void Walk(string dir){foreach(var entry in Directory.EnumerateFileSystemEntries(dir)){
     if((File.GetAttributes(entry)&FileAttributes.ReparsePoint)!=0||SafePaths.IsWithin(entry,output))continue;
     if(Directory.Exists(entry))Walk(entry);else AddFile(entry,Path.Combine(Path.GetFileName(root),Path.GetRelativePath(root,entry)));
    }}Walk(root);
   }else AddFile(path,Path.GetFileName(path));
  }return count;
  void AddFile(string path,string relative){path=Path.GetFullPath(path);if(!File.Exists(path)||!Extensions.Contains(Path.GetExtension(path).ToLowerInvariant())||SafePaths.IsWithin(path,output)||!paths.Add(path))return;Jobs.Add(new(){SourcePath=path,RelativePath=relative});count++;}
 }
 public void Requeue(IEnumerable<FileJob> jobs){foreach(var job in jobs)if(job.State!=JobState.Running){job.State=JobState.Waiting;job.Error=null;job.OutputPath=null;}}
 public void Clear(){if(IsRunning)throw new InvalidOperationException("任务运行中");Jobs.Clear();paths.Clear();}
 public void Cancel()=>cancellation?.Cancel();
 public async Task RunAsync(ConversionOptions options,IProgress<(int Done,int Total)>? progress=null){
  if(IsRunning)throw new InvalidOperationException("批次已在运行");
  var frozen=JsonSerializer.Deserialize<ConversionOptions>(JsonSerializer.Serialize(options))!;text.Validate(frozen);
  if(string.IsNullOrWhiteSpace(frozen.OutputDirectory))throw new ArgumentException("输出目录为空");
  var pending=Jobs.Where(j=>j.State==JobState.Waiting).ToArray();if(pending.Length==0)return;
  var ui=SynchronizationContext.Current;
  Task OnUi(Action action){if(ui is null){action();return Task.CompletedTask;}var tcs=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);ui.Post(_=>{try{action();tcs.SetResult();}catch(Exception e){tcs.SetException(e);}},null);return tcs.Task;}
  IsRunning=true;cancellation=new();var ct=cancellation.Token;var batch=Guid.NewGuid();int done=0;var safe=new SafeOutput();
  using var gate=new SemaphoreSlim(frozen.Concurrency);
  try{await Task.WhenAll(pending.Select(async job=>{
   var started=DateTimeOffset.UtcNow;bool acquired=false;JobState state=JobState.Cancelled;string? output=null;string? error=null;
   try{ct.ThrowIfCancellationRequested();if(!frozen.Formats.Contains(Path.GetExtension(job.SourcePath).TrimStart('.').ToLowerInvariant())){state=JobState.Skipped;}else{await gate.WaitAsync(ct);acquired=true;await OnUi(()=>job.State=JobState.Running);
    output=await Task.Run(()=>safe.WriteAsync(job.SourcePath,job.RelativePath,frozen.OutputDirectory,p=>documents.ConvertAsync(job.SourcePath,p,frozen,ct),ct),ct);state=JobState.Succeeded;}
   }catch(OperationCanceledException){state=JobState.Cancelled;}
   catch(Exception e){state=JobState.Failed;error=SafeError(e);}
   finally{if(acquired)gate.Release();}
   var record=new HistoryRecord(batch,job.Id,started,DateTimeOffset.UtcNow,frozen.Config,JsonSerializer.Serialize(frozen.Rules),state,job.SourcePath,output,error);
   try{await history.SaveAsync(record);}catch(Exception e){error=(error is null?"":error+"；")+"历史写入失败："+e.GetType().Name;}
   await OnUi(()=>{job.OutputPath=output;job.Error=error;job.State=state;});progress?.Report((Interlocked.Increment(ref done),pending.Length));
  }));}finally{IsRunning=false;cancellation.Dispose();cancellation=null;}
 }
 // Parser exceptions may embed source text; never persist their raw messages.
 private static string SafeError(Exception e)=>e switch{
  System.Text.RegularExpressions.RegexMatchTimeoutException=>"规则执行超时",
  UnauthorizedAccessException=>"文件访问权限不足",
  System.Text.DecoderFallbackException=>"无法可靠识别编码",
  InvalidDataException=>"文档验证或转换范围检查失败，请检查格式与配置",
  IOException=>"文件读写失败，请检查空间、占用和路径",
  _=>"转换失败："+e.GetType().Name};
}
