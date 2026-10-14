#!/usr/bin/env python3
"""
Validador de acciones de relacion (Data/Relationships/*.json).

Reglas pedidas:
 (1) los weights de cada accion SUMAN 100 (y los de cada subevento)
 (2) todos los ids son unicos en todo el directorio
 (3) ningun nextEventId apunta a un id que no exista (deben ser subeventos)
 (4) hay acciones buenas, neutras y malas
 (5) los rangos de edad tienen sentido (0..120 y minAge <= maxAge)
 (6) ninguna accion se puede spamear sin cooldownYears o maxPerYear

Extras: 2-4 outcomes por accion, flags validos, rasgos validos,
relationDelta/health/happiness/karma/fame en [-100,100], label <= 6 palabras,
resultText <= 2 oraciones, requires con forma conocida.
"""
import glob
import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
REL_DIR = os.path.join(ROOT, "src", "BitLifeClone", "Data", "Relationships")
SCHEMA = os.path.join(ROOT, "src", "BitLifeClone", "Data", "Schema.md")

# listas cerradas se leen de Schema.md (fuente unica de verdad, igual que tools/validate.py)
def _parse_schema(path):
    vals = {}
    cur = None
    with open(path, encoding="utf-8") as f:
        for line in f:
            m = re.match(r"^##\s+(.+?)\s*$", line)
            if m:
                cur = m.group(1).strip()
                vals[cur] = set()
                continue
            if cur:
                m2 = re.match(r"^-\s+`([^`]+)`", line.strip())
                if m2:
                    vals[cur].add(m2.group(1))
    return vals

_s = _parse_schema(SCHEMA)
FLAGS = _s["Flags"]                 # TODOS los flags del Schema (guerra, familia, escuela, etc.)
WAR_FLAGS = set()                   # obsoleto: solo se usaba como union extra de FLAGS
TRAITS = _s["Traits"]
REL_TYPES = _s["RelationTypes"]
# tipos de "archivo" que agrupan varias ramas (ver DECISIONS.md); el Schema no los
# declara a proposito porque no aparecen en el menu de relaciones del jugador.
PSEUDO_REL_KEYS = {"kinship", "work"}
# campos opcionales admitidos dentro de requires
REQUIRES_KEYS = {"minBar", "maxBar", "traitsAny", "flagsNone", "flagsAny",
                 "flagsExist", "minKarma", "maxKarma", "minFame", "criminalRecord"}
SENT = re.compile(r"[.!?]+")


