// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Profile;

using System.Globalization;

/// <summary>
/// Compares version strings using semantic versioning 2.0.0 precedence rules.
/// </summary>
/// <remarks>
/// Used to decide whether a prerelease has actually superseded the current stable version. A
/// prerelease at or below the stable version is stale and should not be advertised.
/// </remarks>
public static class SemanticVersion
{
	/// <summary>
	/// Determines whether one version is strictly newer than another.
	/// </summary>
	/// <param name="version">The candidate version, with or without a leading <c>v</c>.</param>
	/// <param name="reference">The version to compare against, with or without a leading <c>v</c>.</param>
	/// <returns>
	/// <see langword="true"/> when <paramref name="version"/> is strictly greater. A null or empty
	/// <paramref name="version"/> is never greater, and any parseable version is greater than a null
	/// or empty <paramref name="reference"/>. Two unparseable versions compare as not greater.
	/// </returns>
	public static bool IsGreater(string? version, string? reference)
	{
		if (string.IsNullOrWhiteSpace(version))
		{
			return false;
		}

		if (string.IsNullOrWhiteSpace(reference))
		{
			return true;
		}

		return Compare(StripPrefix(version), StripPrefix(reference)) > 0;
	}

	private static string StripPrefix(string value) =>
		value.StartsWith('v') || value.StartsWith('V') ? value[1..] : value;

	/// <summary>
	/// Compares two version strings by semantic versioning precedence.
	/// </summary>
	/// <param name="left">The first version, without a leading <c>v</c>.</param>
	/// <param name="right">The second version, without a leading <c>v</c>.</param>
	/// <returns>A negative number, zero, or a positive number as <paramref name="left"/> sorts before,
	/// equal to, or after <paramref name="right"/>. Returns zero when either side cannot be parsed.</returns>
	private static int Compare(string left, string right)
	{
		if (!TryParse(left, out int[]? leftCore, out string? leftPre) ||
			!TryParse(right, out int[]? rightCore, out string? rightPre))
		{
			return 0;
		}

		for (int i = 0; i < 3; i++)
		{
			int coreComparison = leftCore[i].CompareTo(rightCore[i]);
			if (coreComparison != 0)
			{
				return coreComparison;
			}
		}

		// A version without a prerelease label outranks the same core version with one.
		bool leftHasPre = leftPre is not null;
		bool rightHasPre = rightPre is not null;
		if (leftHasPre != rightHasPre)
		{
			return leftHasPre ? -1 : 1;
		}

		return leftHasPre ? ComparePrerelease(leftPre!, rightPre!) : 0;
	}

	/// <summary>
	/// Parses the numeric core and prerelease label out of a version string.
	/// </summary>
	/// <param name="value">The version string to parse.</param>
	/// <param name="core">Receives the major, minor, and patch numbers. A missing minor or patch reads as zero.</param>
	/// <param name="prerelease">Receives the prerelease label, or <see langword="null"/> when there is none.</param>
	/// <returns><see langword="true"/> when the version parsed.</returns>
	private static bool TryParse(string value, out int[] core, out string? prerelease)
	{
		core = [0, 0, 0];
		prerelease = null;

		// Build metadata after '+' takes no part in precedence.
		int buildIndex = value.IndexOf('+', StringComparison.Ordinal);
		if (buildIndex >= 0)
		{
			value = value[..buildIndex];
		}

		int prereleaseIndex = value.IndexOf('-', StringComparison.Ordinal);
		if (prereleaseIndex >= 0)
		{
			prerelease = value[(prereleaseIndex + 1)..];
			value = value[..prereleaseIndex];
		}

		string[] parts = value.Split('.');
		if (parts.Length is 0 or > 3)
		{
			return false;
		}

		for (int i = 0; i < parts.Length; i++)
		{
			if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out int part))
			{
				return false;
			}

			core[i] = part;
		}

		return true;
	}

	/// <summary>
	/// Compares two prerelease labels by dot-separated identifier, per semantic versioning rules.
	/// </summary>
	/// <param name="left">The first prerelease label.</param>
	/// <param name="right">The second prerelease label.</param>
	/// <returns>A negative number, zero, or a positive number as <paramref name="left"/> sorts before,
	/// equal to, or after <paramref name="right"/>.</returns>
	private static int ComparePrerelease(string left, string right)
	{
		string[] leftParts = left.Split('.');
		string[] rightParts = right.Split('.');
		int shared = Math.Min(leftParts.Length, rightParts.Length);

		for (int i = 0; i < shared; i++)
		{
			bool leftNumeric = int.TryParse(leftParts[i], NumberStyles.None, CultureInfo.InvariantCulture, out int leftNumber);
			bool rightNumeric = int.TryParse(rightParts[i], NumberStyles.None, CultureInfo.InvariantCulture, out int rightNumber);

			if (leftNumeric && rightNumeric)
			{
				int numberComparison = leftNumber.CompareTo(rightNumber);
				if (numberComparison != 0)
				{
					return numberComparison;
				}

				continue;
			}

			// Numeric identifiers always sort before alphanumeric ones.
			if (leftNumeric != rightNumeric)
			{
				return leftNumeric ? -1 : 1;
			}

			int textComparison = string.CompareOrdinal(leftParts[i], rightParts[i]);
			if (textComparison != 0)
			{
				return textComparison;
			}
		}

		// All shared identifiers matched, so the label with more identifiers sorts higher.
		return leftParts.Length.CompareTo(rightParts.Length);
	}
}
