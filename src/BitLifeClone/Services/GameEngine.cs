using System.Text.Json;
using BitLifeClone.Models;

namespace BitLifeClone.Services;

/// <summary>
/// El motor del juego: Age Up, eventos aleatorios, actividades, trabajo y relaciones.
/// Todo lo que pasa aqui agrega entradas al feed (Person.History).
/// </summary>
public sealed class GameEngine
{
    private readonly Random _rng = new();
    private readonly DataLoader _data;
    private readonly JobService _jobs;

    public GameEngine(DataLoader data, JobService jobs)
    {
        _data = data;
        _jobs = jobs;
    }

    public DataLoader Data => _data;
    public JobService Jobs => _jobs;

    // ================================================================== AGE UP

    /// <summary>El boton central: +1 ano, sueldo, escuela, evento aleatorio, salud/vejez.</summary>
    public List<LifeEvent> AgeUp(GameState game)
    {
        var p = game.Person;
        var evts = new List<LifeEvent>();

        if (p.IsDead)
            return evts;

        p.Age++;
        game.Year = p.BirthYear + p.Age;

        p.RemoveFlag("NEW_BORN");
        p.JobYears = p.JobId is null ? 0 : p.JobYears + 1;

        // ---- escuela (solo si esta viva)
        LogSchool(game, evts);

        // ---- sueldo anual si tiene trabajo (en la carcel no pagan nomina)
        if (p.JobId is not null && !p.HasFlag("IN_PRISON") && _jobs.FindJob(p.JobId) is { } job)
        {
            var salary = _jobs.SalaryOf(job) ?? 0;
            p.Money += salary;
            evts.Add(new LifeEvent
            {
                Age = p.Age,
                Text = $"Cobraste ${salary:N0} de {job.Title}.",
                MoneyDelta = salary
            });
        }

        // ---- desgaste de edad
        if (p.Age >= 40)
        {
            var drop = (p.Age - 40) / 10; // -0/-2/-4... lento y cada vez mas
            if (drop > 0)
            {
                p.AddStat("health", -drop);
                p.AddStat("fitness", -drop);
            }
        }

        // ---- guerra: declaracion anual / avance mensual / fin
        ApplyWarTick(game, evts);

        // ---- evento aleatorio del pool que toque (50% los anos con eventos)
        if (_data.CivilEvents.Count > 0 && _rng.Next(100) < 50)
            RunRandomPoolEvent(game, evts, maxDepth: 3);

        // ---- evento anual del modulo escuela (solo edades escolares)
        RunSchoolYearEvent(game, evts);

        // ---- mantenimiento anual de las propiedades
        ApplyPropertyMaintenance(game, evts);

        // ---- armas de gracia: vejez (60+) o salud al suelo
        if (!p.IsDead && CheckDeath(game, evts))
            evts.Add(new LifeEvent { Age = p.Age, Text = $"☠️ {p.Name} murió a los {p.Age} años." });

        p.History.AddRange(evts);
        return evts;
    }

    private void LogSchool(GameState game, List<LifeEvent> evts)
    {
        var p = game.Person;
        var byAge = p.Age switch
        {
            6 when p.Education == EducationLevel.None => EducationLevel.PrimarySchool,
            12 when p.Education == EducationLevel.PrimarySchool => EducationLevel.LowerSecondary,
            15 when p.Education == EducationLevel.LowerSecondary => EducationLevel.UpperSecondary,
            18 when p.Education == EducationLevel.UpperSecondary => EducationLevel.HighSchool,
            _ => (EducationLevel?)null
        };

        if (byAge.HasValue)
        {
            p.Education = byAge.Value;
            var label = byAge.Value switch
            {
                EducationLevel.PrimarySchool => "Empezaste la escuela primaria.",
                EducationLevel.LowerSecondary => "Pasaste a la secundaria básica.",
                EducationLevel.UpperSecondary => "Pasaste a la escuela secundaria alta.",
                EducationLevel.HighSchool => "🎓 Te graduaste de la escuela.",
                _ => byAge.Value.ToString()
            };
            evts.Add(new LifeEvent { Age = p.Age, Text = label });

            if (byAge.Value == EducationLevel.HighSchool)
                p.AddFlag("GRADUATED_SECONDARY");
        }

        // notas reaccionan a smarts (y no hay escuela despues de graduarse)
        if (p.Age >= 6 && p.Age < 18 && !p.HasFlag("EXPELLED") && !p.HasFlag("DROPPED_OUT"))
            p.AddStat("grades", p.Stat("smarts") >= 70 ? 3 : p.Stat("smarts") < 40 ? -4 : 0);

        // universidad y posgrado: avance por ano mientras haya matricula activa
        AdvanceUniversity(game, evts);
    }

    /// <summary>Decide donde sigue saliendo el pool de eventos del Age Up:
    /// guerra (por bandera o especialidad militar) o vida civil (por cityStatus).</summary>
    private string PoolFor(GameState game)
    {
        var p = game.Person;
        if (game.InWar)
        {
            if (p.HasFlag("POW") && _data.EventsByPool.ContainsKey("pool_pow")) return "pool_pow";
            if (p.HasFlag("REFUGEE") && _data.EventsByPool.ContainsKey("pool_refugee")) return "pool_refugee";

            // militar: el pool lo define la especialidad del trabajo (Schema.md ## Specialties)
            if (p.JobId is { } jid && _jobs.FindJob(jid) is { } job && job.Specialty is { } sp)
            {
                var milPool = sp switch
                {
                    "infantry" => "pool_trench",
                    "drone" => "pool_drone",
                    "tank" => "pool_tank",
                    "pilot" => "pool_air",
                    "navy" => "pool_naval",
                    "special_forces" => "pool_special_ops",
                    "medical" => "pool_civil_medic",
                    "journalist" => "pool_civil_journalist",
                    _ => null
                };
                if (milPool is not null && _data.EventsByPool.ContainsKey(milPool))
                    return milPool;
            }

            var warCivil = "pool_civil_" + game.CityStatus;
            if (_data.EventsByPool.ContainsKey(warCivil)) return warCivil;
        }

        if (_data.EventsByPool.TryGetValue("pool_civil_" + game.CityStatus, out _))
            return "pool_civil_" + game.CityStatus;
        return "pool_civil_safe";
    }

