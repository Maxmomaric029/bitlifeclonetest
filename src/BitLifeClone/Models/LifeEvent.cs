namespace BitLifeClone.Models;

/// <summary>
/// Una entrada del feed: "Fui al gym, +2 Fitness".
/// Text es la linea principal, Detail el detalle (texto del evento/subevento)
/// y Deltas los cambios de stats para pintarlos en verde/rojo.
/// </summary>
public sealed class LifeEvent
{
    /// <summary>Edad que tenia el jugador cuando paso.</summary>
    public int Age { get; set; }

    public string Text { get; set; } = string.Empty;

    /// <summary>(Opcional) Segunda linea con mas detalle del outcome.</summary>
    public string? Detail { get; set; }

    /// <summary>Deltas de stats aplicados (claves del Schema.md).</summary>
    public Dictionary<string, int> Deltas { get; set; } = new();

    /// <summary>Dinero movido por este evento (+/-).</summary>
    public int MoneyDelta { get; set; }

    /// <summary>Linea resumen de deltas para la UI ("+4 Felicidad, -3 Salud").</summary>
    public string DeltasLabel
    {
        get
        {
            var parts = new List<string>();
            foreach (var (key, delta) in Deltas)
            {
                if (delta == 0) continue;
                var name = key switch
                {
                    "happiness" => "Felicidad",
                    "health" => "Salud",
                    "smarts" => "Inteligencia",
                    "looks" => "Apariencia",
                    "fitness" => "Fitness",
                    "fame" => "Fama",
                    "karma" => "Karma",
                    "stress" => "Estrés",
                    "grades" => "Notas",
                    "attendance" => "Asistencia",
                    "discipline" => "Disciplina",
                    "popularity" => "Popularidad",
                    _ => key
                };
                parts.Add($"{(delta > 0 ? "+" : "")}{delta} {name}");
            }
            if (MoneyDelta != 0)
                parts.Add($"{(MoneyDelta > 0 ? "+" : "")}${MoneyDelta}");
            return string.Join(", ", parts);
        }
    }

    public string AgeLabel => $"{Age} años";
}