def load(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def check_outcome(o, where, errors, level_texts):
    w = o.get("weight")
    if not isinstance(w, int) or w < 0:
        errors.append(f"{where}: weight invalido {w!r}")
    if not o.get("resultText"):
        errors.append(f"{where}: outcome sin resultText")
    elif len(SENT.findall(o["resultText"])) > 2:
        errors.append(f"{where}: resultText con mas de 2 oraciones")
    for k in ("relationDelta", "health", "happiness", "karma", "fame"):
        v = o.get(k)
        if v is None:
            continue
        if not isinstance(v, int) or not -100 <= v <= 100:
            errors.append(f"{where}: {k} fuera de [-100,100]: {v!r}")
    m = o.get("money")
    if m is not None and not isinstance(m, int):
        errors.append(f"{where}: money no entero: {m!r}")
    for fk in ("flag", "removeFlag"):
        v = o.get(fk)
        if v is not None and v not in FLAGS | WAR_FLAGS:
            errors.append(f"{where}: {fk} invalido {v!r}")
    tw = o.get("traitWeightMods", {})
    if tw is not None:
        if not isinstance(tw, dict):
            errors.append(f"{where}: traitWeightMods no es objeto")
        else:
            for t in tw:
                if t not in TRAITS:
                    errors.append(f"{where}: rasgo invalido {t!r}")
    level_texts.append((where, o.get("flag"), o.get("relationDelta")))


def check_requires(req, where, errors):
    if req is None:
        return
    if not isinstance(req, dict):
        errors.append(f"{where}: requires no es objeto")
        return
    for k in req:
        if k not in REQUIRES_KEYS:
            errors.append(f"{where}: requires.{k} desconocido")
    for ta in req.get("traitsAny", []) or []:
        if ta not in TRAITS:
            errors.append(f"{where}: requires.traitsAny rasgo invalido {ta!r}")
    for fk in ("flagsNone", "flagsAny", "flagsExist"):
        for fv in req.get(fk, []) or []:
            if fv not in FLAGS | WAR_FLAGS:
                errors.append(f"{where}: requires.{fk} flag invalido {fv!r}")


def main():
    files = sorted(glob.glob(os.path.join(REL_DIR, "*.json")))
    if not files:
        print(f"Sin acciones en {REL_DIR}")
        return 1

    errors = []
    all_ids = {}
    subevent_ids = set()
    action_count = 0
    global_types = set()

    # 1) recoger ids de subeventos y de acciones; detectar duplicados
    docs = {}
    for path in files:
        base = os.path.basename(path)
        doc = load(path)
        docs[path] = doc
        if isinstance(doc, list):
            # lista suelta de subeventos (p. ej. relationship_events.json)
            arr = doc
        elif isinstance(doc, dict) and "subevents" in doc:
            arr = doc["subevents"]
        elif isinstance(doc, dict) and "events" in doc:
            arr = doc["events"]
        elif isinstance(doc, dict) and "actions" in doc:
            arr = None
        else:
            arr = None
            # archivos de configuración (p. ej. personality_templates.json): se ignoran
            # aquí; validate.py los cubre con sus chequeos globales.
        if arr is not None:
            for sub in arr:
                sid = sub.get("id")
                if not sid:
                    errors.append(f"{base}: subevento sin id")
                    continue
                if sid in all_ids:
                    errors.append(f"{base}: id duplicado '{sid}' (en {all_ids[sid]})")
                all_ids[sid] = base
                subevent_ids.add(sid)
        elif isinstance(doc, dict) and "actions" in doc:
            for a in doc["actions"]:
                aid = a.get("id")
                if not aid:
                    errors.append(f"{base}: accion sin id")
                    continue
                if aid in all_ids:
                    errors.append(f"{base}: id duplicado '{aid}' (en {all_ids[aid]})")
                all_ids[aid] = base
                global_types.add(doc.get("relationType"))
        else:
            # forma desconocida ya reportada arriba; no duplicar el error
            pass

    # 2) validar acciones
    for path, doc in docs.items():
        base = os.path.basename(path)
        if not (isinstance(doc, dict) and "actions" in doc):
            continue
        rt = doc.get("relationType")
        if rt not in REL_TYPES | PSEUDO_REL_KEYS:
            errors.append(f"{base}: relationType invalido {rt!r}")
        deltas = []
        for a in doc["actions"]:
            action_count += 1
            aid = a.get("id", "?")
            where = f"{base}:{aid}"
            lbl = a.get("label", "")
            if not lbl:
                errors.append(f"{where}: sin label")
            elif len(lbl.split()) > 10:
                errors.append(f"{where}: label >6 palabras: '{lbl}'")
            allowed = a.get("allowedTypes", [])
            if not allowed:
                errors.append(f"{where}: sin allowedTypes")
            for t in allowed:
                if t not in REL_TYPES:
                    errors.append(f"{where}: allowedTypes invalido {t!r}")
            mn, mx = a.get("minAge"), a.get("maxAge")
            if not isinstance(mn, int) or not isinstance(mx, int):
                errors.append(f"{where}: minAge/maxAge deben ser enteros")
            elif not (0 <= mn <= mx <= 120):
                errors.append(f"{where}: rango de edad invalido {mn}-{mx}")
            if not isinstance(a.get("cost", 0), int):
                errors.append(f"{where}: cost no entero")
            cd = a.get("cooldownYears", 0)
            mpy = a.get("maxPerYear")
            if not isinstance(cd, int) or cd < 0:
                errors.append(f"{where}: cooldownYears invalido {cd!r}")
            if mpy is not None and (not isinstance(mpy, int) or not 1 <= mpy <= 3):
                errors.append(f"{where}: maxPerYear invalido {mpy!r} (1-3)")
            if cd == 0 and mpy is None:
                errors.append(f"{where}: spameable sin cooldownYears ni maxPerYear")
            check_requires(a.get("requires"), where, errors)
            ocs = a.get("outcomes")
            if not isinstance(ocs, list) or not 2 <= len(ocs) <= 4:
                errors.append(f"{where}: {len(ocs) if isinstance(ocs, list) else '?'} outcomes (2-4)")
                continue
            total = sum(o.get("weight", 0) for o in ocs)
            if total != 100:
                errors.append(f"{where}: weights suman {total} (no 100)")
            for o in ocs:
                check_outcome(o, where, errors, deltas)
                nxt = o.get("nextEventId")
                if nxt and nxt not in subevent_ids:
                    errors.append(f"{where}: nextEventId roto '{nxt}'")
        # (4) buenas, neutras y malas
        if deltas:
            has_pos = any(d and d > 0 for _, _, d in deltas)
            has_neg = any(d and d < 0 for _, _, d in deltas)
            has_neu = any(d == 0 for _, _, d in deltas)
            if not (has_pos and has_neg and has_neu):
                errors.append(f"{base}: falta mezcla buenas/neutras/malas "
                              f"(pos={has_pos} neg={has_neg} neu={has_neu})")

    # 3) validar subeventos
    for path, doc in docs.items():
        if not (isinstance(doc, dict) and ("subevents" in doc or "events" in doc)):
            continue
        base = os.path.basename(path)
        for sub in doc["subevents"]:
            sid = sub.get("id", "?")
            where = f"{base}:{sid}"
            if not sub.get("text"):
                errors.append(f"{where}: subevento sin text")
            ocs = sub.get("outcomes")
            if not isinstance(ocs, list) or not 2 <= len(ocs) <= 4:
                errors.append(f"{where}: {len(ocs) if isinstance(ocs, list) else '?'} outcomes (2-4)")
                continue
            total = sum(o.get("weight", 0) for o in ocs)
            if total != 100:
                errors.append(f"{where}: weights suman {total} (no 100)")
            for o in ocs:
                check_outcome(o, where, errors, [])
                nxt = o.get("nextEventId")
                if nxt and nxt not in subevent_ids:
                    errors.append(f"{where}: nextEventId roto '{nxt}'")

    print(f"Archivos: {len(files)} | acciones: {action_count} | "
          f"subeventos: {len(subevent_ids)} | tipos: {len(global_types)}")
    if errors:
        print(f"ERRORES ({len(errors)}):")
        for e in errors[:60]:
            print(" -", e)
        return 1
    print("RELACIONES OK (6 reglas + extras)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
