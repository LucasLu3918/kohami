using Kohami.M0.Core;
using Kohami.M0.Native;
try {
if(args.Length==2&&args[0]=="initialize-rings") {
 // This creates synthetic development rings only. It does not issue native authority or
 // satisfy the production OS provisioning evidence. SetupOnly remains fenced.
 KeyRings.Initialize(M0Settings.Load(args[1]));Console.WriteLine("SYNTHETIC_RINGS_INITIALIZED_OWNER_NOT_ACTIVATED");return;
}
if(args.Length==2&&args[0]=="development-fixture-bootstrap"){await DevelopmentFixtureBootstrap.RunAsync(args[1]);return;}
if(args.Length==2&&args[0]=="development-fixture-bootstrap-checks"){await DevelopmentFixtureBootstrap.RunSqlChecksAsync(args[1]);return;}
if(args.Length==2&&args[0]=="development-fixture-bootstrap-verify"){await DevelopmentFixtureBootstrap.VerifySqlBootstrapAsync(args[1]);return;}
if(args.Length==3&&args[0] is "native" or "native-resume"){await Cli.Run(args);return;}
if(args.Length==2&&args[0]=="probe-sql"){await SqlProvision.ProbeAsync(args[1]);return;}
if(args.Length==2&&args[0]=="provision-sql"){await SqlProvision.RunAsync(args[1]);return;}
if(args.Length==2&&args[0]=="repair-grants-fixture"){await SqlProvision.ApplyFixtureGrantsAsync(args[1],"localhost,14336");return;}
if(args.Length==2&&args[0]=="repair-grants-local"){await SqlProvision.ApplyFixtureGrantsAsync(args[1],"localhost,14334");return;}
if(args.Length==2&&args[0]=="fixture-status"){await SqlProvision.CheckTargetAsync(args[1],"localhost,14336");return;}
if(args.Length==2&&args[0]=="provision-fixture"){await SqlProvision.RunAsync(args[1],"localhost,14336");return;}
if(args.Length==2&&args[0]=="write-fixture-config"){SqlProvision.WriteFixtureConfig(args[1]);return;}
if(args.Length==2&&args[0]=="migrate-native-local"){await SqlProvision.MigrateNativeLocalAsync(args[1]);return;}
Console.Error.WriteLine("USAGE: initialize-rings <private-config>; development-fixture-bootstrap <dedicated-fixture-config>; development-fixture-bootstrap-checks <dedicated-fixture-config>; development-fixture-bootstrap-verify <dedicated-fixture-config>; migrate-native-local <repo-root>; native Owner operations require the OS-confirmed broker");Environment.ExitCode=2;

} catch (Exception ex) { Console.Error.WriteLine("Diagnostic type="+ex.GetType().Name+(ex is Microsoft.Data.SqlClient.SqlException sql ? " sql-number="+sql.Number : "")); Console.Error.WriteLine("NATIVE_OPERATION_BLOCKED_OR_FAILED: no secret details logged; inspect scoped checkpoints");Environment.ExitCode=1;}
