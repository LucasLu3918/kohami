.DEFAULT_GOAL := help

NATIVE_DLL := tools/native-operations/bin/Release/net10.0/Native.dll
DB_VIEWER_COMPOSE := infra/db-viewer/compose.yaml
LOCAL_ENV := .local/environments/local.env

.PHONY: help env-local sql-start sql-stop sql-test-start sql-test-stop sql-test-check sql-test-status native-build app-build db-migrate local-accounts db-viewer viewer-stop dev dev-worker services status

help: ## 顯示可用的本機服務指令
	@awk 'BEGIN {FS = ":.*##"} /^[a-zA-Z0-9_-]+:.*##/ {printf "%-18s %s\n", $$1, $$2}' $(MAKEFILE_LIST)

env-local: ## 從私有 SQL 設定建立本機環境檔（不覆寫既有檔案）
	@if test -f "$(LOCAL_ENV)"; then echo "local.env 已存在；不覆寫私有憑證"; else python3 scripts/foundation/create-local-env.py; fi

sql-start: ## 啟動 OrbStack 與既有 kohami-sql SQL Server
	orbctl start
	docker start kohami-sql-sqlserver-1

sql-stop: ## 停止 kohami-sql SQL Server，保留資料庫與 volume
	docker stop kohami-sql-sqlserver-1

sql-test-start: native-build ## 啟動專用 kohami-sql-test SQL Server 與 Kohami 測試資料庫
	KOHAMI_DOCKER_CONTEXT=orbstack ./infra/sql-server/manage.sh start

sql-test-stop: ## 停止 kohami-sql-test，保留測試資料庫與 volumes
	KOHAMI_DOCKER_CONTEXT=orbstack ./infra/sql-server/manage.sh stop

sql-test-check: ## 檢查 kohami-sql-test marker 與 Strict TLS 連線
	KOHAMI_DOCKER_CONTEXT=orbstack ./infra/sql-server/manage.sh check

sql-test-status: ## 顯示 kohami-sql-test 狀態
	KOHAMI_DOCKER_CONTEXT=orbstack ./infra/sql-server/manage.sh status

native-build: ## 建置資料庫遷移與本機管理工具
	dotnet build tools/native-operations/Native.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nr:false

app-build: ## 建置 API、worker 與後端模組
	dotnet build Kohami.slnx -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nr:false

db-migrate: sql-start native-build ## 將 kohami-sql 的舊資料複製到 Kohami 並套用遷移
	dotnet "$(NATIVE_DLL)" migrate-kohami-sql "$(CURDIR)"

local-accounts: db-migrate ## 在已標記的本機 Kohami 資料庫建立／核對展示會員與唯讀管理員
	dotnet "$(NATIVE_DLL)" seed-local-accounts "$(CURDIR)"

db-viewer: db-migrate ## 建立唯讀資料庫帳號並啟動本機網頁檢視器
	@if test -f ".local/kohami-sql/identity/db-viewer.env" && ! test -L ".local/kohami-sql/identity/db-viewer.env"; then \
		echo "沿用既有唯讀資料庫帳號"; \
	else \
		dotnet "$(NATIVE_DLL)" provision-kohami-sql-viewer "$(CURDIR)"; \
	fi
	docker compose -f "$(DB_VIEWER_COMPOSE)" up -d

viewer-stop: ## 停止 CloudBeaver，保留本機設定與資料庫
	docker compose -f "$(DB_VIEWER_COMPOSE)" down

dev: db-migrate app-build local-accounts ## 啟動本機 API 與 Nuxt Storefront（含展示登入帳號）
	python3 scripts/foundation/run-local.py

dev-worker: db-migrate app-build local-accounts ## 啟動本機 API、worker 與 Nuxt Storefront
	python3 scripts/foundation/run-local.py --with-worker

services: db-viewer ## 啟動本機 SQL Server 與資料庫網頁檢視器

status: ## 顯示本機 SQL Server 與資料庫檢視器狀態
	docker ps --filter name=kohami-sql-sqlserver-1 --format '{{.Names}} {{.Status}} {{.Ports}}'
	KOHAMI_DOCKER_CONTEXT=orbstack ./infra/sql-server/manage.sh status
	docker compose -f "$(DB_VIEWER_COMPOSE)" ps
