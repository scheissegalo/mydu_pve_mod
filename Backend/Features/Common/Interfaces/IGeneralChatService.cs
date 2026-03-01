using System.Threading.Tasks;

namespace Mod.DynamicEncounters.Features.Common.Interfaces;

/// <summary>Sends messages to the general (SUPPORT) chat channel as the bot user.</summary>
public interface IGeneralChatService
{
    Task SendToGeneralAsync(string message);
}
