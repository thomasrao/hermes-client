using HermesSocketLibrary.Requests.Messages;

namespace TwitchChatTTS.Twitch.Redemptions
{
    public interface IRedemptionManager
    {
        Task Execute(RedeemableAction action, string senderDisplayName, long senderId);
        IList<RedeemableAction> Get(string twitchRedemptionId);
        void Initialize(IEnumerable<Redemption> redemptions, IDictionary<string, RedeemableAction> actions);
    }
}