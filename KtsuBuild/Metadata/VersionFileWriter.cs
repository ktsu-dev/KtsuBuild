// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Metadata;

using KtsuBuild.Utilities;
#if !NET10_0_OR_GREATER
using static Polyfill;
#endif

/// <summary>
/// Writes VERSION.md file.
/// </summary>
public static class VersionFileWriter
{
	/// <summary>
	/// Writes the VERSION.md file.
	/// </summary>
	/// <param name="version">The version string.</param>
	/// <param name="outputPath">The output directory.</param>
	/// <param name="lineEnding">The line ending to use.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public static async Task WriteAsync(string version, string outputPath, string lineEnding, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(version);
		Ensure.NotNull(outputPath);
		Ensure.NotNull(lineEnding);

		string filePath = Path.Combine(outputPath, "VERSION.md");
		string content = version.Trim() + lineEnding;
		await LineEndingHelper.WriteFileAsync(filePath, content, lineEnding, cancellationToken).ConfigureAwait(false);
	}
}
