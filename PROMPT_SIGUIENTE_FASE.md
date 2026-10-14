# PROMPT — Continuar el contenido de BitLifeClone (fase escuela + relaciones faltantes)

Copia todo lo de abajo y pásalo tal cual a otra IA.

---

## Contexto

Trabajas en el repo `BitLifeClone/` (juego tipo BitLife en C# / WPF, .NET). Los datos del juego son JSON y viven en `BitLifeClone/src/BitLifeClone/Data/`. Los scripts de control están en `BitLifeClone/tools/`.

Lee primero, en este orden:
1. `BitLifeClone/src/BitLifeClone/Data/Schema.md` — **fuente única de verdad**: listas cerradas de stats, flags, rasgos, education, cityStatus, relationTypes, currencies, licenses, specialties, branches. **No puedes inventar valores nuevos**: si necesitas uno, lo agregas a `Schema.md` con su justificación en la sección `## Añadidos`.
2. `BitLifeClone/tools/validate.py` — validador global. Córrelo así: `cd BitLifeClone && python tools/validate.py` (exit 0 = OK, exit 1 = errores). Debe quedar siempre en **0 errores**.
3. `BitLifeClone/tools/test_validator.py` — test del validador (planta 6 errores a propósito). Corre `python tools/test_validator.py` y exige **6/6 detectados**.
4. `BitLifeClone/PROGRESS.md`, `DECISIONS.md`, `CHANGELOG.md`, `PENDING.md` — bitácora. **Actualízalos al cerrar cada tarea.**
5. Un archivo de referencia de estilo ya terminado: `Data/Relationships/spouse.json` (acciones) y `Data/School/grades.json` (sistema + eventos).

## Convenciones obligatorias

- Valores de esquema (flags, stats, rasgos, education, ids, pools, especialidades) en **inglés/snake_case**. Solo textos visibles (`label`, `title`, `text`, `resultText`) en **español dominicano informal**.
- **ids únicos a nivel global** en todo `Data/` (el validador falla si se repiten, incluso entre archivos).
- Cada lista de `outcomes` debe sumar **weight = 100**.
- Entre 1 y 3 outcomes por choice/acción.
- Cadenas `nextEventId` de **máximo 4 niveles** (raíz + 3 subeventos) y sin ciclos. Todo `nextEventId` debe apuntar a un id que exista en algún JSON de `Data/`.
- `flag: "DEAD"` solo con `weight <= 15`.
- **Nada de textos repetidos**: el validador caza `resultText`/`text`/`label` duplicados (global para narrativos, same-file para labels).
- `ageMin`/`ageMax` enteros, `0 <= min <= max <= 120`.
- Todo outcome debe traer solo las 12 stats del Schema (`happiness`, `health`, `fitness`, `smarts`, `looks`, `fame`, `karma`, `stress`, `grades`, `attendance`, `discipline`, `popularity`) más `money` y las claves permitidas (`weight`, `resultText`, `flag`, `removeFlag`, `traitWeightMods`, `nextEventId`, `relationDelta`, `setCity`, `loseJob`, `loseProperty`). **Cualquier otra clave es error.**
- Los `traitWeightMods` solo usan los 13 rasgos de `Schema.md`.

## Puerta de control (obligatoria)

1. Termina **una** tarea.
2. Corre `python tools/validate.py` → debe dar `VALIDACION OK` (exit 0). Si hay errores —incluidos los de archivos viejos— **arréglalos antes de seguir**. Nunca acumules errores.
3. Corre `python tools/test_validator.py` → 6/6.
4. Anota la tarea en `PROGRESS.md` (formato `fecha-hora | tarea | archivo(s) | eventos/acciones | errores encontrados/corregidos`) y lo que cambie en `CHANGELOG.md`/`DECISIONS.md`.
5. Cada 5 tareas, corre el validador sobre **todo** `Data/` y limpia.

## Lo que falta — hazlo en este orden

### Tarea 1 — `Data/Relationships/teacher.json`
Tipo `teacher` (ya declarado en `Schema.md`, sin archivo). ~14-18 acciones: participar en clase, preguntar duda después de clase, pedir prórroga, ayudar a limpiar el aula, caerle bien, quejarse por una nota, confrontar una nota injusta, pedir carta de recomendación, pedir crédito extra, hacerle una broma, reportar matoneo, disculparse, faltar a su clase, pedir que te cambie de asiento. Flags útiles: `TEACHERS_PET`, `DETENTION`, `TRUANT`, `CHEATER_CAUGHT`, `BULLIED`, `SUSPENDED`. Engancha 1-2 `nextEventId` a ids existentes de `Relationships/subevents.json` (por ejemplo `sub_estranged_family`). Estructura: objeto con `relationType` + `actions`, cada acción con `id` (prefijo `teacher_`), `label`, `allowedTypes`, `minAge`, `maxAge`, `cost`, `cooldownYears`, `maxPerYear`, `requires`, `outcomes`.

### Tarea 2 — `Data/Relationships/classmate.json`
Tipo `classmate`. ~14-18 acciones: hacer grupo, prestar apuntes, copiar en un examen, defenderlo de un bully, burlarse de él, pedirle plata, invitarlo a tu casa, hacer un trabajo por él, delatarlo, pelear, reconciliarse, ayudarlo a estudiar, tomarle el pelo por su ropa, hacer un pacto. Flags: `BULLY`, `BULLIED`, `CHEATER_CAUGHT`, `CLASS_CLOWN`, `IN_CLUB`, `ON_TEAM`. Prefijo de ids `classmate_`.

### Tarea 3 — `Data/Relationships/kinship.json`
Los tipos que faltan del Schema: `grandparent`, `uncle_aunt`, `cousin` (y de paso `boss`, `coworker` si te alcanza). ~10-12 acciones por tipo, ids con prefijo `kin_`, `boss_`, `coworker_`. Puedes usar un archivo por tipo si prefieres, pero respeta el id único global.

### Tarea 4 — `Data/School/school_jobs.json`
Trabajos escolares (categoría `school`): tutor de compañeros, monitor de pasillo, ayudante de biblioteca, repartidor de la cafetería, asistente de laboratorio, estudiante-trabajador de oficina, ayudante de la profesora de kínder. Cada uno con `hoursCost`, `cost`, `ageMin`/`ageMax` escolares (12-22), `baseSalary` pequeño en `DOP` o `USD`, flag `WORKING_PART_TIME`, y outcomes que muevan `stress`, `grades`, `money`, `discipline`.

### Tarea 5 — `Data/School/special_chains.json`
Cadenas especiales de escuela, cada una de 1-3 subeventos (nunca más de 4 niveles):
- matoneo: `sch_bully_start` → `sch_bully_escalate` → `sch_bully_aftermath` (flags `BULLIED`/`BULLY`, `DEPRESSED`, hasta `EXPELLED`).
- expulsión: `sch_expulsion` → `sch_reinstatement` → `DROPPED_OUT` (flags `EXPELLED`, `DROPPED_OUT`).
- beca: `sch_scholarship_offer` → `sch_scholarship_renew` → `sch_scholarship_lost` (flag `SCHOLARSHIP`).

### Tarea 6 — `Data/INDEX.md`
Índice de todos los archivos de `Data/` con una línea cada uno: ruta, qué contiene, cuántos ids, de qué fase es. Enlaza desde `PROGRESS.md`.

### Tarea 7 — Verificación final
Corre el validador sobre todo `Data/` (debe dar 0 errores), el test (6/6), y reporta: archivos totales, ids totales, y la lista de lo que quedó en `PENDING.md`.

## Reglas de trabajo

- **No toques** el cliente WPF ni el código C# en esta fase: es solo contenido JSON de `Data/` y la bitácora.
- Antes de escribir un archivo nuevo, mira uno parecido ya terminado y copia su forma exacta.
- Si un archivo que ya existe tiene errores viejos, arréglalos y anótalos en `CHANGELOG.md` (ahí van los cambios a archivos que YA existían; los nuevos van en `PROGRESS.md`).
- Si tomas una decisión de diseño sin poder consultar (nombre de id, mezclar tipos en un archivo, etc.), escríbela en `DECISIONS.md` con el porqué.
- Si tras 3 intentos algo no pasa el validador, anótalo en `PENDING.md` y sigue con lo demás.
- Al terminar cada tarea, di en tu respuesta: archivo creado, cuántos ids/acciones, y la salida literal del validador.
