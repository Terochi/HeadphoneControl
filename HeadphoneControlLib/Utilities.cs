using System.Diagnostics;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace HeadphoneControlLib;

public static class Utilities
{
	public static Action PressKey(byte virtualKey) => () => simulateKeyPress(virtualKey);
	public static Action Shutdown() => shutdown;
	public static Action ShutdownIn(int seconds) => () => shutdownIn(seconds);

	private static CommandPrecomputed[] commandsPrecomputed = null!;

	private static MMDevice audioDevice = null!;
	private static AudioEndpointVolume volumeEndpoint = null!;
	private static float referenceVolume;
	private static float previousVolume;
	private static List<float> volumeChanges = null!;
	private static DateTime? resetVolumeAt;

	private static DateTime lastInput = DateTime.Now;

	private const int time_window = 500;
	private const int reset_time = 100;

	private const float move_distance = 0.02f;
	private const float percentage_distance = 0.01f;

	private static void setVolume(float volume)
	{
#if DEBUG
		Console.WriteLine($"Setting volume to {volume}");
#endif
		volumeEndpoint.MasterVolumeLevelScalar = referenceVolume = previousVolume = volume;
		volumeEndpoint.Mute = false;
	}

	public static void SetupHeadphoneControl(Command[] commands)
	{
		commandsPrecomputed = new CommandPrecomputed[commands.Length];

		for (int i = 0; i < commands.Length; i++)
		{
			Command command = commands[i];
			VolumeRangePrecomputed[] range = new VolumeRangePrecomputed[command.Thresholds.Length];

#if DEBUG
			Console.Write($"{command.Name} - {{");
#endif

			for (int j = 0; j < command.Thresholds.Length; j++)
			{
				float min = (MathF.Min(command.Thresholds[j].X1, command.Thresholds[j].X2) - 0.5f) * move_distance;
				float max = (MathF.Max(command.Thresholds[j].X1, command.Thresholds[j].X2) + 0.5f) * move_distance;
#if DEBUG
				Console.Write($"{min:0.00} - {max:0.00}, ");
#endif
				range[j] = new VolumeRangePrecomputed(min, max, command.Thresholds[j].Relative);
			}

#if DEBUG
			Console.WriteLine("}");
#endif

			commandsPrecomputed[i] = new CommandPrecomputed(command.Name, command.Action, range);
		}

		audioDevice = new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
		volumeEndpoint = audioDevice.AudioEndpointVolume;

		referenceVolume = volumeEndpoint.MasterVolumeLevelScalar;
		previousVolume = referenceVolume;

		resetVolumeAt = null;

		volumeChanges = new List<float>(32);

		volumeEndpoint.OnVolumeNotification += data =>
		{
			if (resetVolumeAt.HasValue)
			{
				if (DateTime.Now <= resetVolumeAt.Value)
				{
					setVolume(referenceVolume);
					return;
				}

				resetVolumeAt = null;
			}

			removeOldVolumeChanges();
			addVolumeChange(data.MasterVolume);
			recognizeCommands();

#if DEBUG
			Console.WriteLine($"{previousVolume} => {data.MasterVolume}");
#endif

			lastInput = DateTime.Now;
			previousVolume = data.MasterVolume;
		};
	}

	private static void addVolumeChange(float volume)
	{
		volumeChanges.Add(volume);
	}

	private static void removeOldVolumeChanges()
	{
		if (volumeChanges.Count == 0)
		{
			referenceVolume = previousVolume;
			return;
		}

		if ((DateTime.Now - lastInput).TotalMilliseconds > time_window)
		{
			referenceVolume = previousVolume;
			volumeChanges.Clear();
		}
	}

