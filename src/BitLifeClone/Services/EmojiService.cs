using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BitLifeClone.Services;

/// <summary>
/// Los PNG de JoyPixels 5.0 viven como Resource del assembly (Assets/Images/emoji/joypixels-5/)
/// y index.json mapea codepoints -> archivo. Este servicio extrae el emoji inicial de un texto
/// ("📊 Se graduó..."), busca su PNG y lo devuelve listo para WPF.
/// Si el emoji no tiene asset, devuelve null y el texto intacto (caen al font del sistema).
/// </summary>
public static class EmojiService
{
    private const string BaseUri = "pack://application:,,,/Assets/Images/emoji/joypixels-5/";

    private static Dictionary<string, string>? _codepointsToFile;
    private static readonly ConcurrentDictionary<string, ImageSource?> Cache = new();

    private sealed record EmojiAsset(string Codepoints, string File);

    /// <summary>Extrae el primer emoji del texto. Cleaned = el texto sin ese emoji (si hay asset).</summary>
    public static (ImageSource? Image, string Cleaned) FirstEmoji(string? text)
    {
        var t = text ?? string.Empty;
        var codepoints = ParseLeadingEmoji(t, out var consumed);
        if (codepoints is null)
            return (null, t);

        var image = ResolveImage(codepoints);
        if (image is null)
            return (null, t); // sin asset: dejamos el texto tal cual (font del sistema)

        return (image, t[consumed..].TrimStart());
    }

    /// <summary>
    /// Lee el token emoji inicial de la cadena. Soporta surrogate pairs, selectors (FE0F/FE0E),
    /// tonos de piel (1F3FB-1F3FF), ZWJ (1F469-200D-1F4BB) y banderas (dos regional indicators).
    /// La salida usa el mismo formato que index.json: hex separado por guion ("1f947", "1f9d1-1f3fb").
    /// </summary>
    private static string? ParseLeadingEmoji(string text, out int consumed)
    {
        consumed = 0;
        var codes = new List<int>();
        var i = 0;
        var pendingZwj = false;

        while (i < text.Length)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 >= text.Length)
                return codes.Count == 0 ? null : Join(codes);

            var point = char.ConvertToUtf32(text, i);
            var size = char.IsSurrogatePair(text, i) ? 2 : 1;

            // el token termina en espacio (los textos del juego ponen el emoji antes de " ")
            if (point is 0x20 or 0x09 or 0x0A or 0x0D)
            {
                if (codes.Count == 0 || pendingZwj) return codes.Count == 0 ? null : Join(codes);
                break;
            }

            var isSelector = point is 0xFE0F or 0xFE0E;
            var isSkin = point is >= 0x1F3FB and <= 0x1F3FF;
            var isZwj = point == 0x200D;
            var prevIsRi = codes.Count > 0 && codes[^1] is >= 0x1F1E6 and <= 0x1F1FF;
            var isRi = point is >= 0x1F1E6 and <= 0x1F1FF;

            if (codes.Count == 0 && (isSelector || isSkin || isZwj))
                return null; // un emoji nunca empieza con estos

            var continues = isSelector || isSkin || isZwj || pendingZwj || (prevIsRi && isRi);
            if (!continues)
                break;

            codes.Add(point);
            i += size;
            pendingZwj = isZwj;
        }

        consumed = i;
        return codes.Count == 0 ? null : Join(codes);
    }

    private static string Join(List<int> codes)
    {
        var sb = new StringBuilder();
        for (var k = 0; k < codes.Count; k++)
        {
            if (k > 0) sb.Append('-');
            sb.Append(codes[k].ToString("x"));
        }
        return sb.ToString();
    }

    private static ImageSource? ResolveImage(string codepoints)
    {
        EnsureIndex();
        var map = _codepointsToFile;
        if (map is null) return null;

        // JoyPixels indexa varias variantes (⚖️, ✈️, 🏎️...) por codepoint base sin FE0F/FE0E
        if (!map.TryGetValue(codepoints, out var file))
        {
            var alt = string.Join("-", codepoints.Split('-')
                .Where(part => part != "fe0f" && part != "fe0e"));
            if (alt == codepoints || alt.Length == 0 || !map.TryGetValue(alt, out file))
                return null;
        }

        return Cache.GetOrAdd(codepoints, _ =>
        {
            var bmp = new BitmapImage(new Uri(BaseUri + file));
            bmp.Freeze(); // thread-safe
            return (ImageSource?)bmp;
        });
    }

    private static void EnsureIndex()
    {
        if (_codepointsToFile is not null) return;

        var uri = new Uri(BaseUri + "index.json");
        var resource = System.Windows.Application.GetResourceStream(uri)
            ?? throw new FileNotFoundException("index.json de emojis no encontrado", uri.ToString());

        using var reader = new StreamReader(resource.Stream);
        var map = JsonSerializer.Deserialize<Dictionary<string, EmojiAsset>>(reader.ReadToEnd());

        var byCodepoints = new Dictionary<string, string>(map?.Count ?? 0);
        if (map is not null)
        {
            foreach (var entry in map)
            {
                var asset = entry.Value;
                if (string.IsNullOrEmpty(asset?.Codepoints) || string.IsNullOrEmpty(asset.File))
                    continue;
                byCodepoints[asset.Codepoints.Trim().ToLowerInvariant()] = asset.File;
            }
        }
        _codepointsToFile = byCodepoints;
    }
}
