# Visual Guzheng AR Beatmapper

A custom Python tool built with Pygame to manually map audio tracks into JSON beatmaps. 

## ⚙️ Prerequisites

Install the required Python packages:

```bash
pip install -r requirements.txt
```

## 🚀 How to Run

Run the script from your terminal, providing an input audio file (like `.mp3` or `.wav`) and the desired output `.json` file path. 

**Syntax:**
python note_mapper.py -i <path_to_audio> -o <path_to_json>

**Quick Example:**
python note_mapper.py -i audio/spring_festival.mp3 -o beatmaps/spring_festival.json

*Note: If the JSON file already exists, the tool will load the existing notes so you can resume your progress!*

## 🎮 Controls & Interface

The interface is divided into a control panel on the left and a vertical, scrolling timeline on the right (where notes fall from top to bottom).

### Playback & Navigation
* **[SPACE]** - Play / Pause the audio.
* **[UP / DOWN]** - Change playback speed (0.25x to 1.5x) to catch fast notes.
* **Vertical Scrollbar** (Right Margin) - Drag to quickly jump to different parts of the song.
* **Volume Slider** (Bottom Left) - Adjust the audio volume.
* **Timeline Scrubbing** - Left-click anywhere on the empty timeline to scrub to that exact moment.

### Adding & Editing Notes
Notes are dropped exactly at the red playhead line.
* **[1, 2, 3, 4, 5]** - Drop a note on String 1 (Highest Pitch) to String 5 (Lowest Pitch). Defaults to the "Thumb" gesture.
* **[Left Click Note]** - Cycle the note's gesture style (Thumb -> Index -> Middle -> Ring -> Pinky -> Mute -> Tremolo).
* **[Right Click Note]** - Delete the clicked note.
* **[Drag Tremolo Handle]** - Tremolo notes (`#`) have a white handle at the top. Click and drag this handle to stretch the duration of the tremolo.
* **[Z]** - Undo the last added note.
* **[ESC]** - Save and exit the tool.

> [!CAUTION]
> DO NOT press Ctrl + C to exit the program in the terminal, as this will not save your progress. Always use `[ESC]` to ensure your notes are saved properly.


## 📄 Output Format

The tool automatically sorts your notes chronologically and saves them to the specified JSON file when you press `[ESC]` to quit. The output is structured to be easily parsed by your C# Unity scripts:

{
    "notes": [
        {
            "time": 1.25,
            "string": 1,
            "gesture": "thumb",
            "duration": 0.075
        },
        {
            "time": 2.105,
            "string": 3,
            "gesture": "tremolo",
            "duration": 1.45
        }
    ]
}

