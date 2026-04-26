namespace ModelDebate.iFx.Utilities;

public static class ErrorMessage
{
    #region Members

    public static readonly string ApiKeyMissing = "API key not configured.";
    public static readonly string TurnTimeout   = "Turn timed out with no response.";
    public static readonly string ApiFailed     = "LLM API call failed.";

    #endregion
}
