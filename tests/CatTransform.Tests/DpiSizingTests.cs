using CatTransform.App;
using Xunit;
namespace CatTransform.Tests;
public class DpiSizingTests {
 [Theory]
 [InlineData(96,1440,960)] [InlineData(120,1800,1200)] [InlineData(144,2160,1440)]
 [InlineData(168,2520,1680)] [InlineData(192,2880,1920)] [InlineData(240,3600,2400)] [InlineData(288,4320,2880)]
 public void LogicalWindowSizeIsConvertedExactlyOnce(int dpi,int width,int height){
  Assert.Equal((width,height),WindowSizing.Fit(1440,960,(uint)dpi,7680,4320));
 }
 [Theory]
 [InlineData(96)] [InlineData(120)] [InlineData(144)] [InlineData(168)] [InlineData(192)] [InlineData(240)] [InlineData(288)]
 public void SmallMonitorRemainsReachable(int dpi){
  var size=WindowSizing.Fit(1440,960,(uint)dpi,1280,680);
  var minimum=WindowSizing.Fit(1100,720,(uint)dpi,1280,680);
  Assert.InRange(size.Width,1,1280);Assert.InRange(size.Height,1,680);
  Assert.InRange(minimum.Width,1,size.Width);Assert.InRange(minimum.Height,1,size.Height);
 }
 [Fact]public void FractionalDpiRoundsAtNativeBoundary(){Assert.Equal(25,WindowSizing.Pixels(14,168));}
}
