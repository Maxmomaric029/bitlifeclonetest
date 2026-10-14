# Schema — valores permitidos

Fuente única de verdad. **Solo se pueden usar estos valores** en cualquier JSON de `Data/`.
Si hace falta uno nuevo, se agrega aquí con una línea de justificación al final (`## Añadidos`).

Formato de lista: `- \`VALOR\` — descripción corta.` (el validador `tools/validate.py` los extrae).

## Stats

Stats del personaje. El dinero se lleva aparte (`money`), aunque puede aparecer como delta en un outcome.

- `happiness` — 0-100, ánimo general.
- `health` — 0-100, salud física.
- `fitness` — 0-100, estado físico/deportivo.
- `smarts` — 0-100, inteligencia.
- `looks` — 0-100, atractivo.
- `fame` — 0-100, cuánto te conoce la gente.
- `karma` — -100..100 (negativo = malas acciones).
- `stress` — 0-100, carga mental (sobre 80 = riesgo de colapso).
- `grades` — 0-100, promedio escolar.
- `attendance` — 0-100, asistencia (bajo 60 = riesgo de repetir).
- `discipline` — 0-100, comportamiento (bajo 30 = suspensión/expulsión).
- `popularity` — 0-100, qué tan querido/conocido en la escuela o grupo.

## Flags

Flags del jugador o de una persona. `flag` agrega, `removeFlag` quita.

### Guerra

- `DRAFTED` — reclutado a la fuerza.
- `POW` — prisionero de guerra.
- `WOUNDED` — herido en combate.
- `REFUGEE` — refugiado (fuera del país por la guerra).
- `DESERTER` — desertor.
- `HERO` — condecorado por acciones de guerra.

### Familia y relaciones

- `DEAD` — la persona falleció.
- `ESTRANGED` — relación cortada.
- `MARRIED` — casado.
- `DIVORCED` — divorciado.
- `HEIR` — heredero designado.
- `PREGNANT` — embarazo.
- `ARRESTED` — detenido.
- `BANNED_CONTACT` — contacto prohibido (bloqueo/orden legal).
- `ADOPTED` — adoptado.

### Escuela

- `HONOR_ROLL` — cuadro de honor.
- `REPEATED_YEAR` — repitió año.
- `SUSPENDED` — suspendido.
- `EXPELLED` — expulsado.
- `DROPPED_OUT` — abandonó la escuela.
- `BULLIED` — víctima de acoso.
- `BULLY` — acosador.
- `TEACHERS_PET` — favorito de los profesores.
- `TRUANT` — ausentismo/los pies calientes.
- `CLASS_CLOWN` — el payaso del salón.
- `GIFTED` — talento destacado.
- `STUDENT_COUNCIL` — en el consejo estudiantil.
- `TEAM_CAPTAIN` — capitán de un equipo.
- `SCHOLARSHIP` — becado.
- `GRADUATED_PRIMARY` — terminó primaria.
- `GRADUATED_SECONDARY` — terminó secundaria.
- `ACCEPTED_UNIVERSITY` — admitido en la universidad.
- `DETENTION` — detención escolar.
- `CHEATER_CAUGHT` — pillado copiando.
- `ON_TEAM` — en un equipo deportivo.
- `IN_CLUB` — en un club.
- `WORKING_PART_TIME` — trabajando medio tiempo.
- `BURNOUT` — agotamiento.
- `INJURED_SPORT` — lesionado en deporte.

### Crimen

- `CRIMINAL_RECORD` — antecedentes penales.
- `IN_PRISON` — en la cárcel.
- `ON_PAROLE` — en libertad condicional.
- `WANTED` — buscado por la ley.
- `GANG_MEMBER` — miembro de una pandilla.
- `MAFIA_MADE` — iniciado en la mafia.
- `DRUG_DEALER` — vendedor de drogas.
- `SMUGGLER` — contrabandista.
- `HACKER` — hacker criminal.
- `LAUNDERING` — lavado de dinero.
- `EX_CONVICT` — cumplió condena.

### Salud

- `ADDICT_ALCOHOL` — adicción al alcohol.
- `ADDICT_TOBACCO` — adicción al tabaco.
- `ADDICT_DRUGS` — adicción a drogas.
- `ADDICT_GAMBLING` — ludopatía.
- `DEPRESSED` — depresión.
- `ANXIETY` — trastorno de ansiedad.
- `CHRONIC_ILL` — enfermedad crónica.
- `TERMINAL` — enfermedad terminal.
- `DISABLED` — discapacidad física.
- `IN_REHAB` — en rehabilitación.
- `PREGNANT_TEEN` — embarazo adolescente.
- `HIV` — portador de VIH.
- `CANCER` — cáncer.

