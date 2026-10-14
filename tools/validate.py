#!/usr/bin/env python3
"""
Validador global de Data/ (todos los *.json).

Chequeos:
 (1) nextEventId que no existe
 (2) cadenas de mas de 4 niveles (raiz + 3 subeventos)
 (3) weights que no suman 100 por lista de outcomes (choice / accion / subevento)
 (4) ids duplicados globales
 (5) flags / stats / rasgos / education / cityStatus fuera de Schema.md
 (6) DEAD con weight > 15
 (7) ageMin / ageMax incoherentes (0..120, min <= max)
 (8) textos (text / resultText) duplicados
 (9) JSON invalido

Uso:  python tools/validate.py [dir_datos] [schema.md]
Sale con codigo 1 si hay errores.
"""
import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DEFAULT_DATA = os.path.join(ROOT, "src", "BitLifeClone", "Data")

DATA_DIR = DEFAULT_DATA
SCHEMA_PATH = os.path.join(DEFAULT_DATA, "Schema.md")
_args = [a for a in sys.argv[1:] if not a.startswith("--")]
if len(_args) >= 1:
    DATA_DIR = _args[0]
if len(_args) >= 2:
    SCHEMA_PATH = _args[1]

SECTION_MAP = {
    "Stats": "stats",
    "Flags": "flags",
    "Traits": "traits",
    "Education": "education",
    "CityStatus": "citystatus",
    "RelationTypes": "relations",
    "Currencies": "currencies",
    "Licenses": "licenses",
    "Specialties": "specialties",
    "MilitaryBranches": "branches",
}


def parse_schema(path):
    secs = {}
    cur = None
    with open(path, encoding="utf-8") as f:
        for line in f:
            m = re.match(r"^##\s+(.+?)\s*$", line)
            if m:
                name = m.group(1).strip()
                cur = SECTION_MAP.get(name)
                if cur:
                    secs[cur] = set()
                continue
            if cur:
                m2 = re.match(r"^-\s+`([^`]+)`", line.strip())
                if m2:
                    secs[cur].add(m2.group(1))
    return secs


def walk(obj):
    """Recorre recursivamente y devuelve todos los dicts."""
    if isinstance(obj, dict):
        yield obj
        for v in obj.values():
            yield from walk(v)
    elif isinstance(obj, list):
        for v in obj:
            yield from walk(v)


def norm_text(s):
    return re.sub(r"\s+", " ", s.strip().lower())


