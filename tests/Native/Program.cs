using System.Security.Cryptography;
using Kohami.Core;
using Kohami.Native;
using System.Runtime.Versioning;
[assembly:SupportedOSPlatform("macos")]
var passed=0;
void Reject(Action action,string name){try{action();throw new Exception("UNEXPECTED_ACCEPT:"+name);}catch(InvalidOperationException){passed++;}}
if(args.Length==1 && args[0]=="--safe-diagnostic") {
 if(SafeDiagnostic.ReasonCode("SQL_CERTIFICATE_INVALID")!="SQL_CERTIFICATE_INVALID" ||
    SafeDiagnostic.ReasonCode("SQL_MIGRATION_CHECKSUM_MISMATCH:0004_identity")!="SQL_MIGRATION_CHECKSUM_MISMATCH" ||
    SafeDiagnostic.ReasonCode("connection string contains secret") is not null ||
    SafeDiagnostic.ReasonCode("SQL_CERTIFICATE_INVALID\nsecret") is not null)
  throw new Exception("FAILED: native diagnostic emits only a bounded machine reason code");
 Console.WriteLine("PASS native diagnostics expose only bounded reason codes");
 return;
}
if(args.Length==1 && args[0]=="--fixture-endpoint") {
 if(!SqlProvision.IsDedicatedFixtureEndpoint("localhost,14336") ||
    !SqlProvision.IsDedicatedFixtureEndpoint("localhost,14342") ||
    !SqlProvision.IsDedicatedFixtureEndpoint("localhost,14399") ||
    SqlProvision.IsDedicatedFixtureEndpoint("localhost,14335") ||
    SqlProvision.IsDedicatedFixtureEndpoint("localhost,14400") ||
    SqlProvision.IsDedicatedFixtureEndpoint("example.test,14342"))
  throw new Exception("FAILED: dedicated fixture endpoint range is exact and complete");
 Console.WriteLine("PASS dedicated fixture endpoint range 14336-14399");
 return;
}
if(args.Length==1 && args[0]=="--database-viewer-contract") {
 var generatedCredential=DatabaseViewerProvision.GenerateCredential();
 if(Convert.FromBase64String(generatedCredential).Length!=36)
  throw new Exception("FAILED: database viewer credential uses 36 cryptographically random bytes");
 var createLoginSql=DatabaseViewerProvision.CreateLoginSql();
 if(!createLoginSql.Contains("CREATE LOGIN [kohami_reader]",StringComparison.Ordinal) ||
    !createLoginSql.Contains("PASSWORD=",StringComparison.Ordinal) ||
    !createLoginSql.Contains("QUOTENAME(@credential,'''')",StringComparison.Ordinal))
  throw new Exception("FAILED: database viewer login DDL quotes the parameterized credential");
 Console.WriteLine("PASS database viewer provisioning uses random credential and quoted SQL parameter");
 return;
}
if(!SqlProvision.IsWallet03HttpPort(14336) || !SqlProvision.IsWallet03HttpPort(14350) ||
   SqlProvision.IsWallet03HttpPort(14349) || SqlProvision.IsWallet03HttpPort(14335) ||
   SqlProvision.IsWallet03HttpPort(14400))
 throw new Exception("FAILED: wallet03-http fixture port rejects pinned kohami-sql endpoint");
if(!SqlProvision.IsLocalDemoPort(14336)||!SqlProvision.IsLocalDemoPort(14351)||
   SqlProvision.IsLocalDemoPort(14349)||SqlProvision.IsLocalDemoPort(14350)||
   SqlProvision.IsLocalDemoPort(14335)||SqlProvision.IsLocalDemoPort(14400))
 throw new Exception("FAILED: local demo fixture rejects both protected SQL ports");
