#!/bin/sh
set -eu
IMAGE=ghcr.io/dieses4lipp/pokemon-tcg-discord-bot:main
COMPOSE_URL=https://raw.githubusercontent.com/Dieses4lipp/Pokemon-TCG-Discord-Bot/main/ops/compose.yml
cd /opt/pokebot

# Mounts and limits change together with the code, so the compose file follows main too.
# A download or validation failure keeps the current file instead of breaking the deploy.
compose_changed=false
if curl -fsS "$COMPOSE_URL" -o compose.yml.new && docker compose -f compose.yml.new config --quiet; then
    if cmp -s compose.yml.new compose.yml; then
        rm compose.yml.new
    else
        cp compose.yml compose.yml.bak
        mv compose.yml.new compose.yml
        compose_changed=true
        echo "compose.yml updated from main (previous one kept as compose.yml.bak)"
    fi
else
    rm -f compose.yml.new
    echo "could not fetch a valid compose.yml from main, keeping the current one" >&2
fi

before=$(docker image inspect -f '{{.Id}}' "$IMAGE" 2>/dev/null || echo none)
docker compose pull --quiet
after=$(docker image inspect -f '{{.Id}}' "$IMAGE")

[ "$before" = "$after" ] && [ "$compose_changed" = false ] && exit 0

echo "deploying image ${after#sha256:} (was ${before#sha256:}), compose changed: $compose_changed"
docker compose up -d

sleep 30
running=$(docker inspect -f '{{.State.Running}} {{.RestartCount}}' pokebot)
echo "state: $running"
docker image prune -f >/dev/null
[ "$running" = "true 0" ]
