using System.Security.Cryptography;
using Kohami.M0.Native;
using System.Runtime.Versioning;
[assembly:SupportedOSPlatform("macos")]
var passed=0;
void Reject(Action action,string name){try{action();throw new Exception("UNEXPECTED_ACCEPT:"+name);}catch(InvalidOperationException){passed++;}}
var fixtureTarget=new DevelopmentFixtureTarget("Development",true,true,true,false,true,true,"localhost,14336","KohamiM0","demo_m0","localhost",true,true,"127.0.0.1","KOHAMI-DevFixtureBootstrap");
DevelopmentFixtureBootstrapPolicy.Validate(fixtureTarget);passed++;
void RejectFixture(DevelopmentFixtureTarget target,string name)=>Reject(()=>DevelopmentFixtureBootstrapPolicy.Validate(target),"fixture-"+name);
RejectFixture(fixtureTarget with{EnvironmentName="Production"},"production");
RejectFixture(fixtureTarget with{OptIn=false},"opt-in");
RejectFixture(fixtureTarget with{Interactive=false},"non-interactive");
RejectFixture(fixtureTarget with{ConfigIsPrivate=false},"config-permissions");
RejectFixture(fixtureTarget with{ConfigIsSymlink=true},"config-symlink");
RejectFixture(fixtureTarget with{Dedicated=false},"dedicated-marker");
RejectFixture(fixtureTarget with{FixtureEnabled=false},"fixture-authority");
RejectFixture(fixtureTarget with{Endpoint="db.example.test,14336"},"remote-sql");
RejectFixture(fixtureTarget with{Database="KohamiTest"},"wrong-database");
RejectFixture(fixtureTarget with{Dataset="production"},"wrong-dataset");
RejectFixture(fixtureTarget with{SqlServerName="sql.example.test"},"wrong-tls-name");
RejectFixture(fixtureTarget with{TlsStrict=false},"non-strict-tls");
RejectFixture(fixtureTarget with{CertificatePinValid=false},"certificate-pin");
RejectFixture(fixtureTarget with{ClientAddress="10.0.0.4"},"non-loopback-client");
RejectFixture(fixtureTarget with{ApplicationName="Microsoft SQL Server"},"wrong-sql-client");
var activationNow=DateTimeOffset.UtcNow;var activationToken=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));var activationFingerprint=new string('a',64);
DevelopmentFixtureBootstrapPolicy.ValidateActivation(activationToken,activationToken,activationNow,activationNow.AddMinutes(5),activationNow,activationFingerprint,activationFingerprint);passed++;
void RejectActivation(string token,string confirmation,DateTimeOffset issued,DateTimeOffset expires,string actualFingerprint,string name)=>Reject(()=>DevelopmentFixtureBootstrapPolicy.ValidateActivation(token,confirmation,issued,expires,activationNow,activationFingerprint,actualFingerprint),"activation-"+name);
RejectActivation(activationToken,"different-token",activationNow,activationNow.AddMinutes(5),activationFingerprint,"confirmation");
RejectActivation(activationToken,activationToken,activationNow,activationNow.AddMinutes(11),activationFingerprint,"long-expiry");
RejectActivation(activationToken,activationToken,activationNow,activationNow,activationFingerprint,"expiry-equality");
RejectActivation(activationToken,activationToken,activationNow.AddSeconds(1),activationNow.AddMinutes(5),activationFingerprint,"future-issue");
RejectActivation(activationToken,activationToken,activationNow,activationNow.AddMinutes(5),new string('b',64),"wrong-instance");
var activationHash=DevelopmentFixtureBootstrapPolicy.ActivationHash(activationToken,"operation-a",activationFingerprint);
if(activationHash.Length!=64||activationHash==DevelopmentFixtureBootstrapPolicy.ActivationHash(activationToken,"operation-b",activationFingerprint)||activationHash==DevelopmentFixtureBootstrapPolicy.ActivationHash(activationToken,"operation-a",new string('b',64)))throw new Exception("FAILED: activation hash target and operation binding");passed++;
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
var restore=new RestoreManifest("demo_m0","kohami.m0.v1","KohamiM0Restore_test","kohami.m0.restore.test",now,now,new string('f',64),["session1","totp1"]);
RestoreGuard.Verify(restore,new string('f',64),["session1","totp1"]);passed++;
Reject(()=>RestoreGuard.Verify(restore with{NewDatabase="KohamiM0"},new string('f',64),["session1","totp1"]),"overwrite");
Reject(()=>RestoreGuard.Verify(restore with{TargetNamespace="kohami.m0.v1"},new string('f',64),["session1","totp1"]),"namespace");
Reject(()=>RestoreGuard.Verify(restore with{LedgerThrough=now.AddSeconds(-1)},new string('f',64),["session1","totp1"]),"cutoff");
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
Console.WriteLine($"PASS native signed-scope expiry wrong-key new-target namespace cutoff tests={passed}; live OS confirmation NOT_EXECUTED");
