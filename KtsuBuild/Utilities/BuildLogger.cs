// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Utilities;

using System.Diagnostics.CodeAnalysis;
using KtsuBuild.Abstractions;
using Microsoft.Extensions.Logging;
#if !NET10_0_OR_GREATER
using static Polyfill;
#endif

/// <summary>
/// Implementation of build logger with colored console output.
/// </summary>
[SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Justification = "This is a simple utility logger; LoggerMessage delegates add unnecessary complexity")]
[SuppressMessage("Performance", "CA1873:Use conditional log level evaluation", Justification = "Parameters are already simple strings; IsEnabled guards add unnecessary complexity")]
public class BuildLogger : IBuildLogger
{
	/// <summary>
	/// Structured logging template for a single message parameter.
	/// </summary>
	private const string MessageTemplate = "{Message}";

	private readonly ILogger<BuildLogger>? _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="BuildLogger"/> class.
	/// </summary>
	public BuildLogger()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="BuildLogger"/> class with a logger.
	/// </summary>
	/// <param name="logger">The logger instance.</param>
	public BuildLogger(ILogger<BuildLogger> logger)
	{
		Ensure.NotNull(logger);
		_logger = logger;
	}

	/// <inheritdoc/>
	public bool VerboseEnabled { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether messages go to standard error rather than standard
	/// output. A command whose standard output is its result, such as <c>version bump</c>, sets
	/// this so a script capturing that output gets the result alone.
	/// </summary>
	public bool UseStandardError { get; set; }

	private TextWriter Output => UseStandardError ? Console.Error : Console.Out;

	/// <inheritdoc/>
	public void WriteStepHeader(string message)
	{
		Ensure.NotNull(message);
		string header = $"\n=== {message} ===\n";
		WriteColored(header, ConsoleColor.Cyan);
		_logger?.LogInformation(MessageTemplate, header);
	}

	/// <inheritdoc/>
	public void WriteInfo(string message)
	{
		Ensure.NotNull(message);
		Output.WriteLine(message);
		_logger?.LogInformation(MessageTemplate, message);
	}

	/// <inheritdoc/>
	public void WriteWarning(string message)
	{
		Ensure.NotNull(message);
		WriteColored(message, ConsoleColor.Yellow);
		_logger?.LogWarning(MessageTemplate, message);
	}

	/// <inheritdoc/>
	public void WriteError(string message)
	{
		Ensure.NotNull(message);
		WriteColored(message, ConsoleColor.Red);
		_logger?.LogError(MessageTemplate, message);
	}

	/// <inheritdoc/>
	public void WriteSuccess(string message)
	{
		Ensure.NotNull(message);
		WriteColored(message, ConsoleColor.Green);
		_logger?.LogInformation(MessageTemplate, message);
	}

	/// <inheritdoc/>
	public void WriteVerbose(string message)
	{
		Ensure.NotNull(message);
		if (VerboseEnabled)
		{
			WriteColored(message, ConsoleColor.DarkGray);
			_logger?.LogDebug(MessageTemplate, message);
		}
	}

	private void WriteColored(string message, ConsoleColor color)
	{
		ConsoleColor originalColor = Console.ForegroundColor;
		try
		{
			Console.ForegroundColor = color;
			Output.WriteLine(message);
		}
		finally
		{
			Console.ForegroundColor = originalColor;
		}
	}
}
