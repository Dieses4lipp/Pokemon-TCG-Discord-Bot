"""Compares the rarity names in Data/packSettings.json against the rarities TCGdex reports.

Prints, per configured set, the rarities of its cards that no slot of its profile can roll
(those cards are never pulled), and every rarity name used in a profile that TCGdex does not know.

Usage: python tools/check_pack_rarities.py [repo root]
"""
import json
import os
import sys
import urllib.request
from collections import defaultdict

GRAPHQL_URL = "https://api.tcgdex.net/v2/graphql"
PAGE_SIZE = 100
ANY_RARITY = "*"
NO_RARITY = "None"

repo = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), "..")


def fetch_page(page: int) -> list:
    query = "{ cards(filters:{}, pagination:{page:%d,count:%d}) { rarity set { id } } }" % (page, PAGE_SIZE)
    request = urllib.request.Request(
        GRAPHQL_URL,
        data=json.dumps({"query": query}).encode(),
        headers={"Content-Type": "application/json", "User-Agent": "pokebot-rarity-check"},
    )
    with urllib.request.urlopen(request, timeout=60) as response:
        body = json.load(response)
    if body.get("errors"):
        raise RuntimeError(f"TCGdex GraphQL error on page {page}: {body['errors']}")
    return body["data"]["cards"]


def fetch_rarities_by_set() -> dict:
    rarities = defaultdict(set)
    page = 1
    while True:
        cards = fetch_page(page)
        if not cards:
            return rarities
        for card in cards:
            rarities[card["set"]["id"].lower()].add(card["rarity"] or "")
        page += 1


with open(os.path.join(repo, "Data", "packSettings.json"), encoding="utf-8") as f:
    settings = json.load(f)

rarities_by_set = fetch_rarities_by_set()
known_rarities = {r.lower() for rs in rarities_by_set.values() for r in rs}

problems = 0

for name, profile in settings["profiles"].items():
    for slot in profile["slots"]:
        for rarity in slot["rarities"]:
            if rarity != ANY_RARITY and rarity.lower() not in known_rarities:
                print(f"Profile '{name}', slot '{slot['name']}': TCGdex has no rarity '{rarity}'")
                problems += 1

for set_id, set_config in settings["sets"].items():
    profile = settings["profiles"][set_config["profile"]]
    slot_rarities = {r.lower() for s in profile["slots"] for r in s["rarities"]}
    if ANY_RARITY in slot_rarities:
        continue
    if set_id.lower() not in rarities_by_set:
        print(f"Set '{set_id}': not found on TCGdex")
        problems += 1
        continue
    uncovered = sorted(
        r for r in rarities_by_set[set_id.lower()] if r != NO_RARITY and r.lower() not in slot_rarities)
    if uncovered:
        print(f"Set '{set_id}' ({set_config['profile']}): no slot can roll {', '.join(uncovered)}")
        problems += 1

print(f"{problems} problem(s)")
sys.exit(1 if problems else 0)
