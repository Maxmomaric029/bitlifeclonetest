namespace BitLifeClone.Models;

public enum Gender
{
    Female,
    Male
}

/// <summary>
/// El personaje. Las stats van en diccionario con las claves del Schema.md
/// (happiness, health, smarts, looks, fitness, fame, karma, stress, grades...),
/// para que un outcome JSON pueda tocar cualquiera con la clave igual al JSON.
/// karma va de -100 a 100; el resto 0..100.
/// </summary>
public sealed class Person
{
    public string Name { get; set; } = string.Empty;

    public int Age { get; set; }

    /// <summary>Ano en que nacio (para el ano del juego).</summary>
    public int BirthYear { get; set; }

    public Gender Gender { get; set; } = Gender.Male;

    public string Country { get; set; } = "República Dominicana";

    /// <summary>Dinero en USD (los outcomes y sueldos se convierten al cargar).</summary>
    public long Money { get; set; }

    public EducationLevel Education { get; set; } = EducationLevel.None;

    /// <summary>Id del trabajo actual (null = desempleado).</summary>
    public string? JobId { get; set; }

    /// <summary>Anos seguidos en el trabajo actual (experiencia).</summary>
    public int JobYears { get; set; }

    public List<string> Flags { get; set; } = new();

    public Dictionary<string, int> Stats { get; set; } = NewbornStats();

    public List<Relation> Relations { get; set; } = new();

    /// <summary>Historial/feed en orden cronologico (mas viejo primero).</summary>
    public List<LifeEvent> History { get; set; } = new();

    /// <summary>Ids de licencias obtenidas (los de Data/Licenses/licenses.json).</summary>
    public List<string> Licenses { get; set; } = new();

    /// <summary>Ids de propiedades en poder del jugador (los de Data/Properties/).</summary>
    public List<string> Properties { get; set; } = new();

    /// <summary>Anos cursados de universidad hasta el titulo (meta en el Age Up).</summary>
    public int UniversityYears { get; set; }

    /// <summary>Anos cursados de posgrado/especialidad despues de University.</summary>
    public int GradSchoolYears { get; set; }

    public bool IsDead => Flags.Contains("DEAD");

    public bool IsEmployed => JobId is not null && !Flags.Contains("UNEMPLOYED");

    // ------------------------------------------------------------------ stats

    public static Dictionary<string, int> NewbornStats() => new()
    {
        ["happiness"] = 70,
        ["health"] = 90,
        ["smarts"] = 50,
        ["looks"] = 50,
        ["fitness"] = 50,
        ["fame"] = 0,
        ["karma"] = 0,
        ["stress"] = 0,
        ["grades"] = 70,
        ["attendance"] = 100,
        ["discipline"] = 70,
        ["popularity"] = 50
    };

    public int Stat(string key) =>
        Stats.TryGetValue(key, out var v) ? v : 0;

    public void AddStat(string key, int delta)
    {
        var v = Stat(key) + delta;
        Stats[key] = key == "karma" ? Math.Clamp(v, -100, 100) : Math.Clamp(v, 0, 100);
    }

    public bool HasFlag(string flag) => Flags.Contains(flag);

    public void AddFlag(string flag)
    {
        if (!Flags.Contains(flag))
            Flags.Add(flag);
    }

    public void RemoveFlag(string flag) => Flags.Remove(flag);

    public bool TrySpend(long amount)
    {
        if (Money < amount)
            return false;
        Money -= amount;
        return true;
    }

    /// <summary>Avatar emoji por edad y genero (aun no hay assets propios).</summary>
    public string Avatar
    {
        get
        {
            if (Age < 3) return "👶";
            if (Age < 13) return Gender == Gender.Female ? "👧" : "👦";
            if (Age < 20) return Gender == Gender.Female ? "👩‍🦰" : "👨‍🦰";
            if (Age < 60) return Gender == Gender.Female ? "👩" : "👨";
            if (IsDead) return "🪦";
            return Gender == Gender.Female ? "👵" : "👴";
        }
    }
}
