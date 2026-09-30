#!/bin/sh
set -eu
IMAGE=ghcr.io/dieses4lipp/pokemon-tcg-discord-bot:main
cd /opt/pokebot

before=$(docker image inspect -f '{{.Id}}' "$IMAGE" 2>/dev/null || echo none)
docker compose pull --quiet
after=$(docker image inspect -f '{{.Id}}' "$IMAGE")

[ "$before" = "$after" ] && exit 0

echo "new image ${after#sha256:} (was ${before#sha256:}) - deploying"
docker compose up -d

sleep 30
running=$(docker inspect -f '{{.State.Running}} {{.RestartCount}}' pokebot)
echo "state: $running"
docker image prune -f >/dev/null
[ "$running" = "true 0" ]