    /// <summary>Tira un evento del pool que toca esta vez (eleccion y outcome aleatorios)
    /// y lo encadena con nextEventId.</summary>
    private void RunRandomPoolEvent(GameState game, List<LifeEvent> evts, int maxDepth)
    {
        var pool = PoolFor(game);
        if (!_data.EventsByPool.TryGetValue(pool, out var poolEvents))
            return;

        var p = game.Person;
        var candidates = poolEvents
            .Where(e => e.Choices.Count > 0)
            .Where(e => !e.AgeMin.HasValue || (p.Age >= e.AgeMin && p.Age <= (e.AgeMax ?? 120)))
            .Where(e => MeetsEventRequires(game, e.Requires))
            .ToList();
        if (candidates.Count == 0) return;

        var ev = candidates[_rng.Next(candidates.Count)];
        ResolveChain(game, ev.Id, evts, 0, maxDepth);
    }

    /// <summary>Requires de un evento (banderas del jugador y stats minimas).</summary>
    private bool MeetsEventRequires(GameState game, DataLoader.RelRequires? req)
    {
        if (req is null) return true;
        var p = game.Person;
        if (req.FlagsNone is { Count: > 0 } && req.FlagsNone.Any(p.Flags.Contains)) return false;
        if (req.FlagsExist is { Count: > 0 } && !req.FlagsExist.All(p.Flags.Contains)) return false;
        if (req.FlagsAll is { Count: > 0 } && !req.FlagsAll.All(p.Flags.Contains)) return false;
        if (req.FlagsAny is { Count: > 0 } && !req.FlagsAny.Any(p.Flags.Contains)) return false;
        return MeetsStatRequires(game, req);
    }

    private bool MeetsStatRequires(GameState game, DataLoader.RelRequires req)
    {
        var p = game.Person;
        if (req.MinStat is { Count: > 0 })
            foreach (var (key, min) in req.MinStat)
                if (p.Stat(key) < min) return false;
        if (req.MaxStat is { Count: > 0 })
            foreach (var (key, max) in req.MaxStat)
                if (p.Stat(key) > max) return false;
        if (!string.IsNullOrEmpty(req.Education) &&
            Enum.TryParse<EducationLevel>(req.Education, out var need) &&
            p.Education < need)
            return false;
        if (req.MinKarma.HasValue && p.Stat("karma") < req.MinKarma.Value) return false;
        if (req.MaxKarma.HasValue && p.Stat("karma") > req.MaxKarma.Value) return false;
        return true;
    }

    private void ResolveChain(GameState game, string eventId, List<LifeEvent> evts, int depth, int maxDepth)
    {
        if (depth >= maxDepth || !_data.CivilEvents.TryGetValue(eventId, out var ev))
            return;

        var choice = ev.Choices[_rng.Next(ev.Choices.Count)];
        var outcome = PickWeighted(choice.Outcomes);
        if (outcome is null) return;

        var p = game.Person;
        var deltas = new Dictionary<string, int>();

        foreach (var (key, node) in outcome.StatsData)
        {
            if (node.ValueKind == JsonValueKind.Number && node.TryGetInt32(out var delta))
            {
                p.AddStat(key, delta);
                deltas[key] = delta;
            }
        }

        var money = outcome.Money ?? 0;
        if (money != 0) p.Money += money;

        if (!string.IsNullOrEmpty(outcome.Flag)) p.AddFlag(outcome.Flag);
        if (!string.IsNullOrEmpty(outcome.RemoveFlag)) p.RemoveFlag(outcome.RemoveFlag);
        if (outcome.LoseJob == true && p.JobId is not null)
        {
            p.JobId = null;
            p.JobYears = 0;
        }
        if (!string.IsNullOrEmpty(outcome.SetCity))
            game.CityStatus = outcome.SetCity;

        evts.Add(new LifeEvent
        {
            Age = p.Age,
            Text = ev.Text,
            Detail = outcome.ResultText,
            Deltas = deltas,
            MoneyDelta = money
        });

        if (!string.IsNullOrEmpty(outcome.NextEventId))
            ResolveChain(game, outcome.NextEventId, evts, depth + 1, maxDepth);
    }

    /// <summary>Muerte por vejez (60+) o salud al suelo. Pone flag DEAD.</summary>
    private bool CheckDeath(GameState game, List<LifeEvent> evts)
    {
        var p = game.Person;
        if (p.Stat("health") <= 0)
        {
            p.AddFlag("DEAD");
            evts.Add(new LifeEvent { Age = p.Age, Text = "Tu cuerpo no aguantó más." });
            return false; // ya se registro
        }
        if (p.Age < 60)
            return false;

        var chance = (p.Age - 60) / 120.0 + (100 - p.Stat("health")) / 400.0;
        if (_rng.NextDouble() < chance)
        {
            p.AddFlag("DEAD");
            return true;
        }
        return false;
    }

    // ============================================================ ACTIVIDADES

    /// <summary>Una actividad del menu (Gym, Meditar...): coste fijo y deltas fijos.</summary>
    public sealed record Activity(string Id, string Label, long Cost,
        Dictionary<string, int> Deltas, string SuccessText);

