using System.Data;
using System.Text.Json.Nodes;
using Kohami.Core;
using Microsoft.Data.SqlClient;
namespace Kohami.Commerce;

public sealed record CommerceDocument(string Kind,string Id,string Owner,long Version,JsonObject Body);
public sealed record CommerceReply(JsonObject Body,int Status=200,long? Version=null);
public sealed class CommerceStore(SqlRuntime sql) {
 public const string Dataset="kohami_local";
 public async Task<T> Transaction<T>(Func<CommerceTransaction,Task<T>> work,CancellationToken ct=default,bool worker=false,bool maintenanceContinuation=false) {
  if(maintenanceContinuation&&!worker)throw new InvalidOperationException("WORKER_CONTINUATION_ONLY");
  await using var c=await sql.OpenAsync(ct,worker);await using var tx=(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable,ct);
  await using(var securityCmd=SqlRuntime.Command(c,"DECLARE @r int;EXEC @r=sys.sp_getapplock @Resource='kohami.identity_access.security',@LockMode=@mode,@LockOwner='Transaction',@LockTimeout=10000;IF @r<0 THROW 50001,'SECURITY_LOCK_TIMEOUT',1;IF NOT EXISTS(SELECT 1 FROM identity_access.SecurityState WHERE Id=1 AND (Maintenance=0 OR @continue=1)) THROW 50002,'MAINTENANCE_REQUIRED',1;",tx,("@mode",worker?"Exclusive":"Shared"),("@continue",maintenanceContinuation))) await securityCmd.ExecuteNonQueryAsync(ct);
  await using(var cmd=SqlRuntime.Command(c,"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource='kohami.identity_access.commerce.kohami_local',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 50001,'COMMERCE_LOCK_TIMEOUT',1;",tx)) await cmd.ExecuteNonQueryAsync(ct);
  var result=await work(new(c,tx,ct));await tx.CommitAsync(ct);return result;
 }
}
public sealed class CommerceTransaction(SqlConnection c,SqlTransaction tx,CancellationToken ct) {
 public SqlCommand Command(string text,params (string Name,object? Value)[] args)=>SqlRuntime.Command(c,text,tx,args);
 public async Task<int> Execute(string text,params (string Name,object? Value)[] args){await using var cmd=Command(text,args);return await cmd.ExecuteNonQueryAsync(ct);}
 public async Task<object?> Scalar(string text,params (string Name,object? Value)[] args){await using var cmd=Command(text,args);return await cmd.ExecuteScalarAsync(ct);}
 public async Task<List<object[]>> Rows(string text,params (string Name,object? Value)[] args){await using var cmd=Command(text,args);await using var r=await cmd.ExecuteReaderAsync(ct);var rows=new List<object[]>();while(await r.ReadAsync(ct)){var v=new object[r.FieldCount];r.GetValues(v);rows.Add(v);}return rows;}
 public async Task<CommerceDocument?> Find(string kind,string id){CommerceWire.Identifier(id);var rows=await Rows("SELECT OwnerId,Version,Body FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind=@k AND Id=@i",("@k",kind),("@i",id));return rows.Count==0?null:new(kind,id,(string)rows[0][0],(long)rows[0][1],JsonNode.Parse((string)rows[0][2])!.AsObject());}
 public async Task<CommerceDocument> Get(string kind,string id)=>await Find(kind,id)??throw new KohamiException(404,"RESOURCE_NOT_FOUND");
 public async Task Save(CommerceDocument d,bool fresh=false){d.Body["version"]=CommerceWire.Version(d.Kind,d.Id,d.Version);if(fresh)await Execute("INSERT commerce.Documents(Dataset,Kind,Id,OwnerId,Version,Body) VALUES('kohami_local',@k,@i,@o,@v,@b)",("@k",d.Kind),("@i",d.Id),("@o",d.Owner),("@v",d.Version),("@b",d.Body.ToJsonString()));else if(await Execute("UPDATE commerce.Documents SET OwnerId=@o,Version=@v,Body=@b WHERE Dataset='kohami_local' AND Kind=@k AND Id=@i",("@k",d.Kind),("@i",d.Id),("@o",d.Owner),("@v",d.Version),("@b",d.Body.ToJsonString()))!=1)throw new KohamiException(404,"RESOURCE_NOT_FOUND");}
 public async Task<CommerceReply?> Replay(string actor,string scope,string key,string hash){var rows=await Rows("SELECT RequestHash,Response,StatusCode,Version FROM commerce.Idempotency WHERE Dataset='kohami_local' AND Actor=@a AND Scope=@s AND OperationKey=@k",("@a",actor),("@s",scope),("@k",key));if(rows.Count==0)return null;if((string)rows[0][0]!=hash)throw new KohamiException(409,"KEY_CONFLICT");return new(JsonNode.Parse((string)rows[0][1])!.AsObject(),(int)rows[0][2],rows[0][3] is DBNull?null:(long)rows[0][3]);}
 public async Task Record(string actor,string scope,string key,string hash,CommerceReply r)=>await Execute("INSERT commerce.Idempotency(Dataset,Actor,Scope,OperationKey,RequestHash,Response,StatusCode,Version) VALUES('kohami_local',@a,@s,@k,@h,@b,@c,@v)",("@a",actor),("@s",scope),("@k",key),("@h",hash),("@b",r.Body.ToJsonString()),("@c",r.Status),("@v",r.Version));
 public async Task Job(string kind,string target,string actor,long securityVersion,string? permission,string resourceKind,string resourceId,JsonObject payload,DateTimeOffset now)=>await Execute("INSERT commerce.Jobs(Dataset,Id,Kind,TargetId,Actor,SecurityVersion,Permission,ResourceKind,ResourceId,State,Payload,CreatedAt) VALUES('kohami_local',@id,@k,@t,@a,@v,@p,@rk,@ri,'NotStarted',@b,@now)",("@id",FoundationHttp.Id("job")),("@k",kind),("@t",target),("@a",actor),("@v",securityVersion),("@p",permission),("@rk",resourceKind),("@ri",resourceId),("@b",payload.ToJsonString()),("@now",now));
}
