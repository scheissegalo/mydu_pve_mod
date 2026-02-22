using System.Threading.Tasks;
using Backend;
using Microsoft.AspNetCore.Mvc;
using Mod.DynamicEncounters;
using Mod.DynamicEncounters.Helpers;
using NQ;
using NQ.Interfaces;
using NQutils.Exceptions;

namespace Mod.DynamicEncounters.Api.Controllers;

/// <summary>Send chat messages as the bot user (e.g. to the general/SUPPORT channel).</summary>
[Route("chat")]
public class ChatController : Controller
{
    /// <summary>Send a message to the general channel (SUPPORT) as the bot user. All players see it in the general chat.</summary>
    [HttpPost]
    [Route("general")]
    public async Task<IActionResult> SendToGeneral([FromBody] SendChatMessageRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "Message is required", message = request?.Message });

        try
        {
            var orleans = ModBase.ServiceProvider.GetOrleans();
            var chatGrain = orleans.GetChatGrain(ModBase.Bot.PlayerId);
            await chatGrain.SendMessage(
                new MessageContent
                {
                    message = request.Message.Trim(),
                    channel = new MessageChannel
                    {
                        channel = MessageChannelType.SUPPORT,
                        targetId = 0,
                        channelFilter = ""
                    }
                });
            return Ok(new { sent = true, channel = "general" });
        }
        catch (BusinessException ex)
        {
            return BadRequest(new { error = "Failed to send chat message", message = ex.Message });
        }
    }
}

public class SendChatMessageRequest
{
    public string Message { get; set; } = "";
}