if(args.Length==1 && args[0]=="--native-target") {
 const string runtime="Server=localhost,14349;Database=Kohami;User ID=kohami_api;Password=placeholder;Encrypt=Strict;Trust Server Certificate=False";
 var native=NativeSqlTarget.BuildConnectionString(runtime,"native-only-secret","/private/cert.pem","localhost");
 var parsed=new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(native);
 if(parsed.DataSource!="localhost,14349" || parsed.InitialCatalog!="Kohami" || parsed.UserID!="kohami_native" || parsed.Password!="native-only-secret" || parsed.TrustServerCertificate || parsed.Encrypt!=Microsoft.Data.SqlClient.SqlConnectionEncryptOption.Strict || parsed.HostNameInCertificate!="localhost" || !string.Equals(parsed["Server Certificate"] as string,"/private/cert.pem",StringComparison.Ordinal))
  throw new Exception("FAILED: native authority uses the exact pinned local runtime target");
 foreach(var unsafeRuntime in new[]{runtime.Replace("14349","14334"),runtime.Replace("localhost","remote.example.test"),runtime.Replace("Kohami","master"),runtime.Replace("Trust Server Certificate=False","Trust Server Certificate=True")})
  Reject(()=>NativeSqlTarget.BuildConnectionString(unsafeRuntime,"secret","/private/cert.pem","localhost"),"unsafe-native-target");
 Console.WriteLine("PASS native authority target follows pinned local runtime SQL endpoint");
 return;
}
if(SafeDiagnostic.ReasonCode("SQL_CERTIFICATE_INVALID")!="SQL_CERTIFICATE_INVALID" ||
   SafeDiagnostic.ReasonCode("SQL_MIGRATION_CHECKSUM_MISMATCH:0004_identity")!="SQL_MIGRATION_CHECKSUM_MISMATCH" ||
   SafeDiagnostic.ReasonCode("connection string contains secret") is not null ||
   SafeDiagnostic.ReasonCode("SQL_CERTIFICATE_INVALID\nsecret") is not null)
 throw new Exception("FAILED: native diagnostic emits only a bounded machine reason code");
passed++;
if(args.Length==2&&args[0]=="--sql-migrations")await Kohami.NativeTests.SqlMigrationAcceptance.RunAsync(args[1]);
else if(args.Length==2&&args[0]=="--sql-baseline-adoption")await Kohami.NativeTests.SqlBaselineAdoptionAcceptance.RunAsync(args[1]);
using var issuer=ECDsa.Create(ECCurve.NamedCurves.nistP256);var pub=issuer.ExportSubjectPublicKeyInfo();var now=DateTimeOffset.UtcNow;
var preview=new OperationPreview("InitialOwnerPrepare","owner1","kohami_local",new string('a',64),1,new string('b',64),"synthetic test");
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
var restore=new RestoreManifest("kohami_local","kohami.identity_access.v1","KohamiRestore_test","kohami.identity_access.restore.test",now,now,new string('f',64),["session1","session2"]);
RestoreGuard.Verify(restore,new string('f',64),["session1","session2"]);passed++;
Reject(()=>RestoreGuard.Verify(restore with{NewDatabase="Kohami"},new string('f',64),["session1","session2"]),"overwrite");
Reject(()=>RestoreGuard.Verify(restore with{TargetNamespace="kohami.identity_access.v1"},new string('f',64),["session1","session2"]),"namespace");
Reject(()=>RestoreGuard.Verify(restore with{LedgerThrough=now.AddSeconds(-1)},new string('f',64),["session1","session2"]),"cutoff");
Reject(()=>RestoreGuard.Verify(restore,new string('f',64),["session1"]),"missing key");
Reject(()=>PermitVerifier.Verify(Sign(preview with{Action="PublicReset"},now),preview with{Action="PublicReset"},pub,now),"unknown action");
Reject(()=>PermitVerifier.Verify(Sign(preview with{Target="owner1\nforged"},now),preview with{Target="owner1\nforged"},pub,now),"newline binding");
Reject(()=>PermitVerifier.Verify(Sign(preview with{PreviewHash="false"},now),preview with{PreviewHash="false"},pub,now),"hash shape");
if(args.Length==2 && args[0]=="--tls-local") {
 var setting=Kohami.Core.RuntimeSettings.Load(args[1]);new Kohami.Core.SqlRuntime(setting).ValidateTls();passed++;
 var name=setting.SqlServerName;setting.SqlServerName="other.example.test";
 Reject(()=>new Kohami.Core.SqlRuntime(setting).ValidateTls(),"hostname");setting.SqlServerName=name;
 var ca=setting.SqlCaPath;setting.SqlCaPath=setting.SessionCertificatePath;
 // PFX is intentionally not a CA certificate input. Parse must fail, never create keys.
 try{new Kohami.Core.SqlRuntime(setting).ValidateTls();throw new Exception("UNEXPECTED_ACCEPT:wrong CA");}catch(CryptographicException){passed++;}
 setting.SqlCaPath=ca;
}
var migrationPlan=SqlMigrations.Load(Directory.GetCurrentDirectory());
var migrationIds=migrationPlan.Select(m=>m.Id).ToArray();
if(migrationIds.Length<2||!migrationIds.SequenceEqual(migrationIds.Order(StringComparer.Ordinal))||migrationIds.Distinct(StringComparer.Ordinal).Count()!=migrationIds.Length)throw new Exception("FAILED: SQL migrations have a stable unique ascending order");passed++;
var walletMigration=migrationPlan.SingleOrDefault(m=>m.Id=="0013_member_wallet_ledger");
if(walletMigration is null||!walletMigration.Batches.Any(batch=>batch.Sql.Replace("m0commerce.","commerce.",StringComparison.Ordinal).Contains("commerce.WalletAccounts",StringComparison.Ordinal)&&batch.Sql.Replace("m0commerce.","commerce.",StringComparison.Ordinal).Contains("commerce.WalletLedger",StringComparison.Ordinal)&&batch.Sql.Contains("wallet_debit",StringComparison.Ordinal)))throw new Exception("FAILED: member wallet migration defines account, immutable ledger, and atomic debit procedure");passed++;
var walletApiGrantMigration=migrationPlan.SingleOrDefault(m=>m.Id=="0014_wallet_payment_api_writes");
if(walletApiGrantMigration is null)throw new Exception("FAILED: wallet payment API grants use a new versioned migration");
if(walletApiGrantMigration.SourceHash!="d8d21fa137ecda4442865dcf53c655d39ddc1e89e9c52987ccf8057ba3d9ff12"||
   SchemaContract.Supported.SingleOrDefault(row=>row.MigrationId=="0014_wallet_payment_api_writes")!=("0014_wallet_payment_api_writes",walletApiGrantMigration.SourceHash))
    throw new Exception("FAILED: wallet payment grants are limited and readiness follows the migration");
