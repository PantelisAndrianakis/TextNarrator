# <p align="center"> TextNarrator</p>

<p align="center"> <img src="https://github.com/PantelisAndrianakis/TextNarrator/blob/main/Images/Icon.png" alt="TextNarrator Logo" width="128" height="128"></p>

TextNarrator is a Windows desktop application that converts text to speech, reading your content aloud while highlighting each sentence as it's being read. Perfect for proofreading, accessibility, multitasking, or simply resting your eyes while consuming written content.

<p align="center">
  <img src="https://github.com/PantelisAndrianakis/TextNarrator/blob/main/Images/Screenshot.png" alt="TextNarrator Screenshot" width="800">
</p>

## Features

- **Dual Speech Engine Support**: Uses both Windows.Media.SpeechSynthesis and System.Speech.Synthesis for optimal voice quality and compatibility
- **Multiple Voice Options**: Choose from all available system voices
- **Sentence Highlighting**: Automatically highlights the current sentence being read for easy visual tracking
- **Playback Controls**: Play, Pause, Stop, and Restart functionality for complete control over narration
- **Smart Text Parsing**: Intelligently identifies sentences even with complex punctuation
- **Improved Pronunciation**: Automatically expands common abbreviations for better speech quality
- **Visual Progress Tracking**: Shows current sentence and total count in the application title
- **Configurable Pauses**: Adjustable delay between sentences for natural-sounding narration

## System Requirements

- Windows 10 version 10.0.19041.0 or later
- .NET 8.0 Runtime
- System speech voices installed

## Installation

1. Download the latest release from the releases page
2. Run the executable file (`TextNarrator.exe`)
3. No installation required - the application runs as a standalone executable

## Usage

1. Launch TextNarrator
2. Paste or type your text into the main text area
3. Select your preferred voice from the dropdown menu
4. Use the playback controls to manage narration:
   - **Play**: Start reading from the beginning or current position
   - **Pause**: Temporarily stop reading (maintains position)
   - **Stop**: Stop reading completely
   - **Restart**: Return to the beginning of the text
5. The current sentence being read will be highlighted in yellow

## Abbreviation Handling

TextNarrator automatically improves pronunciation by expanding common abbreviations, including:

- **Titles**: Dr., Mr., Mrs., Ms., Prof., etc.
- **Common Abbreviations**: etc., vs., e.g., i.e., a.m., p.m., etc.
- **Geographic Terms**: St., Ave., Blvd., Rd., etc.
- **Units and Measurements**: ft., in., lb., oz., etc.
- **Academic and Professional**: Ph.D., M.D., B.A., etc.

This helps create a more natural listening experience by ensuring abbreviations are properly pronounced.

## Building from Source

If you want to build TextNarrator from source:

### Using the Build Script (Recommended)

1. Clone the repository
2. Ensure you have the .NET 8.0 SDK installed
3. Run the included `Build.bat` script
4. The compiled application will be in the `bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\` directory

### Using Visual Studio

1. Clone the repository
2. Open the solution in Visual Studio 2022 or later
3. Ensure you have the .NET 8.0 SDK installed
4. Build the solution using the Release configuration
5. The compiled application will be in the `bin/Release` directory

## Technical Details

- Built with .NET 8.0
- Uses Windows Forms for the user interface
- Leverages both Windows.Media.SpeechSynthesis and System.Speech APIs
- Published as a single-file application for easy distribution

## License

This project is licensed under the terms included in the [LICENSE.txt](LICENSE.txt) file.

## Acknowledgments

- Icon design created for TextNarrator
- Thanks to the .NET community for the System.Speech library
