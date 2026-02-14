# <p align="center"> TextNarrator</p>

<p align="center"> <img src="https://github.com/PantelisAndrianakis/TextNarrator/blob/main/Images/Icon.png" alt="TextNarrator Logo" width="128" height="128"></p>

TextNarrator is a Windows desktop application that converts text to speech, reading your content aloud while highlighting each sentence as it's being read. Perfect for proofreading, accessibility, multitasking, or simply resting your eyes while consuming written content.

<p align="center">
  <img src="https://github.com/PantelisAndrianakis/TextNarrator/blob/main/Images/Screenshot.png" alt="TextNarrator Screenshot" width="800">
</p>

## Features

- **Triple Speech Engine Support**: 
  - **Piper TTS**: High-quality neural voices with natural-sounding speech (auto-downloads on first use)
  - **Windows Modern TTS**: Windows.Media.SpeechSynthesis for optimal voice quality
  - **Windows Legacy TTS**: System.Speech.Synthesis for compatibility
- **Multiple Voice Options**: Choose from all available system voices plus downloadable Piper voices
- **Sentence-by-Sentence Reading**: Reads one sentence at a time with smart buffering for seamless playback
- **Real-time Highlighting**: Automatically highlights the current sentence being read for easy visual tracking
- **Progress Notifications**: Shows download progress, conversion status, and current sentence in the title bar
- **Playback Controls**: Play, Pause, Stop, and Restart functionality for complete control over narration
- **Smart Text Processing**: 
  - Intelligently identifies sentences even with complex punctuation
  - Handles em-dashes (—) and en-dashes (–) as natural sentence breaks
  - Removes markdown formatting (hashtags, etc.) for cleaner narration
- **Improved Pronunciation**: Automatically expands common abbreviations for better speech quality
- **Offline Support**: After initial download, Piper voices work completely offline
- **Auto-Download**: Automatically downloads Piper TTS engine and voice models when internet is available

## System Requirements

- **Operating System**: Windows 10 version 10.0.19041.0 or later
- **.NET Runtime**: .NET 8.0 Runtime
- **Voices**: System speech voices installed (Windows TTS)
- **For Piper TTS** (optional):
  - Internet connection for first-time download
  - ~60-100MB disk space (engine + voice models)
  - Works offline after initial setup

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

## Piper TTS Integration

TextNarrator now includes **Piper TTS** - a fast, local neural text-to-speech system that provides high-quality, natural-sounding voices.

### What is Piper?

Piper is an offline neural TTS system that runs entirely on your computer, offering:
- **High-quality speech**: Neural voices that sound more natural than traditional TTS
- **Fast generation**: Optimized for real-time sentence-by-sentence playback
- **Privacy-focused**: All processing happens locally on your machine
- **Free and open-source**: Based on the Piper project by Rhasspy

### First-Time Setup

**Automatic Download (Recommended)**:
1. Select a Piper voice from the dropdown (e.g., "Piper: Amy (en-US, Medium)")
2. The app will automatically download:
   - Piper TTS engine (~40MB, one-time download)
   - Selected voice model (~20MB per voice)
3. Downloads happen in the background with progress shown in the title bar
4. **After initial download, Piper works completely offline!**

**Manual Installation**:
If you prefer to download manually or have no internet:
1. Download `piper_windows_amd64.zip` from [Piper Releases](https://github.com/rhasspy/piper/releases/tag/2023.11.14-2)
2. Extract contents to `TextNarrator/piper/` folder
3. Download voice models from [rhasspy's sample page](https://rhasspy.github.io/piper-samples/) or directly from [HuggingFace](https://huggingface.co/rhasspy/piper-voices/tree/v1.0.0)
4. Place `.onnx` and `.onnx.json` files in `TextNarrator/models/` folder

### Available Piper Voices

Default voices available for download:
- **en_US-amy-medium**: American English (female, medium quality)
- **en_US-lessac-medium**: American English (male, medium quality)
- **en_GB-alan-medium**: British English (male, medium quality)
- **en_GB-alba-medium**: British English (female, medium quality)

Additional voices can be downloaded from the [Piper Voices repository](https://huggingface.co/rhasspy/piper-voices).

### How It Works

1. **Sentence-by-Sentence**: Piper reads one sentence at a time for better control
2. **Smart Buffering**: Pre-generates the next sentence while playing current one (zero lag!)
3. **Progress Tracking**: Title bar shows conversion and playback progress
4. **Text Preprocessing**: Handles dashes, hashtags, and abbreviations before TTS

### System Requirements for Piper

- Windows 10/11 (64-bit)
- ~60MB disk space (piper.exe + one voice model)
- Internet connection for initial download only
- Works offline after first-time setup

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
- Leverages three TTS engines:
  - **Piper TTS**: Local neural text-to-speech (via subprocess)
  - **Windows.Media.SpeechSynthesis**: Modern Windows TTS API
  - **System.Speech**: Legacy Windows TTS API
- Audio playback via NAudio for Piper voices
- Published as a single-file application for easy distribution
- Auto-downloads required components when internet is available

## License

This project is licensed under the terms included in the [LICENSE.txt](LICENSE.txt) file.

## Acknowledgments

- Icon design created for TextNarrator
- [Piper TTS](https://github.com/rhasspy/piper) by Rhasspy for high-quality neural voices
- [NAudio](https://github.com/naudio/NAudio) for audio playback
- Thanks to the .NET community for the System.Speech library
