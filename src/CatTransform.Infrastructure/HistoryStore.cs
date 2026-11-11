using System.Text;
using System.Text.Json;
using CatTransform.Core;
using Microsoft.Data.Sqlite;
namespace CatTransform.Infrastructure;

public sealed record HistoryPackage(int schema_version,HistoryRecord[] records);
public sealed class HistoryStore : IHistoryStore {
 private readonly string path;
 private readonly SemaphoreSlim gate=new(1);
 public HistoryStore(string? directory=null){var dir=directory??UserPaths.Data;Directory.CreateDirectory(dir);path=Path.Combine(dir,"catTransform.db");using var db=Open();using var cmd=db.CreateCommand();cmd.CommandText="PRAGMA journal_mode=WAL; CREATE TABLE IF NOT EXISTS history(batch TEXT NOT NULL,file TEXT NOT NULL,finished TEXT NOT NULL,source TEXT,config TEXT NOT NULL,state INTEGER NOT NULL,payload TEXT NOT NULL,PRIMARY KEY(batch,file)); CREATE INDEX IF NOT EXISTS history_finished ON history(finished);";cmd.ExecuteNonQuery();}
 private SqliteConnection Open(){var db=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=path,DefaultTimeout=15}.ToString());db.Open();return db;}
 private static void Insert(SqliteConnection db,SqliteTransaction tx,HistoryRecord r){using var cmd=db.CreateCommand();cmd.Transaction=tx;cmd.CommandText="INSERT OR IGNORE INTO history(batch,file,finished,source,config,state,payload) VALUES($b,$f,$t,$s,$c,$state,$p)";cmd.Parameters.AddWithValue("$b",r.BatchId.ToString());cmd.Parameters.AddWithValue("$f",r.FileId.ToString());cmd.Parameters.AddWithValue("$t",r.Finished.ToString("O"));cmd.Parameters.AddWithValue("$s",(object?)r.SourcePath??DBNull.Value);cmd.Parameters.AddWithValue("$c",r.Config);cmd.Parameters.AddWithValue("$state",(int)r.State);cmd.Parameters.AddWithValue("$p",JsonSerializer.Serialize(r));cmd.ExecuteNonQuery();}
 public async Task SaveAsync(HistoryRecord record){await gate.WaitAsync();try{using var db=Open();using var tx=db.BeginTransaction();Insert(db,tx,record);tx.Commit();}finally{gate.Release();}}
 public Task<IReadOnlyList<HistoryRecord>> QueryAsync(string search="",int page=0,int pageSize=100,JobState? state=null)=>Task.Run<IReadOnlyList<HistoryRecord>>(()=>{
  using var db=Open();using var cmd=db.CreateCommand();cmd.CommandText="SELECT payload FROM history WHERE (source LIKE $q OR config LIKE $q OR batch LIKE $q) AND ($state < 0 OR state=$state) ORDER BY finished DESC LIMIT $n OFFSET $o";cmd.Parameters.AddWithValue("$state",state is null?-1:(int)state);cmd.Parameters.AddWithValue("$q","%"+search+"%");cmd.Parameters.AddWithValue("$n",Math.Clamp(pageSize,1,10000));cmd.Parameters.AddWithValue("$o",Math.Max(page,0)*Math.Clamp(pageSize,1,10000));using var reader=cmd.ExecuteReader();var rows=new List<HistoryRecord>();while(reader.Read())rows.Add(JsonSerializer.Deserialize<HistoryRecord>(reader.GetString(0))!);return rows;
 });
 public async Task DeleteAsync(Guid id){await gate.WaitAsync();try{using var db=Open();using var cmd=db.CreateCommand();cmd.CommandText="DELETE FROM history WHERE file=$f";cmd.Parameters.AddWithValue("$f",id.ToString());cmd.ExecuteNonQuery();}finally{gate.Release();}}
 public async Task<string> ExportJsonAsync(bool includePaths){var rows=await AllAsync();return JsonSerializer.Serialize(new HistoryPackage(1,rows.Select(r=>includePaths?r:r with{SourcePath=null,OutputPath=null}).ToArray()),new JsonSerializerOptions{WriteIndented=true});}
 private async Task<List<HistoryRecord>> AllAsync(){var all=new List<HistoryRecord>();for(int page=0;;page++){var rows=await QueryAsync("",page,10000);all.AddRange(rows);if(rows.Count<10000)return all;}}
 public async Task ExportCsvAsync(string file,bool includePaths){var rows=await AllAsync();static string Field(string? s){s??="";if(s.TrimStart().FirstOrDefault() is '=' or '+' or '-' or '@'||s.StartsWith('\t')||s.StartsWith('\r'))s="'"+s;return "\""+s.Replace("\"","\"\"")+"\"";}
  var sb=new StringBuilder("BatchId,FileId,Finished,Config,State,Source,Output,Error\r\n");foreach(var r in rows)sb.AppendLine(string.Join(',',new[]{r.BatchId.ToString(),r.FileId.ToString(),r.Finished.ToString("O"),r.Config,r.State.ToString(),includePaths?r.SourcePath:null,includePaths?r.OutputPath:null,r.Error}.Select(Field)));await File.WriteAllTextAsync(file,sb.ToString(),new UTF8Encoding(true));
 }
 public async Task ImportAsync(string json,bool replace,bool replacementConfirmed=false){
  if(Encoding.UTF8.GetByteCount(json)>32_000_000)throw new InvalidDataException("历史文件超过 32 MB");
  var package=JsonSerializer.Deserialize<HistoryPackage>(json)??throw new InvalidDataException("历史文件为空");
  if(package.schema_version!=1||package.records is null||package.records.Length>100000)throw new InvalidDataException("历史包格式无效");
  foreach(var r in package.records){if(r.BatchId==Guid.Empty||r.FileId==Guid.Empty||r.Finished<r.Started||!Enum.IsDefined(r.State)||r.Config.Length>32||r.RulesJson.Length>2_000_000)throw new InvalidDataException("历史记录数据无效");using var rules=JsonDocument.Parse(r.RulesJson);}
  if(replace&&!replacementConfirmed)throw new InvalidOperationException("替换历史需要明确确认");
  await gate.WaitAsync();try{using var db=Open();if(replace){var backup=path+"."+DateTime.UtcNow.ToString("yyyyMMddHHmmssfff")+".bak";using var target=new SqliteConnection($"Data Source={backup}");target.Open();db.BackupDatabase(target);}
   using var tx=db.BeginTransaction();if(replace){using var cmd=db.CreateCommand();cmd.Transaction=tx;cmd.CommandText="DELETE FROM history";cmd.ExecuteNonQuery();}foreach(var r in package.records)Insert(db,tx,r);tx.Commit();
  }finally{gate.Release();}
 }
}
