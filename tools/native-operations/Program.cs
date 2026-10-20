using Kohami.M0.Core;
using Kohami.M0.Native;
try {
var normalized=args.ToList();var seenLocalDir=false;var seenPurpose=false;
for(var i=1;i<normalized.Count;){
 var option=normalized[i];if(option is not ("--local-dir" or "--fixture-purpose")){i++;continue;}
 if(i+1>=normalized.Count||string.IsNullOrWhiteSpace(normalized[i+1]))throw new InvalidOperationException("LOCAL_PROFILE_ARGUMENT_INVALID");
 if(option=="--local-dir"){if(seenLocalDir)throw new InvalidOperationException("LOCAL_PROFILE_ARGUMENT_INVALID");seenLocalDir=true;Environment.SetEnvironmentVariable("KOHAMI_M0_LOCAL_DIR",normalized[i+1]);}
 else{if(seenPurpose||normalized[i+1] is not ("wallet03-http" or "local-demo"))throw new InvalidOperationException("FIXTURE_PURPOSE_INVALID");seenPurpose=true;Environment.SetEnvironmentVariable("KOHAMI_M0_FIXTURE_PURPOSE",normalized[i+1]);}
 normalized.RemoveRange(i,2);
}
args=normalized.ToArray();
if(seenPurpose&&(args.Length==0||args[0]!="write-fixture-config"))throw new InvalidOperationException("FIXTURE_PURPOSE_ARGUMENT_NOT_APPLICABLE");
if(args.Length==2&&args[0]=="initialize-rings") {
 // This creates synthetic development rings only. It does not issue native authority or
 // satisfy the production OS provisioning evidence. SetupOnly remains fenced.
 KeyRings.Initialize(M0Settings.Load(args[1]));Console.WriteLine("SYNTHETIC_RINGS_INITIALIZED_OWNER_NOT_ACTIVATED");return;
}
if(args.Length==3&&args[0] is "native" or "native-resume"){await Cli.Run(args);return;}
if(args.Length==2&&args[0]=="probe-sql"){await SqlProvision.ProbeAsync(args[1]);return;}
if(args.Length==2&&args[0]=="provision-sql"){await SqlProvision.RunAsync(args[1]);return;}
if(args.Length==2&&args[0]=="repair-grants-fixture"){await SqlProvision.ApplyFixtureGrantsAsync(args[1],"localhost,14336");return;}
if(args.Length==2&&args[0]=="repair-grants-local"){await SqlProvision.ApplyFixtureGrantsAsync(args[1],"localhost,14334");return;}
if(args.Length is 2 or 3&&args[0]=="fixture-status"){await SqlProvision.CheckTargetAsync(args[1],FixtureEndpoint(args));return;}
if(args.Length is 2 or 3&&args[0]=="provision-fixture"){await SqlProvision.RunAsync(args[1],FixtureEndpoint(args));return;}
if(args.Length is 2 or 3&&args[0]=="write-fixture-config"){SqlProvision.WriteFixtureConfig(args[1],FixturePort(args));return;}
if(args.Length is 2 or 3&&args[0]=="seed-issue02-owner"){await SqlProvision.SeedIssue02OwnerAsync(args[1],FixturePort(args));return;}
if(args.Length==2&&args[0]=="migrate-native-local"){await SqlProvision.MigrateNativeLocalAsync(args[1]);return;}
if(args.Length==3&&args[0]=="migrate-dedicated-fixture"){await SqlProvision.MigrateDedicatedFixtureAsync(args[1],FixturePort(args));return;}
if(args.Length==3&&args[0]=="verify-local-demo-catalog"){await SqlProvision.VerifyLocalDemoCatalogAsync(args[1],FixturePort(args));return;}
if(args.Length==3&&args[0]=="wallet03-http-activate"){await SqlProvision.ActivateWallet03HttpAsync(args[1],FixturePort(args));return;}
if(args.Length==2&&args[0]=="migrate-kohami-sql"){await SqlProvision.MigrateKohamiSqlFixtureAsync(args[1]);return;}
if(args.Length==2&&args[0]=="local-development-bootstrap"){await SqlProvision.InitializeLocalDevelopmentAsync(args[1]);return;}
if(args.Length==2&&args[0]=="adopt-native-local-baseline"){await SqlProvision.AdoptNativeLocalBaselineAsync(args[1]);return;}
Console.Error.WriteLine("USAGE: initialize-rings <private-config>; migrate-native-local|migrate-kohami-sql|local-development-bootstrap|adopt-native-local-baseline <repo-root>; migrate-dedicated-fixture|verify-local-demo-catalog|seed-issue02-owner|wallet03-http-activate <repo-root> <port>; fixture subcommands accept --local-dir <repo-local-profile>; native production Owner operations require the OS-confirmed broker");Environment.ExitCode=2;

static string FixtureEndpoint(string[] arguments)=>"localhost,"+FixturePort(arguments);
static int FixturePort(string[] arguments){var port=arguments.Length==3&&int.TryParse(arguments[2],out var parsed)?parsed:14336;if(port<14336||port>14399)throw new InvalidOperationException("FIXTURE_PORT_OUT_OF_RANGE");if(Environment.GetEnvironmentVariable("KOHAMI_M0_FIXTURE_PURPOSE")=="local-demo"&&port is 14349 or 14350)throw new InvalidOperationException("FIXTURE_PORT_RESERVED");return port;}

} catch (Exception ex) {
 Console.Error.WriteLine("Diagnostic type="+ex.GetType().Name+(ex is Microsoft.Data.SqlClient.SqlException sql ? " sql-number="+sql.Number : ""));
 if(ex is InvalidOperationException && SafeDiagnostic.ReasonCode(ex.Message) is { } reasonCode) Console.Error.WriteLine("Safe reason code="+reasonCode);
 Console.Error.WriteLine("NATIVE_OPERATION_BLOCKED_OR_FAILED: no secret details logged; inspect scoped checkpoints");Environment.ExitCode=1;
}
