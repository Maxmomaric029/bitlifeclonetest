# Data/INDEX.md — índice de archivos

Una línea por archivo: ruta · qué contiene · fases. Corre `python tools/validate.py` desde la raíz del repo para validar TODO (0 errores = OK).

Actualizado: 2026-10-13 · 61 archivos JSON · 1220 ids.

## Raíz
- `Schema.md` — fuente única de verdad: stats, flags, rasgos, education, cityStatus, relationTypes, currencies, licenses, specialties, branches · (A1)

## Events/ (pools de eventos por id)
- `pool_civil_{safe,threatened,underattack,occupied,destroyed}.json` — vida civil según cityStatus · (C-origen/Curn)
- `pool_civil_{medic,journalist}.json` — vida civil de sanidad y periodismo · (C1)
- `pool_{trench,tank,air,naval,drone,special_ops}.json` — guerra por especialidad militar · (C2)
- `pool_{pow,refugee,draft}.json` — prisionero de guerra, refugiado y reclutamiento · (C2)

## Jobs/ (trabajos)
- `Jobs.fixed.json` — 25 categorías / 323 trabajos con sueldo por fórmula y empresas · (A3)
- `Companies.json` — negocios donde se aplican los trabajos · (A3)
- `military_extra.json` — fuerzas especiales, drones y warrant de Navy/Marines/Air Force · (B1)
- `agencies.json` — policía, CIA, DEA, Secret Service, Border Patrol, CSI · (B2)
- `part_time.json`, `freelance.json`, `publisher.json` — medio tiempo, freelance y editorial · (B3)
- `careers_fame.json`, `careers_crime.json`, `careers_pro.json` — fama, crimen y carreras de salud/gobierno/ciencia · (B4)
- `rd_jobs.json` — identidad RD: modernos y absurdos · (B5)
- `_original_Jobs.json.bak` — respaldo del original (no se carga: no es .json activo) · (A3)

## Relationships/
- `{mother,father,sibling,child,friend,enemy}.json` — acciones por tipo de relación · (D-origen/D0)
- `partner.json` — amistad/amor incluyendo ex · (D0)
- `spouse.json` — 20 acciones de convivencia, finanzas, Suegros, infidelidad, divorcio · (D1)
- `kinship.json` — grandparent/uncle_aunt/cousin + boss/coworker (ByType) · (D3)
- `work_relations.json` — acciones de boss y coworker · (D3)
- `teacher.json`, `classmate.json` — escuela: profesoras y compis de clase · (E3/E7)
- `relationship_events.json` — subeventos de embarazo/terapia de pareja · (D2)
- `subevents.json` — subeventos de relaciones encadenados por nextEventId (cadenas de hasta 4) · (D0/D2)
- `personality_templates.json` — reglas de rasgos NPC por relación/flag/education · (D3)

## School/
- `grades.json` — sistema de notas (escala, ponderación, reglas de cierre de año) + eventos de examen · (E1)
- `menu_actions.json` — acciones del menú de escuela: estudiar, tarea, grupo, faltar · (E2)
- `extracurriculars.json` — clubes, equipos y consejo (es flag ON_TEAM/IN_CLUB/STUDENT_COUNCIL) · (E2)
- `school_jobs.json` — trabajitos escolares con `WORKING_PART_TIME` y sueldo DOP · (E4)
- `school_year_events.json` — eventos anuales: primer día, matrícula, boletines, graduaciones · (E5)
- `special_chains.json` — cadenas: matoneo, expulsión, beca y atleta · (E6)

## War/
- `war_rules.json` — motor de guerra: declaración, escala de tiempo (1 mes = 1 clic), draft, cityStatus, fin, pools por especialidad · (C3)
- `rivalries.json` — 50 pares de países con weight de declaración (≥45 puede declarar) · (C3)

## Crime/
- `crime.json` — 16 acciones: robo, asalto, carterismo, drogas, contrabando, hackeo, lavado, pandilla, mafia, soborno · (Cr1)
- `crime_chains.json` — 10 cadenas: audiencias y veredictos por delito + entrada a prisión · (Cr1)
- `prison.json` — 11 acciones solo con `IN_PRISON`: rutina, taller, aliados, audiencia de libertad condicional, fuga · (Cr1)

## Health/
- `health.json` — 17 acciones del menú de clínica: chequeo, terapia, siquiatría, rehab, adicciones, cáncer, VIH, paliativos, embarazo · (H1)
- `health_events.json` — cadenas de diagnóstico (inconcluso → especialista → biopsia), VIH, espiral de adicción y depresión · (H1)

## Fame/
- `fame.json` — 14 acciones: videos, agencia, contratos, haters, entrevistas, juntas, pleito público, retiro · (F1)
- `fame_events.json` — cadenas: viral, intento de cancelación, disputa de contrato y bajón de fama · (F1)

## Licenses/
- `licenses.json` — 8 licencias del Schema con coste, edad, requisitos y chance del examen · (L1)

## Properties/
- `properties.json` — 10 propiedades (casas, carros, yate, finca, local) con precio y mantenimiento anual · (P1)

## World/
- `world.json` — 10 países con ciudades de nacimiento (labels y notas) · (W1)
- `names.json` — nombres masculinos/femeninos y apellidos dominicanos · (W1)
