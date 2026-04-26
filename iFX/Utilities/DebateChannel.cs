using System.Threading.Channels;

namespace ModelDebate.iFx.Utilities;

public sealed class DebateChannel<T>
{
    #region Members

    public Channel<T> ClaudeInbox { get; }
    public Channel<T> GptInbox    { get; }

    #endregion

    #region C'tor

    public DebateChannel()
    {
        UnboundedChannelOptions options = new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        };

        ClaudeInbox = Channel.CreateUnbounded<T>(options);
        GptInbox    = Channel.CreateUnbounded<T>(options);
    }

    #endregion
}
