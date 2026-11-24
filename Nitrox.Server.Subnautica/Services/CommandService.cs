using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.Commands.ArgConverters.Core;
using Nitrox.Server.Subnautica.Models.Commands.Core;

namespace Nitrox.Server.Subnautica.Services;

/// <summary>
///     Enables processing of commands from a string.
/// </summary>
internal sealed partial class CommandService(CommandRegistryService registry, ILogger<CommandService> logger, ILoggerFactory loggerFactory) : BackgroundService, IHostedLifecycleService, ICommandSubmit
{
    private readonly ILogger<CommandService> logger = logger;
    private readonly ILoggerFactory loggerFactory = loggerFactory;

    private readonly CommandRegistryService registry = registry;
    private readonly Channel<Task> runningCommands = Channel.CreateUnbounded<Task>();

    [GeneratedRegex(@"""(?:[^""\\]|\\.)*""|\S+", RegexOptions.NonBacktracking | RegexOptions.ExplicitCapture)]
    private static partial Regex ArgumentsRegex { get; }

    public bool ExecuteCommand(ReadOnlySpan<char> inputText, ICommandContext context, out Task<bool>? commandTask)
    {
        commandTask = null;
        inputText = inputText.Trim();
        if (inputText.IsEmpty)
        {
            return false;
        }

        // Extract command name from command input.
        int endOfNameIndex = inputText.IndexOf(' ');
        if (endOfNameIndex == -1)
        {
            endOfNameIndex = inputText.Length;
        }
        ReadOnlySpan<char> commandName = inputText[.. endOfNameIndex];

        if (!registry.TryGetHandlersByCommandName(context, commandName, out List<CommandHandlerEntry> handlers))
        {
            if (registry.TryGetRequiredPermission(context, commandName, out Perms required))
            {
                logger.ZLogInformation($"Command {commandName.ToString():@CommandName} needs the rank {required} or higher (yours is {context.Permissions})");
            }
            else
            {
                logger.ZLogInformation($"Unknown command {commandName.ToString():@CommandName}");
            }
            return false;
        }

        string commandArgs = inputText[endOfNameIndex ..].Trim().ToString();
        List<Token> tokens = Tokenize(commandArgs);
        string logText = CommandLogText.Redact(inputText.ToString());

        // Each handler that can take this many words, with its words: exact matches first, then handlers whose last text
        // parameter takes the rest of the line (a whispered message, a broadcast, a reason)
        List<(CommandHandlerEntry Handler, List<string> Args)> candidates = [];
        foreach (CommandHandlerEntry candidate in handlers)
        {
            if (candidate.Parameters.Length == tokens.Count)
            {
                candidates.Add((candidate, tokens.Select(token => token.Text).ToList()));
            }
        }
        foreach (CommandHandlerEntry candidate in handlers)
        {
            if (GetRestOfLineArguments(candidate.ParameterTypes, tokens, commandArgs) is { } restOfLine)
            {
                candidates.Add((candidate, restOfLine));
            }
        }
        if (candidates.Count == 0)
        {
            logger.ZLogInformation($"Command {commandName.ToString():@CommandName} does not support the provided arguments. See below for more information.");
            ExecuteCommand($"help {commandName}", context, out commandTask);
            return false;
        }

        // Plain values (numbers, words, enums) are parsed at once; arguments that need a lookup (a player name) are
        // converted on a worker below.
        List<(CommandHandlerEntry Handler, List<string> Args)> needConversion = [];
        foreach ((CommandHandlerEntry candidate, List<string> args) in candidates)
        {
            object[] parsed = new object[args.Count + 1];
            parsed[0] = context;
            bool allParsed = true;
            for (int i = 0; i < args.Count; i++)
            {
                if (registry.TryParseToType(args[i], candidate.ParameterTypes[i]) is not { } value)
                {
                    allParsed = false;
                    break;
                }
                parsed[i + 1] = value;
            }
            if (allParsed)
            {
                RunHandler(candidate, parsed, logText);
                return true;
            }
            needConversion.Add((candidate, args));
        }

        commandTask = QueueTryRunFirstArgConvertedHandler(context, needConversion, commandName.ToString(), logText);
        return true;
    }

    private readonly record struct Token(string Text, int Start);

    private static List<Token> Tokenize(string commandArgs)
    {
        List<Token> tokens = [];
        foreach (ValueMatch match in ArgumentsRegex.EnumerateMatches(commandArgs))
        {
            ReadOnlySpan<char> text = commandArgs.AsSpan(match.Index, match.Length);
            // Trim the quotes of a quoted argument.
            text = text switch
            {
                ['"', .., '"'] or ['\'', .., '\''] when text.Length >= 2 => text[1..^1],
                _ => text
            };
            tokens.Add(new Token(text.ToString(), match.Index));
        }
        return tokens;
    }

    /// <summary>
    ///     The arguments for a handler whose last parameter is text, taking everything after the other arguments as that
    ///     text; null if the handler takes no such text or the line has no more words than it has parameters.
    /// </summary>
    internal static List<string>? GetRestOfLineArguments(Type[] parameterTypes, string commandArgs)
    {
        return GetRestOfLineArguments(parameterTypes, Tokenize(commandArgs), commandArgs);
    }

    private static List<string>? GetRestOfLineArguments(Type[] parameterTypes, List<Token> tokens, string commandArgs)
    {
        if (parameterTypes.Length == 0 || parameterTypes[^1] != typeof(string) || tokens.Count <= parameterTypes.Length)
        {
            return null;
        }
        List<string> args = tokens.Take(parameterTypes.Length - 1).Select(token => token.Text).ToList();
        string rest = commandArgs[tokens[parameterTypes.Length - 1].Start..].Trim();
        args.Add(rest);
        return args;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        runningCommands.Writer.TryComplete();
        logger.ZLogTrace($"Waiting for commands to finish processing...");
        await EnsureCommandsAreProcessedAsync(cancellationToken);
        logger.ZLogTrace($"Done waiting for commands");
        await base.StopAsync(cancellationToken);
    }

    public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken)
    {
        logger.ZLogInformation($"To get help for commands, run help in console or /help in chatbox");
        return Task.CompletedTask;
    }

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) => await EnsureCommandsAreProcessedAsync(stoppingToken);

    private async Task EnsureCommandsAreProcessedAsync(CancellationToken cancellationToken = default)
    {
        await foreach (Task task in runningCommands.Reader.ReadAllAsync(cancellationToken))
        {
            if (task == null)
            {
                continue;
            }
            await task;
        }
    }

    private Task<bool> QueueTryRunFirstArgConvertedHandler(ICommandContext context, List<(CommandHandlerEntry Handler, List<string> Args)> looselyCompatibleHandlers, string commandName, string logText)
    {
        Task<bool> tryRunHandlerTask = Task.Run(async () =>
        {
            List<(CommandHandlerEntry handler, ConvertResult[][] conversions)> failedHandlers = [];
            foreach ((CommandHandlerEntry handler, List<string> args) in looselyCompatibleHandlers)
            {
                ConvertResult[][] values = await TryConvertStringParamsToObjectParams(registry, args, handler.ParameterTypes);
                if (values.Any(v => !v.LastOrDefault().Success))
                {
                    failedHandlers.Add((handler, values));
                    continue;
                }

                RunHandler(handler, [context, ..values.Select(v => v.LastOrDefault().Value).ToArray()], logText);
                return true;
            }

            logger.ZLogInformation($"Command '{logText:@Command}' failed to match to any command handlers.{Environment.NewLine}{GetErrorMessagesFromFailedHandlers(failedHandlers)}");
            return false;
        });
        runningCommands.Writer.TryWrite(tryRunHandlerTask);
        return tryRunHandlerTask;

        static async Task<ConvertResult[][]> TryConvertStringParamsToObjectParams(CommandRegistryService registry, List<string> args, Type[] parameterTypes)
        {
            List<ConvertResult[]> result = null;
            for (int i = 0; i < parameterTypes.Length; i++)
            {
                ConvertResult[] conversions;
                if (registry.TryParseToType(args[i], parameterTypes[i]) is { } parsedValue)
                {
                    conversions = [ConvertResult.Ok(parsedValue)];
                }
                else
                {
                    conversions = [ConvertResult.Fail($"Failed to parse {args[i]} to a {parameterTypes[i].Name}")];
                }
                if (conversions is [] || !conversions[0].Success)
                {
                    conversions = [..conversions, ..await registry.TryConvertToType(args[i], parameterTypes[i])];
                }

                result ??= [];
                result.Add(conversions);
            }
            return result?.ToArray() ?? [];
        }

        static string GetErrorMessagesFromFailedHandlers(List<(CommandHandlerEntry handler, ConvertResult[][] conversions)> failedHandlers)
        {
            StringBuilder sb = new();
            int indent = 0;
            foreach ((CommandHandlerEntry handler, ConvertResult[][] conversions) handlerResult in failedHandlers)
            {
                for (int argIndex = 0; argIndex < handlerResult.conversions.Length; argIndex++)
                {
                    string[] messages = handlerResult.conversions[argIndex].Where(c => c is { Success: false, Value: string }).Select(c => c.Value).OfType<string>().ToArray();
                    if (messages is [])
                    {
                        continue;
                    }

                    sb.Append("Arg ")
                      .Append(argIndex.ToString())
                      .AppendLine(": ");
                    indent++;
                    for (int i = 0; i < messages.Length; i++)
                    {
                        sb.Append(GetIndentText()).Append("- ").Append(messages[i]);
                        if (i != messages.Length - 1)
                        {
                            sb.AppendLine();
                        }
                    }
                    indent--;
                    sb.AppendLine();
                }
            }
            return sb.ToString();

            string GetIndentText() => new(' ', indent * 4);
        }
    }

    private void RunHandler(CommandHandlerEntry handler, Span<object> args, string inputText)
    {
        ILogger? commandLogger = NullLogger.Instance;
        if (args.Length > 0 && args[0] is ICommandContext context)
        {
            commandLogger = context.Logger = loggerFactory.CreateLogger(handler.Owner.GetType());
        }

        try
        {
            if (!runningCommands.Writer.TryWrite(RunHandlerWithExceptionLoggingAsync(commandLogger, handler, args.ToArray(), inputText)))
            {
                logger.ZLogError($"Failed to track command task");
            }
        }
        catch (Exception ex)
        {
            logger.ZLogError(ex, $"Error occurred while executing command {inputText:@Command}");
        }

        static async Task RunHandlerWithExceptionLoggingAsync(ILogger logger, CommandHandlerEntry handler, object[] args, string inputText)
        {
            try
            {
                await handler.InvokeAsync(args);
            }
            catch (Exception ex)
            {
                logger.ZLogError(ex, $"Error occurred while executing command '{inputText:@Command}'");
            }
        }
    }
}