    private static readonly List<Activity> Activities = new()
    {
        new("gym", "🏋️ Ir al gimnasio", 30, new() { ["fitness"] = 4, ["health"] = 1, ["happiness"] = 1 },
            "Hiciste la rutina completa del gimnasio."),
        new("meditate", "🧘 Meditar", 0, new() { ["stress"] = -6, ["happiness"] = 2 },
            "Meditaste 20 minutos y respiraste profundo."),
        new("library", "📚 Leer en la biblioteca", 0, new() { ["smarts"] = 3 },
            "Leíste un buen rato en la biblioteca."),
        new("doctor", "🩺 Ir al médico", 80, new() { ["health"] = 5, ["happiness"] = -1 },
            "Fuiste al médico y revisaste todo."),
        new("barber", "💈 Arreglarte en la barbería", 50, new() { ["looks"] = 3, ["happiness"] = 2 },
            "Saliste de la barbería hecho un rey."),
        new("walk", "🚶 Dar una caminata", 0, new() { ["fitness"] = 2, ["happiness"] = 1, ["stress"] = -2 },
            "Diste un paseo largo y despejaste la cabeza."),
        new("mirror", "🪞 Mirarte al espejo", 0, new() { ["happiness"] = 1, ["looks"] = 1 },
            "Te miraste al espejo y no estas mal."),
        new("netflix", "🎬 Ver una película", 5, new() { ["happiness"] = 3, ["fitness"] = -1 },
            "Viste una buena película en el sofá.")
    };

    public IReadOnlyList<Activity> AllActivities => Activities;

    public LifeEvent RunActivity(GameState game, string activityId)
    {
        var act = Activities.FirstOrDefault(a => a.Id == activityId);
        if (act is null)
            return Feed(game, "Actividad desconocida.");

        var p = game.Person;
        if (p.IsDead)
            return Feed(game, "Hasta los muertos descansan.");

        if (act.Cost > 0 && !p.TrySpend(act.Cost))
            return Feed(game, $"No tienes ${act.Cost} para {act.Label}.");

        var deltas = new Dictionary<string, int>();
        foreach (var (key, delta) in act.Deltas)
        {
            p.AddStat(key, delta);
            deltas[key] = delta;
        }

        var evt = new LifeEvent
        {
            Age = p.Age,
            Text = act.SuccessText,
            Deltas = deltas,
            MoneyDelta = -(int)act.Cost
        };
        p.History.Add(evt);
        return evt;
    }

    // ================================================================= TRABAJO

    /// <summary>Trabajos a los que el jugador puede aplicar (estudio y edad).
    /// En la prision no hay bolsa de trabajo: los trabajos alli son acciones del menu.</summary>
    public IEnumerable<Job> EligibleJobs(GameState game, string? categoryId) =>
        _jobs.JobsRequiringAtMost(game.Person.Education, game.Person.Age)
             .Where(j => j.Id != game.Person.JobId
                         && !game.Person.IsDead
                         && !game.Person.HasFlag("IN_PRISON")
                         && game.Person.Age >= 16
                         && (categoryId is null || j.Category == categoryId));

    /// <summary>Aplica al trabajo. Probabilidad segun estudio/inteligencia. false = rechazado.</summary>
    public bool ApplyForJob(GameState game, string jobId)
    {
        var p = game.Person;
        if (p.IsDead || p.IsEmployed || _jobs.FindJob(jobId) is not { } job)
            return false;

        var eduOk = job.Education is null || job.Education.Value <= p.Education;
        var chance = eduOk ? 55 + p.Stat("smarts") / 4 : 10;
        if (_rng.Next(100) >= chance)
            return false;

        p.JobId = jobId;
        p.JobYears = 0;
        p.AddFlag("EMPLOYED");
        p.RemoveFlag("UNEMPLOYED");

        var salary = _jobs.SalaryOf(job);
        p.Money += salary ?? 0; // firmas y paga el primer ano (simplificacion)
        return true;
    }

    /// <summary>Ascenso: hay cadena y se tras 50% + experiencia. Devuelve mensaje.</summary>
    public (bool ok, string msg) TryPromote(GameState game)
    {
        var p = game.Person;
        if (p.JobId is null || _jobs.FindJob(p.JobId) is not { } job)
            return (false, "No tienes trabajo.");
        if (_jobs.NextPromotion(job) is not { } next)
            return (false, "Ya estás en el tope de tu cadena.");

        var chance = 40 + p.JobYears * 10 + p.Stat("smarts") / 5;
        if (_rng.Next(100) >= chance)
            return (false, "Te dijeron que todavía no (pruba otro ano).");

        p.JobId = next.Id;
        return (true, $"Ascendiste a {next.Title} 🎉");
    }

    public void QuitJob(GameState game)
    {
        var p = game.Person;
        if (p.JobId is null) return;
        p.JobId = null;
        p.JobYears = 0;
        p.RemoveFlag("EMPLOYED");
        p.AddFlag("UNEMPLOYED");
    }

    // =============================================================== RELACIONES

    /// <summary>Acciones disponibles para una relacion (edad, bar, flags y coste).</summary>
    public IReadOnlyList<DataLoader.RelAction> EligibleActions(GameState game, Relation rel)
    {
        var p = game.Person;
        return _data.ActionsFor(rel.Type).Where(a =>
            p.Age >= a.MinAge && p.Age <= a.MaxAge
            && rel.Bar >= (a.Requires?.MinBar ?? 0)
            && rel.Bar <= (a.Requires?.MaxBar ?? 100)
            && (a.Requires?.FlagsAny is not { Count: > 0 } || a.Requires.FlagsAny.Any(rel.Flags.Contains))
            && (a.Requires?.FlagsNone is null || !a.Requires.FlagsNone.Any(rel.Flags.Contains))
            && (a.Requires?.MinKarma is null || p.Stat("karma") >= a.Requires.MinKarma.Value)
            && (a.Requires?.MaxKarma is null || p.Stat("karma") <= a.Requires.MaxKarma.Value)
        ).ToList();
    }