### Fama

- `CELEBRITY` — famoso.
- `VIRAL` — viral en redes.
- `CANCELLED` — cancelado por el público.
- `SCANDAL` — envuelto en escándalo.
- `UNDER_CONTRACT` — bajo contrato con una disquera/empresa.
- `HATER_MAGNET` — imán de odio.

### Trabajo y otros

- `EMPLOYED` — tiene empleo.
- `UNEMPLOYED` — sin empleo.
- `RETIRED` — retirado.
- `VETERAN` — veterano de guerra.
- `HOMELESS` — sin hogar.
- `STUDYING_ABROAD` — estudiando fuera del país.
- `LOTTERY_WINNER` — ganó la lotería.
- `BUSINESS_OWNER` — dueño de un negocio.
- `LICENSED_DRIVER` — tiene licencia de conducir.
- `LICENSED_PILOT` — tiene licencia de piloto.

## Traits

Rasgos de personalidad (NPCs y jugador).

- `affectionate` — afectuoso.
- `cold` — frío.
- `stingy` — tacaño.
- `generous` — generoso.
- `resentful` — rencoroso.
- `forgiving` — perdonavidas.
- `jealous` — celoso/envidioso.
- `loyal` — leal.
- `manipulative` — manipulador.
- `strict` — estricto.
- `easygoing` — relajado.
- `religious` — religioso.
- `party_animal` — fiestero.

## Education

Nivel educativo del personaje.

- `None` — sin estudios.
- `PrimarySchool` — primaria.
- `LowerSecondary` — secundaria básica.
- `UpperSecondary` — secundaria alta.
- `HighSchool` — bachiller.
- `CommunityCollege` — colegio comunitario/técnico.
- `University` — universidad.
- `GraduateSchool` — posgrado.
- `MedicalSchool` — medicina.
- `LawSchool` — derecho.
- `VeterinarySchool` — veterinaria.
- `PharmacySchool` — farmacia.
- `NursingSchool` — enfermería.
- `BusinessSchool` — negocios.

## CityStatus

Estado de la ciudad durante una guerra.

- `safe` — normal, sin guerra activa.
- `threatened` — tensión, posible ataque.
- `underattack` — siendo atacada.
- `occupied` — ocupada por el enemigo.
- `destroyed` — destruida/ruinas.

## RelationTypes

Tipos de persona en el menú de relaciones.

- `mother` — madre.
- `father` — padre.
- `sibling` — hermano/a.
- `spouse` — esposo/a.
- `partner` — pareja (novio/a).
- `child` — hijo/a.
- `friend` — amigo/a.
- `enemy` — enemigo.
- `enemy_ex` — enemigo o ex.
- `ex` — ex pareja.
- `grandparent` — abuelo/a.
- `uncle_aunt` — tío/a.
- `cousin` — primo/a.
- `teacher` — profesor.
- `classmate` — compañero de clase.
- `boss` — jefe.
- `coworker` — compañero de trabajo.

## Currencies

- `USD` — dólar.
- `EUR` — euro.
- `DOP` — peso dominicano.

## Licenses

- `DriverLicense` — licencia de conducir.
- `PilotLicense` — licencia de piloto.
- `MedicalLicense` — licencia médica.
- `LawLicense` — licencia de abogado.
- `FirearmLicense` — licencia de armas.
- `FishingLicense` — licencia de pesca.
- `BusinessLicense` — licencia de negocio.
- `NursingLicense` — licencia de enfermería.

## Specialties

Especialidad militar (campo `specialty`, define el pool de guerra).

- `infantry` — infantería (pool_trench).
- `drone` — operador de drones (pool_drone).
- `tank` — blindados (pool_tank).
- `pilot` — piloto (pool_air).
- `navy` — marina (pool_naval).
- `special_forces` — fuerzas especiales (pool_special_ops).
- `medical` — sanidad (pool_civil_medic).
- `journalist` — periodista (pool_civil_journalist).
- `logistics` — logística.
- `engineer` — ingeniería militar.
- `military_police` — policía militar.

## MilitaryBranches

- `army` — Ejército.
- `navy` — Armada.
- `marines` — Infantería de Marina.
- `air_force` — Fuerza Aérea.

## Añadidos

Valores nuevos agregados con justificación (uno por línea):

- `DOP` — el juego es dominicano; se necesita la moneda local para trabajos informales.
- `enemy_ex` — pedido explícito en la fase D; agrupa enemigos y ex en un archivo.
- `special_forces` — el pool `pool_special_ops` ya existe y necesita su especialidad.
