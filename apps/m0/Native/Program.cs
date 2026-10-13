using Kohami.M0.Core;
using Kohami.M0.Native;
try {
if(args.Length==2&&args[0]=="initialize-rings") {
 // This creates synthetic development rings only. It does not issue native authority or
 // satisfy the production OS provisioning evidence. SetupOnly remains fenced.
 KeyRings.Initialize(M0Settings.Load(args[1]));Console.WriteLine("SYNTHETIC_RINGS_INITIALIZED_OWNER_NOT_ACTIVATED");return;
}
if(args.Length==3&&args[0]=="native"){await Cli.Run(args);return;}
if(args.Length==2&&args[0]=="provision-sql"){await SqlProvision.RunAsync(args[1]);return;}
Console.Error.WriteLine("USAGE: initialize-rings <private-config>; owner operations require the OS-confirmed broker");Environment.ExitCode=2;

} catch { Console.Error.WriteLine("NATIVE_OPERATION_BLOCKED_OR_FAILED: no secret details logged; inspect scoped checkpoints");Environment.ExitCode=1;}
