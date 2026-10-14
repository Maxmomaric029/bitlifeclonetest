#!/usr/bin/env python3
"""
Refactor de Data/Jobs.json:
 1. Business Jobs -> seccion "companies" (negocios donde se aplica, no oficios).
 2. Sueldos por formula: {tier, baseSalary} por cadena (mult 0.75/1.0/1.3/1.7,
    y x1.3 por nivel tras la 4). Base = rung tier-2 listado; si es patologico
    (minimo de la cadena) -> mediana; si no hay tier-2 -> tier1/0.75.
 3. Moneda original por job + meta.eurToUsd (la conversion al USD ocurre al cargar).
 4. Reemplaza piloto de Airline y cadena de Policia; anade FBI y Militar.
"""
import json
import statistics
from copy import deepcopy

PATH = "src/BitLifeClone/Data/Jobs.json"
EUR = 1.10

def usd(v, cur):
    if v is None:
        return None
    return v * EUR if cur == "EUR" else v

def note_add(job, text):
    job["note"] = (job.get("note") + " | " + text) if job.get("note") else text

def cut_link(jobs, source_id, reason):
    for j in jobs:
        if j["id"] == source_id and j.get("promotesTo"):
            j["promotesTo"] = None
            note_add(j, reason)

def compute_tiers(jobs):
    """Corte de ciclos + profundidad (longest path) = tier."""
    by_id = {j["id"]: j for j in jobs}
    # corta ciclos
    for start in list(by_id):
        seen, cur = set(), start
        while by_id[cur].get("promotesTo"):
            if cur in seen:
                cut_link(jobs, cur, "Ascenso cortado: creaba un ciclo en los tiers.")
                break
            seen.add(cur)
            cur = by_id[cur]["promotesTo"]
            if cur not in by_id:
                break
    incoming = {j["id"]: 0 for j in jobs}
    for j in jobs:
        if j.get("promotesTo") in incoming:
            incoming[j["promotesTo"]] += 1
    depth = {}
    changed = True
    while changed:
        changed = False
        for j in jobs:
            d = depth.get(j["id"], 1)
            t = j.get("promotesTo")
            if t in by_id:
                td = depth.get(t, 1)
                if td < d + 1:
                    depth[t] = d + 1
                    changed = True
            if j["id"] not in depth:
                depth[j["id"]] = 1
                changed = True
    return {j["id"]: depth.get(j["id"], 1) for j in jobs}

def components(jobs):
    parent = {j["id"]: j["id"] for j in jobs}
    def find(x):
        while parent[x] != x:
            parent[x] = parent[parent[x]]
            x = parent[x]
        return x
    def union(a, b):
        ra, rb = find(a), find(b)
        if ra != rb:
            parent[rb] = ra
    for j in jobs:
        if j.get("promotesTo") in parent:
            union(j["id"], j["promotesTo"])
    groups = {}
    for j in jobs:
        groups.setdefault(find(j["id"]), []).append(j)
    return list(groups.values())

def assign_salary_fields(jobs, tiers):
    singles = set()
    for j in jobs:
        t = j.get("promotesTo")
        has_in = any(o.get("promotesTo") == j["id"] for o in jobs)
        if t is None and not has_in:
            singles.add(j["id"])
    for group in components(jobs):
        listed = [j for j in group if j.get("startingWage") is not None]
        tier2 = next((j for j in group if tiers[j["id"]] == 2), None)
        tier1 = next((j for j in group if tiers[j["id"]] == 1), None)
        base_val = base_cur = None
        t2u = usd(tier2.get("startingWage"), tier2.get("currency")) if tier2 and tier2.get("startingWage") is not None else None
        med = None
        if listed:
            med = statistics.median(usd(j["startingWage"], j.get("currency")) for j in listed)
        if t2u is not None and t2u >= (med if med is not None else t2u):
            base_val, base_cur = tier2["startingWage"], tier2.get("currency") or "USD"
        elif med is not None:
            base_val, base_cur = round(med), "USD"
        elif tier1 and tier1.get("startingWage") is not None:
            base_val = round(tier1["startingWage"] / 0.75)
            base_cur = tier1.get("currency") or "USD"
        for j in group:
            if j["id"] not in singles:
                j["tier"] = tiers[j["id"]]
            if base_val is not None:
                j["baseSalary"] = base_val
                j["currency"] = base_cur
            j.pop("startingWage", None)

def reorder(job):
    order = ["id", "title", "promotesTo", "tier", "baseSalary", "currency",
             "education", "experienceYears", "canHoldWithCriminalRecord", "note"]
    return {k: job[k] for k in order if k in job}

