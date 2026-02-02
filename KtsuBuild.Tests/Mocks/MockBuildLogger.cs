// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Tests.Mocks;

using KtsuBuild.Abstractions;

/// <summary>
/// A no-op implementation of IBuildLogger for use in unit tests.
/// </summary>
public class MockBuildLogger : IBuildLogger
{
	/// <inheritdoc />
	public bool VerboseEnabled { get; set; }

	/// <inheritdoc />
	public void WriteError(string message)
	{
	}

	/// <inheritdoc />
	public void WriteInfo(string message)
	{
	}

	/// <inheritdoc />
	public void WriteStepHeader(string message)
	{
	}

	/// <inheritdoc />
	public void WriteSuccess(string message)
	{
	}

	/// <inheritdoc />
	public void WriteVerbose(string message)
	{
	}

	/// <inheritdoc />
	public void WriteWarning(string message)
	{
	}
}
