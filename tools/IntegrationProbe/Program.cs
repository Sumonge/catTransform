using OpenccNetLib;
foreach(var config in new[]{"s2t","t2s","s2tw","s2twp","s2hk","tw2s","tw2sp","hk2s"}) {
 if(!Opencc.IsValidConfig(config)) throw new Exception(config);
 var cc=new Opencc(config);
 var input="汉字转换：软件、鼠标、网络。漢字轉換：軟體、滑鼠、網路。";
 var expected=cc.Convert(input);
 Parallel.For(0,2000,_=>{if(cc.Convert(input)!=expected)throw new Exception("Concurrent mismatch");});
 Console.WriteLine($"{config}: {expected} [2000 concurrent calls consistent]");
}
