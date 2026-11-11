using CatTransform.Application;
using CatTransform.Core;
using CatTransform.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
namespace CatTransform.App;
public sealed partial class WorkspaceViewModel : ObservableObject {
 public BatchService Batch {get;}
 public ITextConverter Text {get;}
 public PreviewService Preview {get;}
 public HistoryStore History {get;}
 private readonly SettingsStore store;
 public Settings Settings {get;private set;}
 private string input="",result="",status="就绪 · 完全离线";
 public string Input {get=>input;set=>SetProperty(ref input,value);}
 public string Result {get=>result;set=>SetProperty(ref result,value);}
 public string Status {get=>status;set=>SetProperty(ref status,value);}
 private CancellationTokenSource? debounce;
 private long revision;
 public WorkspaceViewModel(BatchService batch,ITextConverter text,PreviewService preview,HistoryStore history,SettingsStore store){Batch=batch;Text=text;Preview=preview;History=history;this.store=store;Settings=store.Load();}
 public void Update(Settings settings){Text.Validate(settings.Conversion);store.Save(settings);Settings=settings;}
 public async Task ConvertTextAsync(){
  var version=Interlocked.Increment(ref revision);debounce?.Cancel();debounce=new();var ct=debounce.Token;var source=Input;var options=Settings.Conversion;
  try{await Task.Delay(250,ct);var output=await Task.Run(()=>Text.Convert(source,options),ct);if(version==revision)Result=output;}
  catch(OperationCanceledException){}catch(Exception e){if(version==revision)Status="文本转换失败："+e.GetType().Name;}
 }
}
