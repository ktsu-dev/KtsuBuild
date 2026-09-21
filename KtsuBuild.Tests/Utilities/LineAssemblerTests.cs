// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Tests.Utilities;

using KtsuBuild.Utilities;

/// <summary>
/// Tests for the line assembler that <c>ProcessRunner</c> uses to turn stream chunks into lines.
/// </summary>
[TestClass]
public class LineAssemblerTests
{
	/// <summary>Feeds the chunks through an assembler and returns the lines it emitted.</summary>
	private static List<string> Assemble(params string[] chunks)
	{
		List<string> lines = [];
		LineAssembler assembler = new(lines.Add);
		foreach (string chunk in chunks)
		{
			assembler.Append(chunk);
		}

		assembler.Flush();
		return lines;
	}

	/// <summary>Asserts the chunks assemble into exactly <paramref name="expected"/>.</summary>
	private static void AssertLines(string[] chunks, params string[] expected) =>
		CollectionAssert.AreEqual(expected, Assemble(chunks));

	[TestMethod]
	public void Append_TerminatedLines_AreEmittedWithoutTheTerminator() =>
		AssertLines(["one\ntwo\n"], "one", "two");

	[TestMethod]
	public void Append_UnterminatedFinalLine_IsEmittedOnFlush() =>
		// The case RunCommand's own line handler loses entirely.
		AssertLines(["one\ntwo"], "one", "two");

	[TestMethod]
	public void Append_NothingWritten_EmitsNothing() =>
		AssertLines([]);

	[TestMethod]
	public void Append_TrailingNewline_DoesNotEmitAnEmptyFinalLine() =>
		AssertLines(["only\n"], "only");

	[TestMethod]
	public void Append_BlankLines_ArePreserved() =>
		AssertLines(["a\n\nb\n"], "a", "", "b");

	[TestMethod]
	public void Append_CarriageReturnNewline_IsOneEnding() =>
		AssertLines(["a\r\nb\r\n"], "a", "b");

	[TestMethod]
	public void Append_LoneCarriageReturn_EndsALine() =>
		// Progress output overwrites itself with \r, and each rewrite is its own line.
		AssertLines(["50%\r100%\r"], "50%", "100%");

	[TestMethod]
	public void Append_CarriageReturnNewlineSplitAcrossChunks_IsStillOneEnding() =>
		AssertLines(["a\r", "\nb\r\n"], "a", "b");

	[TestMethod]
	public void Append_LineSplitAcrossChunks_IsJoined() =>
		AssertLines(["par", "tial", " line\n"], "partial line");

	[TestMethod]
	public void Append_TrailingCarriageReturn_EndsTheLineOnFlush() =>
		AssertLines(["done\r"], "done");

	[TestMethod]
	public void Append_NullCallback_DoesNotThrow()
	{
		LineAssembler assembler = new(null);
		assembler.Append("anything\nat all");
		assembler.Flush();
	}
}
