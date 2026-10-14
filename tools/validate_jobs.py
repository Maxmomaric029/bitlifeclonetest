#!/usr/bin/env python3
"""Validación de Data/Jobs/: ids, promociones, tiers, sueldos por formula, enums.

Datasets: Jobs.fixed.json (meta + categories) y los *.json de categoria sueltos
({category, name, jobs}). Los ids deben ser unicos globales en el directorio.
"""
import glob
import json
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
JOBS_DIR = os.path.join(ROOT, 'src', 'BitLifeClone', 'Data', 'Jobs')

EDU = {None, "None", "PrimarySchool", "LowerSecondary", "UpperSecondary", "HighSchool",
       "CommunityCollege", "University", "GraduateSchool", "MedicalSchool", "LawSchool",
       "VeterinarySchool", "PharmacySchool", "NursingSchool", "BusinessSchool"}
CURR = {None, "USD", "EUR", "DOP"}

# Datasets de categoria suelta: {category, name, jobs}
files = {}
paths = sorted(glob.glob(os.path.join(JOBS_DIR, '*.json')))
datasets = []   # [(base, meta|None, categories, companies)]
for path in paths:
    base = os.path.basename(path)
    doc = json.load(open(path, encoding='utf-8'))
    if base == 'Companies.json':
        continue  # solo empresas, sin trabajos
    if base == 'Jobs.fixed.json':
        datasets.append((base, doc.get('meta', {}), doc.get('categories', [])))
    else:
        # dataset por categoria: envolver en categories
        datasets.append((base, None, [doc]))

all_ids = {}
errors = []

meta = None
for base, _m, _c in datasets:
    if _m:
        meta = _m
if meta is None:
    print('ERRORES: no hay Jobs.fixed.json con meta')
    sys.exit(1)
mults = meta['tierMultipliers']
growth = meta['tierGrowthAfterFour']


def mult(t):
    if t is None:
        return 1.0
    if t <= len(mults):
        return mults[t - 1]
    return mults[-1] * growth ** (t - len(mults))


def tier_mult_for(cat_meta, t):
    # los datasets sueltos usan la meta del Jobs.fixed.json
    return mult(t)


def salary(j):
    if j.get('baseSalary') is None:
        return None
    rate = meta.get('eurToUsd', 1.0) if j.get('currency') == 'EUR' else 1
    return round(j['baseSalary'] * rate * mult(j.get('tier')))


for base, _m, cats in datasets:
    for c in cats:
        cid = c.get('id', base)
        for j in c.get('jobs', []):
            if j['id'] in all_ids:
                errors.append(f"{base}: id dup: {j['id']} (ya en {all_ids[j['id']]})")
            all_ids[j['id']] = cid
            if j.get('education') not in EDU:
                errors.append(f"{j['id']}: edu {j.get('education')!r}")
            if j.get('currency') not in CURR:
                errors.append(f"{j['id']}: moneda {j.get('currency')!r}")
            if not j.get('title'):
                errors.append(f"{j['id']}: sin titulo")
            if j.get('baseSalary') is not None and not isinstance(j['baseSalary'], (int, float)):
                errors.append(f"{j['id']}: baseSalary no numerico")
            if j.get('tier') is not None and not isinstance(j['tier'], int):
                errors.append(f"{j['id']}: tier no entero")

# promociones: dentro del mismo dataset (por choice diseno)
for base, _m, cats in datasets:
    for c in cats:
        by = {j['id']: j for j in c.get('jobs', [])}
        for j in c.get('jobs', []):
            t = j.get('promotesTo')
            if t and t not in all_ids:
                errors.append(f"{j['id']}: promotesTo roto -> {t}")
            elif t and t in all_ids and t in by:
                tj = by[t]
                if (j.get('tier') or 0) >= (tj.get('tier') or 0):
                    errors.append(f"{j['id']}: tier {j.get('tier')} >= {t} tier {tj.get('tier')}")
                s1, s2 = salary(j), salary(tj)
                if s1 is not None and s2 is not None and s2 <= s1:
                    errors.append(f"{j['id']}: sueldo {s1} -> {t} {s2} (no crece)")
            # flag militar coherente
            if j.get('branch') is not None and j.get('currency') not in (None, 'USD'):
                errors.append(f"{j['id']}: militar con moneda no USD")

print('Archivos:', len(paths), '| trabajos:', len(all_ids))
if errors:
    print('ERRORES:')
    for e in errors[:40]:
        print(' -', e)
    sys.exit(1)
print('VALIDACION OK')
