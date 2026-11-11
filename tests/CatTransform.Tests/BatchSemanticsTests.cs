using CatTransform.Application;
using CatTransform.Conversion;
using CatTransform.Core;
using CatTransform.Documents;
using CatTransform.Infrastructure;
using Microsoft.Data.Sqlite;
using Xunit;
namespace CatTransform.Tests;
public sealed class BatchSemanticsTests {
 [Fact]public async Task ConfigurationFrozenAndAppendsWaitForNextBatch(){
  var dir=Path.Combine(Path.GetTempPath(),"catTransform-freeze-"+Guid.NewGuid());Directory.CreateDirectory(dir);
  try{var source=Path.Combine(dir,"a.txt");var later=Path.Combine(dir,"b.txt");await File.WriteAllTextAsync(source,"汉字");await File.WriteAllTextAsync(later,"汉字");var store=new HistoryStore(Path.Combine(dir,"db"));var gate=new GatedConverter();var batch=new BatchService(new TextConverter(),gate,store);var rules=new[]{new Rule{Enabled=true,Pattern="a",Replacement="b"}};var options=new ConversionOptions{OutputDirectory=Path.Combine(dir,"out"),Rules=rules};batch.Add([source],options.OutputDirectory);
   var running=batch.RunAsync(options);await gate.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));rules[0]=rules[0] with{Replacement="changed"};batch.Add([later],options.OutputDirectory);gate.Release.SetResult();await running;
   Assert.Equal("b",gate.Received!.Rules[0].Replacement);Assert.Equal(JobState.Waiting,batch.Jobs[1].State);Assert.Single(await store.QueryAsync());await batch.RunAsync(options);var records=await store.QueryAsync();Assert.Equal(2,records.Select(r=>r.BatchId).Distinct().Count());
  }finally{SqliteConnection.ClearAllPools();Directory.Delete(dir,true);}
 }
 private sealed class GatedConverter:IDocumentConverter {
  public TaskCompletionSource Started {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
  public TaskCompletionSource Release {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
  public ConversionOptions? Received;
  public async Task ConvertAsync(string source,string destination,ConversionOptions options,CancellationToken ct){Received=options;Started.TrySetResult();await Release.Task.WaitAsync(ct);await File.WriteAllTextAsync(destination,"漢字",ct);}
  public Task<string> ExtractAsync(string source,ConversionOptions options,CancellationToken ct)=>File.ReadAllTextAsync(source,ct);
 }
 [Fact]public async Task SqliteUncommittedWriteRollsBack(){var dir=Path.Combine(Path.GetTempPath(),"catTransform-db-"+Guid.NewGuid());Directory.CreateDirectory(dir);try{var store=new HistoryStore(dir);using(var db=new SqliteConnection($"Data Source={Path.Combine(dir,"catTransform.db")}")){db.Open();using var tx=db.BeginTransaction();using var cmd=db.CreateCommand();cmd.Transaction=tx;cmd.CommandText="INSERT INTO history VALUES('test','test','2026-01-01',NULL,'s2t',3,'invalid')";cmd.ExecuteNonQuery();}Assert.Empty(await store.QueryAsync());}finally{SqliteConnection.ClearAllPools();Directory.Delete(dir,true);}}
}
