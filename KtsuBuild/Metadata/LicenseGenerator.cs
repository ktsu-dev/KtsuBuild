// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Metadata;

using System.Diagnostics.CodeAnalysis;
using KtsuBuild.Utilities;
#if !NET10_0_OR_GREATER
using static Polyfill;
#endif

/// <summary>
/// Generates license and copyright files.
/// </summary>
public static class LicenseGenerator
{
	/// <summary>
	/// Generates LICENSE.md and COPYRIGHT.md files.
	/// </summary>
	/// <remarks>
	/// An existing COPYRIGHT.md is kept as it is, and its text is what LICENSE.md carries. ktsu.Sdk derives
	/// every source file's required header from COPYRIGHT.md, so rewriting it from the clock would fail
	/// every file in the repository against IDE0073 the first time a run lands in a new year. Moving the
	/// year on is a deliberate change that also rewrites the headers, not a side effect of a build.
	/// </remarks>
	/// <param name="serverUrl">The GitHub server URL.</param>
	/// <param name="owner">The repository owner.</param>
	/// <param name="repository">The repository name.</param>
	/// <param name="outputPath">The output directory.</param>
	/// <param name="lineEnding">The line ending to use.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	[SuppressMessage("Microsoft.Design", "CA1054:UriParametersShouldNotBeStrings", Justification = "String URLs are simpler for CLI tool configuration")]
	public static async Task GenerateAsync(
		string serverUrl,
		string owner,
		string repository,
		string outputPath,
		string lineEnding,
		CancellationToken cancellationToken = default) =>
		await GenerateAsync(serverUrl, owner, repository, outputPath, lineEnding, DateTime.UtcNow.Year, cancellationToken).ConfigureAwait(false);

	/// <summary>
	/// Generates LICENSE.md and COPYRIGHT.md files, taking the current year from the caller.
	/// </summary>
	/// <param name="serverUrl">The GitHub server URL.</param>
	/// <param name="owner">The repository owner.</param>
	/// <param name="repository">The repository name.</param>
	/// <param name="outputPath">The output directory.</param>
	/// <param name="lineEnding">The line ending to use.</param>
	/// <param name="currentYear">The year a newly created COPYRIGHT.md runs to.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	internal static async Task GenerateAsync(
		string serverUrl,
		string owner,
		string repository,
		string outputPath,
		string lineEnding,
		int currentYear,
		CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(serverUrl);
		Ensure.NotNull(owner);
		Ensure.NotNull(repository);
		Ensure.NotNull(outputPath);
		Ensure.NotNull(lineEnding);

		string template = LicenseTemplate;

		// Build project URL
		string projectUrl = $"{serverUrl}/{repository}";

		// Keep an existing copyright line, and make one up only for a repository that has none
		string copyrightPath = Path.Combine(outputPath, "COPYRIGHT.md");
		string? existingCopyright = File.Exists(copyrightPath)
			? (await File.ReadAllTextAsync(copyrightPath, cancellationToken).ConfigureAwait(false)).Trim()
			: null;
		string copyright = string.IsNullOrEmpty(existingCopyright)
			? $"Copyright (c) 2023-{currentYear} {owner} contributors"
			: existingCopyright!;

		// Replace placeholders
		string licenseContent = template
			.Replace("{PROJECT_URL}", projectUrl)
			.Replace("{COPYRIGHT}", copyright);

		// Write LICENSE.md
		string licensePath = Path.Combine(outputPath, "LICENSE.md");
		await LineEndingHelper.WriteFileAsync(licensePath, licenseContent, lineEnding, cancellationToken).ConfigureAwait(false);

		// Write COPYRIGHT.md
		await LineEndingHelper.WriteFileAsync(copyrightPath, copyright + lineEnding, lineEnding, cancellationToken).ConfigureAwait(false);
	}

	private const string LicenseTemplate = """
		MIT License

		{PROJECT_URL}

		{COPYRIGHT}

		Permission is hereby granted, free of charge, to any person obtaining a copy
		of this software and associated documentation files (the "Software"), to deal
		in the Software without restriction, including without limitation the rights
		to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
		copies of the Software, and to permit persons to whom the Software is
		furnished to do so, subject to the following conditions:

		The above copyright notice and this permission notice shall be included in all
		copies or substantial portions of the Software.

		THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
		IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
		FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
		AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
		LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
		OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
		SOFTWARE.
		""";
}
