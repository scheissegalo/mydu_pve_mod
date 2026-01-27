using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mod.DynamicEncounters.Overrides.ApiClient.Data;
using Mod.DynamicEncounters.Overrides.ApiClient.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Mod.DynamicEncounters.Overrides.ApiClient.Services;

public class PveModQuestsApiClient(IServiceProvider provider) : IPveModQuestsApiClient
{
    private readonly IHttpClientFactory _httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
    private readonly ILogger<PveModQuestsApiClient> _logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger<PveModQuestsApiClient>();

    public async Task<JToken> GetPlayerQuestsAsync(ulong playerId)
    {
        var baseUrl = PveModBaseUrl.GetBaseUrl().TrimEnd('/');
        var url = $"{baseUrl}/quest/player/{playerId}";
        
        _logger.LogInformation("PveModQuestsApiClient: Calling GET {Url}", url);

        using var client = _httpClientFactory.CreateClient();
        
        var responseMessage = await client.GetAsync(url);
        
        _logger.LogInformation("PveModQuestsApiClient: Response status {StatusCode}", responseMessage.StatusCode);

        return JToken.Parse(await responseMessage.Content.ReadAsStringAsync());
    }

    public async Task<JToken> GetNpcQuests(ulong playerId, long factionId, Guid territoryId, int seed)
    {
        var baseUrl = PveModBaseUrl.GetBaseUrl().TrimEnd('/');
        var url = $"{baseUrl}/quest/giver";
        
        _logger.LogInformation("PveModQuestsApiClient: Calling POST {Url}", url);
        
        using var client = _httpClientFactory.CreateClient();

        var responseMessage = await client.PostAsync(
            url,
            new StringContent(
                JsonConvert.SerializeObject(new
                {
                    playerId,
                    factionId,
                    territoryId,
                    seed
                }),
                Encoding.UTF8,
                "application/json"
            )
        );
        
        _logger.LogInformation("PveModQuestsApiClient: Response status {StatusCode}", responseMessage.StatusCode);
        
        return JToken.Parse(await responseMessage.Content.ReadAsStringAsync());
    }

    public async Task<BasicOutcome> AcceptQuest(Guid questId, ulong playerId, long factionId, Guid territoryId, int seed)
    {
        var baseUrl = PveModBaseUrl.GetBaseUrl().TrimEnd('/');
        var url = $"{baseUrl}/quest/player/accept";
        
        using var client = _httpClientFactory.CreateClient();

        var responseMessage = await client.PostAsync(
            url,
            new StringContent(
                JsonConvert.SerializeObject(new
                {
                    questId,
                    playerId,
                    factionId,
                    territoryId,
                    seed
                }),
                Encoding.UTF8,
                "application/json"
            )
        );
        
        return JsonConvert.DeserializeObject<BasicOutcome>(await responseMessage.Content.ReadAsStringAsync());
    }

    public async Task<BasicOutcome> AbandonQuest(Guid questId, ulong playerId)
    {
        var baseUrl = PveModBaseUrl.GetBaseUrl().TrimEnd('/');
        var url = $"{baseUrl}/quest/player/abandon";
        
        using var client = _httpClientFactory.CreateClient();

        var responseMessage = await client.PostAsync(
            url,
            new StringContent(
                JsonConvert.SerializeObject(new
                {
                    questId,
                    playerId
                }),
                Encoding.UTF8,
                "application/json"
            )
        );
        
        return JsonConvert.DeserializeObject<BasicOutcome>(await responseMessage.Content.ReadAsStringAsync());
    }
}