"""Run the local mock API and HTTPS Nuxt shell; no provisioning or Owner bypass."""
from pathlib import Path
import os,subprocess,signal
root=Path(__file__).resolve().parents[2];local=root/'.local/m0'
required=['runtime-private.json','browser/public.pem','browser/private.key']
if any(not (local/x).is_file() for x in required):raise SystemExit('BLOCKED: provision isolated SQL and private development material first')
env=os.environ.copy();env.update(M0_CONFIG=str(local/'runtime-private.json'),M0_CONTRACT=str(root/'docs/m0/Core.openapi.json'),M0_HTTPS_CERT=str(local/'browser/public.pem'),M0_HTTPS_KEY=str(local/'browser/private.key'))
api=root/'apps/m0/Api/bin/Release/net10.0/Api.dll'
if not api.is_file():raise SystemExit('BLOCKED: build API Release first')
children=[]
def stop(*args):
 for p in children:
  if p.poll() is None:p.terminate()
for sig in (signal.SIGINT,signal.SIGTERM):signal.signal(sig,stop)
try:
 children.append(subprocess.Popen(['/usr/local/share/dotnet/dotnet',str(api)],cwd=root,env=env))
 children.append(subprocess.Popen(['npm','run','dev'],cwd=root/'apps/storefront',env=env))
 print('Local mock: https://localhost:3000 — native activation and browser trust remain required',flush=True)
 while all(p.poll() is None for p in children):
  try:children[0].wait(timeout=1)
  except subprocess.TimeoutExpired:pass
finally:
 stop()
 for p in children:
  try:p.wait(timeout=5)
  except subprocess.TimeoutExpired:p.kill()