    /// <summary>Ejecuta una accion de relacion. Devuelve las entradas del feed (la accion + sus subeventos).</summary>
    public List<LifeEvent> RunRelationAction(GameState game, Relation rel, DataLoader.RelAction action)
    {
        var p = game.Person;
        if (p.IsDead)
            return new List<LifeEvent> { Feed(game, "Hasta los muertos descansan.") };

        if (!p.TrySpend(action.Cost))
            return new List<LifeEvent> { Feed(game, $"No tienes ${action.Cost} para esa acción.") };

        var outcome = PickWeighted(action.Outcomes);
        if (outcome is null)
            return new List<LifeEvent> { Feed(game, "Esa acción no tiene resultados configurados.") };

        var deltas = new Dictionary<string, int>();
        foreach (var (key, node) in outcome.StatsData)
            if (node.ValueKind == JsonValueKind.Number && node.TryGetInt32(out var delta))
            {
                p.AddStat(key, delta);
                deltas[key] = delta;
            }

        var money = outcome.Money ?? 0;
        if (money != 0) p.Money += money;

        var barDelta = outcome.RelationDelta ?? 0;
        if (barDelta != 0)
            rel.Bar = Math.Clamp(rel.Bar + barDelta, 0, 100);

        if (!string.IsNullOrEmpty(outcome.Flag)) p.AddFlag(outcome.Flag);
        if (!string.IsNullOrEmpty(outcome.RemoveFlag)) p.RemoveFlag(outcome.RemoveFlag);
        if (outcome.LoseJob == true && p.JobId is not null)
        {
            p.JobId = null;
            p.JobYears = 0;
        }

        var evt = new LifeEvent
        {
            Age = p.Age,
            Text = $"{rel.Name} ({rel.KindLabel}): {outcome.ResultText}",
            Detail = barDelta != 0 ? $"Relación {(barDelta > 0 ? "+" : "")}{barDelta}" : null,
            Deltas = deltas,
            MoneyDelta = money - (int)action.Cost
        };
        p.History.Add(evt);

        var evts = new List<LifeEvent> { evt };
        if (!string.IsNullOrEmpty(outcome.NextEventId))
            ResolveSubEventChain(game, rel, outcome.NextEventId, evts, 1);

        return evts;
    }

    /// <summary>Resuelve la cadena de subeventos de relacion (subevents.json) encadenando via nextEventId.</summary>
    private void ResolveSubEventChain(GameState game, Relation rel, string subEventId, List<LifeEvent> evts, int depth)
    {
        if (depth > 4 || !_data.SubEvents.TryGetValue(subEventId, out var sub))
            return;

        // subevento con choices (relationship_events.json): primero una choice al azar,
        // luego su outcome ponderado. Sin choices: outcome ponderado directo.
        DataLoader.        DataLoader.RelOutcome? outcome;
        if (sub.Choices is { Count: > 0 })
        {
            var choice = sub.Choices[_rng.Next(sub.Choices.Count)];
            outcome = PickWeighted(choice.Outcomes);
        }
        else
        {
            outcome = PickWeighted(sub.Outcomes);
        }
        if (outcome is null) return;

        var p = game.Person;
        var deltas = new Dictionary<string, int>();
        foreach (var (key, node) in outcome.StatsData)
            if (node.ValueKind == JsonValueKind.Number && node.TryGetInt32(out var delta))
            {
                p.AddStat(key, delta);
                deltas[key] = delta;
            }

        var money = outcome.Money ?? 0;
        if (money != 0) p.Money += money;

        var barDelta = outcome.RelationDelta ?? 0;
        if (barDelta != 0)
            rel.Bar = Math.Clamp(rel.Bar + barDelta, 0, 100);

        if (!string.IsNullOrEmpty(outcome.Flag)) p.AddFlag(outcome.Flag);
        if (!string.IsNullOrEmpty(outcome.RemoveFlag)) p.RemoveFlag(outcome.RemoveFlag);
        if (outcome.LoseJob == true && p.JobId is not null)
        {
            p.JobId = null;
            p.JobYears = 0;
        }
        if (outcome.LoseProperty == true && p.Properties.Count > 0)
            p.Properties.RemoveAt(_rng.Next(p.Properties.Count));

        evts.Add(new LifeEvent
        {
            Age = p.Age,
            Text = $"{rel.Name}: {outcome.ResultText}",
            Detail = sub.Text,
            Deltas = deltas,
            MoneyDelta = money
        });

        if (!string.IsNullOrEmpty(outcome.NextEventId))
            ResolveSubEventChain(game, rel, outcome.NextEventId, evts, depth + 1);
    }

    // ==================================================================== GUERRA

