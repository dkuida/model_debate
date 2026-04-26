using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Logging;
using ModelDebate.Manager.Debate.Interface;

namespace ModelDebate.Manager.Debate.Service
{
    public sealed class DebateLogger : IDebateLogger, IDisposable
    {
        #region Members

        // private readonly StreamWriter          m_Writer;
        private readonly IComponentContext     m_Context;
        private readonly ILogger<DebateLogger> m_Logger;
        private          bool                  m_Disposed;
        private          ILogger               m_ChatLogger;

        #endregion

        #region Props

        // public string LogFilePath { get; }

        #endregion

        #region C'tor

        public DebateLogger(IComponentContext context , ILogger<DebateLogger> logger)
        {
            // string timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
            // LogFilePath    = Path.Combine(logDirectory, $"debate-{timestamp}.log");
            // m_Writer       = new StreamWriter(LogFilePath, append: false) { AutoFlush = true };
            
            m_Context    = context;
            m_Logger     = logger;
            m_ChatLogger = m_Context.ResolveNamed<ILogger>("chat");
        }

        #endregion

        #region Public

        public Task LogAsync(IDebateMessage message, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(message);

            if (m_Disposed)
            {
                m_Logger.LogWarning("LogAsync called on disposed DebateLogger — ignoring message {Kind}", message?.Kind);
                return Task.CompletedTask;
            }

            switch (message)
            {
                case DebateResponse response:
                    m_ChatLogger.LogInformation(
                        "[{SentAt:HH:mm:ss}] [{From}/{Model}] [tokens: {InputTokens} in / {OutputTokens} out] [latency: {LatencySeconds:F1}s]\n{Body}\n---",
                        response.SentAt,
                        response.FromParticipant,
                        response.Metadata.ModelId,
                        response.Metadata.InputTokens,
                        response.Metadata.OutputTokens,
                        response.Metadata.Latency.TotalSeconds,
                        response.Content);
                    break;

                case ThinkingNotification thinking:
                    m_ChatLogger.LogInformation(
                        "[{SentAt:HH:mm:ss}] [{From}] is thinking...",
                        thinking.SentAt,
                        thinking.FromParticipant);
                    break;

                case HeartbeatPing heartbeat:
                    m_ChatLogger.LogInformation(
                        "[{SentAt:HH:mm:ss}] [Heartbeat from {From}]",
                        heartbeat.SentAt,
                        heartbeat.FromParticipant);
                    break;

                case SeedMessage seed:
                    m_ChatLogger.LogInformation(
                        "[{SentAt:HH:mm:ss}] [Seed] Topic: {Topic}",
                        seed.SentAt,
                        seed.Topic);
                    break;

                default:
                    m_ChatLogger.LogInformation(
                        "[{SentAt:HH:mm:ss}] [{Kind}] {From}",
                        message.SentAt,
                        message.Kind,
                        message.FromParticipant);
                    break;
            }

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            if (m_Disposed)
            {
                return;
            }

            m_Disposed = true;
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Private

        #endregion
    }
}
