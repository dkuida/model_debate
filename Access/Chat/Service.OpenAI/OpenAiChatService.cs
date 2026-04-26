using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ModelDebate.Access.Chat.Interface;
using OpenAI.Chat;

namespace ModelDebate.Access.Chat.Service.OpenAI;

public class OpenAiChatService : IChatResource
{
    #region Members

    private readonly ChatClient                    m_Client;
    private readonly string                        m_ModelId;
    private readonly ILogger<OpenAiChatService>    m_Logger;

    #endregion

    #region C'tor

    public OpenAiChatService(string apiKey, string modelId, ILogger<OpenAiChatService> logger)
    {
        Debug.Assert(!string.IsNullOrEmpty(apiKey),  "apiKey is not null or empty");
        Debug.Assert(!string.IsNullOrEmpty(modelId), "modelId is not null or empty");
        m_Client  = new ChatClient(model: modelId, apiKey: apiKey);
        m_ModelId = modelId;
        m_Logger  = logger;
    }

    #endregion

    #region Public

    public async Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct)
    {
        Debug.Assert(request is not null, "request is not null");

        Stopwatch stopwatch = Stopwatch.StartNew();

        List<ChatMessage> messages =
        [
            ChatMessage.CreateSystemMessage(request.SystemPrompt),
            ChatMessage.CreateUserMessage(request.UserMessage)
        ];

        ChatCompletion completion = await m_Client.CompleteChatAsync(messages, cancellationToken: ct).ConfigureAwait(false);

        stopwatch.Stop();

        string content      = completion.Content[0].Text;
        int    inputTokens  = completion.Usage.InputTokenCount;
        int    outputTokens = completion.Usage.OutputTokenCount;
        string modelId      = completion.Model ?? m_ModelId;

        ModelMetadata metadata = new ModelMetadata(
            provider:     "OpenAI",
            modelId:      modelId,
            modelVersion: modelId,
            inputTokens:  inputTokens,
            outputTokens: outputTokens,
            latency:      stopwatch.Elapsed);

        return new ChatResponse(content, metadata);
    }

    #endregion
}
