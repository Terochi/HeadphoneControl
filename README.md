# Headphone control

This project is a means to control various functions (mainly playback of music)
with the controls of a scroll wheel on top of my wireless headphones.

This leverages on the fact that the scroll wheel changes system audio levels to detect direction and amount of scroll.

The sequences of volume changes can then be decoded into different actions, such as pausing/playling music.

The actions can be easily configured by specifying the `Command[]` when calling
`Utilities.SetupHeadphoneControl(commands);`

```csharp
using HeadphoneControlLib;

Command[] commands = {
    // Command to press the next media key
    new Command("Next", Utilities.PressKey(0xB0), new[]
    {
        new VolumeRange(-1, -3), // First: scroll down 1 to 3 steps
        new VolumeRange(1, 3), // Then: scroll up 1 to 3 steps
    }),
};
```