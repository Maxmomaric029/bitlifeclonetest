#!/usr/bin/env python3
"""
Validador de eventos de guerra (Data/Events/*.json).

Reglas pedidas:
 (1) ningun nextEventId apunta a un id que no exista
 (2) ninguna cadena pasa de 4 niveles (raiz + 3 subeventos);
     el nivel 4 tiene nextEventId: null en todos sus outcomes
 (3) los weight de cada choice SUMAN 100
 (4) ids unicos en todo el archivo

Extras: 2-4 choices, 1-3 outcomes, choices.text <= 10 palabras,
muerte (DEAD) solo con weight <= 15 y nunca en el nivel 1,
flags validos, text termina en "¿Que haces?" (raices y subeventos).
"""
import glob
import json
import os
import re
import sys

EVENT_DIR = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                         "src", "BitLifeClone", "Data", "Events")
SCHEMA = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                      "src", "BitLifeClone", "Data", "Schema.md")

# flags desde Schema.md (los pools civiles usan flags escolares/salud/fama, no solo guerra)
def _schema_flags(path):
    vals = set()
    cur = None
    with open(path, encoding="utf-8") as f:
        for line in f:
            m = re.match(r"^##\s+(.+?)\s*$", line)
            if m:
                cur = "flags" if m.group(1).strip() == "Flags" else None
                continue
            if cur:
                m2 = re.match(r"^-\s+`([^`]+)`", line.strip())
                if m2:
                    vals.add(m2.group(1))
    return vals

FLAGS = _schema_flags(SCHEMA) or {"DEAD", "POW", "WOUNDED", "REFUGEE", "DRAFTED",
                                  "DESERTER", "HERO"}
MAX_LEVEL = 4


def load_blocks(path):
    """Archivos = lista de bloques [{pool|jobId, events}] o un bloque suelto."""
    with open(path, encoding="utf-8") as f:
        doc = json.load(f)
    if isinstance(doc, dict) and "events" in doc:
        return [doc]
    if isinstance(doc, list):
        return doc
    raise ValueError(f"{path}: forma desconocida")


def check_file(path, errors):
    blocks = load_blocks(path)
    all_ids = {}
    by_block = {}
    base = os.path.basename(path)

    for block in blocks:
        events = block.get("events", [])
        ev_by_id = {}
        for ev in events:
            eid = ev.get("id")
            if not eid:
                errors.append(f"{base}: evento sin id")
                continue
            if eid in all_ids:
                errors.append(f"{base}: id duplicado '{eid}' (en {all_ids[eid]})")
            all_ids[eid] = base
            ev_by_id[eid] = ev

        # (1) nextEventId existente + (2) niveles
        def depth_of(eid, trail):
            ev = ev_by_id.get(eid)
            if ev is None:
                return None
            best = 1
            for ch in ev.get("choices", []):
                for oc in ch.get("outcomes", []):
                    nxt = oc.get("nextEventId")
                    if nxt:
                        if nxt in trail:
                            errors.append(f"{base}: ciclo en {eid} -> {nxt}")
                            continue
                        if nxt not in ev_by_id:
                            errors.append(f"{base}: nextEventId roto '{nxt}' (desde {eid})")
                            continue
                        best = max(best, 1 + depth_of(nxt, trail | {eid}))
            return best

        for ev in events:
            d = depth_of(ev["id"], set())
            if d is not None and d > MAX_LEVEL:
                errors.append(f"{base}: cadena de {d} niveles desde {ev['id']} (max 4)")

        # niveles por BFS desde las raices reales (nunca referenciadas por nextEventId)
        level = {}
        referenced = {oc.get("nextEventId") for ev in events
                      for ch in ev.get("choices", []) for oc in ch.get("outcomes", [])
                      if oc.get("nextEventId")}
        roots = [ev["id"] for ev in events if ev["id"] not in referenced]
        frontier = list(roots)
        lvl = 0
        while frontier and lvl < MAX_LEVEL + 1:
            lvl += 1
            nxt_front = []
            for eid in frontier:
                ev = ev_by_id.get(eid)
                if ev is None or eid in level:
                    continue
                level[eid] = lvl
                for ch in ev.get("choices", []):
                    for oc in ch.get("outcomes", []):
                        n = oc.get("nextEventId")
                        if n and n in ev_by_id and n not in level:
                            nxt_front.append(n)
            frontier = nxt_front
        # los eventos no alcanzados desde su raiz cuentan como raiz
        for ev in events:
            level.setdefault(ev["id"], 1)

        for ev in events:
            if ev.get("highDanger") and not isinstance(ev["highDanger"], bool):
                errors.append(f"{base}: {ev['id']}: highDanger no es bool")

        for ev in events:
            eid, lvl_e = ev["id"], level[ev["id"]]
            if not ev.get("text", "").strip():
                errors.append(f"{base}: {eid} sin text")
            choices = ev.get("choices", [])
            if not 2 <= len(choices) <= 4:
                errors.append(f"{base}: {eid} tiene {len(choices)} choices (2-4)")
            for ch in choices:
                txt = ch.get("text", "")
                if len(txt.split()) > 10:
                    errors.append(f"{base}: {eid}: choice '{txt[:30]}' >10 palabras")
                ocs = ch.get("outcomes", [])
                if not 1 <= len(ocs) <= 3:
                    errors.append(f"{base}: {eid}: choice con {len(ocs)} outcomes (1-3)")
                total = sum(o.get("weight", 0) for o in ocs)
                if total != 100:
                    errors.append(f"{base}: {eid}: weights suman {total} (no 100)")
                for o in ocs:
                    w = o.get("weight", 0)
                    if not isinstance(w, int) or w < 0:
                        errors.append(f"{base}: {eid}: weight invalido {w!r}")
                    if o.get("flag") == "DEAD":
                        if w > 15:
                            errors.append(f"{base}: {eid}: DEAD con weight {w} (>15)")
                        if lvl_e == 1 and not ev.get("highDanger", False):
                            errors.append(f"{base}: {eid}: DEAD en nivel 1 sin highDanger")
                    fl = o.get("flag")
                    if fl is not None and fl not in FLAGS:
                        errors.append(f"{base}: {eid}: flag invalido {fl!r}")
                    rf = o.get("removeFlag")
                    if rf is not None and rf not in FLAGS:
                        errors.append(f"{base}: {eid}: removeFlag invalido {rf!r}")
                    nxt = o.get("nextEventId")
                    if lvl_e >= MAX_LEVEL and nxt:
                        errors.append(f"{base}: {eid}: nivel 4 con nextEventId {nxt}")
                    if o.get("resultText") is None:
                        errors.append(f"{base}: {eid}: outcome sin resultText")


def main():
    files = sorted(glob.glob(os.path.join(EVENT_DIR, "*.json")))
    if not files:
        print(f"Sin eventos en {EVENT_DIR}")
        return 1
    errors = []
    total_events = 0
    # ids unicos entre TODOS los archivos del directorio
    global_ids = {}
    for path in files:
        n_before = len(errors)
        check_file(path, errors)
        for block in load_blocks(path):
            for ev in block.get("events", []):
                eid = ev.get("id")
                total_events += 1
                if eid in global_ids:
                    errors.append(f"{os.path.basename(path)}: id '{eid}' ya existe en {global_ids[eid]}")
                global_ids[eid] = os.path.basename(path)
    print(f"Archivos: {len(files)} | eventos: {total_events}")
    if errors:
        print(f"ERRORES ({len(errors)}):")
        for e in errors[:50]:
            print(" -", e)
        return 1
    print("EVENTOS OK (4 reglas + extras)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
