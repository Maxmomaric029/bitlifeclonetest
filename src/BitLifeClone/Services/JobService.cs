using System.Text.Json;
using System.Text.Json.Serialization;
using BitLifeClone.Models;

namespace BitLifeClone.Services;

/// <summary>
/// Carga TODOS los *.json de Data/Jobs/ (Jobs.fixed.json, agencies, part_time, rd_jobs,
/// military_extra, etc.): trabajos con sueldos por formula, empresas y ascensos.
/// Acepta dos formas de archivo:
///   1) { "meta": {...}, "categories": [ { id, name, jobs: [...] }, ... ] }   (Jobs.fixed.json)
///   2) { "category": "id", "name": "...", "jobs": [...] }                    (el resto)
/// y un archivo de empresas { "companies": [ { id, name } ] } (Companies.json).
/// </summary>
public sealed class JobService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly Dictionary<string, Job> _byId = new(StringComparer.Ordinal);

    public EconomyConfig Economy { get; private set; } = new();
    public List<Company> Companies { get; } = new();
    public List<JobCategory> Categories { get; } = new();

    /// <param name="jobsDir">Carpeta con los JSON de trabajos. Por defecto Data/Jobs junto al exe.</param>
    public JobService(string? jobsDir = null)
    {
        jobsDir ??= Path.Combine(AppContext.BaseDirectory, "Data", "Jobs");

        if (!Directory.Exists(jobsDir))
            throw new DirectoryNotFoundException($"No se encontro la carpeta de trabajos: {jobsDir}");

        var files = Directory.GetFiles(jobsDir, "*.json", SearchOption.TopDirectoryOnly)
                             .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                             .ToList();

        if (files.Count == 0)
            throw new FileNotFoundException("La carpeta de trabajos no tiene ningun .json.", jobsDir);

        foreach (var file in files)
            LoadFile(file);
    }

    public IEnumerable<Job> AllJobs => Categories.SelectMany(c => c.Jobs);

    public JobCategory? FindCategory(string categoryId) =>
        Categories.FirstOrDefault(c => c.Id == categoryId);

    public Job? FindJob(string jobId) =>
        _byId.TryGetValue(jobId, out var job) ? job : null;

    /// <summary>Siguiente puesto de la cadena de ascenso, o null si es el tope.</summary>
    public Job? NextPromotion(Job job) =>
        job.PromotesTo is null ? null : FindJob(job.PromotesTo);

    /// <summary>
    /// Sueldo en USD: BaseSalary convertido a la moneda base y multiplicado
    /// por el factor del tier (trabajos sueltos = x1). null si no tiene datos.
    /// OJO: no se sabe si los sueldos DOP son mensuales o anuales (ver PENDING.md).
    /// </summary>
    public int? SalaryOf(Job job)
    {
        if (job.BaseSalary is null)
            return null;

        double baseUsd = job.BaseSalary.Value * RateToUsd(job.Currency);
        return (int)Math.Round(baseUsd * TierMultiplier(job.Tier));
    }

    /// <summary>Cuantos USD vale 1 unidad de la moneda dada (USD = 1).</summary>
    public double RateToUsd(string? currency) => currency?.ToUpperInvariant() switch
    {
        "EUR" => Economy.EurToUsd,
        "DOP" => Economy.DopToUsd,
        _ => 1.0
    };

    /// <summary>
    /// 0.75 / 1.0 / 1.3 / 1.7 para los 4 primeros niveles; tras la 4 la serie
    /// sigue creciendo x1.3 por nivel (escaleras largas: militar, oficiales...).
    /// </summary>
    public double TierMultiplier(int? tier)
    {
        var m = Economy.TierMultipliers;
        if (tier is null || m.Length == 0 || tier.Value < 1)
            return 1.0;
        if (tier.Value <= m.Length)
            return m[tier.Value - 1];
        return m[^1] * Math.Pow(Economy.TierGrowthAfterFour, tier.Value - m.Length);
    }

    /// <summary>Trabajos a los que se puede optar con el nivel de estudio dado (y la edad, si se pasa).</summary>
    public IEnumerable<Job> JobsRequiringAtMost(EducationLevel level, int? age = null) =>
        AllJobs.Where(j => j.Education is not null
                           && j.Education.Value <= level
                           && (age is null || j.MinAge is null || age.Value >= j.MinAge.Value));

    // ------------------------------------------------------------------ carga

    private void LoadFile(string path)
    {
        JobsFile? doc;
        try
        {
            doc = JsonSerializer.Deserialize<JobsFile>(File.ReadAllText(path), Options);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"JSON invalido en {Path.GetFileName(path)}: {ex.Message}", ex);
        }

        if (doc is null)
            throw new InvalidDataException($"Archivo de trabajos vacio: {Path.GetFileName(path)}");

        if (doc.Meta is not null)
            Economy = doc.Meta;

        if (doc.Companies is not null)
            foreach (var c in doc.Companies)
                if (Companies.All(x => x.Id != c.Id))
                    Companies.Add(c);

        if (doc.Categories is not null)
            foreach (var cat in doc.Categories)
                Merge(cat);

        if (!string.IsNullOrEmpty(doc.Category) && doc.Jobs is not null)
        {
            Merge(new JobCategory
            {
                Id = doc.Category,
                Name = doc.Name ?? doc.Category,
                Note = doc.Note,
                Jobs = doc.Jobs
            });
        }
    }

    private void Merge(JobCategory incoming)
    {
        var target = FindCategory(incoming.Id);
        if (target is null)
        {
            target = new JobCategory { Id = incoming.Id, Name = incoming.Name, Note = incoming.Note };
            Categories.Add(target);
        }

        foreach (var job in incoming.Jobs)
        {
            if (string.IsNullOrEmpty(job.Id))
                continue;
            if (!_byId.TryAdd(job.Id, job))
                continue; // id repetido: el validador ya lo caza; aqui gana el primero

            job.Category ??= incoming.Id;
            target.Jobs.Add(job);
        }
    }

    private sealed class JobsFile
    {
        public EconomyConfig? Meta { get; set; }
        public List<Company>? Companies { get; set; }
        public List<JobCategory>? Categories { get; set; }

        // forma "un archivo = una categoria"
        public string? Category { get; set; }
        public string? Name { get; set; }
        public string? Note { get; set; }
        public List<Job>? Jobs { get; set; }
    }
}
