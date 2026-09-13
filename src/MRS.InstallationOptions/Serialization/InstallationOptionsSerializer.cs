using System.Text.Json;
using MRS.InstallationOptions.Models;

namespace MRS.InstallationOptions.Serialization;

/// <summary>
/// Serializa/deserializa <see cref="InstallationOptions"/> como JSON. Un JSON
/// vacío, parcial o de un formato anterior nunca produce un estado inválido:
/// cualquier campo ausente conserva su valor seguro por defecto (todos
/// habilitados), igual que <c>ProfileService</c> con <c>SecurityOptions</c> (P13).
/// </summary>
public static class InstallationOptionsSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public static string ToJson(Models.InstallationOptions options)
        => JsonSerializer.Serialize(options, Options);

    /// <summary>
    /// Deserializa JSON en <see cref="InstallationOptions"/>. Un JSON vacío (<c>"{}"</c>)
    /// o <c>null</c>/vacío como cadena devuelve <see cref="InstallationOptions.Default"/>;
    /// un JSON malformado lanza <see cref="JsonException"/> (el llamador decide si
    /// usar el valor por defecto o propagar el error).
    /// </summary>
    public static Models.InstallationOptions FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Models.InstallationOptions.Default;

        return JsonSerializer.Deserialize<Models.InstallationOptions>(json, Options)
               ?? Models.InstallationOptions.Default;
    }

    /// <summary>Variante que nunca lanza: cualquier JSON inválido se resuelve como <see cref="InstallationOptions.Default"/>.</summary>
    public static Models.InstallationOptions FromJsonOrDefault(string? json)
    {
        try
        {
            return FromJson(json);
        }
        catch (JsonException)
        {
            return Models.InstallationOptions.Default;
        }
    }
}
