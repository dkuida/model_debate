namespace ModelDebate.Manager.Debate.Interface;

public class DebateOptions
{
    #region Members

    public string AnthropicModel      { get; }
    public string OpenAiModel         { get; }
    public int    TurnTimeoutSeconds  { get; }
    public string LogDirectory        { get; }
    public int    MaxTurns            { get; }

    #endregion

    #region C'tor

    public DebateOptions(string anthropicModel,
                         string openAiModel,
                         int    turnTimeoutSeconds,
                         string logDirectory,
                         int    maxTurns = int.MaxValue)
    {
        AnthropicModel     = anthropicModel;
        OpenAiModel        = openAiModel;
        TurnTimeoutSeconds = turnTimeoutSeconds;
        LogDirectory       = logDirectory;
        MaxTurns           = maxTurns;
    }

    #endregion
}
