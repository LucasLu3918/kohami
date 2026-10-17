using System.Security.Cryptography;
using Kohami.M0.Core;
using Kohami.M0.Native;
using System.Runtime.Versioning;
[assembly:SupportedOSPlatform("macos")]
var passed=0;
void Reject(Action action,string name){try{action();throw new Exception("UNEXPECTED_ACCEPT:"+name);}catch(InvalidOperationException){passed++;}}
if(args.Length==2&&args[0]=="--sql-migrations")await Kohami.M0.NativeTests.SqlMigrationAcceptance.RunAsync(args[1]);
else if(args.Length==2&&args[0]=="--sql-baseline-adoption")await Kohami.M0.NativeTests.SqlBaselineAdoptionAcceptance.RunAsync(args[1]);
using var issuer=ECDsa.Create(ECCurve.NamedCurves.nistP256);var pub=issuer.ExportSubjectPublicKeyInfo();var now=DateTimeOffset.UtcNow;
var preview=new OperationPreview("InitialOwnerPrepare","owner1","demo_m0",new string('a',64),1,new string('b',64),"synthetic test");
NativePermit Sign(OperationPreview pr,DateTimeOffset at){var p=new NativePermit(Guid.NewGuid().ToString(),pr,at,at.AddMinutes(10),"");return p with{Signature=Convert.ToBase64String(issuer.SignData(PermitVerifier.Payload(p),HashAlgorithmName.SHA256,DSASignatureFormat.Rfc3279DerSequence))};}
var permit=Sign(preview,now);PermitVerifier.Verify(permit,preview,pub,now);passed++;
if(!NativeResumeEpoch.Allows("TransferDemoOwner",7,8)||NativeResumeEpoch.Allows("TransferDemoOwner",6,8)||NativeResumeEpoch.Allows("CredentialRecovery",7,8)||!NativeResumeEpoch.Allows("CredentialRecovery",8,8))throw new Exception("FAILED: native resume epoch progression");passed++;
Reject(()=>PermitVerifier.Verify(permit,preview with{Target="owner2"},pub,now),"target");
Reject(()=>PermitVerifier.Verify(permit,preview with{SecurityEpoch=2},pub,now),"epoch");
Reject(()=>PermitVerifier.Verify(permit,preview with{InstanceDbFingerprint=new string('c',64)},pub,now),"instance");
Reject(()=>PermitVerifier.Verify(permit,preview with{PreviewHash=new string('d',64)},pub,now),"preview");
Reject(()=>PermitVerifier.Verify(permit,preview,pub,now.AddMinutes(10)),"expiry equality");
Reject(()=>PermitVerifier.Verify(Sign(preview with{DatasetId="prod"},now),preview with{DatasetId="prod"},pub,now),"dataset");
using var wrong=ECDsa.Create(ECCurve.NamedCurves.nistP256);Reject(()=>PermitVerifier.Verify(permit,preview,wrong.ExportSubjectPublicKeyInfo(),now),"issuer");
var journalPath=Path.Combine(Path.GetTempPath(),"kohami-native-test-"+Guid.NewGuid().ToString("N"));var journalId=Guid.NewGuid().ToString();var journalHash=new string('e',64);HostJournal.Append(journalPath,journalId,"Intent",journalHash);if(!HostJournal.HasCheckpoint(journalPath,journalId,journalHash)||HostJournal.HasCheckpoint(journalPath,journalId,new string('f',64))||HostJournal.HasCheckpoint(journalPath,Guid.NewGuid().ToString(),journalHash))throw new Exception("FAILED: native resume checkpoint binding");passed++;Directory.Delete(journalPath,true);
var restore=new RestoreManifest("demo_m0","kohami.m0.v1","KohamiM0Restore_test","kohami.m0.restore.test",now,now,new string('f',64),["session1","session2"]);
RestoreGuard.Verify(restore,new string('f',64),["session1","session2"]);passed++;
Reject(()=>RestoreGuard.Verify(restore with{NewDatabase="KohamiM0"},new string('f',64),["session1","session2"]),"overwrite");
Reject(()=>RestoreGuard.Verify(restore with{TargetNamespace="kohami.m0.v1"},new string('f',64),["session1","session2"]),"namespace");
Reject(()=>RestoreGuard.Verify(restore with{LedgerThrough=now.AddSeconds(-1)},new string('f',64),["session1","session2"]),"cutoff");
Reject(()=>RestoreGuard.Verify(restore,new string('f',64),["session1"]),"missing key");
Reject(()=>PermitVerifier.Verify(Sign(preview with{Action="PublicReset"},now),preview with{Action="PublicReset"},pub,now),"unknown action");
Reject(()=>PermitVerifier.Verify(Sign(preview with{Target="owner1\nforged"},now),preview with{Target="owner1\nforged"},pub,now),"newline binding");
Reject(()=>PermitVerifier.Verify(Sign(preview with{PreviewHash="false"},now),preview with{PreviewHash="false"},pub,now),"hash shape");
if(args.Length==2 && args[0]=="--tls-local") {
 var setting=Kohami.M0.Core.M0Settings.Load(args[1]);new Kohami.M0.Core.M0Sql(setting).ValidateTls();passed++;
 var name=setting.SqlServerName;setting.SqlServerName="other.example.test";
 Reject(()=>new Kohami.M0.Core.M0Sql(setting).ValidateTls(),"hostname");setting.SqlServerName=name;
 var ca=setting.SqlCaPath;setting.SqlCaPath=setting.SessionCertificatePath;
 // PFX is intentionally not a CA certificate input. Parse must fail, never create keys.
 try{new Kohami.M0.Core.M0Sql(setting).ValidateTls();throw new Exception("UNEXPECTED_ACCEPT:wrong CA");}catch(CryptographicException){passed++;}
 setting.SqlCaPath=ca;
}
var migrationPlan=SqlMigrations.Load(Directory.GetCurrentDirectory());
var migrationIds=migrationPlan.Select(m=>m.Id).ToArray();
if(migrationIds.Length<2||!migrationIds.SequenceEqual(migrationIds.Order(StringComparer.Ordinal))||migrationIds.Distinct(StringComparer.Ordinal).Count()!=migrationIds.Length)throw new Exception("FAILED: SQL migrations have a stable unique ascending order");passed++;
if(!M0SchemaContract.IsCurrent(migrationPlan.Select(m=> (m.Id,m.SourceHash))))throw new Exception("FAILED: readiness source hashes match the native migration plan");passed++;
if(!M0SchemaContract.IsCurrent(M0SchemaContract.Supported)||M0SchemaContract.IsCurrent(M0SchemaContract.Supported.Skip(1))||M0SchemaContract.IsCurrent(M0SchemaContract.Supported.Take(2))||M0SchemaContract.IsCurrent(M0SchemaContract.Supported.Append(("9999_unknown",new string('a',64))) )||M0SchemaContract.IsCurrent(M0SchemaContract.Supported.Select((row,index)=>index==1?(row.MigrationId,new string('a',64)):(row.MigrationId,row.SourceHash))))throw new Exception("FAILED: schema readiness rejects missing, incomplete, unknown, or mismatched ledger rows");passed++;
foreach(var migration in migrationPlan){if(migration.SourceHash.Length!=64||migration.SourceHash.Any(c=>!Uri.IsHexDigit(c)))throw new Exception("FAILED: SQL migration source has a SHA-256 identity");passed++;}
Reject(()=>SqlMigrations.ValidatePlan(migrationPlan.Reverse().ToArray()),"migration-order");
Reject(()=>SqlMigrations.ValidatePlan([migrationPlan[0],migrationPlan[0]]),"migration-duplicate");
Reject(()=>SqlMigrations.ValidatePlan([new SqlMigrationDefinition("0001_empty",[("test"," ")])]),"migration-empty-source");
Console.WriteLine($"PASS native signed-scope expiry wrong-key new-target namespace cutoff tests={passed}; live OS confirmation NOT_EXECUTED");
