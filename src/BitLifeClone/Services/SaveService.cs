using System.Text.Json;
using System.Text.Json.Serialization;
using BitLifeClone.Models;

namespace BitLifeClone.Services;

/// <summary>
/// Guardar/cargar partida con System.Text.Json. Los archivos viven en saves/
/// (carpeta del repo segun README; si no se encuentra, junto al exe).
/// </summary>
public sealed class SaveService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string SavesDir { get; }

    public SaveService()
    {
        SavesDir = ResolveSavesDir();
        Directory.CreateDirectory(SavesDir);
    }

    /// <summary>saves/ del repo (sube hasta encontrarlo) o saves/ junto al exe.</summary>
    private static string ResolveSavesDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir.FullName, "saves");
            if (Directory.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        return Path.Combine(AppContext.BaseDirectory, "saves");
    }

    /// <summary>Guarda la partida con el nombre del personaje (+ marca de tiempo si repite).</summary>
    public string Save(GameState game)
    {
        game.SavedAt = DateTime.Now;

        var baseName = SafeFileName(string.IsNullOrWhiteSpace(game.Person.Name)
            ? "partida"
            : game.Person.Name);

        var path = Path.Combine(SavesDir, $"{baseName}.json");
        var n = 1;
        while (File.Exists(path) && File.GetLastWriteTime(path) < DateTime.Now.AddSeconds(-30))
        {
            path = Path.Combine(SavesDir, $"{baseName}_{n}.json");
            n++;
        }

        File.WriteAllText(path, JsonSerializer.Serialize(game, Options));
        return path;
    }

    public GameState? Load(string fileName)
    {
        var path = Path.Combine(SavesDir, fileName);
        if (!File.Exists(path))
            return null;

        try
        {
            return JsonSerializer.Deserialize<GameState>(File.ReadAllText(path), Options);
        }
        catch (JsonException)
        {
            return null; // partida corrupta: no rompe el juego
        }
    }

    /// <summary>(archivo, etiqueta) de cada save, del mas nuevo al mas viejo.</summary>
    public List<(string FileName, string Label)> ListSaves()
    {
        var list = new List<(string, string)>();
        foreach (var file in Directory.GetFiles(SavesDir, "*.json"))
        {
            try
            {
                var header = JsonSerializer.Deserialize<GameState>(File.ReadAllText(file), Options);
                var person = header?.Person;
                if (person is null) continue;

                var stamp = File.GetLastWriteTime(file).ToString("dd/MM HH:mm");
                list.Add((Path.GetFileName(file),
                    $"{person.Name} · {person.Age} años · {stamp}"));
            }
            catch (JsonException)
            {
                // save corrupto: se salta
            }
        }
        return list.OrderByDescending(x => x.Label).ToList();
    }

    private static string SafeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Replace(' ', '_');
    }
}
