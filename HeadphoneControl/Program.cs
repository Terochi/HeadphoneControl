using HeadphoneControlLib;

namespace HeadphoneControl;

internal static class Program
{
	private static readonly Command[] commands =
	{
		new Command("Play/Pause", Utilities.PressKey(0xB3), new[]
		{
			new VolumeRange(1, 3),
			new VolumeRange(-1, -3),
		}),
		new Command("Next", Utilities.PressKey(0xB0), new[]
		{
			new VolumeRange(-1, -3),
			new VolumeRange(1, 3),
		}),
		new Command("Shutdown", Utilities.Shutdown(), new[]
		{
			new VolumeRange(0, false)
		}),
	};


	[STAThread]
	public static void Main()
	{
		Utilities.SetupHeadphoneControl(commands);
		Application.Run();
	}
}