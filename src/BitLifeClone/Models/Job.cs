namespace BitLifeClone.Models;

/// <summary>
/// Nivel de estudio. El ORDEN es el de Data/Schema.md (## Education) y la
/// comparacion numerica (<=) depende de el: no lo reordenes sin tocar el Schema.
/// </summary>
public enum EducationLevel
{
    None,
    PrimarySchool,
    LowerSecondary,
    UpperSecondary,
    HighSchool,
    CommunityCollege,
    University,
    GraduateSchool,
    MedicalSchool,
    LawSchool,
    VeterinarySchool,
    PharmacySchool,
    NursingSchool,
    BusinessSchool
}

/// <summary>Categoria de trabajos (Corporate, Military, Part-time...).</summary>
public sealed class JobCategory
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Note { get; set; }
    public List<Job> Jobs { get; set; } = new();
}

/// <summary>Un trabajo. El sueldo NO es fijo: se calcula con
/// BaseSalary x Moneda x Multiplier(Tier) (ver JobService.SalaryOf).</summary>
public sealed class Job
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    /// <summary>Id del trabajo al que asciende; null = tope de la cadena.</summary>
    public string? PromotesTo { get; set; }

    /// <summary>Posicion en la cadena (1 = entrada); null = trabajo suelto.</summary>
    public int? Tier { get; set; }

    /// <summary>Salario base de TODA la cadena (mismo valor en cada rung).</summary>
    public int? BaseSalary { get; set; }

    /// <summary>Moneda del baseSalary ("USD"/"EUR"/"DOP"); se convierte al calcular.</summary>
    public string? Currency { get; set; }

    /// <summary>null = dato desconocido (wiki incompleta).</summary>
    public EducationLevel? Education { get; set; }

    /// <summary>Anios de experiencia laboral exigidos (ademas de estudio).</summary>
    public int? ExperienceYears { get; set; }

    /// <summary>null = dato desconocido en la fuente.</summary>
    public bool? CanHoldWithCriminalRecord { get; set; }

    /// <summary>Id de la categoria a la que pertenece (se rellena al cargar si falta).</summary>
    public string? Category { get; set; }

    public string? Icon { get; set; }

    /// <summary>Edad minima para el trabajo (part-time, RD, escolares). null = sin minimo propio.</summary>
    public int? MinAge { get; set; }

    /// <summary>Solo militares: army / navy / marines / air_force.</summary>
    public string? Branch { get; set; }

    /// <summary>Solo militares: define el pool de guerra (ver Schema.md).</summary>
    public string? Specialty { get; set; }

    /// <summary>Horas del presupuesto semanal que consume (trabajos escolares).</summary>
    public int? HoursCost { get; set; }

    /// <summary>Regla especial en texto libre; la interpreta el motor de eventos.</summary>
    public string? SpecialLogic { get; set; }

    public string? Note { get; set; }
}

/// <summary>Negocio donde se aplica (Bank, Grocery Store...): no es un oficio.</summary>
public sealed class Company
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Note { get; set; }
}

/// <summary>meta de la economia: moneda base, tasas y reglas de la formula de sueldos.</summary>
public sealed class EconomyConfig
{
    public string Currency { get; set; } = "USD";
    public double EurToUsd { get; set; } = 1.10;

    /// <summary>1 DOP en USD (aprox. 60 DOP por dolar). Ajustable desde el JSON meta.</summary>
    public double DopToUsd { get; set; } = 1.0 / 60.0;

    public double[] TierMultipliers { get; set; } = { 0.75, 1.0, 1.3, 1.7 };
    public double TierGrowthAfterFour { get; set; } = 1.3;
}
