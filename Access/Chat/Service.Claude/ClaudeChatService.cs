using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Anthropic;
using Anthropic.Core;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Logging;
using ChatAccessDefinitions=ModelDebate.Access.Chat.Interface;
using Microsoft.Extensions.AI;

namespace ModelDebate.Access.Chat.Service.Claude;

public class ClaudeChatService : ChatAccessDefinitions.IChatResource
{
    #region Members

    private readonly AnthropicClient              m_Client;
    private readonly string                       m_ModelId;
    private readonly ILogger<ClaudeChatService>   m_Logger;

    #endregion

    #region C'tor

    public ClaudeChatService(string apiKey, string modelId, ILogger<ClaudeChatService> logger)
    {
        Debug.Assert(!string.IsNullOrEmpty(apiKey),   "apiKey is not null or empty");
        Debug.Assert(!string.IsNullOrEmpty(modelId), "modelId is not null or empty");
        m_Client  = new AnthropicClient(new ClientOptions { ApiKey = apiKey });
        m_ModelId = modelId;
        m_Logger  = logger;
    }

    #endregion

    #region Public

    public async Task<ChatAccessDefinitions.ChatResponse> CompleteAsync(ChatAccessDefinitions.ChatRequest request, CancellationToken ct)
    {
        Debug.Assert(request is not null, "request is not null");

        Stopwatch stopwatch = Stopwatch.StartNew();

        MessageCreateParams parameters = new()
        {
            Model     = m_ModelId,
            MaxTokens = 1024,
            System    = request.SystemPrompt,
            Messages  = [ new() { Role = Role.User, Content = request.UserMessage } ]
        };
        
        Message message = await m_Client.Messages.Create(parameters, cancellationToken: ct).ConfigureAwait(false);

        stopwatch.Stop();

        message.Content[0].TryPickText(out TextBlock? textBlock);
        string content = textBlock!.Text;

        int inputTokens  = (int)message.Usage.InputTokens;
        int outputTokens = (int)message.Usage.OutputTokens;

        ChatAccessDefinitions.ModelMetadata metadata = new ChatAccessDefinitions.ModelMetadata(
            provider:     "Anthropic",
            modelId:      message.Model ?? m_ModelId,
            modelVersion: message.Model ?? m_ModelId,
            inputTokens:  inputTokens,
            outputTokens: outputTokens,
            latency:      stopwatch.Elapsed);

        return new ChatAccessDefinitions.ChatResponse(content, metadata);
    }

    #endregion
}
