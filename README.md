# zfs-tool
Small command line tool that helps with various zfs tasks, e.g. cleanup snapshots.

## Introduction

ZFS is great as a filesystem and has good tools, but sometimes it can be a bit tricky to analyse and cleanup snapshots with the given tools, especially if you would like to gather free space. This is where `zfs-tool` should help.

## Installation

```bash
# download release archive (linux x64)
wget https://github.com/sandreas/zfs-tool/releases/download/v0.0.2/zfs-tool-0.0.2-linux-x64.tar.gz
# extract archive
tar xzf zfs-tool-*.tar.gz
# move zfs-tool to current dir
mv zfs-tool-*/zfs-tool .

# cleanup remaining dirs and files
rmdir zfs-tool-*/
rm zfs-tool-*.tar.gz

# run zfs tool an show version
./zfs-tool --version
```

## `list-snapshots`

The `list-snapshots` command can be used to filter, order and print out snapshots in various ways.

### TL;DR - examples

```bash
# list snapshots ordered by "written" property
zfs-tool list-snapshots --order-by="written"

# list snapshots on rpool/data with extra property "Reclaim", showing the space reclaimed after deletion
zfs-tool list-snapshots --contains="rpool/data@" --extra-properties="Reclaim"

# list snapshots ordered by "path asc, creation desc", thereby limiting to 5 newest snapshos per dataset
zfs-tool list-snapshots --order-by="path,-creation" --limit="5"

# custom output template with to destroy snapshots older than 180 days
zfs-tool list-snapshots --keep-time="180d" --format="zfs destroy {FullName}     # {Creation} {Written}"

# list all snapshots in rpool/data containing @backup that need to be deleted to gather 1 GB space
# CAUTION: this will automatically gather ReclaimSum for every matching snapshot and take long
zfs-tool list-snapshots --contains='rpool/data@' --contains='@backup' --required-space='1G'

# create custom destroy script for snapshots older than 180 days
echo '#!/bin/sh' > cleanup.sh
zfs-tool list-snapshots \
          --contains="rpool/data@" \
          --keep-time="180d" \
          --extra-properties=All \
          --format="zfs destroy {FullName}     # {Creation:yyyy-MM-dd HH\\:mm}  rcl: {ReclaimPadded} sum: {ReclaimSumPadded}" >> cleanup.sh

```
### Properties:

Properties can be used to format the output as well as filter or format the contents

- `Path` - main path of the snapshot (pool, vol or dataset), e.g. `rpool/data`
- `Name` - snapshot name, e.g. `zfs-auto-snap_monthly-2024-08-01-0452`
- `Creation` - snapshot creation date, e.g. `2024-08-01 06:52` (dates have custom formats)
- `FullName`¹ - full snapshot name
- `Written`¹ - written property
- `Reclaim`² - relaimed space if the snapshot is destroyed
- `ReclaimSum`² - aggregated reclaimed space, if snapshot-range from this path is destroyed

¹ these properties have a `...Padded` pendant to allow table like formatting 
² these `--extra-properties` are probably slow, they need to be calculated or determined by extra shell commands

### Parameters:

- `--format` - format the output, e.g. `{Creation:yyyy-MM-dd HH\\:mm} {FullNamePadded} {WrittenPadded}`
- `--keep-time` - keep snapshots for a specific time period (e.g. `--keep-time=30d` - don't delete snapshots younger than 30 days)
- `--contains` - full name contains a string, e.g. `--contains="rpool/data@"`      
- `--matches` - full name matches regex
- `--order-by` - order snapshots by field, e.g. `--order-by=path,creation,name`
- `--limit` - limit snapshots per path to a certain amount, e.g. `--limit=5`
- `--required-space` - determines which snapshots must be destroyed for gathering a certain amount of space, e.g. `--required-space=10G` - CAUTION: This can be really slow when not combined with other filtering parameters like `--contains`, `--matches` or `--limit`
- `--extra-properties` - load extra properties for snapshots (may be slow)
  - `None` - default
  - `Reclaim` - load space a destroy would gather
  - `ReclaimSum` - load aggregated space for followup snapshots
  - `All` - load all extra properties