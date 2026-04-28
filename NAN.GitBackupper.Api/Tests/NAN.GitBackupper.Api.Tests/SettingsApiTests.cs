using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NAN.GitBackupper.Api.Models;

namespace NAN.GitBackupper.Api.Tests;

public sealed class SettingsApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public SettingsApiTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory.WithWebHostBuilder(_ => { });
    }

    [Fact]
    public async Task Get_settings_returns_ok()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/settings");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var model = await response.Content.ReadFromJsonAsync<SettingsModel>(jsonOptions);
        Assert.NotNull(model);
        Assert.NotNull(model.TargetItems);
    }
}
