namespace ModelDebate.Access.Chat.Interface;

public class ChatResponse
{
    #region Members

    public string        Content  { get; }
    public ModelMetadata Metadata { get; }

    #endregion

    #region C'tor

    public ChatResponse(string content, ModelMetadata metadata)
    {
        Content  = content;
        Metadata = metadata;
    }

    #endregion
}
