// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Profile;

using System.Text.Json;

/// <summary>
/// Reads the pieces of a <c>global.json</c> the profile needs.
/// </summary>
public static class GlobalJson
{
	/// <summary>
	/// Reads the version an MSBuild SDK is pinned to.
	/// </summary>
	/// <param name="content">The contents of a <c>global.json</c>, or <see langword="null"/> when the
	/// repository has none.</param>
	/// <param name="sdkName">The SDK to look up, such as <c>ktsu.Sdk</c>.</param>
	/// <returns>The pinned version, or <see langword="null"/> when the file is missing, unparseable, or
	/// does not pin that SDK.</returns>
	/// <remarks>
	/// Only the base SDK entry is read. A <c>global.json</c> normally pins every variant to the same
	/// version whether or not the repository uses them, so the variants say nothing extra.
	/// </remarks>
	public static string? TryGetMsBuildSdkVersion(string? content, string sdkName)
	{
		if (string.IsNullOrWhiteSpace(content) || string.IsNullOrEmpty(sdkName))
		{
			return null;
		}

		try
		{
			using JsonDocument document = JsonDocument.Parse(content, new JsonDocumentOptions
			{
				CommentHandling = JsonCommentHandling.Skip,
				AllowTrailingCommas = true,
			});

			return document.RootElement.ValueKind == JsonValueKind.Object &&
				document.RootElement.TryGetProperty("msbuild-sdks", out JsonElement sdks) &&
				sdks.ValueKind == JsonValueKind.Object &&
				sdks.TryGetProperty(sdkName, out JsonElement version) &&
				version.ValueKind == JsonValueKind.String
					? version.GetString()
					: null;
		}
		catch (JsonException)
		{
			return null;
		}
	}
}