    /// <summary>Revisa cada ano si hay guerra nueva y, si ya hay, avanza el reloj
    /// de meses, la etapa de la ciudad, el reclutamiento y el fin del conflicto.</summary>
    private void ApplyWarTick(GameState game, List<LifeEvent> evts)
    {
        var p = game.Person;
        var rules = _data.WarRules;
        if (!game.InWar)
        {
            // declaracion: solo rivalries que tocan el pais del jugador
            if (rules?.Declaration is not { } decl || _data.Rivalries.Count == 0) return;
            var countryId = CountryIdOf(p.Country);
            if (countryId is null) return;

            var chance = 0.0;
            string? enemy = null;
            foreach (var r in _data.Rivalries)
            {
                if (r.Weight < decl.MinRivalryToDeclare) continue;
                if (r.A != countryId && r.B != countryId) continue;
                var pc = Math.Min(decl.MaxAnnualProbability,
                    decl.AnnualBaseProbability * r.Weight * decl.RivalryWeightFactor
                    * decl.SamePlayerCountryBias);
                if (pc > chance) { chance = pc; enemy = r.A == countryId ? r.B : r.A; }
            }
            if (enemy is null || _rng.NextDouble() >= chance) return;

            game.InWar = true;
            game.EnemyCountry = enemy;
            game.WarMonths = 0;
            game.CityStatus = "safe";
            evts.Add(new LifeEvent
            {
                Age = p.Age,
                Text = $"💥 {EnemyLabelOrId(game)} le declaró la guerra a {p.Country}. Los países se preparan.",
                Deltas = new() { ["stress"] = 10, ["happiness"] = -5 }
            });
            return;
        }

        // ---- guerra activa: avanza un mes por Age Up
        game.WarMonths += rules?.TimeScale?.WarMonthsPerClick ?? 1;

        // reclutamiento una vez (35% por ano en guerra)
        if (rules?.Draft is { } draft && !p.HasFlag("DRAFTED") && !p.HasFlag("POW")
            && !p.HasFlag("REFUGEE") && p.Age >= draft.MinAge && p.Age <= draft.MaxAge
            && _rng.Next(100) < (int)(draft.ChanceIfAtWar * 100))
        {
            var deferred = draft.DeferEducation ?? new();
            var isDeferred = deferred.Contains(p.Education.ToString());
            var exempt = draft.ExemptFlags ?? new();
            if (!isDeferred && !exempt.Any(p.Flags.Contains))
            {
                p.AddFlag("DRAFTED");
                if (p.JobId is not null) { p.JobId = null; p.JobYears = 0; }
                evts.Add(new LifeEvent
                {
                    Age = p.Age,
                    Text = "📜 Te reclutaron a la fuerza: reportas al cuartel mañana mismo.",
                    Deltas = new() { ["stress"] = 15, ["happiness"] = -10 }
                });
            }
        }

        // etapa de la ciudad segun progreso de la guerra (24 meses de referencia)
        var progress = Math.Min(1.0, game.WarMonths / 24.0);
        var steps = rules?.CityStatusProgression;
        if (steps is { Count: > 0 })
        {
            var target = "safe";
            foreach (var step in steps)
                if (progress >= step.WarProgress) target = step.Status;
            if (target != game.CityStatus && _rng.NextDouble() < (rules?.CityStatusTransitionChance ?? 0.25))
            {
                game.CityStatus = target;
                if (target == "occupied" || target == "destroyed")
                    LoseAllProperties(game, evts);
            }
        }

        // posible fin de la guerra: chance mensual creciente
        if (rules?.WarEnd is { } end && game.WarMonths >= end.MinWarMonths)
        {
            var monthsOver = game.WarMonths - end.MinWarMonths;
            var endChance = Math.Min(end.MaxMonthlyEndChance,
                end.BaseMonthlyEndChance + end.GrowthPerMonth * monthsOver);
            if (_rng.NextDouble() < endChance)
            {
                game.InWar = false;
                // vuelven a casa los movilizados; el prisionero queda liberado
                if (p.HasFlag("DRAFTED")) p.RemoveFlag("DRAFTED");
                if (p.HasFlag("POW")) p.RemoveFlag("POW");
                game.WarMonths = 0;
                game.EnemyCountry = null;
                game.CityStatus = "safe";
                evts.Add(new LifeEvent
                {
                    Age = p.Age,
                    Text = "🕊️ La guerra terminó. Las campanas tocan en todo el país y la vida sigue.",
                    Deltas = new() { ["happiness"] = 10, ["stress"] = -8 }
                });
            }
        }
    }

    public string? CountryIdOf(string countryLabel) =>
        _data.Countries.FirstOrDefault(c =>
            string.Equals(c.Label, countryLabel, StringComparison.OrdinalIgnoreCase)
            || string.Equals(c.Id, countryLabel, StringComparison.OrdinalIgnoreCase))?.Id;

    private string EnemyLabelOrId(GameState game) =>
        _data.Countries.FirstOrDefault(c => c.Id == game.EnemyCountry)?.Label ?? game.EnemyCountry ?? "El enemigo";

    private void LoseAllProperties(GameState game, List<LifeEvent> evts)
    {
        var p = game.Person;
        if (p.Properties.Count == 0) return;
        var lost = new List<string>(p.Properties);
        p.Properties.Clear();
        evts.Add(new LifeEvent
        {
            Age = p.Age,
            Text = $"🏠 Perdiste {lost.Count} propiedad(es) con la ocupación de tu ciudad.",
            Deltas = new() { ["happiness"] = -15, ["stress"] = 10 }
        });
    }

    // ============================================================== UNIVERSIDAD

    /// <summary>Matriculas disponibles segun estudio y dinero (universidad y posgrados).</summary>
    public List<(EducationLevel level, string label, int cost)> UniversityOptions(GameState game)
    {
        var p = game.Person;
        var list = new List<(EducationLevel, string, int)>();
        if (p.IsDead || p.HasFlag("IN_PRISON")) return list;
        if (p.HasFlag("EXPELLED") || p.HasFlag("DROPPED_OUT")) return list;

        var ageMin = 17;
        if (p.Age < ageMin) return list;

        if (p.Education >= EducationLevel.HighSchool && p.Education < EducationLevel.University
            && !p.HasFlag("ACCEPTED_UNIVERSITY"))
        {
            list.Add((EducationLevel.University, "🎓 Matricularme en la universidad", 12000));
            list.Add((EducationLevel.CommunityCollege, "🛠️ Colegio técnico (más barato)", 4000));
        }
        if (p.Education >= EducationLevel.University)
        {
            list.Add((EducationLevel.GraduateSchool, "📜 Posgrado / maestría", 25000));
            list.Add((EducationLevel.MedicalSchool, "🩺 Escuela de medicina", 60000));
            list.Add((EducationLevel.LawSchool, "⚖️ Escuela de derecho", 50000));
            list.Add((EducationLevel.NursingSchool, "💉 Escuela de enfermería", 30000));
            list.Add((EducationLevel.VeterinarySchool, "🐾 Escuela de veterinaria", 48000));
            list.Add((EducationLevel.PharmacySchool, "💊 Escuela de farmacia", 45000));
            list.Add((EducationLevel.BusinessSchool, "💼 Escuela de negocios", 35000));
        }
        return list;
    }

