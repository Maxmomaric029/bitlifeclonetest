#!/usr/bin/env python3
"""
Test del validador: planta 6 errores conocidos y confirma que validate.py los detecta.
Si alguno se le escapa, sale con codigo 1.
"""
import os
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
VALIDATE = os.path.join(HERE, "validate.py")
SCHEMA = os.path.join(ROOT, "src", "BitLifeClone", "Data", "Schema.md")


def ev(eid, text, outcomes):
    return {
        "id": eid, "pool": "t_test", "text": text,
        "choices": [{"text": "Que haces", "outcomes": outcomes}],
    }


CASES = {
    # (1) nextEventId inexistente
    "c1_next.json": {
        "events": [ev("t_next", "Caso uno unico. ¿Qué haces?",
                      [{"weight": 100, "resultText": "texto unico c1", "nextEventId": "t_no_existe"}])]
    },
    # (2) cadena de 5 niveles
    "c2_chain.json": {
        "events": [
            ev("t_c1", "texto unico c2 raiz. ¿Qué haces?", [{"weight": 100, "resultText": "c2 r1", "nextEventId": "t_c2"}]),
            ev("t_c2", "nivel 2. ¿Qué haces?", [{"weight": 100, "resultText": "c2 r2", "nextEventId": "t_c3"}]),
            ev("t_c3", "nivel 3. ¿Qué haces?", [{"weight": 100, "resultText": "c2 r3", "nextEventId": "t_c4"}]),
            ev("t_c4", "nivel 4. ¿Qué haces?", [{"weight": 100, "resultText": "c2 r4", "nextEventId": "t_c5"}]),
            ev("t_c5", "nivel 5. ¿Qué haces?", [{"weight": 100, "resultText": "c2 r5", "nextEventId": None}]),
        ]
    },
    # (3) weights suman 90
    "c3_weights.json": {
        "events": [ev("t_w", "texto unico c3. ¿Qué haces?",
                      [{"weight": 50, "resultText": "c3 a"}, {"weight": 40, "resultText": "c3 b"}])]
    },
    # (4) id duplicado
    "c4_dup.json": {
        "events": [
            ev("t_dup", "texto unico c4 primero. ¿Qué haces?", [{"weight": 100, "resultText": "c4 a"}]),
            ev("t_dup", "texto unico c4 segundo. ¿Qué haces?", [{"weight": 100, "resultText": "c4 b"}]),
        ]
    },
    # (5) flag inventado
    "c5_flag.json": {
        "events": [ev("t_flag", "texto unico c5. ¿Qué haces?",
                      [{"weight": 100, "resultText": "c5 a", "flag": "FAKE_FLAG_XYZ"}])]
    },
    # (6) DEAD con weight 40
    "c6_dead.json": {
        "events": [ev("t_dead", "texto unico c6. ¿Qué haces?",
                      [{"weight": 40, "resultText": "c6 a", "flag": "DEAD"}, {"weight": 60, "resultText": "c6 b"}])]
    },
}

EXPECT = {
    "c1_next.json": "nextEventId roto",
    "c2_chain.json": "cadena de 5 niveles",
    "c3_weights.json": "weights suman 90",
    "c4_dup.json": "id duplicado",
    "c5_flag.json": "flag fuera de Schema 'FAKE_FLAG_XYZ'",
    "c6_dead.json": "DEAD con weight 40",
}


def main():
    with tempfile.TemporaryDirectory() as tmp:
        for name, doc in CASES.items():
            with open(os.path.join(tmp, name), "w", encoding="utf-8") as f:
                import json
                json.dump(doc, f)
        proc = subprocess.run(
            [sys.executable, VALIDATE, tmp, SCHEMA],
            capture_output=True, text=True,
        )
        out = proc.stdout + proc.stderr
        print(out)
        missing = [f"{name} -> {marker}" for name, marker in EXPECT.items() if marker not in out]
        ok = True
        if proc.returncode != 1:
            print(f"FALLO: returncode esperado 1, fue {proc.returncode}")
            ok = False
        if missing:
            print("FALLARON (no detectados):")
            for m in missing:
                print(" -", m)
            ok = False
        if not ok:
            return 1
        print("TEST VALIDADOR OK: los 6 errores fueron detectados")
        return 0


if __name__ == "__main__":
    sys.exit(main())
