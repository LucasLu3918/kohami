#!/usr/bin/env python3
"""Fresh isolated synthetic development environment. No Owner/native authority issuance.
Secrets stay ignored, restrictive files. Does not touch KohamiTest or system trust.
"""
import json, os, pathlib, secrets, subprocess, sys
ROOT=pathlib.Path(__file__).resolve().parents[2]
LOCAL_ROOT=ROOT/'.local'
LOCAL=pathlib.Path(os.environ.get('KOHAMI_M0_LOCAL_DIR',str(LOCAL_ROOT/'m0'))).expanduser()
if not LOCAL.is_absolute(): LOCAL=ROOT/LOCAL
LOCAL=pathlib.Path(os.path.abspath(LOCAL))
reserved=os.environ.get('KOHAMI_M0_LOCAL_RESERVED')=='1'
try: PROFILE_RELATIVE=LOCAL.relative_to(LOCAL_ROOT)
except ValueError: sys.exit('REFUSE_UNSAFE_OR_EXISTING_LOCAL_PROFILE')
if LOCAL_ROOT.is_symlink() or not LOCAL_ROOT.is_dir() or not PROFILE_RELATIVE.parts:
    sys.exit('REFUSE_UNSAFE_OR_EXISTING_LOCAL_PROFILE')
ancestor=LOCAL_ROOT
for part in PROFILE_RELATIVE.parts[:-1]:
    ancestor=ancestor/part
    if ancestor.is_symlink() or (ancestor.exists() and not ancestor.is_dir()):
        sys.exit('REFUSE_UNSAFE_OR_EXISTING_LOCAL_PROFILE')
if LOCAL.is_symlink(): sys.exit('REFUSE_UNSAFE_OR_EXISTING_LOCAL_PROFILE')
if LOCAL.exists() and not reserved:
    sys.exit('ALREADY_INITIALIZED: no overwrite; inspect current environment')
if LOCAL.exists() and (not LOCAL.is_dir() or any(LOCAL.iterdir())):
    sys.exit('REFUSE_UNSAFE_OR_EXISTING_LOCAL_PROFILE')
def run(args):
    subprocess.run(args,check=True,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
def write(path,value,mode=0o600):
    path.write_text(value);path.chmod(mode)
def cert(name,ca=False):
    base=LOCAL/name;base.mkdir(mode=0o700)
    cfg=base/'openssl.cnf'
    write(cfg,'[req]\ndistinguished_name=dn\nprompt=no\nx509_extensions=ext\n[dn]\nCN='+('localhost' if name in ['sql','browser'] else 'KOHAMI M0 '+name)+'\n[ext]\nbasicConstraints=critical,CA:'+('TRUE' if ca else 'FALSE')+'\nkeyUsage=critical,'+('keyCertSign,cRLSign' if ca else 'digitalSignature,keyEncipherment')+'\n'+('subjectKeyIdentifier=hash\nauthorityKeyIdentifier=keyid:always\n' if ca else 'subjectKeyIdentifier=hash\nauthorityKeyIdentifier=keyid,issuer\nextendedKeyUsage=serverAuth\nsubjectAltName=DNS:localhost\n'))
    run(['openssl','req','-new','-newkey','rsa:3072','-nodes','-keyout',str(base/'private.key'),'-out',str(base/'request.csr'),'-config',str(cfg)])
    if ca:
        run(['openssl','x509','-req','-in',str(base/'request.csr'),'-signkey',str(base/'private.key'),'-out',str(base/'public.pem'),'-days','365','-extensions','ext','-extfile',str(cfg)])
    else:
        run(['openssl','x509','-req','-in',str(base/'request.csr'),'-CA',str(LOCAL/'ca/public.pem'),'-CAkey',str(LOCAL/'ca/private.key'),'-CAcreateserial','-out',str(base/'public.pem'),'-days','90','-extensions','ext','-extfile',str(cfg)])
    for path in base.iterdir():path.chmod(0o600)
    return base
os.umask(0o077);LOCAL.mkdir(parents=True,mode=0o700,exist_ok=reserved)
ca=cert('ca',True);sql=cert('sql');browser=cert('browser');session=cert('session')
# SQL container service UID must read its TLS key, and only that mounted directory.
# The parent is 0700; runtime consumers do not mount it. UID enforcement by container.
sqlmount=LOCAL/'sql-tls';sqlmount.mkdir(mode=0o750)
write(sqlmount/'server.pem',(sql/'public.pem').read_text(),0o644)
write(sqlmount/'server.key',(sql/'private.key').read_text(),0o640)
passwords={name:'K0!'+secrets.token_urlsafe(36) for name in ['sa','api','worker','native','migrator','backup','restore']}
write(LOCAL/'sql-container.env','MSSQL_SA_PASSWORD='+passwords['sa']+'\n')
# Provisioner only. Native secret is imported through the OS-confirmed broker then removed.
write(LOCAL/'provisioner-private.json',json.dumps(passwords))
settings={'datasetId':'demo_m0','origin':'https://localhost:3000','identityNamespace':'kohami.m0.v1','sqlCertificatePath':str(sql/'public.pem'),'sqlCaPath':str(ca/'public.pem'),'sqlServerName':'localhost'}
for name in ['session']:
    p='K0!'+secrets.token_urlsafe(36);pf=LOCAL/(name+'-pfx.pass');write(pf,p)
    run(['openssl','pkcs12','-export','-in',str(LOCAL/name/'public.pem'),'-inkey',str(LOCAL/name/'private.key'),'-out',str(LOCAL/name/'wrap.pfx'),'-passout','file:'+str(pf)])
    settings[name+'RingPath']=str(LOCAL/(name+'-ring'));settings[name+'CertificatePath']=str(LOCAL/name/'wrap.pfx');settings[name+'CertificatePassword']=p
    pf.unlink();(LOCAL/name/'private.key').unlink()
def conn(name):return 'Server=localhost,14334;Database=KohamiM0;User ID=kohami_m0_'+name+';Password='+passwords[name]+';Encrypt=Strict;TrustServerCertificate=False;'
settings['connectionString']=conn('api');settings['workerConnectionString']=''
write(LOCAL/'runtime-private.json',json.dumps(settings))
worker={key:value for key,value in settings.items() if not key.startswith('session')};worker['connectionString']='';worker['workerConnectionString']=conn('worker');write(LOCAL/'worker-private.json',json.dumps(worker))
print('SYNTHETIC_LOCAL_MATERIAL_PREPARED: no system trust change; no Owner; no permit; native credential import pending')
