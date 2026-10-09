#!/bin/sh
# Copies the newest database backup the bot wrote (daily, see Core/DatabaseBackup.cs) to the NAS
# share on CT 105. Fails, and so marks the unit failed, when that backup is older than 26 hours:
# a stuck bot or a full volume shows up here instead of as a silently stale copy.
set -eu
BACKUP_DIR=/opt/pokebot/data/backups
SHARE=//192.168.178.190/Netzwerkspeicher
REMOTE_DIR=Backups/pokebot
# "username=..." and "password=..." lines, chmod 600
CREDENTIALS=/etc/pokebot-backup.cred

latest=$(find "$BACKUP_DIR" -maxdepth 1 -name 'bot-*.db' -mmin -1560 | sort | tail -n 1)
if [ -z "$latest" ]; then
  echo "no backup newer than 26 h in $BACKUP_DIR" >&2
  exit 1
fi

name=$(basename "$latest")
# mkdir fails harmlessly once the directories exist; the put decides the exit status
smbclient "$SHARE" -A "$CREDENTIALS" -c "mkdir Backups; mkdir $REMOTE_DIR; cd $REMOTE_DIR; put $latest $name"
echo "copied $name to $SHARE/$REMOTE_DIR"
