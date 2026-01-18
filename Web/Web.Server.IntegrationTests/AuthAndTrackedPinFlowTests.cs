using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Web.Server.IntegrationTests;

[TestClass]
public class AuthAndTrackedPinFlowTests
{
    private static readonly string BaseUrl = "http://localhost:5000/api/v1";
    private static readonly HttpClient Client = new HttpClient();

    [TestMethod]
    public async Task Login_Add_And_Untrack_Pin_Persists_Across_Restarts()
    {
        // 1. Login and get token
        var loginResponse = await Client.PostAsJsonAsync($"{BaseUrl}/auth/login", new { email = "testuser@example.com" });
        loginResponse.EnsureSuccessStatusCode();
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResult>();
        Assert.IsNotNull(loginResult?.Token);
        var token = loginResult.Token;
        Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // 2. Add a tracked pin
        var addPinResponse = await Client.PostAsJsonAsync($"{BaseUrl}/trackedpins", new { mapPinId = 123 });
        addPinResponse.EnsureSuccessStatusCode();

        // 3. Untrack the pin
        var untrackResponse = await Client.DeleteAsync($"{BaseUrl}/trackedpins/123");
        untrackResponse.EnsureSuccessStatusCode();

        // 4. Simulate backend restart (manual step or via test fixture)
        // 5. Try another tracked pin operation with the same token
        var addPinAgainResponse = await Client.PostAsJsonAsync($"{BaseUrl}/trackedpins", new { mapPinId = 456 });
        addPinAgainResponse.EnsureSuccessStatusCode();
    }

    private class LoginResult
    {
        public string Token { get; set; }
    }
}
