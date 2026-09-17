// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Tests.Mocks;

using System.Collections.ObjectModel;
using KtsuBuild.Abstractions;

/// <summary>
/// An implementation of <see cref="IBuildLogger"/> that keeps what was written, for tests whose
/// subject is the message rather than the exit code.
/// </summary>
/// <remarks>
/// A command handler's only observable output, besides its exit code, is what it reports. A skipped
/// project that is never named reads exactly like a project that ran, so the wording of those
/// reports is behaviour a test should be able to assert on. <see cref="MockBuildLogger"/> discards
/// it, which is the right default everywhere the messages are incidental.
/// </remarks>
public sealed class RecordingBuildLogger : IBuildLogger
{
	/// <inheritdoc />
	public bool VerboseEnabled { get; set; }

	/// <summary>Gets the messages written as errors.</summary>
	public Collection<string> Errors { get; } = [];

	/// <summary>Gets the messages written as information.</summary>
	public Collection<string> Infos { get; } = [];

	/// <summary>Gets the messages written as step headers.</summary>
	public Collection<string> StepHeaders { get; } = [];

	/// <summary>Gets the messages written as successes.</summary>
	public Collection<string> Successes { get; } = [];

	/// <summary>Gets the messages written as verbose output.</summary>
	public Collection<string> Verboses { get; } = [];

	/// <summary>Gets the messages written as warnings.</summary>
	public Collection<string> Warnings { get; } = [];

	/// <inheritdoc />
	public void WriteError(string message) => Errors.Add(message);

	/// <inheritdoc />
	public void WriteInfo(string message) => Infos.Add(message);

	/// <inheritdoc />
	public void WriteStepHeader(string message) => StepHeaders.Add(message);

	/// <inheritdoc />
	public void WriteSuccess(string message) => Successes.Add(message);

	/// <inheritdoc />
	public void WriteVerbose(string message) => Verboses.Add(message);

	/// <inheritdoc />
	public void WriteWarning(string message) => Warnings.Add(message);

	/// <summary>
	/// Determines whether any message of the given kind contains the supplied text.
	/// </summary>
	/// <param name="messages">The recorded messages to search.</param>
	/// <param name="text">The text to look for.</param>
	/// <returns>True when at least one message contains the text.</returns>
	public static bool Any(IEnumerable<string> messages, string text)
	{
		ArgumentNullException.ThrowIfNull(messages);
		return messages.Any(m => m.Contains(text, StringComparison.Ordinal));
	}
}
