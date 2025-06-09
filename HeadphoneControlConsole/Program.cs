using HeadphoneControlLib;

Command[] commands =
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
		new VolumeRange(0, 0, false)
	}),
};

Utilities.SetupHeadphoneControl(commands);

Console.ReadKey(true);