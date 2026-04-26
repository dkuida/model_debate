using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModelDebate.Access.Chat.Service.Claude;
using ModelDebate.Access.Chat.Service.OpenAI;
using ModelDebate.Manager.Debate.Interface;
using ModelDebate.Manager.Debate.Service;
using Serilog;
using Serilog.Settings.Configuration;
using ILogger=Microsoft.Extensions.Logging.ILogger;

namespace ModelDebate.Client.Runner
{
    internal static class Program
    {
        private static readonly string s_DebateloggerSectionName = "DebateLogger";

        #region Public

        private static async Task Main()
        {
            // 1. Validate required API keys — must come from env vars, never from config files
            string? anthropicKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
            string? openAiKey    = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            string  logFilePath  = $"logs/chat-{DateTimeOffset.Now:HH:m:ss}.txt";
            if (string.IsNullOrEmpty(anthropicKey))
            {
                Console.Error.WriteLine("ERROR: ANTHROPIC_API_KEY is not set.");
                Environment.Exit(1);
            }

            if (string.IsNullOrEmpty(openAiKey))
            {
                Console.Error.WriteLine("ERROR: OPENAI_API_KEY is not set.");
                Environment.Exit(1);
            }

            // 2. Load appsettings.json — this is the authoritative source for all non-secret config
            ;
            IConfiguration config = new ConfigurationBuilder()
                                    .SetBasePath(AppContext.BaseDirectory)
                                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                                    .AddInMemoryCollection(new Dictionary<string, string?>
                                    {
                                        [$"{s_DebateloggerSectionName}:WriteTo:1:Args:path"] = logFilePath
                                    })
                                    .Build();

            // 3. Resolve settings: env var overrides appsettings.json value
            string anthropicModel = Environment.GetEnvironmentVariable("ANTHROPIC_MODEL")
                                    ?? config["AnthropicModel"]
                                    ?? "claude-sonnet-4-6";
            string openAiModel = Environment.GetEnvironmentVariable("OPENAI_MODEL")
                                 ?? config["OpenAiModel"]
                                 ?? "gpt-4o";
            int turnTimeoutSeconds = int.TryParse(
                                         Environment.GetEnvironmentVariable("TURN_TIMEOUT_SECONDS"),
                                         out int t)
                                         ? t
                                         : int.TryParse(config["TurnTimeoutSeconds"], out int tc)
                                             ? tc
                                             : 60;
            string configLogDir = config["LogDirectory"] ?? string.Empty;
            string logDirectory = Environment.GetEnvironmentVariable("LOG_DIR")
                                  ?? (string.IsNullOrWhiteSpace(configLogDir)
                                          ? Directory.GetCurrentDirectory()
                                          : configLogDir);
            int maxTurns = int.TryParse(
                               Environment.GetEnvironmentVariable("MAX_TURNS"),
                               out int mt)
                           ? mt
                           : int.TryParse(config["MaxTurns"], out int mc)
                             ? mc : int.MaxValue;
            using CancellationTokenSource cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };
            // 6. Configure Serilog from appsettings.json — all sink/level config lives in JSON
            Serilog.Core.Logger technicalLogger = new LoggerConfiguration()
                                                  .ReadFrom.Configuration(config)
                                                  .Enrich.FromLogContext()
                                                  .CreateLogger();

            ConfigurationReaderOptions readerOptions = new ConfigurationReaderOptions()
            {
                SectionName = s_DebateloggerSectionName,
            };
            LoggerConfiguration chatLoggerConfig = new LoggerConfiguration()
                                                   .ReadFrom
                                                   .Configuration(config, readerOptions)
                                                   .Enrich.FromLogContext();

            ILogger chatLogger = LoggerFactory.Create(b =>
                b.AddSerilog(chatLoggerConfig
                    .CreateLogger(), dispose: false)).CreateLogger(s_DebateloggerSectionName);

            // 7. Create ILoggerFactory backed by Serilog
            ILoggerFactory loggerFactory = LoggerFactory.Create(b =>
                b.AddSerilog(technicalLogger, dispose: false));

            // 8. Build Autofac container
            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterInstance(config)
                   .As<IConfiguration>()
                   .As<IConfigurationRoot>()
                   ;

