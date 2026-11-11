using CatTransform.Conversion;
using CatTransform.Core;
using Xunit;
namespace CatTransform.Tests;
public sealed class SafetyTests {
 [Fact]public void BadRegexReplacementRejectedBeforeBatch()=>Assert.Throws<ArgumentException>(()=>new TextConverter().Validate(new(){Rules=[new(){Kind=RuleKind.Regex,Pattern="a",Replacement="$99"}]}));
 [Fact]public void HotkeyRoundTrip(){var key=Hotkey.Parse("Ctrl+Alt+T");Assert.Equal(3u,key.Modifiers);Assert.Equal(0x54u,key.Key);Assert.Equal("Ctrl+Alt+T",Hotkey.Format(key.Modifiers,key.Key));}
 [Fact]public void MillionCharactersConvertWithinBound(){var converter=new TextConverter();var input=string.Concat(Enumerable.Repeat("汉字转换测试",166666));var timer=System.Diagnostics.Stopwatch.StartNew();var result=converter.Convert(input,new());Assert.Equal(input.Length,result.Length);Assert.StartsWith("漢字轉換測試",result);Assert.EndsWith("漢字轉換測試",result);Assert.True(timer.Elapsed<TimeSpan.FromSeconds(30),timer.Elapsed.ToString());}
 [Theory][InlineData(RuleKind.Replace)][InlineData(RuleKind.Regex)]public void ReplacementGrowthBounded(RuleKind kind){var converter=new TextConverter();var options=new ConversionOptions{Rules=[new(){Enabled=true,Kind=kind,Pattern="a",Replacement=new string('b',1000)}]};Assert.Throws<InvalidDataException>(()=>converter.Convert(new string('a',10000),options));}
 [Fact]public void RegexGroupsExpand(){var converter=new TextConverter();var options=new ConversionOptions{Rules=[new(){Enabled=true,Kind=RuleKind.Regex,Pattern="(abc)",Replacement="${1}-$1-$$-$&"}]};Assert.Equal("abc-abc-$-abc",converter.Convert("abc",options));}
 [Fact]public void DirectionLocaleFormatAndRegionConditions(){var converter=new TextConverter();var rule=new Rule{Enabled=true,Pattern="software",Replacement="软件",Direction="s2t",Locale="tw",Format="csv",Region="header"};var o=new ConversionOptions{Config="s2twp",Rules=[rule]};converter.Validate(o);Assert.Equal("軟體",converter.Convert("software",o,"csv","header"));Assert.Equal("software",converter.Convert("software",o,"csv","body"));Assert.Equal("software",converter.Convert("software",o with{Config="t2s"},"csv","header"));}
}