# ---------------------------------------------------------------- nuevos datos
def chain(items, base):
    """items = [(id, title, salary)] en orden ascendente; base = salary del tier 2."""
    out = []
    for i, (jid, title, sal) in enumerate(items):
        j = {"id": jid, "title": title, "education": None, "experienceYears": 0,
             "canHoldWithCriminalRecord": None}
        if i + 1 < len(items):
            j["promotesTo"] = items[i + 1][0]
        else:
            j["promotesTo"] = None
        if len(items) > 1:
            j["tier"] = i + 1
        if sal is not None:
            j["baseSalary"] = base
            j["currency"] = "USD"
        out.append(j)
    return out

army_e = [("E-1", "Private", 24000), ("E-2", "Private E-2", 27000),
          ("E-3", "Private First Class", 28500), ("E-4", "Specialist", 33000),
          ("E-5", "Sergeant", 38000), ("E-6", "Staff Sergeant", 44000),
          ("E-7", "Sergeant First Class", 53000), ("E-8", "Master Sergeant", 66000),
          ("E-9", "Sergeant Major", 80000), ("E-9 top", "Command Sergeant Major", 90000)]
navy_e = army_e[:9]
navy_e[1] = ("E-2", "Seaman Apprentice", 27000)
navy_e = [("E-1", "Seaman Recruit", 24000), ("E-2", "Seaman Apprentice", 27000),
          ("E-3", "Seaman", 28500), ("E-4", "Petty Officer 3rd", 33000),
          ("E-5", "Petty Officer 2nd", 38000), ("E-6", "Petty Officer 1st", 44000),
          ("E-7", "Chief Petty Officer", 53000), ("E-8", "Senior Chief Petty Officer", 66000),
          ("E-9", "Master Chief Petty Officer", 80000)]
marines_e = [("E-1", "Private", 24000), ("E-2", "Private First Class", 27000),
             ("E-3", "Lance Corporal", 28500), ("E-4", "Corporal", 33000),
             ("E-5", "Sergeant", 38000), ("E-6", "Staff Sergeant", 44000),
             ("E-7", "Gunnery Sergeant", 53000), ("E-8", "Master Sergeant", 66000),
             ("E-9", "Master Gunnery Sergeant", 80000), ("E-9 top", "Sergeant Major", 90000)]
af_e = [("E-1", "Airman Basic", 24000), ("E-2", "Airman", 27000),
        ("E-3", "Airman First Class", 28500), ("E-4", "Senior Airman", 33000),
        ("E-5", "Staff Sergeant", 38000), ("E-6", "Technical Sergeant", 44000),
        ("E-7", "Master Sergeant", 53000), ("E-8", "Senior Master Sergeant", 66000),
        ("E-9", "Chief Master Sergeant", 80000)]

def mk_chain(prefix, ladder, base):
    return chain([(f"{prefix}_{lvl.lower().replace('-', '').replace(' ', '')}", title, sal)
                  for lvl, title, sal in ladder], base)

military_jobs = []
military_jobs += mk_chain("army", army_e, 27000)
military_jobs += mk_chain("navy", navy_e, 27000)
military_jobs += mk_chain("marines", marines_e, 27000)
military_jobs += mk_chain("air_force", af_e, 27000)
military_jobs += chain([("army_wo1", "Warrant Officer 1 (WO1)", 52000),
                        ("army_cw2", "Chief Warrant Officer 2 (CW2)", 60000),
                        ("army_cw3", "Chief Warrant Officer 3 (CW3)", 70000),
                        ("army_cw4", "Chief Warrant Officer 4 (CW4)", 80000),
                        ("army_cw5", "Chief Warrant Officer 5 (CW5)", 100000)], 60000)

officers = [("second_lieutenant", "Second Lieutenant", 46000),
            ("first_lieutenant", "First Lieutenant", 54000),
            ("captain", "Captain", 70000), ("major", "Major", 85000),
            ("lieutenant_colonel", "Lieutenant Colonel", 100000),
            ("colonel", "Colonel", 125000),
            ("brigadier_general", "Brigadier General", 160000),
            ("major_general", "Major General", 180000),
            ("lieutenant_general", "Lieutenant General", 195000),
            ("general", "General", 205000)]
for pfx in ("army", "marines", "air_force"):
    military_jobs += chain([(f"{pfx}_{i}", t, s) for i, t, s in officers], 54000)

navy_off = [("navy_ensign", "Ensign", 46000), ("navy_lieutenant_jg", "Lieutenant JG", 54000),
            ("navy_lieutenant", "Lieutenant", 70000), ("navy_lieutenant_commander", "Lieutenant Commander", 85000),
            ("navy_commander", "Commander", 100000), ("navy_captain", "Captain", 125000),
            ("navy_rear_admiral_lower", "Rear Admiral (lower half)", 160000),
            ("navy_rear_admiral_upper", "Rear Admiral (upper half)", 180000),
            ("navy_vice_admiral", "Vice Admiral", 195000), ("navy_admiral", "Admiral", 205000)]
