namespace BitLifeClone.Models;

/// <summary>
/// Una persona del circulo del jugador (madre, hermano, jefe, profe...).
/// Type usa los RelationTypes del Schema.md (mother, father, sibling, friend...).
/// Bar es el nivel de relacion 0..100 (estilo BitLife).
/// </summary>
public sealed class Relation
{
    /// <summary>Id unico de la relacion (no confundir con ids de JSON).</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    /// <summary>RelationType del Schema.md (mother, friend, boss...).</summary>
    public string Type { get; set; } = "friend";

    public int Age { get; set; }

    public Gender Gender { get; set; } = Gender.Female;

    /// <summary>Nivel de relacion 0..100.</summary>
    public int Bar { get; set; } = 50;

    public List<string> Flags { get; set; } = new();

    public List<string> Traits { get; set; } = new();

    public bool IsDead => Flags.Contains("DEAD");

    /// <summary>Mustra como (mama, papa, hermano...) para el feed y los titulos.</summary>
    public string KindLabel => Type switch
    {
        "mother" => "Mamá",
        "father" => "Papá",
        "sibling" => "Hermano/a",
        "spouse" => "Esposo/a",
        "partner" => "Pareja",
        "child" => "Hijo/a",
        "friend" => "Amigo/a",
        "enemy" => "Enemigo/a",
        "enemy_ex" => "Enemigo/ex",
        "ex" => "Ex",
        "grandparent" => "Abuelo/a",
        "uncle_aunt" => "Tío/a",
        "cousin" => "Primo/a",
        "teacher" => "Profesor/a",
        "classmate" => "Compañero de clase",
        "boss" => "Jefe",
        "coworker" => "Compañero de trabajo",
        _ => Type
    };

    public string Avatar
    {
        get
        {
            if (IsDead) return "🪦";
            if (Age < 3) return "👶";
            if (Age < 13) return Gender == Gender.Female ? "👧" : "👦";
            if (Age < 20) return Gender == Gender.Female ? "👩‍🦰" : "👨‍🦰";
            if (Age < 60) return Gender == Gender.Female ? "👩" : "👨";
            return Gender == Gender.Female ? "👵" : "👴";
        }
    }
}