passed++;
var walletCheckoutPermissionMigration=migrationPlan.SingleOrDefault(m=>m.Id=="0029_wallet_checkout_api_permissions");
if(walletCheckoutPermissionMigration is null)throw new Exception("FAILED: wallet checkout permissions use an append-only migration");
var walletCheckoutPermissionSql=string.Join("\n",walletCheckoutPermissionMigration.Batches.Select(batch=>batch.Sql));
if(!walletCheckoutPermissionSql.Contains("GRANT EXECUTE ON OBJECT::commerce.wallet_debit TO role_kohami_api",StringComparison.Ordinal)||
   !walletCheckoutPermissionSql.Contains("GRANT INSERT ON OBJECT::commerce.Collections TO role_kohami_api",StringComparison.Ordinal)||
   !walletCheckoutPermissionSql.Contains("GRANT INSERT ON OBJECT::commerce.RefundUnits TO role_kohami_api",StringComparison.Ordinal)||
   walletCheckoutPermissionSql.Contains("GRANT INSERT ON OBJECT::commerce.WalletLedger TO role_kohami_api",StringComparison.Ordinal)||
   walletCheckoutPermissionSql.Contains("GRANT UPDATE ON OBJECT::commerce.WalletAccounts TO role_kohami_api",StringComparison.Ordinal)||
   SchemaContract.Supported.SingleOrDefault(row=>row.MigrationId==walletCheckoutPermissionMigration.Id)!=(walletCheckoutPermissionMigration.Id,walletCheckoutPermissionMigration.SourceHash))
    throw new Exception("FAILED: wallet checkout receives only its required atomic debit and accounting write permissions");
passed++;
var catalogAdminReadMigration=migrationPlan.SingleOrDefault(m=>m.Id=="0015_catalog_admin_read");
if(catalogAdminReadMigration is null)throw new Exception("FAILED: catalog admin read uses an append-only migration");
if(catalogAdminReadMigration.SourceHash!="b258012939bd42fb10c21ac70469e3f04ead867c067fc7584e5f44c7874d8ac0"||
   SchemaContract.Supported.SingleOrDefault(row=>row.MigrationId=="0015_catalog_admin_read")!=("0015_catalog_admin_read",catalogAdminReadMigration.SourceHash))
 throw new Exception("FAILED: catalog projection migration source is preserved and readiness order remains append-only");
