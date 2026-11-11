namespace CatTransform.App;

// Only native window coordinates are converted. XAML dimensions remain DIPs.
internal static class WindowSizing {
 internal static int Pixels(double dips,uint dpi) => checked((int)Math.Round(dips * dpi / 96.0,MidpointRounding.AwayFromZero));
 internal static (int Width,int Height) Fit(double width,double height,uint dpi,int workWidth,int workHeight) =>
  (Math.Min(Pixels(width,dpi),workWidth),Math.Min(Pixels(height,dpi),workHeight));
}
