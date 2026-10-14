#!/usr/bin/env python3
"""
A3: normaliza Jobs.json y separa las 'Business Jobs' (companies) a Companies.json.

Cambios:
 - moneda unificada a USD (EUR -> USD con eurToUsd)
 - baseSalary null -> valor por defecto segun categoria
 - education null -> 'None'
 - tier null -> 1 (ajustando baseSalary pa conservar el sueldo mostrado)
 - campos nuevos: category, icon (""), y branch/specialty en militares
 - cadenas promotesTo: se fuerza que el sueldo crezca en cada salto
 - companies -> Companies.json

Salidas:
 - src/BitLifeClone/Data/Jobs/Jobs.fixed.json
 - src/BitLifeClone/Data/Jobs/Companies.json
"""
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATA = os.path.join(ROOT, "src", "BitLifeClone", "Data")
SRC = os.path.join(DATA, "Jobs.json")
OUT_DIR = os.path.join(DATA, "Jobs")
OUT_JOBS = os.path.join(OUT_DIR, "Jobs.fixed.json")
OUT_COMP = os.path.join(OUT_DIR, "Companies.json")

DEFAULT_BASE = {
    "part_time": 12000, "freelance": 15000, "restaurant": 16000,
    "retailer": 16000, "small_business": 18000, "municipal": 30000,
}
SPECIALTY = [
    ("army", "army", "infantry"),
    ("marines", "marines", "infantry"),
    ("navy", "navy", "navy"),
    ("air_force", "air_force", "pilot"),
]
PILOT_IDS = {"student_pilot", "military_pilot", "flight_lead",
             "instructor_pilot", "squadron_commander", "wing_commander"}


def main():
    with open(SRC, encoding="utf-8") as f:
        doc = json.load(f)

    meta = doc["meta"]
    eur = meta.get("eurToUsd", 1.1)
    mults = meta["tierMultipliers"]
    growth = meta["tierGrowthAfterFour"]

    def mult(t):
        if t is None:
            return 1.0
        if t <= len(mults):
            return mults[t - 1]
        return mults[-1] * growth ** (t - len(mults))

    changes = {"eur": 0, "null_base": 0, "null_edu": 0, "null_tier": 0,
               "monotonic": 0, "military": 0}
    categories = []

    for cat in doc["categories"]:
        cid = cat["id"]
        new_jobs = []
        for j in cat["jobs"]:
            jid = j["id"]
            base = j.get("baseSalary")
            cur = j.get("currency")
            tier = j.get("tier")

            if cur == "EUR":
                base = round(base * eur) if base is not None else None
                cur = "USD"
                changes["eur"] += 1

            if base is None:
                base = DEFAULT_BASE.get(cid, 20000)
                changes["null_base"] += 1

            if j.get("education") is None:
                j["education"] = "None"
                changes["null_edu"] += 1

            if tier is None:
                # standalone: conservar el sueldo -> base = base / mult(1)
                base = round(base / mult(1))
                tier = 1
                changes["null_tier"] += 1

            j["baseSalary"] = base
            j["currency"] = "USD"
            j["tier"] = tier
            j["category"] = cid
            j["icon"] = ""
            j.setdefault("canHoldWithCriminalRecord", False)
            j.pop("note", None)

            if cid == "military":
                for pref, branch, spec in SPECIALTY:
                    if jid.startswith(pref):
                        j["branch"] = branch
                        j["specialty"] = spec
                        break
                else:
                    if jid in PILOT_IDS:
                        j["branch"] = "air_force"
                        j["specialty"] = "pilot"
                changes["military"] += 1
            new_jobs.append(j)

        # forzar crecimiento monotono en cada cadena promotesTo
        by_id = {j["id"]: j for j in new_jobs}

        def sal(j):
            return round(j["baseSalary"] * mult(j.get("tier")))

        for j in new_jobs:
            seen = 0
            nxt = j.get("promotesTo")
            while nxt and nxt in by_id and seen < 30:
                p, c = j, by_id[nxt]
                if sal(c) <= sal(p):
                    c["baseSalary"] = round(p["baseSalary"] * 1.15)
                    changes["monotonic"] += 1
                    j = c
                nxt = c.get("promotesTo")
                seen += 1

        categories.append({"id": cid, "name": cat.get("name"), "jobs": new_jobs})

    out = {"meta": meta, "categories": categories}

    companies = [{"id": c["id"], "name": c["name"]} for c in doc.get("companies", [])]

    os.makedirs(OUT_DIR, exist_ok=True)
    with open(OUT_JOBS, "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, indent=2)
    with open(OUT_COMP, "w", encoding="utf-8") as f:
        json.dump({"companies": companies}, f, ensure_ascii=False, indent=2)

    total = sum(len(c["jobs"]) for c in categories)
    print(f"Jobs.fixed.json: {len(categories)} categorias, {total} trabajos")
    print(f"Companies.json: {len(companies)} empresas")
    print("cambios:", changes)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
