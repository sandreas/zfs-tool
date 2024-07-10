#!/bin/sh

OBJ_DIR="${PWD}/zfsclean/obj"

# fix for https://github.com/dotnet/sdk/issues/21072
find "$OBJ_DIR" -type d -name 'R2R' -exec rm -r "{}" \;

dotnet publish -r linux-x64 -p:TargetFramework=net6.0 -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:PublishTrimmed=true --self-contained -c Release -o ./dist zfsclean/zfs_tool.csproj
# -p:PublishSingleFile=true --self-contained true -p:PublishReadyToRun=true -p:PublishTrimmed=true