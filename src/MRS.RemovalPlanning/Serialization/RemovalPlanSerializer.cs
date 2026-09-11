using System.Text.Json;
using System.Text.Json.Serialization;
using MRS.RemovalPlanning.Models;

namespace MRS.RemovalPlanning.Serialization;

/// <summary>Guarda/carga un <see cref="RemovalPlan"/> como JSON (p. ej. <c>output/removal-plan.json</c>).</summary>
public static class RemovalPlanSerializer
{
    public const int CurrentFormatVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Documento persistido: versión de formato + el plan (con fecha, imagen, componentes, acciones, avisos y bloqueos).</summary>
    public sealed record RemovalPlanDocument(int FormatVersion, RemovalPlan Plan);

    public static string ToJson(RemovalPlan plan)
        => JsonSerializer.Serialize(new RemovalPlanDocument(CurrentFormatVersion, plan), Options);

    public static RemovalPlan FromJson(string json)
    {
        var document = JsonSerializer.Deserialize<RemovalPlanDocument>(json, Options)
            ?? throw new InvalidOperationException("El JSON no contiene un plan de eliminación válido.");
        return document.Plan;
    }

    public static void SaveToFile(RemovalPlan plan, string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(filePath, ToJson(plan));
    }

    public static RemovalPlan LoadFromFile(string filePath)
        => FromJson(File.ReadAllText(filePath));
}
