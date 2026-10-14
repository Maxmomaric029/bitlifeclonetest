namespace BitLifeClone.Models;

/// <summary>
/// Estado completo de la partida (lo que se serializa a saves/*.json).
/// </summary>
public sealed class GameState
{
    public Person Person { get; set; } = new();

    /// <summary>Ano actual del juego (empieza en el ano de nacimiento).</summary>
    public int Year { get; set; }

    /// <summary>Fecha de guardado (para listar partidas).</summary>
    public DateTime SavedAt { get; set; }

    // ------------------------------------------------------------------ guerra

    /// <summary>
    /// Hay una guerra activa. El reloj pasa de anos a meses (1 mes por Age Up)
    /// y el pool de eventos cambia al que toque segun especialidad y cityStatus.
    /// </summary>
    public bool InWar { get; set; }

    /// <summary>CityStatus del Schema.md mientras dure la guerra.</summary>
    public string CityStatus { get; set; } = "safe";

    /// <summary>Pais enemigo durante la guerra (id ingles, igual que rivalries.json).</summary>
    public string? EnemyCountry { get; set; }

    /// <summary>Meses transcurridos desde la declaracion de guerra.</summary>
    public int WarMonths { get; set; }

    // ------------------------------------------------------------------ ayudas

    public Relation? FindRelation(string relationId) =>
        Person.Relations.FirstOrDefault(r => r.Id == relationId);

    public Relation? FindRelationByType(string type) =>
        Person.Relations.FirstOrDefault(r => r.Type == type && !r.IsDead);
}
