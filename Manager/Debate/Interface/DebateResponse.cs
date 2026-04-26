using System;

namespace ModelDebate.Manager.Debate.Interface;

public class DebateResponse : IDebateMessage
{
    #region Members

    public string           MessageId       { get; }
    public string           FromParticipant { get; }
    public DateTimeOffset   SentAt          { get; }
    public MessageKind      Kind            => MessageKind.Response;
    public string           Content         { get; }
    public ResponseMetadata Metadata        { get; }

    #endregion

    #region C'tor

    public DebateResponse(string           fromParticipant,
                          string           content,
                          ResponseMetadata metadata)
    {
        MessageId       = Guid.NewGuid().ToString("N");
        SentAt          = DateTimeOffset.UtcNow;
        FromParticipant = fromParticipant;
        Content         = content;
        Metadata        = metadata;
    }

    #endregion
}
