using Mapster;

namespace NAN.GitBackupper.Api.Mapping;

/// <summary>Central place for Mapster <see cref="TypeAdapterConfig"/> rules.</summary>
public static class MappingConfig
{
    public static void Register(TypeAdapterConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
    }
}
