# Setting up BasicTTSDemo Scene

## For Package Maintainers

To generate the complete BasicTTSDemo.unity scene file:

1. Open Unity with the uPiper project
2. Run the menu command: `uPiper/Internal/Generate BasicTTSDemo Scene File`
3. The scene will be created and saved to `Assets/uPiper/Samples~/BasicTTSDemo/BasicTTSDemo.unity`
4. Open the saved scene and verify all references are correctly set
5. Save the scene again if any adjustments were made
6. Commit the generated scene file

## For End Users

The BasicTTSDemo.unity scene should already be included when you import the sample.
Simply open the scene and press Play to test the TTS functionality.

If the scene is missing or corrupted, you can regenerate it using:
`uPiper/Samples/Create Basic TTS Demo Scene`