var identityRenameMigration=migrationPlan.SingleOrDefault(m=>m.Id=="0016_kohami_identity_and_dataset_rename");
if(identityRenameMigration is null||
   SchemaContract.Supported.SingleOrDefault(row=>row.MigrationId==identityRenameMigration.Id)!=(identityRenameMigration.Id,identityRenameMigration.SourceHash))
 throw new Exception("FAILED: the identity and dataset migration source remains immutable");
passed++;
var runtimePermissionMigration=migrationPlan.SingleOrDefault(m=>m.Id=="0019_runtime_service_permissions");
var memberRegistrationMigration=migrationPlan.SingleOrDefault(m=>m.Id=="0020_member_registration");
var securityLockMigration=migrationPlan.SingleOrDefault(m=>m.Id=="0021_identity_security_lock");
var reportAuditGrantMigration=migrationPlan.SingleOrDefault(m=>m.Id=="0023_report_access_audit_api_insert");
var workerPurgeGrantMigration=migrationPlan.SingleOrDefault(m=>m.Id=="0024_security_log_purge_worker_execute");
var retentionWorkerGrantMigration=migrationPlan.SingleOrDefault(m=>m.Id=="0025_retention_worker_execute");
var nativeScopeMigration=migrationPlan.SingleOrDefault(m=>m.Id=="0026_kohami_native_operation_scope");
var nativeLockBridgeMigration=migrationPlan.SingleOrDefault(m=>m.Id=="0027_native_security_lock_bridge");
var nativeReconcileLockBridgeMigration=migrationPlan.SingleOrDefault(m=>m.Id=="0028_reconcile_security_lock_bridge");
var runtimePermissionSql=runtimePermissionMigration is null?"":string.Join("\n",runtimePermissionMigration.Batches.Select(batch=>batch.Sql));
string[] expectedRuntimeGrants=[
 "GRANT SELECT,INSERT,UPDATE ON OBJECT::commerce.Documents TO role_kohami_api",
 "GRANT SELECT,INSERT ON OBJECT::commerce.Idempotency TO role_kohami_api",
 "GRANT SELECT ON OBJECT::identity_access.SecurityState TO role_kohami_api",
 "GRANT EXECUTE ON OBJECT::identity_access.security_lock TO role_kohami_api",
 "GRANT EXECUTE ON OBJECT::identity_access.security_load_session TO role_kohami_api",
 "GRANT SELECT,INSERT,UPDATE ON OBJECT::commerce.Documents TO role_kohami_worker",
 "GRANT EXECUTE ON OBJECT::identity_access.security_dispatch_allowed TO role_kohami_worker",
];
if(runtimePermissionMigration is null||expectedRuntimeGrants.Any(grant=>!runtimePermissionSql.Contains(grant,StringComparison.OrdinalIgnoreCase))||
   runtimePermissionSql.Contains("ON SCHEMA::",StringComparison.OrdinalIgnoreCase)||runtimePermissionSql.Contains("db_datawriter",StringComparison.OrdinalIgnoreCase)||
   runtimePermissionSql.Contains("security_accept_totp",StringComparison.OrdinalIgnoreCase)||runtimePermissionSql.Contains("security_accept_recovery",StringComparison.OrdinalIgnoreCase))
 throw new Exception("FAILED: runtime permission migration restores only declared object and procedure grants");
var memberRegistrationSql=memberRegistrationMigration is null?"":string.Join("\n",memberRegistrationMigration.Batches.Select(batch=>batch.Sql));
if(memberRegistrationMigration is null||!memberRegistrationSql.Contains("security_register_member",StringComparison.Ordinal)||!memberRegistrationSql.Contains("MemberProfiles",StringComparison.Ordinal)||!memberRegistrationSql.Contains("AdminAuthenticated",StringComparison.Ordinal))
 throw new Exception("FAILED: member registration migration persists optional profile data and bounded sessions");
var securityLockSql=securityLockMigration is null?"":string.Join("\n",securityLockMigration.Batches.Select(batch=>batch.Sql));
if(securityLockMigration is null||!securityLockSql.Contains("kohami.identity_access.security",StringComparison.Ordinal)||!securityLockSql.Contains("NCHAR(109)+NCHAR(48)",StringComparison.Ordinal)||!securityLockSql.Contains("role_kohami_api",StringComparison.Ordinal))
 throw new Exception("FAILED: security lock migration bridges the deployed transaction guard to the current shared lock");