    /// <summary>Aplica a la matricula: chance segun smarts y notas (bequita si sobresale).</summary>
    public (bool ok, string msg) EnrollUniversity(GameState game, EducationLevel level, int cost)
    {
        var p = game.Person;
        if (!p.TrySpend(cost))
            return (false, $"No tienes ${cost} para la matrícula.");

        // beca si promete: smarts alto + notas altas
        var bonus = p.Stat("smarts") / 3 + p.Stat("grades") / 5;
        if (level >= EducationLevel.University && _rng.Next(100) >= 40 + bonus)
        {
            return (false, "No pasaron la admisión: te recomiendan mejorar notas y volver el próximo año.");
        }

        if (!p.HasFlag("ACCEPTED_UNIVERSITY") && level == EducationLevel.University)
            p.AddFlag("ACCEPTED_UNIVERSITY");

        p.Education = level;
        p.UniversityYears = 0;
        p.GradSchoolYears = 0;
        if (p.Stat("smarts") >= 80 && _rng.Next(100) < 20)
        {
            p.AddFlag("SCHOLARSHIP");
            p.Money += cost / 2; // media beca de hito
            return (true, "🎓 Matrícula hecha. Y ganaste media beca por tu récord académico.");
        }
        return (true, "🎓 Matrícula hecha. Estudias duro y esperas el título.");
    }

    private void AdvanceUniversity(GameState game, List<LifeEvent> evts)
    {
        var p = game.Person;
        var isGrad = p.Education >= EducationLevel.GraduateSchool && p.Education != EducationLevel.University;
        var studying = p.Education == EducationLevel.University || p.Education == EducationLevel.CommunityCollege
                        || isGrad;
        if (!studying || p.IsDead) return;
        if (p.Age < 18 || p.Age > 70) return;

        p.AddStat("stress", 2);
        p.AddStat("smarts", 1);
        var passed = p.Stat("smarts") + p.Stat("discipline") >= 100;

        if (p.Education == EducationLevel.CommunityCollege)
        {
            p.UniversityYears++;
            if (p.UniversityYears >= 2 && passed)
            {
                p.Education = EducationLevel.HighSchool;
                p.UniversityYears = 0;
                evts.Add(new LifeEvent { Age = p.Age, Text = "🎓 Te graduaste del colegio técnico." });
            }
        }
        else if (p.Education == EducationLevel.University)
        {
            p.UniversityYears++;
            if (p.UniversityYears >= 4 && passed)
            {
                p.Education = EducationLevel.GraduateSchool;
                p.UniversityYears = 0;
                p.RemoveFlag("ACCEPTED_UNIVERSITY");
                evts.Add(new LifeEvent
                {
                    Age = p.Age,
                    Text = "🎓 ¡Te graduaste de la universidad!",
                    Deltas = new() { ["happiness"] = 10, ["smarts"] = 3 }
                });
            }
        }
        else
        {
            p.GradSchoolYears++;
            if (p.GradSchoolYears >= 3 && passed)
            {
                // guarda el nivel que ya tenia y el nuevo: el titulo reemplaza al anterior
                evts.Add(new LifeEvent
                {
                    Age = p.Age,
                    Text = $"🎓 Terminaste {p.Education}.",
                    Deltas = new() { ["happiness"] = 10, ["smarts"] = 3 }
                });
                p.GradSchoolYears = 0;
            }
        }
    }

    // ========================================================= EVENTOS ESCUELA

    /// <summary>Una tirada anual del modulo escuela (examenes, matricula, actos, cadenas).</summary>
    private void RunSchoolYearEvent(GameState game, List<LifeEvent> evts)
    {
        var p = game.Person;
        if (p.Age < 6 || p.Age > 22 || p.IsDead) return;
        if (p.HasFlag("EXPELLED") || p.HasFlag("DROPPED_OUT")) return;
        if (!_data.EventsByPool.TryGetValue("school", out var schoolEvents)) return;
        if (_rng.Next(100) >= 45) return;

        var candidates = schoolEvents
            .Where(e => e.Choices.Count > 0)
            .Where(e => !e.AgeMin.HasValue || (p.Age >= e.AgeMin && p.Age <= (e.AgeMax ?? 120)))
            .Where(e => MeetsEventRequires(game, e.Requires))
            .ToList();
        if (candidates.Count == 0) return;

        var ev = candidates[_rng.Next(candidates.Count)];
        ResolveChain(game, ev.Id, evts, 0, maxDepth: 3);
    }

    // ========================================================= ACCIONES GENERICAS

    /// <summary>Acciones disponibles de un menu generico (crime, health, fame...)</summary>
    public IReadOnlyList<DataLoader.GenericAction> EligibleGenericActions(GameState game, string catalog)
    {
        if (!_data.MenuActions.TryGetValue(catalog, out var list))
            return Array.Empty<DataLoader.GenericAction>();

        var p = game.Person;
        return list.Where(a =>
            p.Age >= a.MinAge && p.Age <= a.MaxAge
            && MeetsRequires(game, a.Requires)
        ).ToList();
    }