	private static void recognizeCommands()
	{
		var changes = getVolumeChanges();
#if DEBUG
		printChanges(changes);
#endif

		foreach (var command in commandsPrecomputed)
		{
			if (changes.Count != command.Thresholds.Length)
			{
				continue;
			}

			bool match = true;

			for (int i = 0; i < changes.Count; i++)
			{
				if (command.Thresholds[i].Relative &&
					(changes[i].Delta < command.Thresholds[i].Min || changes[i].Delta > command.Thresholds[i].Max))
				{
					match = false;
					break;
				}

				if (!command.Thresholds[i].Relative && (changes[i].To < command.Thresholds[i].Min ||
														changes[i].To > command.Thresholds[i].Max))
				{
					match = false;
					break;
				}
			}

			if (match)
			{
#if DEBUG
				Console.WriteLine("Matched: " + command.Name);
#endif
				resetVolumeAt = DateTime.Now.AddMilliseconds(reset_time);
				setVolume(referenceVolume);
				volumeChanges.Clear();
				command.Action();
				break;
			}
		}
	}

	private static int getVolumePercentage(float change) => (int)MathF.Round(change / percentage_distance);

	private static void printChanges(List<Change> changes)
	{
		if (changes.Count == 0)
			return;

		Change first = changes[0];
		bool firstUp = first.Delta > 0;
		Change last = changes[^1];
		bool lastUp = last.Delta > 0;

		Console.Write(!firstUp
			? $"{getVolumePercentage(first.From):D3}        "
			: "      ");

		foreach (Change change in changes)
		{
			Console.Write(change.Delta > 0 ? $"{getVolumePercentage(change.To):D3}        " : " ");
		}

		Console.WriteLine();
		Console.Write(firstUp ? " " : "‾");

		foreach (Change change in changes)
		{
			Console.Write(change.Delta > 0 ? "    /‾" : "‾‾\\   ");
		}

		Console.WriteLine(lastUp ? "‾‾" : "");
		Console.Write("   ");

		foreach (Change change in changes)
		{
			Console.Write($"{getVolumePercentage(change.Delta):+00;-00}   ");
		}

		Console.WriteLine();
		Console.Write(firstUp ? "__" : "  ");

		foreach (Change change in changes)
		{
			Console.Write(change.Delta > 0 ? "_/    " : "   \\__");
		}

		Console.WriteLine(lastUp ? "" : "_");
		Console.Write(firstUp
			? $"{getVolumePercentage(first.From):D3}        "
			: "      ");

		foreach (Change change in changes)
		{
			Console.Write(change.Delta < 0 ? $"{getVolumePercentage(change.To):D3}        " : " ");
		}

		Console.WriteLine();
	}

	private static List<Change> getVolumeChanges()
	{
		List<Change> changes = new List<Change>();

		float aggregate = 0;
		float previous = referenceVolume;

		foreach (float volume in volumeChanges)
		{
			float diff = volume - previous;

			int signDiff = MathF.Sign(diff) - MathF.Sign(aggregate);
			bool differentDirection = signDiff * signDiff > 1;

			if (differentDirection && aggregate != 0)
			{
				changes.Add(new Change(previous - aggregate, previous, aggregate));
				aggregate = 0;
			}

			aggregate += diff;

			previous = volume;
		}

		if (aggregate != 0)
		{
			changes.Add(new Change(previous - aggregate, previous, aggregate));
		}

		return changes;
	}


	private static void simulateKeyPress(byte virtualKey)
	{
		[DllImport("user32.dll", SetLastError = true)]
		static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);

		keybd_event(virtualKey, 0, 0, 0);
		keybd_event(virtualKey, 0, 2, 0);
	}

	private static void shutdown()
	{
		Process.Start("shutdown", "/s /t 0");
	}

	private static void shutdownIn(int seconds)
	{
		Process.Start("shutdown", $"/s /t {seconds}");
	}
}

public record VolumeRange(int X1, int X2, bool Relative = true)
{
	public VolumeRange(int value, bool Relative = true)
		: this(value, value, Relative)
	{
	}
}

internal record VolumeRangePrecomputed(float Min, float Max, bool Relative);

public record Command(string Name, Action Action, VolumeRange[] Thresholds);

internal record CommandPrecomputed(string Name, Action Action, VolumeRangePrecomputed[] Thresholds);

internal record struct Change(float From, float To, float Delta);