if(SchemaContract.Supported.SingleOrDefault(row=>row.MigrationId==runtimePermissionMigration?.Id)!=(runtimePermissionMigration?.Id,runtimePermissionMigration?.SourceHash))
 throw new Exception("FAILED: runtime permission migration remains source-pinned");
var reportAuditGrantSql=reportAuditGrantMigration is null?"":string.Join("\n",reportAuditGrantMigration.Batches.Select(batch=>batch.Sql));
if(reportAuditGrantMigration is null||
   !reportAuditGrantSql.Contains("GRANT INSERT ON OBJECT::commerce.ReportAccessAudit TO role_kohami_api",StringComparison.OrdinalIgnoreCase)||
   reportAuditGrantSql.Contains("GRANT SELECT",StringComparison.OrdinalIgnoreCase)||
   SchemaContract.Supported.SingleOrDefault(row=>row.MigrationId==reportAuditGrantMigration.Id)!=(reportAuditGrantMigration.Id,reportAuditGrantMigration.SourceHash))
 throw new Exception("FAILED: report audit API receives only append permission in the latest migration");
var workerPurgeGrantSql=workerPurgeGrantMigration is null?"":string.Join("\n",workerPurgeGrantMigration.Batches.Select(batch=>batch.Sql));
if(workerPurgeGrantMigration is null||
   !workerPurgeGrantSql.Contains("GRANT EXECUTE ON OBJECT::identity_access.security_purge_expired_logs TO role_kohami_worker",StringComparison.OrdinalIgnoreCase)||
   workerPurgeGrantSql.Contains("GRANT SELECT",StringComparison.OrdinalIgnoreCase)||
   SchemaContract.Supported.SingleOrDefault(row=>row.MigrationId==workerPurgeGrantMigration.Id)!=(workerPurgeGrantMigration.Id,workerPurgeGrantMigration.SourceHash))
 throw new Exception("FAILED: worker receives only execute permission for security log purge in the latest migration");
var retentionWorkerGrantSql=retentionWorkerGrantMigration is null?"":string.Join("\n",retentionWorkerGrantMigration.Batches.Select(batch=>batch.Sql));
if(retentionWorkerGrantMigration is null||
   !retentionWorkerGrantSql.Contains("GRANT EXECUTE ON OBJECT::commerce.purge_report_access_audit TO role_kohami_worker",StringComparison.OrdinalIgnoreCase)||
   !retentionWorkerGrantSql.Contains("GRANT EXECUTE ON OBJECT::commerce.purge_expired_guest_proofs TO role_kohami_worker",StringComparison.OrdinalIgnoreCase)||
   retentionWorkerGrantSql.Contains("GRANT SELECT",StringComparison.OrdinalIgnoreCase)||
   SchemaContract.Supported.SingleOrDefault(row=>row.MigrationId==retentionWorkerGrantMigration.Id)!=(retentionWorkerGrantMigration.Id,retentionWorkerGrantMigration.SourceHash))
 throw new Exception("FAILED: worker receives only execute permission for each remaining retention procedure in the latest migration");
var nativeScopeSql=nativeScopeMigration is null?"":string.Join("\n",nativeScopeMigration.Batches.Select(batch=>batch.Sql));
if(nativeScopeMigration is null||
   !nativeScopeSql.Contains("DB_NAME()<>'Kohami' OR @Dataset<>'kohami_local'",StringComparison.OrdinalIgnoreCase)||
   !nativeScopeSql.Contains("identity_access.security_native_activate_owner",StringComparison.OrdinalIgnoreCase)||
   !nativeScopeSql.Contains("se.Stage='AdminAuthenticated'",StringComparison.OrdinalIgnoreCase)||
   nativeScopeSql.Contains("EncryptedSeed",StringComparison.OrdinalIgnoreCase)||
   nativeScopeSql.Contains("MfaVerifiedAt",StringComparison.OrdinalIgnoreCase)||
   nativeScopeSql.Contains("DB_NAME()<>'KohamiM0'",StringComparison.OrdinalIgnoreCase)||
   nativeScopeSql.Contains("@Dataset<>'demo_m0'",StringComparison.OrdinalIgnoreCase)||
   SchemaContract.Supported.SingleOrDefault(row=>row.MigrationId==nativeScopeMigration.Id)!=(nativeScopeMigration.Id,nativeScopeMigration.SourceHash))
 throw new Exception("FAILED: native operations use the renamed Kohami dataset scope in the latest migration");
