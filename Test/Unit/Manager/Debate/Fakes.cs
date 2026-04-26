using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ModelDebate.Access.Chat.Interface;
using ModelDebate.Manager.Debate.Interface;

namespace Test.Unit.Manager.Debate
{
    internal sealed class FakeChatResource : IChatResource
    {
        #region Members

        public List<ChatRequest> ReceivedRequests { get; } = new List<ChatRequest>();

        private readonly ChatResponse             m_Response;
        private readonly TimeSpan                 m_Delay;
        private readonly int                      m_MaxCallsBeforeCancel;
        private readonly CancellationTokenSource? m_CtsToCancel;
        private readonly bool                     m_ShouldThrow;
        private          int                      m_CallCount;

        #endregion

        #region C'tor

        public FakeChatResource(
            ChatResponse             response,
            int                      maxCallsBeforeCancel = int.MaxValue,
            CancellationTokenSource? ctsToCancel          = null,
            TimeSpan                 delay                = default,
            bool                     shouldThrow          = false)
        {
            m_Response             = response;
            m_MaxCallsBeforeCancel = maxCallsBeforeCancel;
            m_CtsToCancel          = ctsToCancel;
            m_Delay                = delay;
            m_ShouldThrow          = shouldThrow;
        }

        #endregion

        #region Public

        public async Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct)
        {
            ReceivedRequests.Add(request);

            if (m_ShouldThrow)
            {
                throw new System.Net.Http.HttpRequestException("Simulated API failure");
            }

            if (m_Delay > TimeSpan.Zero)
            {
                await Task.Delay(m_Delay, ct);
            }

            m_CallCount++;
            if (m_CallCount >= m_MaxCallsBeforeCancel)
            {
                m_CtsToCancel?.Cancel();
            }

            return m_Response;
        }

        #endregion

        #region Private

        #endregion
    }

    internal sealed class FakeDebateLogger : IDebateLogger
    {
        #region Members

        public List<IDebateMessage> LoggedMessages { get; } = new List<IDebateMessage>();

        #endregion

        #region Public

        public string LogFilePath => string.Empty;

        public Task LogAsync(IDebateMessage message, CancellationToken ct)
        {
            LoggedMessages.Add(message);
            return Task.CompletedTask;
        }

        #endregion

        #region Private

        #endregion
    }

    internal static class FakeResponses
    {
        #region Public

        public static ChatResponse Make(
            string provider,
            string modelId,
            string content = "A witty debate argument.")
        {
            ModelMetadata metadata = new ModelMetadata(
                provider:     provider,
                modelId:      modelId,
                modelVersion: modelId,
                inputTokens:  42,
                outputTokens: 21,
                latency:      TimeSpan.FromMilliseconds(100));

            return new ChatResponse(content, metadata);
        }

        #endregion
    }
}
