# Release Notes

Bugfix release of `zfs-tool`, working around an issue, when zfs unexpectedly 
shifts the command line output by one char.

## fixed

- workaround for zfs shifted output issue

## changed

- improved internal cli handling
- improved date handling (always use default format and override locale)
- improved error handling and output
- improved default output for snapshot destroyal

