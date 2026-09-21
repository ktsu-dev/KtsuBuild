// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Utilities;

using System.Collections.Generic;
using System.Text;

/// <summary>
/// Splits a single command-line argument string into the individual arguments it stands for.
/// </summary>
/// <remarks>
/// <para>
/// <c>ktsu.RunCommand</c> takes arguments as a list of unquoted values, while
/// <see cref="Abstractions.IProcessRunner"/>'s string overloads take the one pre-joined string that
/// callers have always passed. Something has to undo the join, and doing it wrong would change what
/// every existing call site runs.
/// </para>
/// <para>
/// The rules are the ones .NET itself applies to <see cref="System.Diagnostics.ProcessStartInfo.Arguments"/>,
/// which are <c>CommandLineToArgvW</c>'s: whitespace separates, double quotes group, and a backslash
/// is only special immediately before a quote — <c>2n</c> backslashes then <c>"</c> give <c>n</c>
/// backslashes and a grouping quote, <c>2n+1</c> give <c>n</c> backslashes and a literal quote, and
/// backslashes before anything else are literal. Inside a quoted run, <c>""</c> is a literal quote.
/// </para>
/// <para>
/// Matching those rules is what makes this safe to drop in. The values produced here are handed to
/// <see cref="System.Diagnostics.ProcessStartInfo.ArgumentList"/>, whose re-quoting is the exact
/// inverse, so a string that used to reach a child process one way still reaches it that way.
/// </para>
/// </remarks>
internal static class CommandLineArguments
{
	/// <summary>
	/// Splits a pre-joined argument string into individual, unquoted arguments.
	/// </summary>
	/// <param name="arguments">The argument string, as passed to the older <c>IProcessRunner</c> overloads.</param>
	/// <returns>
	/// The arguments it denotes, each with its quoting removed. Empty when <paramref name="arguments"/>
	/// is empty or only whitespace.
	/// </returns>
	internal static List<string> Split(string arguments)
	{
		List<string> results = [];
		if (string.IsNullOrEmpty(arguments))
		{
			return results;
		}

		int i = 0;
		while (i < arguments.Length)
		{
			while (i < arguments.Length && (arguments[i] == ' ' || arguments[i] == '\t'))
			{
				i++;
			}

			if (i == arguments.Length)
			{
				break;
			}

			results.Add(NextArgument(arguments, ref i));
		}

		return results;
	}

	/// <summary>
	/// Reads one argument, starting at a non-whitespace character and stopping at the unquoted
	/// whitespace that ends it.
	/// </summary>
	/// <param name="arguments">The whole argument string.</param>
	/// <param name="i">The read position, advanced past the argument that is returned.</param>
	/// <returns>The argument, with its quoting removed.</returns>
	private static string NextArgument(string arguments, ref int i)
	{
		StringBuilder current = new();
		bool inQuotes = false;

		while (i < arguments.Length)
		{
			// A run of backslashes only means anything in terms of what follows it, so it is
			// consumed as a run rather than a character at a time.
			while (i < arguments.Length && arguments[i] == '\\')
			{
				int backslashes = 1;
				while (++i < arguments.Length && arguments[i] == '\\')
				{
					backslashes++;
				}

				if (i < arguments.Length && arguments[i] == '"')
				{
					current.Append('\\', backslashes / 2);
					if (backslashes % 2 != 0)
					{
						// The quote was escaped, so it is content rather than a delimiter.
						current.Append('"');
						i++;
					}
				}
				else
				{
					current.Append('\\', backslashes);
				}
			}

			if (i >= arguments.Length)
			{
				break;
			}

			char c = arguments[i];
			if (c == '"')
			{
				if (inQuotes && i + 1 < arguments.Length && arguments[i + 1] == '"')
				{
					current.Append('"');
					i++;
				}
				else
				{
					inQuotes = !inQuotes;
				}

				i++;
				continue;
			}

			if (!inQuotes && (c == ' ' || c == '\t'))
			{
				break;
			}

			current.Append(c);
			i++;
		}

		return current.ToString();
	}
}
