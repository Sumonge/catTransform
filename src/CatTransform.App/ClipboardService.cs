using CatTransform.Core;
using Microsoft.UI.Dispatching;
using Windows.ApplicationModel.DataTransfer;
namespace CatTransform.App;
public enum ListeningState { Off, Running, Paused }
public sealed class ClipboardService(ITextConverter converter,DispatcherQueue dispatcher) : IDisposable {
 public ListeningState State {get;private set;}
 public string Source {get;private set;}="";
 public string Result {get;private set;}="";
 public ConversionOptions Options {get;set;}=new();
 public event Action? Changed;
 public event Action<string>? Failed;
 private long revision;
 private string? ownWrite;
 public void Start(){if(State==ListeningState.Off)Clipboard.ContentChanged+=OnChanged;State=ListeningState.Running;Changed?.Invoke();}
 public void Pause(){if(State!=ListeningState.Off){State=ListeningState.Paused;Interlocked.Increment(ref revision);Changed?.Invoke();}}
 public void Resume(){if(State==ListeningState.Paused){State=ListeningState.Running;Changed?.Invoke();}}
 public void Stop(){Clipboard.ContentChanged-=OnChanged;State=ListeningState.Off;Interlocked.Increment(ref revision);Source=Result="";ownWrite=null;Changed?.Invoke();}
 private void OnChanged(object? sender,object e){dispatcher.TryEnqueue(async()=>{if(State==ListeningState.Running)await ReadAsync(false);});}
 public async Task ReadAsync(bool manual=true){
  if(!BackgroundPolicy.MayReadClipboard(State!=ListeningState.Off,State==ListeningState.Paused,manual))return;var version=Interlocked.Increment(ref revision);
  try{var content=Clipboard.GetContent();if(!content.Contains(StandardDataFormats.Text))return;var text=await content.GetTextAsync();if(text==ownWrite){ownWrite=null;return;}if(!manual&&text==Source)return;
   var options=Options;var converted=await Task.Run(()=>converter.Convert(text,options,"clipboard"));
   if(version!=revision||!manual&&State!=ListeningState.Running)return;Source=text;Result=converted;Changed?.Invoke();
  }catch(Exception e){if(manual)throw;Failed?.Invoke("剪贴板转换失败："+e.GetType().Name);}
 }
 public void Copy(string text){var data=new DataPackage();data.SetText(text);ownWrite=text;Clipboard.SetContent(data);Clipboard.Flush();}
 public void Dispose()=>Stop();
}
