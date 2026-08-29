// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Profile;

using System.Web;
#if !NET10_0_OR_GREATER
using static Polyfill;
#endif

/// <summary>
/// Builds shields.io badge URLs.
/// </summary>
public static class BadgeBuilder
{
	/// <summary>
	/// Builds a static shields.io badge URL.
	/// </summary>
	/// <param name="label">The left half of the badge. Pass an empty string for a badge with no label.</param>
	/// <param name="message">The right half of the badge.</param>
	/// <param name="color">The badge color as a hex string without a leading <c>#</c>.</param>
	/// <param name="logo">The shields.io logo slug, such as <c>github</c>. Omit for no logo.</param>
	/// <param name="logoColor">The logo color. Ignored when <paramref name="logo"/> is empty.</param>
	/// <returns>The badge URL.</returns>
	/// <remarks>
	/// shields.io treats a hyphen as the delimiter between label, message, and color, so literal
	/// hyphens are escaped by doubling them before the segments are URL encoded.
	/// </remarks>
	public static string Build(string label, string message, string color, string logo = "", string logoColor = "white")
	{
		Ensure.NotNull(label);
		Ensure.NotNull(message);

		string encodedLabel = Encode(label);
		string encodedMessage = Encode(message);

		string url = $"https://img.shields.io/badge/{encodedLabel}-{encodedMessage}-{color}";
		return string.IsNullOrEmpty(logo) ? url : $"{url}?logo={logo}&logoColor={logoColor}";
	}

	private static string Encode(string segment) =>
		HttpUtility.UrlEncode(segment.Replace("-", "--", StringComparison.Ordinal)) ?? string.Empty;
}

/// <summary>
/// The badge colors used across the organization profile, so every table reads consistently.
/// </summary>
public static class BadgeColors
{
	/// <summary>Gets the NuGet brand blue.</summary>
	public static string NuGet => "004880";

	/// <summary>Gets the GitHub brand dark.</summary>
	public static string GitHub => "181717";

	/// <summary>Gets the Windows brand blue, used for winget.</summary>
	public static string Winget => "0078D4";

	/// <summary>Gets the color for a passing state.</summary>
	public static string Success => "2ea44f";

	/// <summary>Gets the color for a failing state.</summary>
	public static string Failure => "d73a4a";

	/// <summary>Gets the color for a cancelled state.</summary>
	public static string Cancelled => "6e7681";

	/// <summary>Gets the color for an unknown or in-progress state.</summary>
	public static string Warning => "dbab09";

	/// <summary>Gets the color for a .NET tool.</summary>
	public static string Tool => "512BD4";

	/// <summary>Gets the color for a windowed application.</summary>
	public static string App => "68217A";

	/// <summary>Gets the color for a command line program.</summary>
	public static string ConsoleApp => "3B3B3B";
}
