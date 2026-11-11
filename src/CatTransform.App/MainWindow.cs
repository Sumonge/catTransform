using System.Diagnostics;
using System.Text;
using System.Text.Json;
using CatTransform.Application;
using CatTransform.Conversion;
using CatTransform.Core;
using CatTransform.Infrastructure;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
namespace CatTransform.App;

public sealed class MainWindow : Window {
 private readonly WorkspaceViewModel vm;
 private readonly NavigationView nav=new(){IsSettingsVisible=false,IsBackButtonVisible=NavigationViewBackButtonVisible.Collapsed,PaneDisplayMode=NavigationViewPaneDisplayMode.Left,OpenPaneLength=240,IsPaneOpen=true};
 private readonly Dictionary<int,NavigationViewItem> navigationItems=[];
 private readonly Grid root=new();
 private readonly SplitView previewDrawer=new(){PanePlacement=SplitViewPanePlacement.Right,DisplayMode=SplitViewDisplayMode.Overlay,OpenPaneLength=720};
 private readonly TextBlock message=new(){TextWrapping=TextWrapping.Wrap};
 private readonly Dictionary<int,UIElement> pages=[];
 private readonly ClipboardService clipboard;
 private readonly NativeTray tray;
 private readonly nint hwnd;
 private readonly WindowDpi dpiWindow;
 private bool quitting,standby,notified;
 private int historyPage;
 private readonly List<ComboBox> globalDirections=[];
 private bool syncingDirections;
 public MainWindow(WorkspaceViewModel vm){
  App.TraceStartup("MainWindow constructor");
  this.vm=vm;Title="catTransform";SystemBackdrop=new MicaBackdrop();
  hwnd=WinRT.Interop.WindowNative.GetWindowHandle(this);
  dpiWindow=new(hwnd,AppWindow);dpiWindow.InitializeSize();
  AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory,"Assets","catTransform.ico"));
  App.TraceStartup("Icon set");
  root.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});root.RowDefinitions.Add(new(){Height=GridLength.Auto});previewDrawer.Content=nav;root.Children.Add(previewDrawer);Grid.SetRow(message,1);message.Margin=new(16,6,16,8);root.Children.Add(message);Content=root;
  root.SizeChanged+=(_,_)=>previewDrawer.OpenPaneLength=Math.Min(720,root.ActualWidth);
  var brand=Stack(Label("catTransform",22),Label("简繁转换 · 离线处理"));brand.Margin=new(16,12,12,20);nav.PaneHeader=brand;
  nav.MenuItems.Add(new NavigationViewItemHeader{Content="转换工具"});
  AddNavigation(0,"批量转换",Symbol.Library);AddNavigation(1,"文本转换",Symbol.Edit);AddNavigation(2,"剪贴板",Symbol.Paste);
  nav.MenuItems.Add(new NavigationViewItemSeparator());nav.MenuItems.Add(new NavigationViewItemHeader{Content="记录与配置"});
  AddNavigation(3,"历史记录",Symbol.Clock);AddNavigation(4,"设置",Symbol.Setting);
  nav.SelectionChanged+=(sender,e)=>{try{App.TraceStartup("Navigation selection");if(e.SelectedItem is NavigationViewItem item)Navigate((int)item.Tag);App.TraceStartup("Navigation completed");}catch(Exception error){App.LogFailure(error);_=Error(error);}};
  clipboard=new(vm.Text,DispatcherQueue){Options=vm.Settings.Conversion with{Config=vm.Settings.ClipboardConfig}};
  clipboard.Failed+=text=>message.Text=text;
  tray=new(hwnd);tray.Command+=id=>{switch(id){case 1:tray.Show();break;case 2:tray.Show();nav.SelectedItem=navigationItems[2];break;case 3:if(clipboard.State==ListeningState.Running)clipboard.Pause();else clipboard.Resume();break;case 4:tray.Show();nav.SelectedItem=navigationItems[4];break;case 5:_=ExitAsync();break;}};
  if(!tray.SetHotkey(vm.Settings.HotkeyModifiers,vm.Settings.HotkeyKey))message.Text="全局暂停快捷键已被其他程序占用";
  AppWindow.Closing+=(sender,e)=>{if(quitting)return;if(BackgroundPolicy.HideOnClose(clipboard.State!=ListeningState.Off,standby)){e.Cancel=true;tray.Hide();if(!notified){tray.Notify();notified=true;}}else if(vm.Batch.IsRunning){e.Cancel=true;_=ExitAsync();}else Cleanup();};
  vm.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(vm.Status))message.Text=vm.Status;};
  root.RequestedTheme=Enum.TryParse<ElementTheme>(vm.Settings.Theme,out var theme)?theme:ElementTheme.Default;
  nav.Loaded+=(_,_)=>{App.TraceStartup("Navigation loaded");if(nav.SelectedItem is null)nav.SelectedItem=navigationItems[0];};
  App.TraceStartup("MainWindow constructed");
 }
 private void AddNavigation(int page,string title,Symbol icon){var item=new NavigationViewItem{Content=title,Tag=page,Icon=new SymbolIcon(icon)};navigationItems.Add(page,item);nav.MenuItems.Add(item);}
 public void ApplyStartup(bool silent){standby=silent&&vm.Settings.SilentStartup;if(standby)tray.Hide();if(vm.Settings.ListenAtStartup)clipboard.Start();}
 private void Cleanup(){clipboard.Dispose();tray.Dispose();dpiWindow.Dispose();}
 private async Task ExitAsync(){if(vm.Batch.IsRunning){if(!await Confirm("退出 catTransform","正在转换。取消未完成任务并退出？"))return;vm.Batch.Cancel();while(vm.Batch.IsRunning)await Task.Delay(100);}quitting=true;Cleanup();Close();}
 private void Navigate(int page){if(page==3||page==4)pages.Remove(page);if(!pages.TryGetValue(page,out var content)){content=page switch{0=>BatchPage(),1=>TextPage(),2=>ClipboardPage(),3=>HistoryPage(),_=>SettingsPage()};pages[page]=content;}nav.Content=content;}
 private static StackPanel Stack(params UIElement[] children){var panel=new StackPanel{Spacing=12};foreach(var child in children)panel.Children.Add(child);return panel;}
 private static StackPanel Row(params UIElement[] children){var panel=Stack(children);panel.Orientation=Orientation.Horizontal;return panel;}
 private static TextBlock Label(string text,int size=14)=>new(){Text=text,FontSize=size,TextWrapping=TextWrapping.Wrap};
 private static TextBox Editor(string text="",bool readOnly=false)=>new(){Text=text,IsReadOnly=readOnly,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MinHeight=180,MaxHeight=420};
 private static ScrollViewer Page(string title,params UIElement[] content){var stack=Stack(Label(title,28));stack.Margin=new(24);foreach(var c in content)stack.Children.Add(c);return OverflowPage(stack);}
 private static ScrollViewer OverflowPage(FrameworkElement content){
  var scroll=new ScrollViewer{Content=content,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollMode=ScrollMode.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
  // 840 DIPs includes page margins. It is an overflow extent, not a fixed window size.
  void FitWidth()=>content.Width=Math.Max(840,scroll.ActualWidth)-content.Margin.Left-content.Margin.Right;
  scroll.SizeChanged+=(_,_)=>FitWidth();FitWidth();return scroll;
 }
 private Button Button(string label,Func<Task> action){var b=new Button{Content=label};b.Click+=async(_,_)=>{b.IsEnabled=false;try{await action();}catch(Exception e){await Error(e);}finally{b.IsEnabled=true;}};return b;}
 private Button Button(string label,Action action)=>Button(label,()=>{action();return Task.CompletedTask;});
 private async Task Error(Exception e){message.Text="操作失败："+e.GetType().Name;await new ContentDialog{XamlRoot=root.XamlRoot,Title="操作未完成",Content=e is ArgumentException or InvalidDataException or InvalidOperationException?e.Message:"请检查文件权限、占用、格式或磁盘空间。错误类型："+e.GetType().Name,CloseButtonText="知道了"}.ShowAsync();}
 private async Task<bool> Confirm(string title,string text)=>await new ContentDialog{XamlRoot=root.XamlRoot,Title=title,Content=text,PrimaryButtonText="确认",CloseButtonText="取消",DefaultButton=ContentDialogButton.Close}.ShowAsync()==ContentDialogResult.Primary;
 private ComboBox Direction(string selected,Action<string> changed,bool global=true){var combo=new ComboBox{ItemsSource=new[]{"简体 → 繁体（标准）","繁体 → 简体（标准）","简体 → 台湾繁体","简体 → 台湾词汇","简体 → 香港繁体","台湾繁体 → 简体","台湾词汇 → 大陆词汇","香港繁体 → 简体"},SelectedIndex=Array.IndexOf(TextConverter.Configurations,selected),MinWidth=200};if(global)globalDirections.Add(combo);combo.SelectionChanged+=(_,_)=>{try{if(!syncingDirections&&combo.SelectedIndex>=0)changed(TextConverter.Configurations[combo.SelectedIndex]);}catch(Exception e){_=Error(e);}};return combo;}
 private void Update(ConversionOptions options){vm.Update(vm.Settings with{Conversion=options});clipboard.Options=options with{Config=vm.Settings.ClipboardConfig};syncingDirections=true;try{foreach(var combo in globalDirections)combo.SelectedIndex=Array.IndexOf(TextConverter.Configurations,options.Config);}finally{syncingDirections=false;}}
 private async Task<string[]> PickFiles(string extension="*"){var picker=new FileOpenPicker();picker.FileTypeFilter.Add(extension);WinRT.Interop.InitializeWithWindow.Initialize(picker,hwnd);return (await picker.PickMultipleFilesAsync()).Select(f=>f.Path).ToArray();}
 private async Task<string?> PickFolder(){var picker=new FolderPicker();picker.FileTypeFilter.Add("*");WinRT.Interop.InitializeWithWindow.Initialize(picker,hwnd);return (await picker.PickSingleFolderAsync())?.Path;}
 private async Task<string?> SaveFile(string name,string extension){var picker=new FileSavePicker{SuggestedFileName=name};picker.FileTypeChoices.Add(extension,[extension]);WinRT.Interop.InitializeWithWindow.Initialize(picker,hwnd);return (await picker.PickSaveFileAsync())?.Path;}
 private UIElement BatchPage(){
  var list=new ListView{ItemsSource=vm.Batch.Jobs,DisplayMemberPath="Display",SelectionMode=ListViewSelectionMode.Extended,MinHeight=260,MaxHeight=420};
  var progress=new ProgressBar{Minimum=0,Maximum=1,Value=0};var summary=Label("将文件或文件夹拖到队列中；点击开始后才会转换。");
  var search=new TextBox{PlaceholderText="按文件名筛选",Width=200};search.TextChanged+=(_,_)=>list.ItemsSource=vm.Batch.Jobs.Where(j=>j.Name.Contains(search.Text,StringComparison.OrdinalIgnoreCase)).ToArray();
  var sort=new ComboBox{ItemsSource=new[]{"加入顺序","文件名","状态"},SelectedIndex=0};sort.SelectionChanged+=(_,_)=>list.ItemsSource=sort.SelectedIndex switch{1=>vm.Batch.Jobs.OrderBy(j=>j.Name).ToArray(),2=>vm.Batch.Jobs.OrderBy(j=>j.State).ToArray(),_=>vm.Batch.Jobs};
  async Task Preview(){if(list.SelectedItem is FileJob j)await ShowPreview(j.SourcePath);}
  var add=Button("添加文件",async()=>{vm.Batch.Add(await PickFiles(),vm.Settings.Conversion.OutputDirectory);list.ItemsSource=vm.Batch.Jobs;});
  var folder=Button("添加文件夹",async()=>{var p=await PickFolder();if(p is not null)vm.Batch.Add([p],vm.Settings.Conversion.OutputDirectory);list.ItemsSource=vm.Batch.Jobs;});
  list.AllowDrop=true;list.DragOver+=(_,e)=>{if(e.DataView.Contains(StandardDataFormats.StorageItems))e.AcceptedOperation=DataPackageOperation.Copy;};
  list.Drop+=async(_,e)=>{try{vm.Batch.Add((await e.DataView.GetStorageItemsAsync()).Select(i=>i.Path),vm.Settings.Conversion.OutputDirectory);list.ItemsSource=vm.Batch.Jobs;}catch(Exception ex){await Error(ex);}};
  var start=Button("开始转换",async()=>{var p=new Progress<(int Done,int Total)>(value=>{progress.Maximum=value.Total;progress.Value=value.Done;summary.Text=$"已完成 {value.Done} / {value.Total}";});await vm.Batch.RunAsync(vm.Settings.Conversion,p);summary.Text=$"完成 · 成功 {vm.Batch.Jobs.Count(j=>j.State==JobState.Succeeded)} · 失败 {vm.Batch.Jobs.Count(j=>j.State==JobState.Failed)} · 跳过 {vm.Batch.Jobs.Count(j=>j.State==JobState.Skipped)} · 取消 {vm.Batch.Jobs.Count(j=>j.State==JobState.Cancelled)}";});
  var page=Page("批量转换",Label("保留原件，安全生成新文件。支持 TXT、MD、SRT、ASS、CSV、JSON、DOCX、EPUB。"),Row(add,folder,Direction(vm.Settings.Conversion.Config,c=>Update(vm.Settings.Conversion with{Config=c}))),Row(search,sort,Button("预览",Preview),Button("详情",async()=>{if(list.SelectedItem is FileJob j)await new ContentDialog{XamlRoot=root.XamlRoot,Title=j.Name,Content=$"源：{j.SourcePath}\n输出：{j.OutputPath}\n{j.Display}\n{j.Error}",CloseButtonText="关闭"}.ShowAsync();})),list,progress,summary,Row(start,Button("取消",()=>vm.Batch.Cancel()),Button("失败重试",()=>vm.Batch.Requeue(vm.Batch.Jobs.Where(j=>j.State==JobState.Failed))),Button("新建任务",()=>vm.Batch.Clear()),Button("输出目录",()=>{Directory.CreateDirectory(vm.Settings.Conversion.OutputDirectory);Process.Start(new ProcessStartInfo(vm.Settings.Conversion.OutputDirectory){UseShellExecute=true});})),Button("选中项重新排队",()=>vm.Batch.Requeue(list.SelectedItems.Cast<FileJob>().ToArray())));
  return FixedQueue(page,list);
 }
 private static UIElement FixedQueue(ScrollViewer page,ListView list){
  var original=(StackPanel)page.Content;var children=original.Children.ToArray();original.Children.Clear();
  var grid=new Grid{Margin=new(24),RowSpacing=12};grid.RowDefinitions.Add(new(){Height=GridLength.Auto});grid.RowDefinitions.Add(new());grid.RowDefinitions.Add(new(){Height=GridLength.Auto});
  var top=Stack(children.Take(4).ToArray());var bottom=Stack(children.Skip(5).ToArray());
  list.MinHeight=100;list.MaxHeight=double.PositiveInfinity;list.ItemContainerStyle=new Style{TargetType=typeof(ListViewItem),Setters={new Setter(FrameworkElement.MinHeightProperty,44d)}};
  grid.Children.Add(top);Grid.SetRow(list,1);grid.Children.Add(list);Grid.SetRow(bottom,2);grid.Children.Add(bottom);
  var viewport=OverflowPage(grid);
  void FitHeight(){grid.Height=Math.Max(600,viewport.ActualHeight-48);}
  grid.Height=600;viewport.SizeChanged+=(_,_)=>FitHeight();return viewport;
 }
 private async Task ShowPreview(string path){var preview=await Task.Run(()=>vm.Preview.CreateAsync(path,vm.Settings.Conversion,CancellationToken.None));
  var differences=new StackPanel{Spacing=4};var before=preview.Original.Split('\n');var after=preview.Converted.Split('\n');
  for(int i=0;i<Math.Max(before.Length,after.Length);i++){var original=i<before.Length?before[i]:"";var converted=i<after.Length?after[i]:"";if(original==converted)continue;differences.Children.Add(new TextBlock{Text=$"第 {i+1} 行\n− {original}\n+ {converted}",TextWrapping=TextWrapping.Wrap,Foreground=new SolidColorBrush(Microsoft.UI.Colors.DarkOrange)});}
  if(differences.Children.Count==0)differences.Children.Add(Label("文本没有差异"));
  var panel=Stack(Row(Label("只读文本预览",24),Button("关闭",()=>previewDrawer.IsPaneOpen=false)),Label(Path.GetFileName(path)),Label("原文"),Editor(preview.Original,true),Label("转换结果"),Editor(preview.Converted,true),Label("差异行",18),differences);panel.Margin=new(20);previewDrawer.Pane=new ScrollViewer{Content=panel};previewDrawer.IsPaneOpen=true;
 }
 private UIElement TextPage(){
  var input=Editor(vm.Input);var output=Editor(vm.Result,true);input.MinHeight=330;output.MinHeight=330;
  var count=Label("0 字符");input.TextChanged+=async(_,_)=>{vm.Input=input.Text;count.Text=$"原文 {input.Text.EnumerateRunes().Count()} 字符";await vm.ConvertTextAsync();output.Text=vm.Result;};
  var columns=new Grid{ColumnSpacing=16};columns.ColumnDefinitions.Add(new());columns.ColumnDefinitions.Add(new());columns.Children.Add(input);Grid.SetColumn(output,1);columns.Children.Add(output);
  var wrap=new ToggleSwitch{Header="自动换行",IsOn=true};wrap.Toggled+=(_,_)=>input.TextWrapping=output.TextWrapping=wrap.IsOn?TextWrapping.Wrap:TextWrapping.NoWrap;
  return Page("文本转换",Row(Direction(vm.Settings.Conversion.Config,c=>{Update(vm.Settings.Conversion with{Config=c});_=Refresh();}),Button("交换",()=>{input.Text=output.Text;}),Button("清空",()=>{input.Text="";output.Text="";}),Button("复制结果",()=>clipboard.Copy(output.Text))),columns,Row(count,wrap,Button("保存 TXT",()=>Save(".txt")),Button("保存 MD",()=>Save(".md"))),Label("正文仅保留在当前会话，不建立文本历史。"));
  async Task Refresh(){await vm.ConvertTextAsync();output.Text=vm.Result;}
  async Task Save(string ext){var path=await SaveFile("转换结果",ext);if(path is not null)await File.WriteAllTextAsync(path,output.Text,new UTF8Encoding(true));}
 }
 private UIElement ClipboardPage(){
  var input=Editor("",true);var output=Editor("",true);var state=Label("");var hide=new ToggleSwitch{Header="隐藏预览内容"};
  void Refresh(){state.Text="监听状态："+(clipboard.State switch{ListeningState.Off=>"已关闭",ListeningState.Running=>"运行中",_=>"已暂停"});input.Text=hide.IsOn?"":clipboard.Source;output.Text=hide.IsOn?"":clipboard.Result;}
  clipboard.Changed+=Refresh;hide.Toggled+=(_,_)=>Refresh();Refresh();
  return Page("剪贴板",Label("监听默认关闭。仅在软件内显示结果，绝不自动写回系统剪贴板。"),Row(Direction(vm.Settings.ClipboardConfig,c=>{vm.Update(vm.Settings with{ClipboardConfig=c});clipboard.Options=vm.Settings.Conversion with{Config=c};},false),Button("读取并转换",()=>clipboard.ReadAsync())),Row(Button("开启监听",async()=>{if(await Confirm("开启剪贴板监听","允许 catTransform 读取后续纯文本剪贴板变化？正文不会保存或上传。"))clipboard.Start();}),Button("暂停 / 恢复",()=>{if(clipboard.State==ListeningState.Running)clipboard.Pause();else clipboard.Resume();}),Button("停止并清空",()=>clipboard.Stop()),Button("复制结果",()=>clipboard.Copy(clipboard.Result))),state,hide,input,output);
 }
 private UIElement HistoryPage(){
  var filter=new ComboBox{ItemsSource=new[]{"全部状态","等待中","转换中","成功","失败","已取消","已跳过"},SelectedIndex=0};
  var search=new TextBox{PlaceholderText="搜索路径、转换配置或批次 UUID",Width=280};var list=new ListView{MinHeight=260,MaxHeight=400,SelectionMode=ListViewSelectionMode.Single};var rows=new List<HistoryRecord>();
  int queryVersion=0;
  async Task Refresh(){int version=++queryVersion;try{var fetched=(await vm.History.QueryAsync(search.Text,historyPage,100,filter.SelectedIndex==0?null:(JobState)(filter.SelectedIndex-1))).ToList();if(version!=queryVersion)return;rows=fetched;list.ItemsSource=rows.Select(r=>$"{r.Finished.LocalDateTime:g} · {r.State} · {Path.GetFileName(r.SourcePath)} · {r.Config}").ToArray();}catch(Exception e){await Error(e);}}
  filter.SelectionChanged+=async(_,_)=>{historyPage=0;await Refresh();};
  HistoryRecord? Selected()=>list.SelectedIndex>=0&&list.SelectedIndex<rows.Count?rows[list.SelectedIndex]:null;
  _=Refresh();var paths=new CheckBox{Content="导出包含完整路径",IsChecked=false};
  var batchActions=Row(Button("批次详情",async()=>{if(Selected() is not {} selected)return;int page=0;var detail=new ListView{MinHeight=240,MaxHeight=360};async Task Load(){detail.ItemsSource=(await vm.History.QueryAsync(selected.BatchId.ToString(),page)).Where(r=>r.BatchId==selected.BatchId).Select(r=>$"{Path.GetFileName(r.SourcePath)} · {r.State} · {r.Config}").ToArray();}await Load();var panel=Stack(Label("批次 "+selected.BatchId),detail,Row(Button("上一页",async()=>{page=Math.Max(0,page-1);await Load();}),Button("下一页",async()=>{page++;await Load();})));await new ContentDialog{XamlRoot=root.XamlRoot,Title="批次详情",Content=panel,CloseButtonText="关闭"}.ShowAsync();}),Button("本页失败重试",()=>{foreach(var r in rows.Where(r=>r.State==JobState.Failed&&r.SourcePath is not null)){vm.Batch.Add([r.SourcePath!],vm.Settings.Conversion.OutputDirectory);vm.Batch.Requeue(vm.Batch.Jobs.Where(j=>j.SourcePath.Equals(r.SourcePath,StringComparison.OrdinalIgnoreCase)));}message.Text="失败记录已加入队列，请手动开始。";}));
  return Page("历史记录",Label("仅保存批量转换元数据。删除历史不会删除文件。"),Row(search,filter,Button("搜索",async()=>{historyPage=0;await Refresh();})),list,Row(Button("上一页",async()=>{historyPage=Math.Max(0,historyPage-1);await Refresh();}),Button("下一页",async()=>{historyPage++;await Refresh();}),Button("详情",async()=>{if(Selected() is {} r)await new ContentDialog{XamlRoot=root.XamlRoot,Title="文件记录",Content=Editor(JsonSerializer.Serialize(r,new JsonSerializerOptions{WriteIndented=true}),true),CloseButtonText="关闭"}.ShowAsync();}),Button("预览",async()=>{if(Selected()?.SourcePath is {} p)await ShowPreview(p);})),Row(Button("重新转换",()=>{if(Selected()?.SourcePath is {} p){vm.Batch.Add([p],vm.Settings.Conversion.OutputDirectory);vm.Batch.Requeue(vm.Batch.Jobs.Where(j=>j.SourcePath.Equals(p,StringComparison.OrdinalIgnoreCase)));message.Text="已追加到队列，请手动开始；使用当前配置。";}}),Button("删除记录",async()=>{if(Selected() is {} r&&await Confirm("删除历史记录","仅删除此文件的元数据，不删除磁盘文件。")){await vm.History.DeleteAsync(r.FileId);await Refresh();}})),batchActions,paths,Row(Button("导出 JSON",async()=>{var p=await SaveFile("catTransform-history",".json");if(p is not null)await File.WriteAllTextAsync(p,await vm.History.ExportJsonAsync(paths.IsChecked==true));}),Button("导出 CSV",async()=>{var p=await SaveFile("catTransform-history",".csv");if(p is not null)await vm.History.ExportCsvAsync(p,paths.IsChecked==true);}),Button("合并导入",()=>Import(false)),Button("替换导入",()=>Import(true))));
  async Task Import(bool replace){var files=await PickFiles(".json");if(files.Length!=1)return;if(replace&&!await Confirm("替换历史","先备份数据库，再替换全部历史元数据。不会更改磁盘文件。"))return;await vm.History.ImportAsync(await File.ReadAllTextAsync(files[0]),replace,replace);historyPage=0;await Refresh();}
 }
 private UIElement SettingsPage(){
  var formatChoices=Row();foreach(var extension in BatchService.Extensions){var format=extension.TrimStart('.');var check=new CheckBox{Content=format.ToUpperInvariant(),IsChecked=vm.Settings.Conversion.Formats.Contains(format)};check.Click+=(_,_)=>{var formats=vm.Settings.Conversion.Formats.ToHashSet();if(check.IsChecked==true)formats.Add(format);else formats.Remove(format);Update(vm.Settings.Conversion with{Formats=formats.Order().ToArray()});};formatChoices.Children.Add(check);}
  var theme=new ComboBox{ItemsSource=new[]{"Default","Light","Dark"},SelectedItem=vm.Settings.Theme};theme.SelectionChanged+=(_,_)=>{var t=(string)theme.SelectedItem;root.RequestedTheme=Enum.Parse<ElementTheme>(t);vm.Update(vm.Settings with{Theme=t});};
  var output=new TextBox{Text=vm.Settings.Conversion.OutputDirectory,IsReadOnly=true};var parallel=new NumberBox{Header="同时转换的文件数（1–16）",Minimum=1,Maximum=16,Value=vm.Settings.Conversion.Concurrency,SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Inline};parallel.ValueChanged+=(_,_)=>{if(!double.IsNaN(parallel.Value))Update(vm.Settings.Conversion with{Concurrency=(int)parallel.Value});};
  var jar=new TextBox{Text=vm.Settings.Conversion.EpubCheckJar??"",IsReadOnly=true};
  var startup=new ToggleSwitch{Header="登录后启动",IsOn=new StartupService().IsEnabled()};var silent=new ToggleSwitch{Header="登录后静默到托盘",IsOn=vm.Settings.SilentStartup};var auto=new ToggleSwitch{Header="启动后自动监听（独立授权）",IsOn=vm.Settings.ListenAtStartup};
  void SaveStartup(){vm.Update(vm.Settings with{Startup=startup.IsOn,SilentStartup=silent.IsOn});new StartupService().Set(startup.IsOn,silent.IsOn,Environment.ProcessPath!);}
  startup.Toggled+=(_,_)=>{try{SaveStartup();}catch(Exception e){_=Error(e);}};silent.Toggled+=(_,_)=>{try{SaveStartup();}catch(Exception e){_=Error(e);}};
  auto.Toggled+=async(_,_)=>{if(auto.IsOn&&!vm.Settings.ListenAtStartup&&!await Confirm("自动监听授权","允许每次启动 catTransform 时读取后续剪贴板文本变化？")){auto.IsOn=false;return;}vm.Update(vm.Settings with{ListenAtStartup=auto.IsOn});};
  var hotkey=new TextBox{Header="全局暂停快捷键",Text=Hotkey.Format(vm.Settings.HotkeyModifiers,vm.Settings.HotkeyKey)};
  var applyHotkey=Button("应用快捷键",()=>{var chord=Hotkey.Parse(hotkey.Text);if(!tray.SetHotkey(chord.Modifiers,chord.Key)){tray.SetHotkey(vm.Settings.HotkeyModifiers,vm.Settings.HotkeyKey);throw new InvalidOperationException("快捷键已被占用");}vm.Update(vm.Settings with{HotkeyModifiers=chord.Modifiers,HotkeyKey=chord.Key});});
  var scope=Editor(JsonSerializer.Serialize(vm.Settings.Conversion.Scope,new JsonSerializerOptions{WriteIndented=true}));
  var rules=Editor(RulePackages.Export(vm.Settings.Conversion.Rules));
  var testInput=Editor("汉字转换，软件与鼠标。");testInput.MinHeight=80;var testResult=Editor("",true);testResult.MinHeight=80;
  var singleRule=new TextBox{Header="单条规则 UUID（留空测试完整规则包）"};
  var ruleName=new TextBox{Header="规则名称",Text="新规则"};var pattern=new TextBox{Header="表达式 / 词语"};var replacement=new TextBox{Header="替换内容"};var kind=new ComboBox{Header="类型",ItemsSource=Enum.GetNames<RuleKind>(),SelectedIndex=0};var stage=new ComboBox{Header="阶段",ItemsSource=Enum.GetNames<RuleStage>(),SelectedIndex=0};
  var ruleList=new ListView{MaxHeight=180,MinHeight=70,ItemsSource=vm.Settings.Conversion.Rules.Select(r=>(r.Enabled?"启用 · ":"禁用 · ")+r.Name).ToArray()};
  ruleList.SelectionChanged+=(_,_)=>{if(ruleList.SelectedIndex>=0)singleRule.Text=vm.Settings.Conversion.Rules[ruleList.SelectedIndex].Id.ToString();};
  void ApplyRuleList(Rule[] items){var selected=ruleList.SelectedIndex;Update(vm.Settings.Conversion with{Rules=items});rules.Text=RulePackages.Export(items);ruleList.ItemsSource=items.Select(r=>(r.Enabled?"启用 · ":"禁用 · ")+r.Name).ToArray();if(items.Length>0)ruleList.SelectedIndex=Math.Min(Math.Max(selected,0),items.Length-1);}
  void MoveRule(int delta){var items=vm.Settings.Conversion.Rules.OrderBy(r=>r.Order).ThenBy(r=>r.Id).ToArray();int index=Array.FindIndex(items,r=>r.Id==vm.Settings.Conversion.Rules[ruleList.SelectedIndex].Id);int target=index+delta;if(target<0||target>=items.Length)return;(items[index],items[target])=(items[target],items[index]);ApplyRuleList(items.Select((r,i)=>r with{Order=i}).ToArray());ruleList.SelectedIndex=target;}
  var ruleActions=Row(Button("启用 / 禁用",()=>{if(ruleList.SelectedIndex<0)return;var items=vm.Settings.Conversion.Rules.ToArray();items[ruleList.SelectedIndex]=items[ruleList.SelectedIndex] with{Enabled=!items[ruleList.SelectedIndex].Enabled};ApplyRuleList(items);}),Button("上移",()=>{if(ruleList.SelectedIndex>=0)MoveRule(-1);}),Button("下移",()=>{if(ruleList.SelectedIndex>=0)MoveRule(1);}),Button("删除选中规则",async()=>{if(ruleList.SelectedIndex>=0&&await Confirm("删除规则","删除选中规则？已有历史快照不受影响。"))ApplyRuleList(vm.Settings.Conversion.Rules.Where((r,i)=>i!=ruleList.SelectedIndex).ToArray());}));
  var rulesPanel=Stack(ruleList,ruleActions,Label("规则包编辑：Enabled 启用；Order 排序；Config / Format / Region 为条件。新建默认禁用。"),Row(ruleName,kind,stage),pattern,replacement,Button("添加禁用规则",()=>{var r=new Rule{Name=ruleName.Text,Pattern=pattern.Text,Replacement=replacement.Text,Kind=(RuleKind)kind.SelectedIndex,Stage=(RuleStage)stage.SelectedIndex,Order=vm.Settings.Conversion.Rules.Length};Update(vm.Settings.Conversion with{Rules=[..vm.Settings.Conversion.Rules,r]});rules.Text=RulePackages.Export(vm.Settings.Conversion.Rules);}),rules,Row(Button("验证并应用规则包",()=>{var r=RulePackages.Import(rules.Text,[],true,vm.Text);Update(vm.Settings.Conversion with{Rules=r});message.Text="规则已保存，后续执行生效。";}),Button("合并导入规则",()=>ImportRules(false)),Button("替换导入规则",()=>ImportRules(true)),Button("导出规则",async()=>{var p=await SaveFile("catTransform-rules",".json");if(p is not null)await File.WriteAllTextAsync(p,RulePackages.Export(vm.Settings.Conversion.Rules));})),singleRule,testInput,Button("测试规则包并显示轨迹",()=>{var r=RulePackages.Import(rules.Text,[],true,vm.Text);if(!string.IsNullOrWhiteSpace(singleRule.Text)){var id=Guid.Parse(singleRule.Text);r=[r.Single(x=>x.Id==id) with{Enabled=true}];}var trace=new List<TraceEntry>();var text=vm.Text.Convert(testInput.Text,vm.Settings.Conversion with{Rules=r},trace:trace);testResult.Text=text+"\n\n执行轨迹（不保存正文）：\n"+JsonSerializer.Serialize(trace,new JsonSerializerOptions{WriteIndented=true});}),testResult);
  var presets=new ComboBox{ItemsSource=new[]{"安全默认","自定义","全部支持的文本区域"},SelectedIndex=1};presets.SelectionChanged+=(_,_)=>{if(presets.SelectedIndex==1)return;var s=presets.SelectedIndex==0?new Scope():new Scope{MarkdownCode=true,CsvHeader=true,JsonKeys=true,DocxHeaders=true,DocxComments=true,DocxTextBoxes=true,EpubNavigation=true,EpubMetadata=true};Update(vm.Settings.Conversion with{Scope=s});scope.Text=JsonSerializer.Serialize(s,new JsonSerializerOptions{WriteIndented=true});};
  return Page("设置",new Expander{Header="外观",HorizontalAlignment=HorizontalAlignment.Stretch,Content=theme},new Expander{Header="文件与输出",HorizontalAlignment=HorizontalAlignment.Stretch,Content=Stack(formatChoices,output,Button("选择输出目录",async()=>{var p=await PickFolder();if(p is not null){SafePaths.RejectLinks(p);Update(vm.Settings.Conversion with{OutputDirectory=p});output.Text=p;}}),Label("EPUBCheck JAR（需 Java 运行环境）"),jar,Button("选择 EPUBCheck",async()=>{var f=await PickFiles(".jar");if(f.Length==1){Update(vm.Settings.Conversion with{EpubCheckJar=f[0]});jar.Text=f[0];}}),presets,Label("范围配置：CSV 列从 0 开始；JSON 路径使用 RFC 6901 JSON Pointer。"),scope,Button("验证并应用范围",()=>{Update(vm.Settings.Conversion with{Scope=JsonSerializer.Deserialize<Scope>(scope.Text)??throw new InvalidDataException("配置为空")});}))},new Expander{Header="转换规则",HorizontalAlignment=HorizontalAlignment.Stretch,Content=rulesPanel},new Expander{Header="性能",Content=parallel,HorizontalAlignment=HorizontalAlignment.Stretch},new Expander{Header="隐私与后台",HorizontalAlignment=HorizontalAlignment.Stretch,Content=Stack(startup,silent,auto,hotkey,applyHotkey,Label("全局暂停 / 恢复：Ctrl+Alt+T。关闭监听清空缓存；不保存正文，不上传数据。"))},new Expander{Header="关于",Content=Label("catTransform v0.1\n离线简繁与地区词汇转换\nOpenccNetLib 1.7.0 · .NET 10 · WinUI 3\n当前为开发构建；完整验收状态见 DEVELOPMENT_PROGRESS.md。"),HorizontalAlignment=HorizontalAlignment.Stretch},Button("恢复默认设置",async()=>{if(await Confirm("恢复默认设置","仅恢复配置，保留规则、历史、输出和备份。")){vm.Update(SettingsStore.Defaults() with{Conversion=SettingsStore.Defaults().Conversion with{Rules=vm.Settings.Conversion.Rules}});new StartupService().Set(false,false,Environment.ProcessPath!);root.RequestedTheme=ElementTheme.Default;Navigate(4);}}));
  async Task ImportRules(bool replace){var f=await PickFiles(".json");if(f.Length!=1)return;var r=RulePackages.Import(await File.ReadAllTextAsync(f[0]),vm.Settings.Conversion.Rules,replace,vm.Text);Update(vm.Settings.Conversion with{Rules=r});rules.Text=RulePackages.Export(r);}
 }
}
