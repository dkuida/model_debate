using System;

namespace ModelDebate.Manager.Debate.Interface;

public interface IDebateMessage
{
    #region Members

    string         MessageId       { get; }
    string         FromParticipant { get; }
    DateTimeOffset SentAt          { get; }
    MessageKind    Kind            { get; }

    #endregion
}
