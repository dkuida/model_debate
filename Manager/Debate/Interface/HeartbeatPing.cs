using System;

namespace ModelDebate.Manager.Debate.Interface;

public class HeartbeatPing : IDebateMessage
{
    #region Members

    public string         MessageId       { get; }
    public string         FromParticipant { get; }
    public DateTimeOffset SentAt          { get; }
    public MessageKind    Kind            => MessageKind.Heartbeat;

    #endregion

    #region C'tor

    public HeartbeatPing(string fromParticipant)
    {
        MessageId       = Guid.NewGuid().ToString("N");
        SentAt          = DateTimeOffset.UtcNow;
        FromParticipant = fromParticipant;
    }

    #endregion
}
