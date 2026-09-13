using System.Text.Json;
using System.Text.Json.Serialization;
using MRS.ProfileEngine.Models;

namespace MRS.ProfileEngine;

/// <summary>Implementación de <see cref="IProfileService"/> basada en archivos JSON.</summary>
public sealed class ProfileService : IProfileService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public ProfileLoadResult LoadFromDirectory(string directoryPath)
    {
        ArgumentNullException.ThrowIfNull(directoryPath);

        var profiles = new List<ProfileDefinition>();
        var errors = new List<ProfileValidationError>();

        if (!Directory.Exists(directoryPath))
        {
            errors.Add(new ProfileValidationError(
                directoryPath, null, ProfileValidationErrorCode.DirectoryNotFound,
                $"No existe el directorio de perfiles: {directoryPath}"));
            return new ProfileLoadResult(profiles, errors);
        }

        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in Directory.EnumerateFiles(directoryPath, "*.json").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            var fileName = Path.GetFileName(file);
            LoadFile(file, fileName, seenIds, profiles, errors);
        }

        return new ProfileLoadResult(profiles, errors);
    }

    private static void LoadFile(
        string filePath, string fileName,
        HashSet<string> seenIds, List<ProfileDefinition> profiles, List<ProfileValidationError> errors)
    {
        ProfileFileModel? raw;
        try
        {
            var json = File.ReadAllText(filePath);
            raw = JsonSerializer.Deserialize<ProfileFileModel>(json, JsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            errors.Add(new ProfileValidationError(
                fileName, null, ProfileValidationErrorCode.InvalidJson,
                $"JSON inválido en {fileName}: {ex.Message}"));
            return;
        }

        if (raw is null)
        {
            errors.Add(new ProfileValidationError(
                fileName, null, ProfileValidationErrorCode.InvalidJson,
                $"JSON vacío o nulo en {fileName}."));
            return;
        }

        if (string.IsNullOrWhiteSpace(raw.Id))
        {
            errors.Add(new ProfileValidationError(
                fileName, null, ProfileValidationErrorCode.MissingId,
                $"Falta 'id' en {fileName}."));
            return;
        }

        if (!seenIds.Add(raw.Id))
        {
            errors.Add(new ProfileValidationError(
                fileName, raw.Id, ProfileValidationErrorCode.DuplicateId,
                $"Id de perfil duplicado: '{raw.Id}' (visto en {fileName})."));
            return;
        }

        if (raw.Version <= 0)
        {
            errors.Add(new ProfileValidationError(
                fileName, raw.Id, ProfileValidationErrorCode.InvalidVersion,
                $"Version inválida ({raw.Version}) en {fileName}."));
            return;
        }

        profiles.Add(new ProfileDefinition
        {
            Id = raw.Id,
            Name = raw.Name ?? string.Empty,
            Description = raw.Description ?? string.Empty,
            Version = raw.Version,
            ComponentIds = (raw.ComponentIds ?? new List<string>()).ToList(),
            Metadata = raw.Metadata,
            // Un JSON sin "securityOptions" (perfil anterior a P13) usa el valor
            // seguro por defecto: nunca se interpreta la ausencia del campo como
            // "sin protección".
            SecurityOptions = raw.SecurityOptions ?? Models.SecurityOptions.Safe,
        });
    }

    public ProfileDefinition? GetProfile(ProfileLoadResult loadResult, string profileId)
    {
        ArgumentNullException.ThrowIfNull(loadResult);
        ArgumentNullException.ThrowIfNull(profileId);

        return loadResult.Profiles.FirstOrDefault(
            p => string.Equals(p.Id, profileId, StringComparison.OrdinalIgnoreCase));
    }

    public ProfileSelectionResult GetSelection(
        ProfileDefinition profile,
        IReadOnlyCollection<string>? knownComponentIds = null,
        IReadOnlyCollection<string>? protectedComponentIds = null)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var known = knownComponentIds is null ? null : new HashSet<string>(knownComponentIds, StringComparer.Ordinal);
        var blocked = protectedComponentIds is null ? null : new HashSet<string>(protectedComponentIds, StringComparer.Ordinal);

        var selected = new List<string>();
        var unknown = new List<string>();
        var blockedIds = new List<string>();

        foreach (var id in profile.ComponentIds)
        {
            // Nunca se selecciona (ni se "arregla") un ComponentId que no exista en el
            // catálogo real: se reporta como desconocido y se descarta silenciosamente
            // de la selección, no de la lista original del perfil.
            if (known is not null && !known.Contains(id))
            {
                unknown.Add(id);
                continue;
            }

            if (blocked is not null && blocked.Contains(id))
                blockedIds.Add(id);

            selected.Add(id);
        }

        return new ProfileSelectionResult(profile.Id, selected, unknown, blockedIds);
    }

    private sealed class ProfileFileModel
    {
        public string Id { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? Description { get; set; }
        public int Version { get; set; } = 1;
        public List<string>? ComponentIds { get; set; }
        public Dictionary<string, string>? Metadata { get; set; }
        public Models.SecurityOptions? SecurityOptions { get; set; }
    }
}
