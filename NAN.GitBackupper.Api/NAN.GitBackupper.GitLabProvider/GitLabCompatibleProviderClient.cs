using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using NAN.Git.Models;

namespace NAN.Git;

/// <summary>GitLab Cloud, self-hosted, or other GitLab-compatible APIs.</summary>
public sealed class GitLabCompatibleProviderClient(IGitLocalizer localizer) : IGitProviderClient
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private const string GitLabDefaultBaseUrl = "https://gitlab.com";

    public async Task<RequestReplay> ValidateAsync(IGitBackupTarget target, CancellationToken ct = default)
    {
        try
        {
            var requestUrl = $"{GetApiRoot(target)}/projects?membership=true&per_page=1&page=1";
            using var client = CreateClient(target);
            using var response = await client.GetAsync(requestUrl, ct);
            if (response.IsSuccessStatusCode)
                return new RequestReplay { IsSended = true, IsSuccess = true, Message = response.ReasonPhrase };

            var err = await response.Content.ReadAsStringAsync(ct);
            return new RequestReplay
            {
                IsSended = true,
                IsSuccess = false,
                Message = localizer.Translate("GitLabApiError", requestUrl, (int)response.StatusCode, err),
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
        long userId;
        using (var userResponse = await client.GetAsync($"{GetApiRoot(target)}/user", ct))
        {
            userResponse.EnsureSuccessStatusCode();
            await using var userStream = await userResponse.Content.ReadAsStreamAsync(ct);
            var user = await JsonSerializer.DeserializeAsync<GitLabUserJson>(userStream, JsonOptions, ct);
            userId = user?.Id ?? 0;
        }

        List<GitRepositoryDescriptor> list = [];
        var page = 1;
        while (true)
        {
            var url = $"{GetApiRoot(target)}/projects?membership=true&per_page=100&page={page}";
            using var response = await client.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var batch =
                await JsonSerializer.DeserializeAsync<GitLabProjectJson[]>(stream, JsonOptions, ct);
            if (batch == null || batch.Length == 0) break;

            foreach (var item in batch)
            {
                if (!string.IsNullOrEmpty(item.PathWithNamespace) && !string.IsNullOrEmpty(item.HttpUrlToRepo))
                {
                    var owned = userId != 0 && item.Owner != null && item.Owner.Id == userId;
                    list.Add(new GitRepositoryDescriptor(
                        item.PathWithNamespace,
                        item.HttpUrlToRepo,
                        item.DefaultBranch,
                        owned));
                }
            }

            if (batch.Length < 100) break;

            page++;
        }

        return list;
    }

    public async Task<IReadOnlyList<string>> ListBranchesAsync(IGitBackupTarget target, GitRepositoryDescriptor repository,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(repository.DisplayKey))
            return [];

        using var client = CreateClient(target);
        var encoded = Uri.EscapeDataString(repository.DisplayKey);
        List<string> names = [];
        var page = 1;
        while (true)
        {
            var url = $"{GetApiRoot(target)}/projects/{encoded}/repository/branches?per_page=100&page={page}";
            using var response = await client.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var batch =
                await JsonSerializer.DeserializeAsync<GitLabBranchJson[]>(stream, JsonOptions, ct);
            if (batch == null || batch.Length == 0) break;

            foreach (var item in batch)
            {
                if (!string.IsNullOrEmpty(item.Name))
                    names.Add(item.Name);
            }

            if (batch.Length < 100) break;

            page++;
        }

        return names;
    }

    public string BuildAuthenticatedCloneUrl(IGitBackupTarget target, string httpsCloneUrl)
    {
        var uri = new Uri(httpsCloneUrl);
        return new UriBuilder(uri)
        {
            UserName = "oauth2",
            Password = NormalizeApiKey(target),
        }.Uri.ToString();
    }

    private static HttpClient CreateClient(IGitBackupTarget target)
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", NormalizeApiKey(target));
        client.Timeout = target.HttpTimeout;
        return client;
    }

    private static string GetApiRoot(IGitBackupTarget target)
    {
        return $"{NormalizeBaseUrl(target)}/api/v4";
    }

    private static string NormalizeApiKey(IGitBackupTarget target)
        => target.ApiKey?.Trim() ?? string.Empty;

    private static string NormalizeBaseUrl(IGitBackupTarget target)
        => string.IsNullOrWhiteSpace(target.GitLabBaseUrl)
            ? GitLabDefaultBaseUrl
            : target.GitLabBaseUrl.Trim().TrimEnd('/');

    private sealed record GitLabUserJson([property: JsonPropertyName("id")] long Id);

    private sealed record GitLabOwnerJson([property: JsonPropertyName("id")] long Id);

    private sealed record GitLabProjectJson([property: JsonPropertyName("path_with_namespace")] string PathWithNamespace,
        [property: JsonPropertyName("http_url_to_repo")] string HttpUrlToRepo,
        [property: JsonPropertyName("default_branch")] string DefaultBranch,
        [property: JsonPropertyName("owner")] GitLabOwnerJson Owner);

    private sealed record GitLabBranchJson([property: JsonPropertyName("name")] string Name);
}
