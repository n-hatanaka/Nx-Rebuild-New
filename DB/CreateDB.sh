#!/bin/bash

# --- 設定項目 ---
PGUSER="akino_user"
PGPASSWORD="your_password"
PGHOST="192.168.12.145"
PGPORT="5432"
DBNAME="nx_db"
CONTAINER_NAME="postgres_srv"

cd "$(dirname "$0")"

export PGPASSWORD=$PGPASSWORD

echo "--- 1. 初期化 (postgres DB経由) ---"
docker exec -e PGPASSWORD=$PGPASSWORD $CONTAINER_NAME psql -U $PGUSER -d postgres -c "DROP DATABASE IF EXISTS $DBNAME;"
docker exec -e PGPASSWORD=$PGPASSWORD $CONTAINER_NAME psql -U $PGUSER -d postgres -c "CREATE DATABASE $DBNAME ENCODING 'UTF8';"

echo "--- 2. スキーマ流し込み ---"
# NAS上のファイルをコンテナのpsqlへ
docker exec -i -e PGPASSWORD=$PGPASSWORD $CONTAINER_NAME psql -U $PGUSER -d $DBNAME < "BaseTables20260811.sql"

echo "--- 3. データ注入 (Zmst ---"
cat initZmst.sql Zmst.csv | docker exec -i -e PGPASSWORD=$PGPASSWORD $CONTAINER_NAME psql -U $PGUSER -d $DBNAME

echo "--- 4. データ注入 (ColName ---"
cat initColName.sql ColName.csv | docker exec -i -e PGPASSWORD=$PGPASSWORD $CONTAINER_NAME psql -U $PGUSER -d $DBNAME

echo "--- 5. データ注入 (tan_m ---"
cat inittan_m.sql tan_m.csv | docker exec -i -e PGPASSWORD=$PGPASSWORD $CONTAINER_NAME psql -U $PGUSER -d $DBNAME

echo "--- 6. データ注入 (gun_m ---"
cat initgun_m.sql gun_m.csv | docker exec -i -e PGPASSWORD=$PGPASSWORD $CONTAINER_NAME psql -U $PGUSER -d $DBNAME



echo "--- 7. 構築完了 ---"