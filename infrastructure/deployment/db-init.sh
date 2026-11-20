#!/bin/bash
# Aplica database/Scripts, Views, StoredProcedures y Seed en el contenedor de SQL Server.
set -euo pipefail
SQLCMD="/opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P ${SA_PASSWORD} -C -b -I"

$SQLCMD -d master -Q "IF DB_ID(N'FlowDesk') IS NULL CREATE DATABASE FlowDesk;"

for folder in Scripts Views StoredProcedures Seed; do
  for file in $(ls /database/$folder/*.sql | sort); do
    name=$(basename "$file")
    if [ "$folder" = "Scripts" ]; then
      applied=$($SQLCMD -d FlowDesk -h -1 -W -Q "SET NOCOUNT ON; IF OBJECT_ID(N'dbo.SchemaVersions') IS NOT NULL SELECT COUNT(*) FROM dbo.SchemaVersions WHERE ScriptName = N'$name' ELSE SELECT 0;" | tail -n 1)
      if [ "$applied" != "0" ]; then echo "= $name"; continue; fi
    fi
    echo "+ $folder/$name"
    $SQLCMD -d FlowDesk -i "$file"
    if [ "$folder" = "Scripts" ]; then
      $SQLCMD -d FlowDesk -Q "INSERT INTO dbo.SchemaVersions (ScriptName) VALUES (N'$name');"
    fi
  done
done
echo "Base de datos lista."
