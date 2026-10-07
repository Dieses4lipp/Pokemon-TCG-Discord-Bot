"""Regenerates the "sets" section of Data/packSettings.json from the cover folders.

Each folder in Assets/sets_covers becomes a set entry with its era profile (see profile_for) and
its non-empty cover images. Profiles (slots and odds) are edited by hand in the JSON and kept as
they are.

Usage: python tools/gen_pack_settings.py [repo root]
"""
import json
import os
import re
import sys
from collections import Counter

repo = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), "..")
settings_path = os.path.join(repo, "Data", "packSettings.json")
covers_dir = os.path.join(repo, "Assets", "sets_covers")


def profile_for(set_id: str) -> str:
    rules = [
        (r"^(base[1-5]|gym[12]|neo[1-4]|lc)$", "wotc"),
        (r"^ecard[1-3]$", "ecard"),
        (r"^ex([1-9]|1[0-6])$", "ex"),
        (r"^dp[1-7]$", "dp"),
        (r"^(pl[1-4]|hgss[1-4]|col1)$", "pl-hgss"),
        (r"^(bw([1-9]|1[01])|xy([1-9]|1[0-2])|g1|sm([1-9]|1[0-2])|sm3\.5|sm7\.5|sm115)$", "bw-sm"),
        (r"^det1$", "det"),
        (r"^dc1$", "dc"),
        (r"^(cel25|2021swsh)$", "any-4"),
        (r"^dv1$", "any-5"),
        (r"^(2011bw|2012bw|201[4-9](xy|sm))$", "single"),
        (r"^(swsh([1-9]|1[0-2])|swsh(3\.5|4\.5|10\.5|12\.5))$", "swsh"),
        (r"^(sv(0[1-9]|10)|sv(03\.5|04\.5|06\.5|08\.5|10\.5b|10\.5w)|me0[12]|me02\.5)$", "sv"),
        (r"^(a[1-4][ab]?|b[12]a?)$", "pocket"),
        (r"^pop[1-9]$", "pop"),
    ]
    for pattern, profile in rules:
        if re.match(pattern, set_id):
            return profile
    return "mixed"


def natural_key(name: str):
    return [int(p) if p.isdigit() else p for p in re.split(r"(\d+)", name)]


with open(settings_path, encoding="utf-8") as f:
    settings = json.load(f)
profiles = settings["profiles"]

sets = {}
for set_id in sorted(os.listdir(covers_dir), key=natural_key):
    folder = os.path.join(covers_dir, set_id)
    if not os.path.isdir(folder):
        continue
    covers = sorted(
        (f for f in os.listdir(folder)
         if f.lower().endswith(".jpg") and os.path.getsize(os.path.join(folder, f)) > 0),
        key=natural_key,
    )
    sets[set_id] = {"profile": profile_for(set_id), "covers": covers}

settings["sets"] = sets

for set_id, set_config in sets.items():
    assert set_config["profile"] in profiles, (set_id, set_config["profile"])
for name, p in profiles.items():
    assert sum(s["count"] for s in p["slots"]) == p["cardsPerPack"], name
    for s in p["slots"]:
        assert abs(sum(s["rarities"].values()) - 100) < 0.01, (name, s["name"], sum(s["rarities"].values()))

with open(settings_path, "w", encoding="utf-8", newline="\r\n") as f:
    json.dump(settings, f, indent=2, ensure_ascii=False)
    f.write("\n")

print(Counter(s["profile"] for s in sets.values()))
print("no covers:", [k for k, v in sets.items() if not v["covers"]])
