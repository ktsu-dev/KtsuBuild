// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Pipeline;

using KtsuBuild.Configuration;
using KtsuBuild.Git;

/// <summary>
/// The state a pipeline run threads between its stages.
/// </summary>
/// <remarks>
/// Stages that only need a workspace and a configuration take them as parameters instead, so a
/// caller such as <c>build</c> can run them without a git repository or a GitHub token.
/// </remarks>
public sealed class PipelineContext
{
	/// <summary>
	/// Gets or sets the configuration this run was prepared with.
	/// </summary>
	public required BuildConfiguration Configuration { get; set; }

	/// <summary>
	/// Gets or sets the version information resolved for this run, or <see langword="null"/> until
	/// it has been resolved.
	/// </summary>
	/// <remarks>
	/// Preparation deliberately leaves this null. Resolving the version is a separate stage because
	/// <c>ci</c> resolves it against the metadata commit, which does not exist yet when a run is
	/// prepared. Null rather than a default instance so a caller that skips the resolving stage
	/// fails loudly instead of reading a version nobody chose, which is the shape of the defect
	/// this whole change exists to remove.
	/// </remarks>
	public VersionInfo? VersionInfo { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether a stage has deliberately established the version this
	/// run publishes.
	/// </summary>
	/// <remarks>
	/// A fresh configuration carries the placeholder version <see cref="BuildConfigurationProvider"/>
	/// seeds, and publishing that placeholder is the defect this whole change exists to remove. The
	/// flag records that a stage chose the version on purpose. It deliberately says nothing about
	/// which value was chosen, because a genuinely new repository's first release can legitimately be
	/// the same string the placeholder uses, so a guard on the value would refuse a correct release
	/// and still pass a wrong one whose placeholder happened to differ.
	/// <para>
	/// <see cref="PipelineService.UpdateMetadataAsync"/> sets it, because that is where <c>ci</c> gets
	/// its version. A caller with no metadata stage calls
	/// <see cref="PipelineService.ApplyResolvedVersion"/> instead. Releasing without either throws.
	/// </para>
	/// </remarks>
	public bool VersionEstablished { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the version increment suppressed the release.
	/// </summary>
	/// <remarks>
	/// This is how <c>[skip ci]</c> and a run with no meaningful changes behave. It suppresses the
	/// release only. Build and test still run, so a workspace whose commits all carry the skip
	/// marker is still compiled and tested.
	/// </remarks>
	public bool ReleaseSuppressedByVersionGate { get; set; }
}
