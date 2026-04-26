using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ModelDebate.Access.Chat.Interface;
using ModelDebate.Manager.Debate.Interface;

namespace ModelDebate.Manager.Debate.Service
{
    public sealed partial class DebateManager : IDebateManager
    {
        #region Members

        private readonly IChatResource          m_ClaudeResource;
        private readonly IChatResource          m_GptResource;
        private readonly IDebateLogger          m_Logger;
        private readonly DebateOptions          m_Options;
        private readonly ILogger<DebateManager> m_ManagerLogger;

        private readonly string m_ClaudeName = "Claude";
        private readonly string m_GptName    = "GPT";

        #endregion

        #region C'tor

        public DebateManager(
            IChatResource          claudeResource,
            IChatResource          gptResource,
            IDebateLogger          logger,
            DebateOptions          options,
            ILogger<DebateManager> managerLogger)
        {
            Debug.Assert(claudeResource is not null, "claudeResource is not null");
            Debug.Assert(gptResource   is not null, "gptResource is not null");
            Debug.Assert(logger        is not null, "logger is not null");
            Debug.Assert(options       is not null, "options is not null");

            m_ClaudeResource = claudeResource;
            m_GptResource    = gptResource;
            m_Logger         = logger;
            m_Options        = options;
            m_ManagerLogger  = managerLogger;
        }

        #endregion

        #region Public

        public async Task RunAsync(SeedMessage seed, CancellationToken ct)
        {
            Debug.Assert(seed is not null, "seed is not null");

            string systemPrompt = BuildSystemPrompt(seed.Topic);
            string lastMessage  = seed.Topic;
            bool   claudeTurn   = true;   // Claude speaks first
            int    turnCount    = 0;

            while (!ct.IsCancellationRequested)
            {
                IChatResource currentResource    = claudeTurn ? m_ClaudeResource : m_GptResource;
                string        currentSpeakerName = claudeTurn ? m_ClaudeName     : m_GptName;

                // Signal thinking — boundary: only Manager types to logger
                ThinkingNotification thinking = new ThinkingNotification(currentSpeakerName);
                await m_Logger.LogAsync(thinking, ct);

                // Boundary map #1: Manager context → Access ChatRequest
                ChatRequest chatRequest = MapToRequest(systemPrompt, lastMessage);

                // Per-turn timeout linked to outer ct
                using CancellationTokenSource turnCts =
                    CancellationTokenSource.CreateLinkedTokenSource(ct);
                turnCts.CancelAfter(TimeSpan.FromSeconds(m_Options.TurnTimeoutSeconds));

                ChatResponse chatResponse;
                try
                {
                    chatResponse = await currentResource.CompleteAsync(chatRequest, turnCts.Token);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    // Turn timeout — outer ct still alive
                    await HandleTurnTimeoutAsync(currentSpeakerName, ct);
                    return;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // API failure
                    await HandleApiFailureAsync(currentSpeakerName, ex, ct);
                    return;
                }

                // Boundary map #2: Access ChatResponse → Manager DebateResponse
                // chatResponse is consumed here and never passed outside this method
                DebateResponse debateResponse = MapToDebateResponse(currentSpeakerName, chatResponse);

                // Only Manager types cross this boundary
                await m_Logger.LogAsync(debateResponse, ct);

                lastMessage = debateResponse.Content;
                turnCount++;

                if (turnCount >= m_Options.MaxTurns)
                {
                    m_ManagerLogger.LogInformation(
                        "Max turns ({MaxTurns}) reached. Ending debate.", m_Options.MaxTurns);
                    break;
                }

                claudeTurn = !claudeTurn;
            }
        }

        #endregion

        #region Private

        private static string BuildSystemPrompt(string topic)
        {
            return
                $"You are in a debate about the following topic: \"{topic}\". " +
                "Keep your responses to 2-3 paragraphs. " +
                "Your opponent is an AI. " +
                "Argue your position directly and concisely.";
        }

        // Boundary map #1 — Manager context → Access ChatRequest
        private static ChatRequest MapToRequest(string systemPrompt, string userMessage)
        {
            return new ChatRequest(systemPrompt, userMessage);
        }

        // Boundary map #2 — Access ChatResponse → Manager DebateResponse
        // chatResponse is consumed here; it is never passed to IDebateLogger or outside this method
        private static DebateResponse MapToDebateResponse(
            string       participantName,
            ChatResponse chatResponse)
        {
            ResponseMetadata metadata = new ResponseMetadata(
                provider:     chatResponse.Metadata.Provider,
                modelId:      chatResponse.Metadata.ModelId,
                modelVersion: chatResponse.Metadata.ModelVersion,
                inputTokens:  chatResponse.Metadata.InputTokens,
                outputTokens: chatResponse.Metadata.OutputTokens,
                latency:      chatResponse.Metadata.Latency);

            return new DebateResponse(participantName, chatResponse.Content, metadata);
        }

        private async Task HandleTurnTimeoutAsync(string participantName, CancellationToken ct)
        {
            LogTurnTimeoutForParticipantParticipantTimeout(participantName, m_Options.TurnTimeoutSeconds);
            ThinkingNotification timeoutMsg = new ThinkingNotification($"{participantName}:Timeout");
            await m_Logger.LogAsync(timeoutMsg, ct);
        }

        private async Task HandleApiFailureAsync(
            string    participantName,
            Exception ex,
            CancellationToken ct)
        {
            Debug.Assert(ex is not null, "ex is not null");
            LogApiFailureForParticipantParticipant(participantName, ex);
            ThinkingNotification errorMsg = new ThinkingNotification($"{participantName}:Error");
            await m_Logger.LogAsync(errorMsg, ct);
        }

        #endregion

        [LoggerMessage(LogLevel.Warning, "Turn timeout for participant {Participant} after {Seconds}s")]
        partial void LogTurnTimeoutForParticipantParticipantTimeout(string participant, int seconds);

        [LoggerMessage(LogLevel.Error, "API failure for participant {Participant}")]
        partial void LogApiFailureForParticipantParticipant(string participant, Exception exception);
    }
}
