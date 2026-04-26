using System;

namespace ModelDebate.Access.Chat.Interface;

public class ModelMetadata
{
    #region Members

    public string   Provider     { get; }
    public string   ModelId      { get; }
    public string   ModelVersion { get; }
    public int      InputTokens  { get; }
    public int      OutputTokens { get; }
    public TimeSpan Latency      { get; }

    #endregion

    #region C'tor

    public ModelMetadata(string   provider,
                         string   modelId,
                         string   modelVersion,
                         int      inputTokens,
                         int      outputTokens,
                         TimeSpan latency)
    {
        Provider     = provider;
        ModelId      = modelId;
        ModelVersion = modelVersion;
        InputTokens  = inputTokens;
        OutputTokens = outputTokens;
        Latency      = latency;
    }

    #endregion
}
