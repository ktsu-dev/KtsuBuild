// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Utilities;

using System.Text;

/// <summary>
/// Turns the chunks a redirected stream arrives in into whole lines.
/// </summary>
/// <remarks>
/// <para>
/// <c>ktsu.RunCommand</c> offers a line-splitting handler of its own, but it discards a final line
/// that is not newline-terminated — <c>printf 'content'</c> reaches the caller as nothing at all.
/// Callers here are given every line a process wrote, terminated or not, the way
/// <c>Process.OutputDataReceived</c> gave it to them, so the assembly happens here instead.
/// </para>
/// <para>
/// Line endings follow <see cref="System.IO.StreamReader.ReadLine"/>: <c>\n</c>, <c>\r</c> and
/// <c>\r\n</c> all end a line. That keeps carriage-return progress output — which build tools emit
/// plenty of — arriving as it is written rather than accumulating into one enormous line. A
/// <c>\r</c> at the end of a chunk is held until the next chunk arrives, so a <c>\r\n</c> split
/// across a read boundary is still one ending rather than two.
/// </para>
/// </remarks>
/// <param name="onLine">Invoked once per line, or <see langword="null"/> to discard.</param>
internal sealed class LineAssembler(Action<string>? onLine)
{
	private readonly StringBuilder buffer = new();
	private readonly Lock gate = new();
	private bool pendingCarriageReturn;

	/// <summary>
	/// Takes the next chunk read from the stream, emitting any lines it completes.
	/// </summary>
	/// <param name="chunk">The text just read, which may start or end mid-line.</param>
	internal void Append(string chunk)
	{
		if (string.IsNullOrEmpty(chunk))
		{
			return;
		}

		lock (gate)
		{
			foreach (char c in chunk)
			{
				if (pendingCarriageReturn)
				{
					pendingCarriageReturn = false;
					Emit();

					// A \r\n is one ending, so the \n that completes it is not another line.
					if (c == '\n')
					{
						continue;
					}
				}

				switch (c)
				{
					case '\r':
						pendingCarriageReturn = true;
						break;
					case '\n':
						Emit();
						break;
					default:
						buffer.Append(c);
						break;
				}
			}
		}
	}

	/// <summary>
	/// Emits whatever the process wrote without terminating, once there is no more to read.
	/// </summary>
	internal void Flush()
	{
		lock (gate)
		{
			if (pendingCarriageReturn)
			{
				pendingCarriageReturn = false;
				Emit();
			}

			if (buffer.Length > 0)
			{
				Emit();
			}
		}
	}

	/// <summary>Hands the buffered line to the callback and starts the next one.</summary>
	private void Emit()
	{
		string line = buffer.ToString();
		buffer.Clear();
		onLine?.Invoke(line);
	}
}