military_jobs += chain(navy_off, 54000)

military_jobs += chain([("student_pilot", "Student Pilot", 55000),
                        ("military_pilot", "Pilot", 85000),
                        ("flight_lead", "Flight Lead", 100000),
                        ("instructor_pilot", "Instructor Pilot", 115000),
                        ("squadron_commander", "Squadron Commander", 140000),
                        ("wing_commander", "Wing Commander", 175000)], 85000)

police_jobs = chain([("cadet", "Cadet", 35000), ("patrol_officer", "Patrol Officer", 55000),
                     ("detective", "Detective", 75000), ("sergeant", "Sergeant", 85000),
                     ("police_lieutenant", "Lieutenant", 100000), ("police_captain", "Captain", 120000),
                     ("deputy_chief", "Deputy Chief", 140000), ("chief_of_police", "Chief of Police", 165000)],
                    55000)

fbi_jobs = chain([("special_agent", "Special Agent", 70000),
                  ("senior_special_agent", "Senior Special Agent", 110000),
                  ("supervisory_special_agent", "Supervisory Special Agent", 140000),
                  ("assistant_special_agent_in_charge", "Assistant Special Agent in Charge", 160000),
                  ("special_agent_in_charge", "Special Agent in Charge", 175000),
                  ("section_chief", "Section Chief", 180000),
                  ("assistant_director", "Assistant Director", 190000),
                  ("deputy_director", "Deputy Director", 200000),
                  ("director", "Director", 220000)], 110000)

commercial_pilots = chain([("flight_cadet", "Flight Cadet", 40000),
                           ("second_officer", "Second Officer", 80000),
                           ("first_officer", "First Officer", 120000),
                           ("airline_captain", "Captain", 220000),
                           ("chief_pilot", "Chief Pilot", 250000)], 80000)

# ---------------------------------------------------------------- main
doc = json.load(open(PATH, encoding="utf-8"))
cats = {c["id"]: c for c in doc["categories"]}

# 1) business -> companies
biz = cats.pop("business")
companies = [{"id": j["id"], "name": j["title"]} for j in biz["jobs"]]

# 2) cortes y reemplazos
cut_link(cats["restaurant"]["jobs"], "sr_bartender",
         "Ascenso a Server eliminado: la promocion bajaba el sueldo (errata wiki).")

for j in cats["airline"]["jobs"]:
    if j["id"] in ("chief_pilot", "pilot", "co_pilot", "pilot_trainee"):
        j["_drop"] = True
cut_link(cats["airline"]["jobs"], "sr_flight_attendant",
         "Ascenso a Pilot Trainee eliminado: la linea de cabina de vuelo es una cadena aparte.")
cats["airline"]["jobs"] = [j for j in cats["airline"]["jobs"] if not j.get("_drop")]
cats["airline"]["jobs"] += deepcopy(commercial_pilots)
cats["airline"]["note"] = "Pilotos = cadena comercial reemplazante (sueldos realistas del usuario)."

cats["police_department"]["jobs"] = deepcopy(police_jobs)
cats["police_department"]["note"] = "Lista propia del usuario (reemplaza la de la wiki)."

cats["military"]["jobs"] = deepcopy(military_jobs)
cats["military"]["note"] = ("Escalas Army/Navy/Marines/Air Force (E-1..E-9), Warrant (Army), "
                            "Oficiales y Piloto de jet. Sueldos de la tabla del usuario; "
                            "baseSalary = rung tier-2 (E-2 = 27.000).")

doc["categories"].append({"id": "fbi", "name": "FBI Jobs",
                          "note": "Lista del usuario.",
                          "jobs": deepcopy(fbi_jobs)})

# 3) tiers + sueldos
for c in doc["categories"]:
    tiers = compute_tiers(c["jobs"])
    assign_salary_fields(c["jobs"], tiers)
    c["jobs"] = [reorder(j) for j in c["jobs"]]

# 4) documento final
out = {
    "meta": {
        "currency": "USD",
        "eurToUsd": EUR,
        "tierMultipliers": [0.75, 1.0, 1.3, 1.7],
        "tierGrowthAfterFour": 1.3,
        "salaryFormula": "SalaryUSD = round(BaseSalary x CurrencyToUSD x Multiplier(Tier)); Tier ausente = x1"
    },
    "companies": companies,
    "categories": doc["categories"],
}
with open(PATH, "w", encoding="utf-8") as f:
    json.dump(out, f, ensure_ascii=False, indent=2)
    f.write("\n")
print("OK - categorias:", len(out["categories"]), "| companies:", len(companies),
      "| trabajos:", sum(len(c["jobs"]) for c in out["categories"]))
