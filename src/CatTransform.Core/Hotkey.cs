namespace CatTransform.Core;
public static class Hotkey {
 public static (uint Modifiers,uint Key) Parse(string chord){
  var parts=chord.ToUpperInvariant().Split('+',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);
  if(parts.Length<2)throw new ArgumentException("快捷键需要 Ctrl、Alt 或 Shift 修饰键");
  uint modifiers=0;foreach(var part in parts[..^1])modifiers|=part switch{"ALT"=>1u,"CTRL"=>2u,"SHIFT"=>4u,_=>throw new ArgumentException("快捷键修饰键无效")};
  var key=parts[^1];uint code=key.Length==1&&key[0] is >= 'A' and <= 'Z'?key[0]:key.StartsWith('F')&&int.TryParse(key[1..],out var f)&&f is >=1 and <=12?(uint)(0x70+f-1):throw new ArgumentException("快捷键支持 A–Z 或 F1–F12");
  return(modifiers,code);
 }
 public static string Format(uint modifiers,uint key)=>string.Join('+',new[]{(modifiers&2)!=0?"Ctrl":null,(modifiers&1)!=0?"Alt":null,(modifiers&4)!=0?"Shift":null,key is >=0x70 and <=0x7b?"F"+(key-0x70+1):((char)key).ToString()}.Where(s=>s is not null));
}