passed++;
var nativeLockBridgeSql=nativeLockBridgeMigration is null?"":string.Join("\n",nativeLockBridgeMigration.Batches.Select(batch=>batch.Sql));
if(nativeLockBridgeMigration is null||
   !nativeLockBridgeSql.Contains("EXEC identity_access.security_lock;",StringComparison.OrdinalIgnoreCase)||
   !nativeLockBridgeSql.Contains("identity_access.security_native_activate_owner",StringComparison.OrdinalIgnoreCase)||
   !nativeLockBridgeSql.Contains("DB_NAME()<>'Kohami' OR @Dataset<>'kohami_local'",StringComparison.OrdinalIgnoreCase)||
   SchemaContract.Supported.SingleOrDefault(row=>row.MigrationId==nativeLockBridgeMigration.Id)!=(nativeLockBridgeMigration.Id,nativeLockBridgeMigration.SourceHash))
 throw new Exception("FAILED: latest native operations bridge current and compatibility security locks");
passed++;
var nativeReconcileLockBridgeSql=nativeReconcileLockBridgeMigration is null?"":string.Join("\n",nativeReconcileLockBridgeMigration.Batches.Select(batch=>batch.Sql));
if(nativeReconcileLockBridgeMigration is null||
   !nativeReconcileLockBridgeSql.Contains("CREATE OR ALTER PROCEDURE ops.Reconcile",StringComparison.OrdinalIgnoreCase)||
   nativeReconcileLockBridgeSql.Split("EXEC identity_access.security_lock;",StringSplitOptions.None).Length<3||
   !nativeReconcileLockBridgeSql.Contains("OWNER_REBIND_INCOMPLETE",StringComparison.OrdinalIgnoreCase)||
   SchemaContract.Supported.SingleOrDefault(row=>row.MigrationId==nativeReconcileLockBridgeMigration.Id)!=(nativeReconcileLockBridgeMigration.Id,nativeReconcileLockBridgeMigration.SourceHash))
 throw new Exception("FAILED: latest native reconciliation preserves Owner rebind rules and both security locks");
passed++;
if(SchemaContract.Supported.LastOrDefault()!=(walletCheckoutPermissionMigration.Id,walletCheckoutPermissionMigration.SourceHash))
 throw new Exception("FAILED: latest schema readiness follows the wallet checkout permissions migration");
passed++;
if(!SchemaContract.IsCurrent(migrationPlan.Select(m=> (m.Id,m.SourceHash))))throw new Exception("FAILED: readiness source hashes match the native migration plan");passed++;
if(!SchemaContract.IsCurrent(SchemaContract.Supported)||SchemaContract.IsCurrent(SchemaContract.Supported.Skip(1))||SchemaContract.IsCurrent(SchemaContract.Supported.Take(2))||SchemaContract.IsCurrent(SchemaContract.Supported.Append(("9999_unknown",new string('a',64))) )||SchemaContract.IsCurrent(SchemaContract.Supported.Select((row,index)=>index==1?(row.MigrationId,new string('a',64)):(row.MigrationId,row.SourceHash))))throw new Exception("FAILED: schema readiness rejects missing, incomplete, unknown, or mismatched ledger rows");passed++;
foreach(var migration in migrationPlan){if(migration.SourceHash.Length!=64||migration.SourceHash.Any(c=>!Uri.IsHexDigit(c)))throw new Exception("FAILED: SQL migration source has a SHA-256 identity");passed++;}
Reject(()=>SqlMigrations.ValidatePlan(migrationPlan.Reverse().ToArray()),"migration-order");
Reject(()=>SqlMigrations.ValidatePlan([migrationPlan[0],migrationPlan[0]]),"migration-duplicate");
Reject(()=>SqlMigrations.ValidatePlan([new SqlMigrationDefinition("0001_empty",[("test"," ")])]),"migration-empty-source");
Console.WriteLine($"PASS native signed-scope expiry wrong-key new-target namespace cutoff tests={passed}; live OS confirmation NOT_EXECUTED");
