#!/bin/sh
set -eu

repo_root=$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)
local_dir="$repo_root/.local/kohami-sql-test/identity"
compose_file="$repo_root/infra/sql-server/compose.yaml"
native_dll="$repo_root/tools/native-operations/bin/Release/net10.0/Native.dll"
docker_context="${KOHAMI_DOCKER_CONTEXT:-orbstack}"

compose() {
  docker --context "$docker_context" compose --env-file "$local_dir/sql-container.env" \
    -f "$compose_file" "$@"
}

native() {
  KOHAMI_LOCAL_DIR="$local_dir" KOHAMI_FIXTURE_PURPOSE=kohami-sql-test \
    dotnet "$native_dll" "$@"
}

prepare() {
  python3 "$repo_root/scripts/foundation/prepare-kohami-sql-test.py"
}

check() {
  if [ ! -f "$local_dir/fixture-private.json" ]; then
    printf '%s\n' 'Not provisioned; run manage.sh start.' >&2
    exit 1
  fi
  native fixture-status "$repo_root" 14333 --local-dir "$local_dir"
}

action="${1:-status}"
case "$action" in
  start)
    prepare
    compose config --quiet
    compose up -d --wait --wait-timeout 240
    if [ ! -f "$local_dir/fixture-private.json" ]; then
      native provision-fixture "$repo_root" 14333 --local-dir "$local_dir" --fixture-purpose kohami-sql-test
      native migrate-kohami-sql "$repo_root" --local-dir "$local_dir" --fixture-purpose kohami-sql-test
      native write-fixture-config "$repo_root" 14333 --local-dir "$local_dir" --fixture-purpose kohami-sql-test
    fi
    check
    ;;
  status)
    if [ ! -f "$local_dir/sql-container.env" ]; then
      printf '%s\n' 'Not initialized; run manage.sh start.'
      exit 0
    fi
    compose ps
    ;;
  check)
    check
    ;;
  restart-probe)
    check
    compose stop sqlserver
    compose up -d --wait --wait-timeout 240 sqlserver
    check
    ;;
  stop)
    if [ ! -f "$local_dir/sql-container.env" ]; then
      printf '%s\n' 'Not initialized; nothing to stop.'
      exit 0
    fi
    compose stop sqlserver
    ;;
  *)
    printf '%s\n' 'Usage: manage.sh start|check|restart-probe|status|stop' >&2
    exit 2
    ;;
esac
