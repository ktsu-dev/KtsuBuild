// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Utilities;

/// <summary>
/// Initializes environment variables for .NET build operations.
/// </summary>
public static class BuildEnvironment
{
	/// <summary>
	/// Sets environment variables to suppress .NET SDK telemetry, first-run experience, and logos.
	/// </summary>
	public static void Initialize()
	{
		Environment.SetEnvironmentVariable("DOTNET_SKIP_FIRST_TIME_EXPERIENCE", "1");
		Environment.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "1");
		Environment.SetEnvironmentVariable("DOTNET_NOLOGO", "true");
	}
}
