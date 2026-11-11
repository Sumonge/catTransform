using CatTransform.Core;
using CatTransform.Infrastructure;
using Xunit;
namespace CatTransform.Tests;
public sealed class BackgroundTests {
 [Theory][InlineData(false,false,false)][InlineData(true,false,true)][InlineData(false,true,true)][InlineData(true,true,true)]
 public void ClosingPolicy(bool listening,bool silent,bool expected)=>Assert.Equal(expected,BackgroundPolicy.HideOnClose(listening,silent));
 [Theory][InlineData(false,false,false,false)][InlineData(true,true,false,false)][InlineData(true,false,false,true)][InlineData(false,false,true,true)]
 public void ClipboardReadPolicy(bool listening,bool paused,bool manual,bool expected)=>Assert.Equal(expected,BackgroundPolicy.MayReadClipboard(listening,paused,manual));
 [Fact]public void StartupAndClipboardDefaultOff(){var settings=SettingsStore.Defaults();Assert.False(settings.Startup);Assert.False(settings.SilentStartup);Assert.False(settings.ListenAtStartup);Assert.DoesNotContain(typeof(Settings).GetProperties(),p=>p.Name is "Text" or "ClipboardText" or "SourceText");}
}
