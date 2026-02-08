// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Metadata;

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
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
		CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(serverUrl);
		Ensure.NotNull(owner);
		Ensure.NotNull(repository);
		Ensure.NotNull(outputPath);
		Ensure.NotNull(lineEnding);

		string template = GetTemplate();
		int year = DateTime.UtcNow.Year;

		// Build project URL
		string projectUrl = $"{serverUrl}/{repository}";

		// Build copyright string
		string copyright = $"Copyright (c) 2023-{year} {owner} contributors";

		// Replace placeholders
		string licenseContent = template
			.Replace("{PROJECT_URL}", projectUrl)
			.Replace("{COPYRIGHT}", copyright);

		// Write LICENSE.md
		string licensePath = Path.Combine(outputPath, "LICENSE.md");
		await LineEndingHelper.WriteFileAsync(licensePath, licenseContent, lineEnding, cancellationToken).ConfigureAwait(false);

		// Write COPYRIGHT.md
		string copyrightPath = Path.Combine(outputPath, "COPYRIGHT.md");
		await LineEndingHelper.WriteFileAsync(copyrightPath, copyright + lineEnding, lineEnding, cancellationToken).ConfigureAwait(false);
	}

	private static string GetTemplate()
	{
		Assembly assembly = Assembly.GetExecutingAssembly();

		// Try to find the embedded resource
		string[] resourceNames = assembly.GetManifestResourceNames();
		string? resourceName = resourceNames.FirstOrDefault(n => n.EndsWith("LICENSE.template", StringComparison.OrdinalIgnoreCase));

		if (resourceName is null)
		{
			// Fallback to default MIT license template
			return GetDefaultTemplate();
		}

		using Stream? stream = assembly.GetManifestResourceStream(resourceName);
		if (stream is null)
		{
			return GetDefaultTemplate();
		}

		using StreamReader reader = new(stream);
		return reader.ReadToEnd();
	}

	private static string GetDefaultTemplate() => """
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
