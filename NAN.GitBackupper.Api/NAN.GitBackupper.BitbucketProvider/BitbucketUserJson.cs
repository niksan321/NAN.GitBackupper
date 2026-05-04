using System.Text.Json.Serialization;

namespace NAN.Git;

public sealed partial class BitbucketProviderClient
{
    private sealed record BitbucketUserJson([property: JsonPropertyName("uuid")] string Uuid);
}
