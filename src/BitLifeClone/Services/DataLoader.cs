using System.Text.Json;
using System.Text.Json.Serialization;
using BitLifeClone.Models;

namespace BitLifeClone.Services;

/// <summary>
/// Cargo general de Data/ (lo que JobService no cubre):
///  - Data/Relationships/*.json: acciones por tipo de relacion (con requires/outcomes).
///  - Data/Events/pool_*.json: TODOS los pools (civiles por cityStatus + guerra).
///  - Data/{Crime,Health,Fame,School}/*.json: acciones de menus genericos.
///  - Data/War: reglas de guerra y rivalries.
///  - Data/{Licenses,Properties,World}: definiciones de licencias, propiedades y mundo.
/// Los outcomes se deserializan con JsonExtensionData para que CUALQUIER stat del
/// Schema venga en el JSON sin que haya que tocar este codigo (convencion del repo).
/// </summary>
public sealed class DataLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _dataDir;

    /// <summary>Acciones por RelationType: type -> acciones disponibles (de todos los archivos).</summary>
    public Dictionary<string, List<RelAction>> ActionsByType { get; } = new();

    /// <summary>Eventos de vida civil (por id). Los tira GameEngine en los Age Up.</summary>
    public Dictionary<string, PoolEvent> CivilEvents { get; } = new();

    /// <summary>Eventos agrupados por pool (pool_civil_safe, pool_trench, school, crime...).</summary>
    public Dictionary<string, List<PoolEvent>> EventsByPool { get; } = new();

    /// <summary>Subeventos de relacions (Data/Relationships/subevents.json): id -> subevento.</summary>
    public Dictionary<string, SubEvent> SubEvents { get; } = new();

    /// <summary>Acciones de menus genericos por catalogo: crime, prison, health, fame, school, school_xc, school_jobs.</summary>
    public Dictionary<string, List<GenericAction>> MenuActions { get; } = new();

    /// <summary>Reglas del motor de guerra (Data/War/war_rules.json).</summary>
    public WarConfig? WarRules { get; private set; }

    /// <summary>Rivalries de guerra (Data/War/rivalries.json): pairs con weight 0-100.</summary>
    public List<Rivalry> Rivalries { get; } = new();

    /// <summary>Definiciones de licencias comprables (Data/Licenses/licenses.json).</summary>
    public List<LicenseDef> LicenseDefs { get; } = new();

    /// <summary>Definiciones de propiedades comprables (Data/Properties/properties.json).</summary>
    public List<PropertyDef> PropertyDefs { get; } = new();

    /// <summary>Mundo: paises con sus ciudades (Data/World/world.json).</summary>
    public List<WorldCountry> Countries { get; } = new();

    /// <summary>Nombres para generar personas (Data/World/names.json).</summary>
    public NamePool Names { get; private set; } = new();

    /// <summary>Plantillas de personalidad para NPCs (personality_templates.json).</summary>
    public PersonalityTemplates? Templates { get; private set; }

    public DataLoader(string? dataDir = null)
    {
        _dataDir = dataDir ?? Path.Combine(AppContext.BaseDirectory, "Data");

        var relDir = Path.Combine(_dataDir, "Relationships");
        if (Directory.Exists(relDir))
            foreach (var file in Directory.GetFiles(relDir, "*.json"))
            {
                if (Path.GetFileName(file).Equals("subevents.json", StringComparison.OrdinalIgnoreCase))
                    LoadSubeventsFile(file);
                else
                    LoadRelationFile(file);
            }

        var eventsDir = Path.Combine(_dataDir, "Events");
        if (Directory.Exists(eventsDir))
            foreach (var file in Directory.GetFiles(eventsDir, "pool_*.json"))
                LoadEventPool(file);

        // ---- acciones de menus genericos (crime/prison/health/fame/school)
        LoadModule(Path.Combine(_dataDir, "Crime", "crime.json"), "crime");
        LoadModule(Path.Combine(_dataDir, "Crime", "prison.json"), "prison");
        LoadModule(Path.Combine(_dataDir, "Health", "health.json"), "health");
        LoadModule(Path.Combine(_dataDir, "Fame", "fame.json"), "fame");
        LoadModule(Path.Combine(_dataDir, "School", "menu_actions.json"), "school");
        LoadModule(Path.Combine(_dataDir, "School", "extracurriculars.json"), "school_xc");
        LoadModule(Path.Combine(_dataDir, "School", "school_jobs.json"), "school_jobs");

        // ---- eventos del modulo escuela (grades/menu/extracurricular/special)
        var schoolDir = Path.Combine(_dataDir, "School");
        if (Directory.Exists(schoolDir))
            foreach (var file in Directory.GetFiles(schoolDir, "*.json"))
                LoadEventPool(file);

        // ---- cadenas de crimen, salud y fama (objetos con "events"); los de acciones se saltan solos
        foreach (var dirName in new[] { "Crime", "Health", "Fame" })
        {
            var d = Path.Combine(_dataDir, dirName);
            if (!Directory.Exists(d)) continue;
            foreach (var file in Directory.GetFiles(d, "*.json"))
                LoadEventPool(file);
        }

        // ---- guerra
        LoadWar(Path.Combine(_dataDir, "War", "war_rules.json"), Path.Combine(_dataDir, "War", "rivalries.json"));

        // ---- definiciones sueltas
        LoadLicenses(Path.Combine(_dataDir, "Licenses", "licenses.json"));
        LoadProperties(Path.Combine(_dataDir, "Properties", "properties.json"));
        LoadWorld(Path.Combine(_dataDir, "World", "world.json"));
        LoadNames(Path.Combine(_dataDir, "World", "names.json"));
        LoadTemplates(Path.Combine(_dataDir, "Relationships", "personality_templates.json"));
    }

    /// <summary>Acciones elegibles para un tipo de relacion (por allowedTypes).</summary>
    public IReadOnlyList<RelAction> ActionsFor(string relationType) =>
        ActionsByType.TryGetValue(relationType, out var list) ? list : Array.Empty<RelAction>();

    // ------------------------------------------------------------------ carga

    private void LoadRelationFile(string path)
    {
        try
        {
            var doc = JsonSerializer.Deserialize<RelDoc>(File.ReadAllText(path), Options);
            if (doc?.Actions is null || doc.Actions.Count == 0)
                return;

            // byType (archivo agrupador, ej. work/kinship) o el relationType del archivo
            var types = doc.ByType is { Count: > 0 }
                ? doc.ByType
                : new List<string> { doc.RelationType ?? "" };

            foreach (var type in types)
            {
                if (string.IsNullOrEmpty(type)) continue;
                if (!ActionsByType.TryGetValue(type, out var list))
                    ActionsByType[type] = list = new List<RelAction>();
                list.AddRange(doc.Actions);
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"JSON invalido en {Path.GetFileName(path)}: {ex.Message}", ex);
        }
    }

    private void LoadEventPool(string path)
    {
        try
        {
            var content = File.ReadAllText(path).TrimStart();
            if (content.StartsWith("["))
            {
                // forma de pools de Data/Events: [{ pool, events: [...] }, ...]
                var blocks = JsonSerializer.Deserialize<List<PoolBlock>>(content, Options);
                if (blocks is null) return;
                foreach (var block in blocks)
                    foreach (var ev in block.Events ?? new())
                        if (!string.IsNullOrEmpty(ev.Id))
                            IndexEvent(ev); // id repetido: el validador ya lo caza
            }
            else
            {
                // forma de modulos: { ..., events: [ ... ] } (School, Crime, Health...)
                var doc = JsonSerializer.Deserialize<EventContainer>(content, Options);
                if (doc?.Events is null) return;
                foreach (var ev in doc.Events)
                    if (!string.IsNullOrEmpty(ev.Id))
                        IndexEvent(ev);
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"JSON invalido en {Path.GetFileName(path)}: {ex.Message}", ex);
        }
    }

    /// <summary>Pone un evento en el indice global y en su pool. Los archivos de eventos
    /// como School y Crime van envueltos en un objeto con "events": [...].</summary>
    private void IndexEvent(PoolEvent ev)
    {
        CivilEvents.TryAdd(ev.Id, ev);
        if (!EventsByPool.TryGetValue(ev.Pool ?? "", out var list))
            EventsByPool[ev.Pool ?? ""] = list = new List<PoolEvent>();
        list.Add(ev);
    }

    /// <summary>Carga los subeventos de relaciones (no tienen choices: poseen los outcomes directos).</summary>
    private void LoadSubeventsFile(string path)
    {
        try
        {
            var doc = JsonSerializer.Deserialize<SubeventDoc>(File.ReadAllText(path), Options);
            if (doc?.Subevents is null) return;
            foreach (var sub in doc.Subevents)
                if (!string.IsNullOrEmpty(sub.Id))
                    SubEvents.TryAdd(sub.Id, sub); // id repetido: el validador ya lo caza
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"JSON invalido en {Path.GetFileName(path)}: {ex.Message}", ex);
        }
    }

    private void LoadModule(string path, string catalog)
    {
        if (!File.Exists(path)) return;
        try
        {
            var doc = JsonSerializer.Deserialize<ModuleDoc>(File.ReadAllText(path), Options);
            if (doc?.Actions is null || doc.Actions.Count == 0) return;
            if (!MenuActions.TryGetValue(catalog, out var list))
                MenuActions[catalog] = list = new List<GenericAction>();
            list.AddRange(doc.Actions);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"JSON invalido en {Path.GetFileName(path)}: {ex.Message}", ex);
        }
    }

    private void LoadWar(string rulesPath, string rivalriesPath)
    {
        try
        {
            if (File.Exists(rulesPath))
                WarRules = JsonSerializer.Deserialize<WarConfig>(File.ReadAllText(rulesPath), Options);

            if (File.Exists(rivalriesPath))
            {
                var doc = JsonSerializer.Deserialize<RivalryDoc>(File.ReadAllText(rivalriesPath), Options);
                if (doc?.Rivalries is not null)
                    Rivalries.AddRange(doc.Rivalries);
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"JSON invalido en {Path.GetFileName(rivalriesPath)}: {ex.Message}", ex);
        }
    }

    private void LoadLicenses(string path)
    {
        if (!File.Exists(path)) return;
        try
        {
            var doc = JsonSerializer.Deserialize<LicenseDoc>(File.ReadAllText(path), Options);
            if (doc?.Exams is not null)
                LicenseDefs.AddRange(doc.Exams);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"JSON invalido en {Path.GetFileName(path)}: {ex.Message}", ex);
        }
    }

    private void LoadProperties(string path)
    {
        if (!File.Exists(path)) return;
        try
        {
            var doc = JsonSerializer.Deserialize<PropertyDoc>(File.ReadAllText(path), Options);
            if (doc?.List is not null)
                PropertyDefs.AddRange(doc.List);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"JSON invalido en {Path.GetFileName(path)}: {ex.Message}", ex);
        }
    }

    private void LoadWorld(string path)
    {
        if (!File.Exists(path)) return;
        try
        {
            var doc = JsonSerializer.Deserialize<WorldDoc>(File.ReadAllText(path), Options);
            if (doc?.Countries is not null)
                Countries.AddRange(doc.Countries);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"JSON invalido en {Path.GetFileName(path)}: {ex.Message}", ex);
        }
    }

    private void LoadNames(string path)
    {
        if (!File.Exists(path)) return;
        try
        {
            Names = JsonSerializer.Deserialize<NamePool>(File.ReadAllText(path), Options) ?? new NamePool();
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"JSON invalido en {Path.GetFileName(path)}: {ex.Message}", ex);
        }
    }

    private void LoadTemplates(string path)
    {
        if (!File.Exists(path)) return;
        try
        {
            Templates = JsonSerializer.Deserialize<PersonalityTemplates>(File.ReadAllText(path), Options);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"JSON invalido en {Path.GetFileName(path)}: {ex.Message}", ex);
        }
    }

    // ------------------------------------------------------- modelos relaciones

    public sealed class RelDoc
    {
        public string? RelationType { get; set; }
        public List<string>? ByType { get; set; }
        public List<RelAction> Actions { get; set; } = new();
    }

    /// <summary>Una accion del menu de relaciones (Label corto, cost, outcomes ponderados).</summary>
    public sealed class RelAction
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public List<string> AllowedTypes { get; set; } = new();
        public int MinAge { get; set; }
        public int MaxAge { get; set; } = 120;
        public long Cost { get; set; }
        public int CooldownYears { get; set; }
        public int? MaxPerYear { get; set; }
        public RelRequires? Requires { get; set; }
        public List<RelOutcome> Outcomes { get; set; } = new();
    }

    public sealed class RelRequires
    {
        public int MinBar { get; set; }
        public int MaxBar { get; set; } = 100;
        public List<string>? TraitsAny { get; set; }

        /// <summary>La accion no sale si el RELACIONADO tiene alguno de estos flags.</summary>
        public List<string>? FlagsNone { get; set; }

        public List<string>? FlagsAny { get; set; }

        /// <summary>La accion solo sale si el JUGADOR tiene TODOS estos flags.</summary>
        public List<string>? FlagsExist { get; set; }

        /// <summary>La accion solo sale si el JUGADOR tiene todos estos flags (igual que FlagsExist).</summary>
        public List<string>? FlagsAll { get; set; }

        public int? MinKarma { get; set; }
        public int? MaxKarma { get; set; }

        /// <summary>Stats minimas del jugador (school menu: fitness 40, etc.).</summary>
        public Dictionary<string, int>? MinStat { get; set; }

        /// <summary>Stats maximas del jugador.</summary>
        public Dictionary<string, int>? MaxStat { get; set; }

        /// <summary>Nivel de estudio minimo exigido (DriverLicense, universidad...).</summary>
        public string? Education { get; set; }
    }

    /// <summary>
    /// Outcome de una accion. Las stats van por ExtensionData: cualquier clave
    /// del Schema (happiness, health, smarts, looks...) llega sin tocar codigo.
    /// </summary>
    public sealed class RelOutcome
    {
        public int Weight { get; set; }
        public string? ResultText { get; set; }
        public int? RelationDelta { get; set; }
        public int? Money { get; set; }
        public bool? LoseProperty { get; set; }
        public string? Flag { get; set; }
        public string? RemoveFlag { get; set; }
        public bool? LoseJob { get; set; }
        public string? NextEventId { get; set; }
        public Dictionary<string, string>? TraitWeightMods { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement> StatsData { get; set; } = new();
    }

    // ---------------------------------------------------------- modelos eventos

    public sealed class PoolBlock
    {
        public string? Pool { get; set; }
        public List<PoolEvent> Events { get; set; } = new();
    }

    public sealed class PoolEvent
    {
        public string Id { get; set; } = string.Empty;

        /// <summary>Pool al que pertenece el evento (pool_civil_safe, pool_trench, school, fame...).</summary>
        public string? Pool { get; set; }

        public string Text { get; set; } = string.Empty;

        public int? AgeMin { get; set; }
        public int? AgeMax { get; set; }

        /// <summary>Requisitos a nivel evento (special_chains: flagsNone EXPELLED...).</summary>
        public RelRequires? Requires { get; set; }

        public List<PoolChoice> Choices { get; set; } = new();
    }

    public sealed class PoolChoice
    {
        public string Text { get; set; } = string.Empty;
        public List<PoolOutcome> Outcomes { get; set; } = new();
    }

    public sealed class PoolOutcome
    {
        public int Weight { get; set; }
        public string? ResultText { get; set; }
        public int? Money { get; set; }
        public string? Flag { get; set; }
        public string? RemoveFlag { get; set; }
        public string? NextEventId { get; set; }
        public string? SetCity { get; set; }
        public bool? LoseJob { get; set; }
        public bool? LoseProperty { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement> StatsData { get; set; } = new();
    }

    /// <summary>{ subevents: [ { id, text, outcomes } ] } de Relationships/subevents.json.</summary>
    public sealed class SubEvent
    {
        public string Id { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public List<RelOutcome> Outcomes { get; set; } = new();
    }

    public sealed class SubeventDoc
    {
        public List<SubEvent>? Subevents { get; set; }
    }

    // --------------------------------------------------------- menus genericos

    /// <summary>{ ... , events: [...] } de los modulos con eventos (School, Crime...).</summary>
    public sealed class EventContainer
    {
        public List<PoolEvent>? Events { get; set; }
    }

    /// <summary>{ module: ..., actions: [...] } de crime/prison/health/fame/school.</summary>
    public sealed class ModuleDoc
    {
        public List<GenericAction>? Actions { get; set; }
    }

    /// <summary>Accion de un menu generico. La mayoria de campos son opcionales porque
    /// cada archivo usa los que necesita (hoursCost solo escuela, baseSalary solo school_jobs).</summary>
    public sealed class GenericAction
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public int MinAge { get; set; }
        public int MaxAge { get; set; } = 120;
        public long Cost { get; set; }
        public int HoursCost { get; set; }
        public int CooldownYears { get; set; }
        public int? MaxPerYear { get; set; }

        /// <summary>Solo school_jobs: paga base de referencia por mes.</summary>
        public long? BaseSalary { get; set; }
        public string? Currency { get; set; }

        public RelRequires? Requires { get; set; }
        public List<RelOutcome> Outcomes { get; set; } = new();
    }

    // --------------------------------------------------------------------- guerra

    /// <summary>Estructura de Data/War/war_rules.json (solo lo que usa el motor).</summary>
    public sealed class WarConfig
    {
        public WarDeclaration? Declaration { get; set; }
        public WarTimeScale? TimeScale { get; set; }
        public WarEnd? WarEnd { get; set; }
        public WarDraft? Draft { get; set; }
        public List<CityStatusStep>? CityStatusProgression { get; set; }
        public Dictionary<string, string>? PoolSelection { get; set; }
        public double CityStatusTransitionChance { get; set; } = 0.25;
    }

    public sealed class WarDeclaration
    {
        public double AnnualBaseProbability { get; set; } = 0.03;
        public double RivalryWeightFactor { get; set; } = 0.0006;
        public double MaxAnnualProbability { get; set; } = 0.25;
        public int MinRivalryToDeclare { get; set; } = 45;
        public double SamePlayerCountryBias { get; set; } = 1.5;
    }

    public sealed class WarTimeScale
    {
        public int WarMonthsPerClick { get; set; } = 1;
    }

    public sealed class WarEnd
    {
        public double BaseMonthlyEndChance { get; set; } = 0.06;
        public double GrowthPerMonth { get; set; } = 0.02;
        public double MaxMonthlyEndChance { get; set; } = 0.60;
        public int MinWarMonths { get; set; } = 2;
    }

    public sealed class WarDraft
    {
        public int MinAge { get; set; } = 18;
        public int MaxAge { get; set; } = 45;
        public double ChanceIfAtWar { get; set; } = 0.35;
        public List<string>? DeferEducation { get; set; }
        public List<string>? ExemptFlags { get; set; }
    }

    public sealed class CityStatusStep
    {
        public double WarProgress { get; set; }
        public string Status { get; set; } = "safe";
    }

    public sealed class Rivalry
    {
        public string A { get; set; } = string.Empty;
        public string B { get; set; } = string.Empty;
        public int Weight { get; set; }
        public string? Casus { get; set; }
    }

    public sealed class RivalryDoc
    {
        public List<Rivalry>? Rivalries { get; set; }
    }

    // -------------------------------------------------- licencias y propiedades

    public sealed class LicenseDoc
    {
        public List<LicenseDef>? Exams { get; set; }
    }

    public sealed class LicenseDef
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public long Cost { get; set; }
        public int AgeMin { get; set; }
        public int AgeMax { get; set; } = 120;
        public int PassChance { get; set; } = 60;

        /// <summary>Flag extra del Schema si la licencia la otorga (LICENSED_DRIVER...). null = nada.</summary>
        public string? Flag { get; set; }
        public RelRequires? Requires { get; set; }
    }

    public sealed class PropertyDoc
    {
        public List<PropertyDef>? List { get; set; }
    }

    public sealed class PropertyDef
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public string Type { get; set; } = "house";
        public long Price { get; set; }

        /// <summary>Costo anual de mantenimiento que se cobra en cada Age Up.</summary>
        public long Maintenance { get; set; }
        public int AgeMin { get; set; }
        public string? Note { get; set; }
    }

    // --------------------------------------------------------------------- mundo

    public sealed class WorldDoc
    {
        public List<WorldCountry>? Countries { get; set; }
    }

    public sealed class WorldCountry
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public List<WorldCity> Cities { get; set; } = new();
    }

    public sealed class WorldCity
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string? Note { get; set; }
    }

    public sealed class NamePool
    {
        public List<string> FemaleNames { get; set; } = new() { "Carla", "Laura", "Sofía" };
        public List<string> MaleNames { get; set; } = new() { "Carlos", "Luis", "Pedro" };
        public List<string> LastNames { get; set; } = new() { "Peralta", "Santana", "Ramírez" };
    }

    // -------------------------------------------------------------- personalidad

    /// <summary>personality_templates.json: pesos de rasgos para NPCs.</summary>
    public sealed class PersonalityTemplates
    {
        public NpcAssignment? NpcAssignment { get; set; }
    }

    public sealed class NpcAssignment
    {
        public Dictionary<string, int> BaseWeights { get; set; } = new();
        public Dictionary<string, Dictionary<string, int>> ByRelation { get; set; } = new();
        public Dictionary<string, Dictionary<string, int>> ByFlag { get; set; } = new();
        public Dictionary<string, Dictionary<string, int>> ByEducation { get; set; } = new();
        public int MinTraitsPerNpc { get; set; } = 1;
        public int MaxTraitsPerNpc { get; set; } = 3;
    }
}