    private bool MeetsRequires(GameState game, DataLoader.RelRequires? req)
    {
        if (req is null) return true;
        var p = game.Person;
        if (req.FlagsNone is { Count: > 0 } && req.FlagsNone.Any(p.Flags.Contains)) return false;
        if (req.FlagsExist is { Count: > 0 } && !req.FlagsExist.All(p.Flags.Contains)) return false;
        if (req.FlagsAll is { Count: > 0 } && !req.FlagsAll.All(p.Flags.Contains)) return false;
        if (req.FlagsAny is { Count: > 0 } && !req.FlagsAny.Any(p.Flags.Contains)) return false;
        if (!MeetsStatRequires(game, req)) return false;
        return true;
    }

    /// <summary>Ejecuta una accion de un menu generico y devuelve la entrada del feed.</summary>
    public List<LifeEvent> RunGenericAction(GameState game, DataLoader.GenericAction action)
    {
        var p = game.Person;
        if (p.IsDead)
        { var dead = new List<LifeEvent> { Feed(game, "Hasta los muertos descansan.") }; return dead; }

        if (action.Cost > 0 && !p.TrySpend(action.Cost))
            return new List<LifeEvent> { Feed(game, $"No tienes ${action.Cost} para esa acción.") };

        var outcome = PickWeighted(action.Outcomes);
        if (outcome is null)
            return new List<LifeEvent> { Feed(game, "Esa acción no tiene resultados configurados.") };

        var evts = new List<LifeEvent>();
        ApplyOutcome(game, outcome, action.Cost, evts);

        if (!string.IsNullOrEmpty(outcome.NextEventId) && _data.CivilEvents.TryGetValue(outcome.NextEventId, out _))
            ResolveChain(game, outcome.NextEventId, evts, 1, maxDepth: 3);

        return evts;
    }

    /// <summary>Aplica un resultado a la vida del jugador: stats, dinero, flags, trabajo.</summary>
    private void ApplyOutcome(GameState game, DataLoader.RelOutcome outcome, long cost, List<LifeEvent> evts)
    {
        var p = game.Person;
        var deltas = new Dictionary<string, int>();
        foreach (var (key, node) in outcome.StatsData)
            if (node.ValueKind == JsonValueKind.Number && node.TryGetInt32(out var delta))
            {
                p.AddStat(key, delta);
                deltas[key] = delta;
            }

        var money = outcome.Money ?? 0;
        if (money != 0) p.Money += money;

        if (!string.IsNullOrEmpty(outcome.Flag)) p.AddFlag(outcome.Flag);
        if (!string.IsNullOrEmpty(outcome.RemoveFlag)) p.RemoveFlag(outcome.RemoveFlag);
        if (outcome.LoseJob == true && p.JobId is not null)
        {
            p.JobId = null;
            p.JobYears = 0;
        }
        if (outcome.LoseProperty == true && p.Properties.Count > 0)
            p.Properties.RemoveAt(_rng.Next(p.Properties.Count));

        evts.Add(new LifeEvent
        {
            Age = p.Age,
            Text = outcome.ResultText ?? string.Empty,
            Deltas = deltas,
            MoneyDelta = money - (int)cost
        });
    }

    /// <summary>Aplica un resultado de pool (pool events usan otro modelo de clase).</summary>
    private void ApplyPoolOutcome(GameState game, DataLoader.PoolOutcome outcome, List<LifeEvent> evts)
    {
        var p = game.Person;
        var deltas = new Dictionary<string, int>();
        foreach (var (key, node) in outcome.StatsData)
            if (node.ValueKind == JsonValueKind.Number && node.TryGetInt32(out var delta))
            {
                p.AddStat(key, delta);
                deltas[key] = delta;
            }

        var money = outcome.Money ?? 0;
        if (money != 0) p.Money += money;
        if (!string.IsNullOrEmpty(outcome.SetCity))
            game.CityStatus = outcome.SetCity;
        if (!string.IsNullOrEmpty(outcome.Flag)) p.AddFlag(outcome.Flag);
        if (!string.IsNullOrEmpty(outcome.RemoveFlag)) p.RemoveFlag(outcome.RemoveFlag);
        if (outcome.LoseJob == true && p.JobId is not null)
        {
            p.JobId = null;
            p.JobYears = 0;
        }
        if (outcome.LoseProperty == true && p.Properties.Count > 0)
            p.Properties.RemoveAt(_rng.Next(p.Properties.Count));

        evts.Add(new LifeEvent
        {
            Age = p.Age,
            Text = outcome.ResultText ?? string.Empty,
            Deltas = deltas,
            MoneyDelta = money
        });
    }

    // ================================================================ LICENCIAS

    /// <summary>Licencias que el jugador puede intentar ahora mismo.</summary>
    public IReadOnlyList<DataLoader.LicenseDef> EligibleLicenses(GameState game)
    {
        var p = game.Person;
        return _data.LicenseDefs.Where(l =>
            !p.Licenses.Contains(l.Id)
            && p.Age >= l.AgeMin && p.Age <= l.AgeMax
            && MeetsRequires(game, l.Requires)).ToList();
    }

    /// <summary>Intento de licencia: paga el examen y hay chance de aprobar.</summary>
    public (bool ok, string msg) TryLicense(GameState game, DataLoader.LicenseDef license)
    {
        var p = game.Person;
        if (!p.TrySpend(license.Cost))
            return (false, $"No tienes ${license.Cost} para el examen.");

        var chance = license.PassChance / 2 + p.Stat("smarts") / 3;
        if (_rng.Next(100) >= chance)
            return (false, "Reprobaste el examen. Se paga igual... vuelve cuando estudies más.");

        p.Licenses.Add(license.Id);
        if (!string.IsNullOrEmpty(license.Flag)) p.AddFlag(license.Flag);
        return (true, $"✅ Aprobaste: {license.Label}.");
    }

    // ============================================================== PROPIEDADES

