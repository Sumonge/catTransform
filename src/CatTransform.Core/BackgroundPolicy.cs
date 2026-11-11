namespace CatTransform.Core;
public static class BackgroundPolicy {
 public static bool HideOnClose(bool listeningOrPaused,bool silentStandby)=>listeningOrPaused||silentStandby;
 public static bool MayReadClipboard(bool listening,bool paused,bool manual)=>manual||listening&&!paused;
}
