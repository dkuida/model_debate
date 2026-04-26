namespace ModelDebate.Access.Chat.Interface;

public class ChatRequest
{
    #region Members

    public string SystemPrompt { get; }
    public string UserMessage  { get; }

    #endregion

    #region C'tor

    public ChatRequest(string systemPrompt, string userMessage)
    {
        SystemPrompt = systemPrompt;
        UserMessage  = userMessage;
    }

    #endregion
}