            builder.RegisterInstance(chatLogger)
                   .Named<ILogger>("chat")
                   .Keyed<ILogger>("chat")
                ;

            // Infrastructure
            builder.RegisterInstance(config)
                   .As<IConfiguration>()
                   .SingleInstance();

            builder.RegisterInstance(loggerFactory)
                   .As<ILoggerFactory>()
                   .ExternallyOwned()
                   .SingleInstance();

            builder.RegisterGeneric(typeof(Logger<>))
                   .As(typeof(ILogger<>))
                   .SingleInstance();

            // Configuration
            builder.RegisterInstance(new DebateOptions(
                       anthropicModel,
                       openAiModel,
                       turnTimeoutSeconds,
                       logDirectory,
                       maxTurns))
                   .SingleInstance();

            // Access layer
            builder.RegisterType<ClaudeChatService>()
                   .WithParameter("apiKey",  anthropicKey!)
                   .WithParameter("modelId", anthropicModel)
                   .AsSelf()
                   .SingleInstance();

            builder.RegisterType<OpenAiChatService>()
                   .WithParameter("apiKey",  openAiKey!)
                   .WithParameter("modelId", openAiModel)
                   .AsSelf()
                   .SingleInstance();

            // Manager layer
            builder.RegisterType<DebateLogger>()
                   .WithParameter("logDirectory", logDirectory)
                   .As<IDebateLogger>()
                   .SingleInstance();

            builder.Register(c => new DebateManager(
                       claudeResource: c.Resolve<ClaudeChatService>(),
                       gptResource: c.Resolve<OpenAiChatService>(),
                       logger: c.Resolve<IDebateLogger>(),
                       options: c.Resolve<DebateOptions>(),
                       managerLogger: c.Resolve<ILogger<DebateManager>>()))
                   .As<IDebateManager>()
                   .SingleInstance();
            
            
            // 9. Run the debate
            using (IContainer container = builder.Build())
            {
                string         topic        = Topic(anthropicModel, openAiModel, turnTimeoutSeconds, logDirectory, maxTurns, cts);
                IDebateLogger  debateLogger = container.Resolve<IDebateLogger>();
                IDebateManager manager      = container.Resolve<IDebateManager>();
                SeedMessage    seed         = new SeedMessage(topic!);

                try
                {
                    await manager.RunAsync(seed, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine();
                    Console.WriteLine("[Debate stopped by user]");
                }

                Console.WriteLine();
                Console.WriteLine(new string('─', 50));

                Console.WriteLine($"Debate transcript saved to: {logFilePath}");
            }

            // 10. Flush Serilog
            technicalLogger.Dispose();
        }

        private static string Topic(string                  anthropicModel,
                                    string                  openAiModel,
                                    int                     turnTimeoutSeconds,
                                    string                  logDirectory,
                                    int                     maxTurns,
                                    CancellationTokenSource cts)
        {
            // 4. Prompt user for debate topic
            Console.WriteLine("╔══════════════════════════════════════╗");
            Console.WriteLine("║         Claude vs GPT Debate         ║");
            Console.WriteLine("╚══════════════════════════════════════╝");
            Console.WriteLine();
            Console.Write("Enter debate topic: ");
            string? topic = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(topic))
            {
                Console.Error.WriteLine("ERROR: Topic cannot be empty.");
                Environment.Exit(1);
            }

            Console.WriteLine();
            Console.WriteLine($"Topic     : {topic}");
            Console.WriteLine($"Claude    : {anthropicModel}");
            Console.WriteLine($"GPT       : {openAiModel}");
            Console.WriteLine($"Timeout   : {turnTimeoutSeconds}s per turn");
            Console.WriteLine($"Max turns : {maxTurns}");
            Console.WriteLine($"Log dir   : {logDirectory}");
            Console.WriteLine();
            Console.WriteLine("Press Ctrl+C to stop the debate at any time.");
            Console.WriteLine(new string('─', 50));
            Console.WriteLine();

            // 5. Wire Ctrl+C to a CancellationToken

           
            return topic;
        }

        #endregion
    }
}
