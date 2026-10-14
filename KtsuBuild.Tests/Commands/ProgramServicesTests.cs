// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Tests.Commands;

using KtsuBuild.Abstractions;
using KtsuBuild.Tool;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Resolves the logger from the tool's own service configuration, the one every command runs with.
/// </summary>
/// <remarks>
/// Every other test constructs <see cref="KtsuBuild.Utilities.BuildLogger"/> directly, which never
/// reaches the constructor dependency injection picks. That is how a second console sink went
/// unnoticed: it printed every line twice, the second copy out of order.
/// </remarks>
[TestClass]
[DoNotParallelize]
public class ProgramServicesTests
{
	[TestMethod]
	public void ConfiguredLogger_WritesEachMessageOnce()
	{
		const string Marker = "ktsubuild-log-marker-183";
		TextWriter originalOut = Console.Out;
		using StringWriter captured = new();
		Console.SetOut(captured);
		try
		{
			ServiceCollection services = new();
			Program.ConfigureServices(services);

			// Disposing the provider drains any background logging queue, so a deferred copy
			// lands in the capture before it is read.
			using (ServiceProvider provider = services.BuildServiceProvider())
			{
				IBuildLogger logger = provider.GetRequiredService<IBuildLogger>();
				logger.WriteInfo(Marker);
			}
		}
		finally
		{
			Console.SetOut(originalOut);
		}

		string output = captured.ToString();
		int occurrences = output.Split(Marker).Length - 1;
		Assert.AreEqual(1, occurrences, $"Expected the message once, got:\n{output}");
	}
}
