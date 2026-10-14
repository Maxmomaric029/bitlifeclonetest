# PENDING

Lo que NO se alcanzó a hacer o queda en duda de diseño. Se actualiza al cierre.
Actualizado: 2026-10-13.

## Resuelto en esta fase (ver PROGRESS.md)
- `Data/Crime/`, `Data/Health/`, `Data/Fame/`, `Data/Licenses/`, `Data/Properties/`, `Data/World/` **existen y pasan el validador** (61 JSON · 1220 ids).
- El cliente consume ahora: TODOS los `pool_*` (por cityStatus/especialidad/POW/REFUGEE), `Data/School/` (menú de escuela con menu_actions/extracurriculars/school_jobs y evento anual del pool `school`), `Data/War` (declaración, reclutamiento, cityStatus, fin y selección de pool por especialidad), subeventos de relaciones encadenados, licenses/properties/world/names y `personality_templates.json` (rasgos NPC al crear relaciones).
- Universidad y posgrados: menú de matrícula en Trabajo, avance anual, media beca.

## Bloqueo principal (fuera del repo)
- **No se ha compilado NUNCA:** la máquina no tiene SDK de .NET (`dotnet --list-sdks` vacio) y el usuario pidió NO compilar. Al instalarlo, correr `dotnet build BitLifeClone.sln` y ajustar lo que marque el compilador. Los archivos CSS fueron verificados solo a mano (lectura completa).

## Cooldowns y repostas
- `cooldownYears`/`maxPerYear` de las acciones (bloqueo de relación y bloqueos genéricos) NO se imponen todavía: ni RunRelationAction ni RunGenericAction llevan registro de usos por año. Es la deuda de UX más visible.

## Diseño pendiente
- **Costos de universidad quemados en C#** (`GameEngine.UniversityOptions`): deberían vivir en Data (un archivo `Data/Education/universities.json`).
- **Drift de rasgos del jugador:** los drift definidos en `playerAssignment` (`personality_templates.json`) no están implementados (trigger por karma/guerra/divorcio/etc.).
- **`Data/School/school_year_events.json` y `special_chains.json`** llegan al jugador solo por evento anual del pool `school`; `grades.json` podría alimentar el sistema de notas por materia (escala/ponderaciones sin consumir en C#).
- **Escala de la guerra:** la duración de referencia para `warProgress` es 24 meses (constante en el código); `war_rules.json` no define duración esperada.
- **Chance de guerra anual es muy baja** (~0.3%/año para RD por design de `war_rules.json`): quizás quede demasiado rara para notarla jugando.
- **Salario de school_jobs:** el sueldo de referencia (`baseSalary` DOP) nunca se usa en C#; el que paga es el outcome `money`. Igual que la nota de los part_time de BitLife original era dinámica.

## Deuda técnica del validador
- El validador ahora chequea `promotesTo` y `branch`⇒`specialty`, pero PENDIENTE: que `branch`/`specialty` apunten al pool de guerra correcto (mapa `poolSelection` en `war_rules.json`) y coherencia de `hoursCost`/`cost` con un presupuesto de horas.
- El `passChance` de licencias no se audita con las stats del jugador (el ajuste se hace solo en el código C#: chance/2 + smarts/3).
