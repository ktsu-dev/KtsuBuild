// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Utilities;

using System.Text;
#if !NET10_0_OR_GREATER
using static Polyfill;
#endif

/// <summary>
/// Writes step outputs to the file GitHub Actions names in <c>GITHUB_OUTPUT</c>, so later steps
/// in the same job can branch on what the pipeline actually did instead of re-deriving it from
/// git state. Outside GitHub Actions the variable is unset and <see cref="Write"/> does nothing,
/// which keeps local runs of the same commands free of side effects.
/// </summary>
public static class GitHubActionsOutput
{
	/// <summary>
	/// The environment variable GitHub Actions points at the running step's output file.
	/// </summary>
	private const string OutputPathVariable = "GITHUB_OUTPUT";

	/// <summary>
	/// Appends outputs to the file named by <c>GITHUB_OUTPUT</c>, or does nothing when that
	/// variable is unset.
	/// </summary>
	/// <param name="outputs">The outputs to write, in the order they should appear.</param>
	/// <exception cref="ArgumentException">A value spans more than one line.</exception>
	public static void Write(IEnumerable<KeyValuePair<string, string>> outputs)
	{
		Ensure.NotNull(outputs);

		string? path = Environment.GetEnvironmentVariable(OutputPathVariable);
		if (string.IsNullOrEmpty(path))
		{
			return;
		}

		WriteTo(path, outputs);
	}

	/// <summary>
	/// Appends outputs to a specific output file.
	/// </summary>
	/// <param name="path">The output file to append to.</param>
	/// <param name="outputs">The outputs to write, in the order they should appear.</param>
	/// <exception cref="ArgumentException">A value spans more than one line.</exception>
	public static void WriteTo(string path, IEnumerable<KeyValuePair<string, string>> outputs)
	{
		Ensure.NotNull(path);
		Ensure.NotNull(outputs);

		// The whole block is built before anything reaches disk. A rejected value part way
		// through must not leave the earlier outputs behind, where a later step would read a
		// partial set as though it were complete.
		StringBuilder builder = new();
		foreach (KeyValuePair<string, string> output in outputs)
		{
			// A newline in a value would be read back as the start of another output, so the
			// value is rejected rather than silently corrupting every output after it. Nothing
			// this command emits is multi-line; the heredoc form GitHub offers for that case is
			// deliberately not implemented until something needs it.
			if (output.Value.Contains('\n') || output.Value.Contains('\r'))
			{
				throw new ArgumentException(
					$"Output '{output.Key}' spans more than one line, which the key=value form cannot represent.",
					nameof(outputs));
			}

			// '\n' rather than the platform newline: the runner parses this file the same way on
			// every platform, and a fixed separator keeps the written bytes predictable.
			builder.Append(output.Key).Append('=').Append(output.Value).Append('\n');
		}

		File.AppendAllText(path, builder.ToString());
	}
}
