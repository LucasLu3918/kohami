#!/bin/sh
set -eu

# All resources belong to this development-only Compose project.
repo_root=$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)
env_file="$repo_root/.local/sql-server.env"
compose_file="$repo_root/infra/sql-server/compose.yaml"
docker_context="${KOHAMI_DOCKER_CONTEXT:-orbstack}"

credentials() {
  if [ ! -f "$env_file" ]; then
    umask 077
    mkdir -p "$repo_root/.local"
    python3 - "$env_file" <<'PYCODE'
from pathlib import Path
import secrets, sys, os
p = Path(sys.argv[1])
with p.open("x") as f:
    f.write("MSSQL_SA_PASSWORD=Kohami!" + secrets.token_hex(24) + "\n")
    f.write("KOHAMI_DB_PASSWORD=Kohami!" + secrets.token_hex(24) + "\n")
os.chmod(p, 0o600)
PYCODE
    printf '%s\n' "Created private .local/sql-server.env (passwords are not printed)."
  fi
}

compose() { docker --context "$docker_context" compose --env-file "$env_file" -f "$compose_file" "$@"; }

run_sa() {
  compose exec -T sqlserver /bin/sh -c \
    'SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -W -h -1 "$@"' sh "$@"
}

run_app() {
  SQLCMDPASSWORD=$(sed -n 's/^KOHAMI_DB_PASSWORD=//p' "$env_file")
  export SQLCMDPASSWORD
  compose exec -T -e SQLCMDPASSWORD sqlserver \
    /opt/mssql-tools18/bin/sqlcmd -S localhost -U kohami_test -d KohamiTest -C -b -W -h -1 "$@"
  unset SQLCMDPASSWORD
}

initialize() {
  app_password=$(sed -n 's/^KOHAMI_DB_PASSWORD=//p' "$env_file")
  case "$app_password" in ''|*[!A-Za-z0-9!]* ) printf '%s\n' 'Invalid local credential format.' >&2; exit 1;; esac
  # Password sent over stdin, never a sqlcmd argument or log output.
  printf '%s\n' \
    "IF DB_ID(N'KohamiTest') IS NULL CREATE DATABASE [KohamiTest];" \
    "IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name=N'kohami_test') CREATE LOGIN [kohami_test] WITH PASSWORD=N'$app_password';" \
    "GO" "USE [KohamiTest];" \
    "IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name=N'kohami_test') CREATE USER [kohami_test] FOR LOGIN [kohami_test];" \
    "IF IS_ROLEMEMBER(N'db_owner',N'kohami_test') <> 1 ALTER ROLE [db_owner] ADD MEMBER [kohami_test];" | run_sa
  unset app_password
}

check() {
  run_app -Q "SET NOCOUNT ON; IF DB_NAME()<>N'KohamiTest' OR ISNULL(IS_SRVROLEMEMBER(N'sysadmin'),1)<>0 THROW 51000,'Unexpected test login scope',1; SELECT 'LOGIN_SCOPE_PASS'; SELECT CONVERT(varchar(40),SERVERPROPERTY('ProductVersion')),CONVERT(varchar(100),SERVERPROPERTY('Edition')); IF OBJECT_ID(N'dbo.KohamiEnvironmentProbe',N'U') IS NULL CREATE TABLE dbo.KohamiEnvironmentProbe(ProbeKey nvarchar(64) PRIMARY KEY, ProbeValue int NOT NULL); IF NOT EXISTS(SELECT 1 FROM dbo.KohamiEnvironmentProbe WHERE ProbeKey=N'persistence') INSERT dbo.KohamiEnvironmentProbe VALUES(N'persistence',1); BEGIN TRAN; UPDATE dbo.KohamiEnvironmentProbe SET ProbeValue=2 WHERE ProbeKey=N'persistence'; ROLLBACK; IF ISNULL((SELECT ProbeValue FROM dbo.KohamiEnvironmentProbe WHERE ProbeKey=N'persistence'),0)<>1 THROW 51001,'Rollback probe failed',1; SELECT 'TRANSACTION_ROLLBACK_PASS';"
}

action="${1:-status}"
case "$action" in
  start) credentials ;;
  status)
    if [ ! -f "$env_file" ]; then printf '%s\n' 'Not initialized; run manage.sh start.'; exit 0; fi
    ;;
  check|restart-probe|stop)
    if [ ! -f "$env_file" ]; then printf '%s\n' 'Not initialized; run manage.sh start.' >&2; exit 1; fi
    ;;
  *) printf '%s\n' 'Usage: manage.sh start|check|restart-probe|status|stop' >&2; exit 2 ;;
esac

case "$action" in
  start)
    compose config --quiet
    compose up -d --wait --wait-timeout 240
    initialize
    check
    ;;
  check) check ;;
  restart-probe)
    check
    compose stop sqlserver
    compose up -d --wait --wait-timeout 240 sqlserver
    run_app -Q "SET NOCOUNT ON; IF ISNULL((SELECT ProbeValue FROM dbo.KohamiEnvironmentProbe WHERE ProbeKey=N'persistence'),0)<>1 THROW 51002,'Persistence probe failed',1; SELECT 'CONTAINER_RESTART_PERSISTENCE_PASS';"
    ;;
  status) compose ps ;;
  stop) compose stop sqlserver ;;
  *) printf '%s\n' 'Usage: manage.sh start|check|restart-probe|status|stop' >&2; exit 2 ;;
esac
