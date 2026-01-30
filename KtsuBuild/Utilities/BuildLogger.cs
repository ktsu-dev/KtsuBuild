// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Utilities;

using KtsuBuild.Abstractions;
using Microsoft.Extensions.Logging;

/// <summary>
/// Implementation of build logger with colored console output.
/// </summary>
public class BuildLogger : IBuildLogger
{
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
		_logger = logger;
	}

	/// <inheritdoc/>
	public bool VerboseEnabled { get; set; }

	/// <inheritdoc/>
	public void WriteStepHeader(string message)
	{
		var header = $"\n=== {message} ===\n";
		WriteColored(header, ConsoleColor.Cyan);
		_logger?.LogInformation("{Message}", header);
	}

	/// <inheritdoc/>
	public void WriteInfo(string message)
	{
		Console.WriteLine(message);
		_logger?.LogInformation("{Message}", message);
	}

	/// <inheritdoc/>
	public void WriteWarning(string message)
	{
		WriteColored(message, ConsoleColor.Yellow);
		_logger?.LogWarning("{Message}", message);
	}

	/// <inheritdoc/>
	public void WriteError(string message)
	{
		WriteColored(message, ConsoleColor.Red);
		_logger?.LogError("{Message}", message);
	}

	/// <inheritdoc/>
	public void WriteSuccess(string message)
	{
		WriteColored(message, ConsoleColor.Green);
		_logger?.LogInformation("{Message}", message);
	}

	/// <inheritdoc/>
	public void WriteVerbose(string message)
	{
		if (VerboseEnabled)
		{
			WriteColored(message, ConsoleColor.DarkGray);
			_logger?.LogDebug("{Message}", message);
		}
	}

	private static void WriteColored(string message, ConsoleColor color)
	{
		var originalColor = Console.ForegroundColor;
		try
		{
			Console.ForegroundColor = color;
			Console.WriteLine(message);
		}
		finally
		{
			Console.ForegroundColor = originalColor;
		}
	}
}
