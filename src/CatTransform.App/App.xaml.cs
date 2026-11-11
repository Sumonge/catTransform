using CatTransform.Application;
using CatTransform.Conversion;
using CatTransform.Core;
using CatTransform.Documents;
using CatTransform.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
namespace CatTransform.App;
public partial class App : Microsoft.UI.Xaml.Application {
 public static IServiceProvider Services {get;private set;}=null!;
 private MainWindow? window;
 public App(){TraceStartup("App constructor");UnhandledException+=(_,e)=>LogFailure(e.Exception);InitializeComponent();TraceStartup("Resources initialized");}
 internal static void TraceStartup(string stage){if(!Environment.GetCommandLineArgs().Contains("--diagnostic"))return;try{var dir=Path.Combine(UserPaths.Data,"logs");Directory.CreateDirectory(dir);File.AppendAllText(Path.Combine(dir,"startup.log"),$"{DateTimeOffset.UtcNow:O} {stage}\n");}catch{}}
 internal static void LogFailure(Exception error){try{var dir=Path.Combine(UserPaths.Data,"logs");Directory.CreateDirectory(dir);File.AppendAllText(Path.Combine(dir,"failures.log"),$"{DateTimeOffset.UtcNow:O} {error.GetType().FullName} 0x{error.HResult:X}\n{error.StackTrace}\n");}catch{}}
 protected override void OnLaunched(LaunchActivatedEventArgs args){
  try{
  TraceStartup("OnLaunched services");
  Services=new ServiceCollection().AddSingleton<ITextConverter,TextConverter>().AddSingleton<IDocumentConverter,DocumentConverter>().AddSingleton<HistoryStore>().AddSingleton<IHistoryStore>(s=>s.GetRequiredService<HistoryStore>()).AddSingleton<SettingsStore>().AddSingleton<BatchService>().AddSingleton<PreviewService>().AddSingleton<WorkspaceViewModel>().BuildServiceProvider();
  TraceStartup("Creating main window");window=new MainWindow(Services.GetRequiredService<WorkspaceViewModel>());TraceStartup("Activating main window");window.Activate();TraceStartup("Activated");window.ApplyStartup(Environment.GetCommandLineArgs().Contains("--tray"));
  }catch(Exception error){LogFailure(error);throw;}
 }
}
