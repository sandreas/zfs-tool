# zfs-tool
Small command line tool that helps with various zfs tasks, e.g. cleanup snapshots.


# Todo
- Interop ZFS Lib instead of cli: https://www.mono-project.com/docs/advanced/pinvoke/
- 

# notes

https://git.bashclub.org/bashclub/zfs-housekeeping

Idea:
- keep-time=14d
- keep-number=30
- free-space=80G


zfsclean --keep=30d --filter="rpool/data/" --format-string="zfs destroy {name}" > cleanup.sh