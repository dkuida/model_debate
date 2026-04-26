using System;

namespace ModelDebate.Manager.Debate.Interface;

public class ThinkingNotification : IDebateMessage
{
    #region Members

    public string         MessageId       { get; }
    public string         FromParticipant { get; }
    public DateTimeOffset SentAt          { get; }
    public MessageKind    Kind            => MessageKind.Thinking;

    #endregion

    #region C'tor

    public ThinkingNotification(string fromParticipant)
    {
        MessageId       = Guid.NewGuid().ToString("N");
        SentAt          = DateTimeOffset.UtcNow;
        FromParticipant = fromParticipant;
    }

    #endregion
}