def main():
    if not os.path.isdir(DATA_DIR):
        print(f"No existe el directorio de datos: {DATA_DIR}")
        return 1
    if not os.path.isfile(SCHEMA_PATH):
        print(f"No existe Schema.md: {SCHEMA_PATH}")
        return 1

    sch = parse_schema(SCHEMA_PATH)
    errors = []

    files = []
    for dirpath, _dirs, names in os.walk(DATA_DIR):
        for n in names:
            if n.lower().endswith(".json"):
                files.append(os.path.join(dirpath, n))
    files.sort()

    docs = {}
    all_ids = {}          # id -> archivo
    id_objs = {}          # id -> objeto
    texts = {}            # texto normalizado -> [(archivo, id)]

    # ---- carga ----
    for path in files:
        base = os.path.relpath(path, DATA_DIR)
        try:
            with open(path, encoding="utf-8") as f:
                docs[path] = json.load(f)
        except Exception as e:
            errors.append(f"{base}: JSON invalido ({e})")

    # ---- ids globales ----
    for path, doc in docs.items():
        base = os.path.relpath(path, DATA_DIR)
        for obj in walk(doc):
            oid = obj.get("id")
            if isinstance(oid, str) and oid:
                if oid in all_ids:
                    errors.append(f"{base}: id duplicado '{oid}' (ya en {all_ids[oid]})")
                else:
                    all_ids[oid] = base
                    id_objs[oid] = obj

    # ---- chequeos por objeto ----
    for path, doc in docs.items():
        base = os.path.relpath(path, DATA_DIR)
        for obj in walk(doc):
            oid = obj.get("id", "?")

            # (3) weights
            outs = obj.get("outcomes")
            if isinstance(outs, list) and outs and all(isinstance(o, dict) and "weight" in o for o in outs):
                total = sum(o.get("weight", 0) for o in outs if isinstance(o.get("weight"), int))
                if total != 100:
                    errors.append(f"{base}:{oid}: weights suman {total} (no 100)")

            # (7) edades
            if "ageMin" in obj or "ageMax" in obj:
                mn, mx = obj.get("ageMin"), obj.get("ageMax")
                if not isinstance(mn, int) or not isinstance(mx, int):
                    errors.append(f"{base}:{oid}: ageMin/ageMax deben ser enteros")
                elif not (0 <= mn <= mx <= 120):
                    errors.append(f"{base}:{oid}: edades incoherentes {mn}-{mx}")

            # (6) DEAD y (5) flags en outcomes/resultados
            for o in (outs if isinstance(outs, list) else []):
                if not isinstance(o, dict):
                    continue
                fl = o.get("flag")
                if fl is not None:
                    if fl not in sch["flags"]:
                        errors.append(f"{base}:{oid}: flag fuera de Schema '{fl}'")
                    if fl == "DEAD" and isinstance(o.get("weight"), int) and o["weight"] > 15:
                        errors.append(f"{base}:{oid}: DEAD con weight {o['weight']} (>15)")
                rf = o.get("removeFlag")
                if rf is not None and rf not in sch["flags"]:
                    errors.append(f"{base}:{oid}: removeFlag fuera de Schema '{rf}'")
                # stats en outcomes: cualquier clave no conocida debe ser stat del Schema
                for k in o:
                    if k in NON_STAT_KEYS:
                        continue
                    if k not in sch["stats"]:
                        errors.append(f"{base}:{oid}: clave/stat fuera de Schema '{k}'")
                # traits
                tw = o.get("traitWeightMods")
                if isinstance(tw, dict):
                    for t in tw:
                        if t not in sch["traits"]:
                            errors.append(f"{base}:{oid}: rasgo fuera de Schema '{t}'")
                # money entero
                if "money" in o and not isinstance(o["money"], int):
                    errors.append(f"{base}:{oid}: money no entero")


            # (5) flags en requires
            req = obj.get("requires")
            if isinstance(req, dict):
                for fk in ("flagsNone", "flagsAny", "flagsAll", "flagsExist"):
                    for v in req.get(fk, []) or []:
                        if v not in sch["flags"]:
                            errors.append(f"{base}:{oid}: requires.{fk} flag fuera de Schema '{v}'")
                for t in req.get("traitsAny", []) or []:
                    if t not in sch["traits"]:
                        errors.append(f"{base}:{oid}: requires.traitsAny rasgo fuera de Schema '{t}'")
                for sk in ("minStat", "maxStat"):
                    for stat in (req.get(sk) or {}):
                        if stat not in sch["stats"]:
                            errors.append(f"{base}:{oid}: requires.{sk}.{stat} stat fuera de Schema")

            # (5) education / cityStatus / currency directos
            edu = obj.get("education")
            if isinstance(edu, str) and edu not in sch["education"]:
                errors.append(f"{base}:{oid}: education fuera de Schema '{edu}'")
            cs = obj.get("cityStatus") or obj.get("status")
            if isinstance(cs, str) and cs not in sch["citystatus"]:
                errors.append(f"{base}:{oid}: cityStatus fuera de Schema '{cs}'")
            cur = obj.get("currency")
            if isinstance(cur, str) and cur not in sch["currencies"]:
                errors.append(f"{base}:{oid}: currency fuera de Schema '{cur}'")
            sp = obj.get("specialty")
            if isinstance(sp, str) and sp not in sch["specialties"]:
                errors.append(f"{base}:{oid}: specialty fuera de Schema '{sp}'")
            br = obj.get("branch")
            if isinstance(br, str) and br not in sch["branches"] and br != "school":
                errors.append(f"{base}:{oid}: branch fuera de Schema '{br}'")
            # (extra) un militar con branch debe definir specialty (apunta al pool de guerra)
            if isinstance(br, str) and br != "school" and not obj.get("specialty"):
                errors.append(f"{base}:{oid}: branch '{br}' sin specialty")

            # (10) promotesTo debe apuntar a un id que exista en Data/
            pn = obj.get("promotesTo")
            if isinstance(pn, str) and pn and pn not in all_ids:
                errors.append(f"{base}:{oid}: promotesTo roto '{pn}'")

            # (8) textos
            # 'title' (cargos/rangos) se repite por diseno entre trabajos; no se auditan.
            for key in ("text", "resultText", "label"):
                v = obj.get(key)
                if isinstance(v, str) and v.strip():
                    # los 'text' cortos suelen ser labels de choice y se repiten sin ser plagio
                    if key == "text" and len(v.strip()) < 60:
                        continue
                    nt = norm_text(v)
                    texts.setdefault(nt, []).append((base, oid, key))

    # (1) nextEventId roto
    for path, doc in docs.items():
        base = os.path.relpath(path, DATA_DIR)
        for obj in walk(doc):
            oid = obj.get("id", "?")
            for o in (obj.get("outcomes") if isinstance(obj.get("outcomes"), list) else []):
                if isinstance(o, dict):
                    nxt = o.get("nextEventId")
                    if isinstance(nxt, str) and nxt and nxt not in all_ids:
                        errors.append(f"{base}:{oid}: nextEventId roto '{nxt}'")

    # (2) profundidad de cadenas (max 4)
    def outcome_lists(obj):
        """Devuelve las listas de outcomes de un objeto: directas y dentro de choices."""
        lists = []
        if isinstance(obj.get("outcomes"), list):
            lists.append(obj["outcomes"])
        for ch in (obj.get("choices") or []):
            if isinstance(ch, dict) and isinstance(ch.get("outcomes"), list):
                lists.append(ch["outcomes"])
        return lists

    def children(oid):
        obj = id_objs.get(oid)
        if not obj:
            return []
        out = []
        for outs in outcome_lists(obj):
            for o in outs:
                if isinstance(o, dict):
                    n = o.get("nextEventId")
                    if isinstance(n, str) and n in id_objs:
                        out.append(n)
        return out

    memo = {}
    visiting = set()
    cycle = set()

    def depth(oid):
        if oid in memo:
            return memo[oid]
        if oid in visiting:
            cycle.add(oid)
            return 0
        visiting.add(oid)
        d = 1
        for c in children(oid):
            d = max(d, 1 + depth(c))
        visiting.discard(oid)
        memo[oid] = d
        return d

    for oid in id_objs:
        d = depth(oid)
        if d > 4:
            errors.append(f"{all_ids.get(oid,'?')}:{oid}: cadena de {d} niveles (max 4)")
    for c in cycle:
        errors.append(f"{all_ids.get(c,'?')}:{c}: ciclo en nextEventId")

    # (8) duplicados de texto
    #   - label/title: solo cuenta si se repite dentro del MISMO archivo (los rangos y
    #     los nombres de accion se repiten a proposito entre ramas).
    #   - resultText / text narrativo: se vigila global (copy-paste real).
    for nt, where in texts.items():
        if len(where) <= 1:
            continue
        by_file = {}
        for b, i, k in where:
            by_file[b] = by_file.get(b, 0) + 1
        same_file_dup = any(c > 1 for c in by_file.values())
        is_narrative = any(k in ("resultText", "text") for _, _, k in where)
        if not (is_narrative or same_file_dup):
            continue
        locs = ", ".join(f"{b}:{i}({k})" for b, i, k in where[:5])
        errors.append(f"texto duplicado: \"{nt[:60]}\" en {locs}")

    # ---- reporte ----
    print(f"Archivos JSON: {len(files)} | ids: {len(all_ids)} | textos unicos: {len(texts)}")
    if errors:
        print(f"ERRORES ({len(errors)}):")
        for e in errors[:200]:
            print(" -", e)
        return 1
    print("VALIDACION OK")
    return 0


# claves de un outcome que NO son stats (todo lo demas debe estar en Schema.Stats)
NON_STAT_KEYS = {
    "weight", "resultText", "result", "money", "flag", "removeFlag",
    "traitWeightMods", "nextEventId", "relationDelta", "setCity",
    "loseJob", "loseProperty",
}


if __name__ == "__main__":
    sys.exit(main())
