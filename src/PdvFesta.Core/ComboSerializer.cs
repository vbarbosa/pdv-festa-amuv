using System.Text.Json;

namespace PdvFesta.Core;

/// <summary>Serializa a composicao de combos para uma coluna TEXT do SQLite.</summary>
internal static class ComboSerializer
{
    public static string Serializar(List<ComboItem> comp) =>
        comp.Count == 0 ? "" : JsonSerializer.Serialize(comp);

    public static List<ComboItem> Desserializar(string json) =>
        string.IsNullOrWhiteSpace(json)
            ? new List<ComboItem>()
            : JsonSerializer.Deserialize<List<ComboItem>>(json) ?? new List<ComboItem>();
}
