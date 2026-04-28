using NAN.Git.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NAN.Git;

public sealed class BitbucketProviderClient : IGitProviderClient
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>HTTPS Git username when authenticating with an Atlassian API token (see Bitbucket docs).</summary>
    private const string BitbucketApiTokenGitUser = "x-bitbucket-api-token-auth";

    public Task<RequestReplay> ValidateAsync(IGitBackupTarget target, CancellationToken ct = default) =>
        ValidateInternalAsync(target, ct);

    private static async Task<RequestReplay> ValidateInternalAsync(IGitBackupTarget target,
        CancellationToken ct)
    {
        try
        {
            using var client = CreateClient(target);
            var workspaceSlugs = await ListWorkspaceSlugsAsync(client, ct);
            if (workspaceSlugs.Count == 0)
                return new RequestReplay { IsSended = true, IsSuccess = true, Message = "OK" };

            var firstSlug = workspaceSlugs.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).First();
            var repoListUrl =
                $"https://api.bitbucket.org/2.0/repositories/{Uri.EscapeDataString(firstSlug)}?role=member&pagelen=1";
            using var response = await client.GetAsync(repoListUrl, ct);
            if (response.IsSuccessStatusCode)
                return new RequestReplay { IsSended = true, IsSuccess = true, Message = response.ReasonPhrase };

            var err = await response.Content.ReadAsStringAsync(ct);
            return new RequestReplay
            {
                IsSended = true,
                IsSuccess = false,
                Message = $"{(int)response.StatusCode}: {err}",
            };
        }
        catch (Exception ex)
        {
            return RequestReplay.Error(ex);
        }
    }

    public async Task<IReadOnlyList<GitRepositoryDescriptor>> ListRepositoriesAsync(IGitBackupTarget target,
        CancellationToken ct = default)
    {
        using var client = CreateClient(target);
        string userUuid = null;
        using (var userResponse = await client.GetAsync("https://api.bitbucket.org/2.0/user", ct))
        {
            userResponse.EnsureSuccessStatusCode();
            await using var userStream = await userResponse.Content.ReadAsStreamAsync(ct);
            var user = await JsonSerializer.DeserializeAsync<BitbucketUserJson>(userStream, JsonOptions, ct);
            userUuid = user?.Uuid;
        }

        // Global GET /2.0/repositories is deprecated (410 Gone). Use workspace-scoped listing.
        var workspaceSlugs = await ListWorkspaceSlugsAsync(client, ct);
        List<GitRepositoryDescriptor> list = [];
        var seenFullNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var slug in workspaceSlugs.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
        {
            var nextUrl =
                $"https://api.bitbucket.org/2.0/repositories/{Uri.EscapeDataString(slug)}?role=member&pagelen=100";
            while (!string.IsNullOrEmpty(nextUrl))
            {
                using var response = await client.GetAsync(nextUrl, ct);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("values", out var values))
                    break;

                foreach (var el in values.EnumerateArray())
                    TryAddRepositoryDescriptor(el, userUuid, seenFullNames, list);

                nextUrl = null;
                if (doc.RootElement.TryGetProperty("next", out var nextEl))
                    nextUrl = nextEl.GetString();
            }
        }

        return list;
    }

    private static async Task<IReadOnlyList<string>> ListWorkspaceSlugsAsync(HttpClient client, CancellationToken ct)
    {
        var slugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var nextUrl = "https://api.bitbucket.org/2.0/user/workspaces?pagelen=100";
        while (!string.IsNullOrEmpty(nextUrl))
        {
            using var response = await client.GetAsync(nextUrl, ct);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("values", out var values))
            {
                foreach (var el in values.EnumerateArray())
                {
                    if (el.TryGetProperty("workspace", out var ws) &&
                        ws.TryGetProperty("slug", out var slugEl))
                    {
                        var s = slugEl.GetString();
                        if (!string.IsNullOrEmpty(s))
                            slugs.Add(s);
                    }
                }
            }

            nextUrl = null;
            if (doc.RootElement.TryGetProperty("next", out var nextEl))
                nextUrl = nextEl.GetString();
        }

        return slugs.ToList();
    }

    private static void TryAddRepositoryDescriptor(
        JsonElement el,
        string userUuid,
        HashSet<string> seenFullNames,
        List<GitRepositoryDescriptor> list)
    {
        if (!el.TryGetProperty("full_name", out var fullNameEl))
            return;
        var fullName = fullNameEl.GetString();
        if (string.IsNullOrEmpty(fullName) || !seenFullNames.Add(fullName))
            return;

        string ownerUuid = null;
        if (el.TryGetProperty("owner", out var ownerEl) &&
            ownerEl.TryGetProperty("uuid", out var ownerUuidEl))
        {
            ownerUuid = ownerUuidEl.GetString();
        }

        var owned = !string.IsNullOrEmpty(userUuid) && !string.IsNullOrEmpty(ownerUuid) &&
                    string.Equals(ownerUuid, userUuid, StringComparison.OrdinalIgnoreCase);

        string cloneHttps = null;
        if (el.TryGetProperty("links", out var links) &&
            links.TryGetProperty("clone", out var clones))
        {
            foreach (var c in clones.EnumerateArray())
            {
                if (c.TryGetProperty("name", out var n) && n.GetString() == "https" &&
                    c.TryGetProperty("href", out var h))
                {
                    cloneHttps = h.GetString();
                    break;
                }
            }
        }

        if (string.IsNullOrEmpty(cloneHttps))
            return;

        string defaultBranch = null;
        if (el.TryGetProperty("mainbranch", out var mainBranch) &&
            mainBranch.TryGetProperty("name", out var mainName))
        {
            defaultBranch = mainName.GetString();
        }

        list.Add(new GitRepositoryDescriptor(fullName, cloneHttps, defaultBranch, owned));
    }

    public async Task<IReadOnlyList<string>> ListBranchesAsync(IGitBackupTarget target, GitRepositoryDescriptor repository,
        CancellationToken ct = default)
    {
        var slash = repository.DisplayKey.IndexOf('/');
        if (slash <= 0 || slash >= repository.DisplayKey.Length - 1)
            return [];

        var workspace = repository.DisplayKey[..slash];
        var repoSlug = repository.DisplayKey[(slash + 1)..];
        using var client = CreateClient(target);
        List<string> names = [];
        var nextUrl =
            $"https://api.bitbucket.org/2.0/repositories/{Uri.EscapeDataString(workspace)}/{Uri.EscapeDataString(repoSlug)}/refs/branches?pagelen=100";
        while (!string.IsNullOrEmpty(nextUrl))
        {
            using var response = await client.GetAsync(nextUrl, ct);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("values", out var values))
                break;

            foreach (var el in values.EnumerateArray())
            {
                if (el.TryGetProperty("name", out var nameEl))
                {
                    var n = nameEl.GetString();
                    if (!string.IsNullOrEmpty(n))
                        names.Add(n);
                }
            }

            nextUrl = null;
            if (doc.RootElement.TryGetProperty("next", out var nextEl))
                nextUrl = nextEl.GetString();
        }

        return names;
    }

    public string BuildAuthenticatedCloneUrl(IGitBackupTarget target, string httpsCloneUrl)
    {
        var uri = new Uri(httpsCloneUrl);
        // REST API with API tokens: Basic user = Atlassian email. Git over HTTPS with the same token:
        // Bitbucket username + token, or static user x-bitbucket-api-token-auth + token (see Atlassian docs).
        var userForGit = LooksLikeAtlassianEmail(target.ProviderUserName)
            ? BitbucketApiTokenGitUser
            : target.ProviderUserName;
        return new UriBuilder(uri)
        {
            UserName = userForGit,
            Password = target.ApiKey,
        }.Uri.ToString();
    }

    private static bool LooksLikeAtlassianEmail(string value) =>
        !string.IsNullOrEmpty(value) && value.Contains('@', StringComparison.Ordinal);

    private static HttpClient CreateClient(IGitBackupTarget target)
    {
        var client = new HttpClient();
        var token = Convert.ToBase64String(
            Encoding.ASCII.GetBytes($"{target.ProviderUserName}:{target.ApiKey}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
        client.Timeout = target.HttpTimeout;
        return client;
    }

    private sealed record BitbucketUserJson([property: JsonPropertyName("uuid")] string Uuid);
}
