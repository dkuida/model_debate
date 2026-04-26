using System;

namespace ModelDebate.Manager.Debate.Interface;

public class SeedMessage : IDebateMessage
{
    #region Members

    public string         MessageId       { get; }
    public string         FromParticipant { get; }
    public DateTimeOffset SentAt          { get; }
    public MessageKind    Kind            => MessageKind.Seed;
    public string         Topic           { get; }

    #endregion

    #region C'tor

    public SeedMessage(string topic, string fromParticipant = "User")
    {
        MessageId       = Guid.NewGuid().ToString("N");
        Topic           = topic;
        FromParticipant = fromParticipant;
        SentAt          = DateTimeOffset.UtcNow;
    }

    #endregion
}