    /// <summary>Propiedades que el jugador puede comprar (edad y no repetida).</summary>
    public IReadOnlyList<DataLoader.PropertyDef> BuyableProperties(GameState game)
    {
        var p = game.Person;
        if (p.HasFlag("IN_PRISON")) return Array.Empty<DataLoader.PropertyDef>();
        return _data.PropertyDefs.Where(pr =>
            !p.Properties.Contains(pr.Id) && p.Age >= pr.AgeMin).ToList();
    }

    public (bool ok, string msg) BuyProperty(GameState game, DataLoader.PropertyDef prop)
    {
        var p = game.Person;
        if (!p.TrySpend(prop.Price))
            return (false, $"No tienes ${prop.Price} para esa propiedad.");
        p.Properties.Add(prop.Id);
        return (true, $"🏠 Compraste: {prop.Label}.");
    }

    /// <summary>Vende una propiedad: 70% del precio (menos si la ciudad esta destruida).</summary>
    public long SellProperty(GameState game, string propertyId)
    {
        var p = game.Person;
        if (!p.Properties.Remove(propertyId)) return 0;
        var basePrice = _data.PropertyDefs.FirstOrDefault(x => x.Id == propertyId)?.Price ?? 0;
        var factor = game.CityStatus == "destroyed" ? 0.2 : game.CityStatus == "occupied" ? 0.5 : 0.7;
        var price = (long)(basePrice * factor);
        p.Money += price;
        return price;
    }

    private void ApplyPropertyMaintenance(GameState game, List<LifeEvent> evts)
    {
        var p = game.Person;
        if (p.Properties.Count == 0) return;
        long total = 0;
        foreach (var id in p.Properties)
            total += _data.PropertyDefs.FirstOrDefault(x => x.Id == id)?.Maintenance ?? 0;
        if (total <= 0) return;

        if (p.Money >= total)
        {
            p.Money -= total;
            evts.Add(new LifeEvent
            {
                Age = p.Age,
                Text = $"🔧 Pagas ${total:N0} de mantenimiento anual de tus propiedades.",
                MoneyDelta = -(int)total
            });
        }
        else
        {
            // no alcanza: se vende una propiedad para cubrir la deuda
            var toSell = p.Properties.Last();
            var price = SellProperty(game, toSell);
            var name = _data.PropertyDefs.FirstOrDefault(x => x.Id == toSell)?.Label ?? toSell;
            evts.Add(new LifeEvent
            {
                Age = p.Age,
                Text = $"😰 No pagaste el mantenimiento y vendiste {name} para cubrir la deuda.",
                MoneyDelta = (int)(price - total)
            });
        }
    }

    // ============================================================== PERSONALIDAD

    private static readonly Dictionary<string, string> TraitOpposite = new()
    {
        ["affectionate"] = "cold", ["cold"] = "affectionate",
        ["stingy"] = "generous", ["generous"] = "stingy",
        ["resentful"] = "forgiving", ["forgiving"] = "resentful",
        ["loyal"] = "manipulative", ["manipulative"] = "loyal",
        ["strict"] = "easygoing", ["easygoing"] = "strict"
    };

    /// <summary>Asigna 1-3 rasgos a un NPC con los pesos de personality_templates.json.</summary>
    public void AssignNpcTraits(Relation rel, string? npcEducationLevel = null)
    {
        var tpl = _data.Templates?.NpcAssignment;
        if (tpl is null) return;

        var weights = new Dictionary<string, int>(tpl.BaseWeights);
        void Merge(Dictionary<string, int>? extra)
        {
            if (extra is null) return;
            foreach (var (trait, w) in extra)
                weights[trait] = Math.Clamp((weights.TryGetValue(trait, out var cur) ? cur : 0) + w, 0, 100);
        }

        Merge(tpl.ByRelation.GetValueOrDefault(rel.Type));
        foreach (var flag in rel.Flags)
            Merge(tpl.ByFlag.GetValueOrDefault(flag));
        if (npcEducationLevel is not null)
            Merge(tpl.ByEducation.GetValueOrDefault(npcEducationLevel));

        rel.Traits.Clear();
        var howMany = _rng.Next(tpl.MinTraitsPerNpc, tpl.MaxTraitsPerNpc + 1);
        var tries = 0;
        while (rel.Traits.Count < howMany && tries < 12)
        {
            tries++;
            var trait = PickWeighted(weights);
            if (trait is null || rel.Traits.Contains(trait)) continue;
            var opposite = TraitOpposite.GetValueOrDefault(trait);
            if (opposite is not null && rel.Traits.Contains(opposite)) continue;
            rel.Traits.Add(trait);
        }
    }

    private string? PickWeighted(Dictionary<string, int> weights)
    {
        var total = weights.Values.Sum();
        if (total <= 0) return null;
        var roll = _rng.Next(total);
        foreach (var (key, w) in weights)
        {
            roll -= w;
            if (roll < 0) return key;
        }
        return null;
    }

    // --------------------------------------------------------------- helpers

    private DataLoader.PoolOutcome? PickWeighted(List<DataLoader.PoolOutcome> outcomes)
    {
        var total = outcomes.Sum(o => o.Weight);
        if (total <= 0) return null;
        var roll = _rng.Next(total);
        foreach (var o in outcomes)
        {
            roll -= o.Weight;
            if (roll < 0) return o;
        }
        return outcomes[^1];
    }

    private DataLoader.RelOutcome? PickWeighted(List<DataLoader.RelOutcome> outcomes)
    {
        var total = outcomes.Sum(o => o.Weight);
        if (total <= 0) return null;
        var roll = _rng.Next(total);
        foreach (var o in outcomes)
        {
            roll -= o.Weight;
            if (roll < 0) return o;
        }
        return outcomes[^1];
    }

    private LifeEvent Feed(GameState game, string text)
    {
        var evt = new LifeEvent { Age = game.Person.Age, Text = text };
        game.Person.History.Add(evt);
        return evt;
    }
}
