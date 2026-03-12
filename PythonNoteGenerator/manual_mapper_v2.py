import pygame
import json
import sys
import argparse
import os

# --- Visual Layout Constants ---
WIDTH, HEIGHT = 1000, 600
PANEL_WIDTH = 330        # The left 1/3 UI panel
LANE_WIDTH = 100         # Width of each vertical string lane
# Centers the 5 lanes (500px) within the right 2/3 of the screen
LANE_START_X = PANEL_WIDTH + ((WIDTH - PANEL_WIDTH) - (5 * LANE_WIDTH)) // 2 
PLAYHEAD_Y = 480         
PX_PER_SEC = 200      

# --- Colors ---
BG_COLOR = (30, 33, 36)
PANEL_BG_COLOR = (20, 22, 25)   # Slightly darker for the left panel
LANE_COLOR_1 = (40, 44, 52)
LANE_COLOR_2 = (45, 50, 58)
PLAYHEAD_COLOR = (255, 50, 50)
TEXT_COLOR = (220, 220, 220)

COLOR_THUMB = (50, 255, 50)       
COLOR_INDEX = (50, 150, 255)      
COLOR_MIDDLE = (200, 50, 255)     
COLOR_RING = (255, 50, 200)       
COLOR_PINKY = (255, 255, 50)      
COLOR_MUTE = (150, 150, 150)      
COLOR_TREMOLO = (255, 150, 0)

ALL_GESTURES = ["thumb", "index", "middle", "ring", "pinky", "mute", "tremolo"]
FIXED_DURATION_GESTURES = ["thumb", "index", "middle", "ring", "pinky", "mute"]

GESTURE_SYMBOLS = {
    "thumb": "1",
    "index": "2",
    "middle": "3",
    "ring": "4",
    "pinky": "5",
    "mute": "X",
    "tremolo": "#"
}


class GuzhengBeatmapper:
    def __init__(self, audio_file, output_file):
        self.audio_file = audio_file
        self.output_file = output_file
        
        # Core State
        self.notes = []
        self.is_playing = False
        self.playhead_time = 0.0
        self.audio_length = 0.0
        self.running = True
        self.dragging_note_idx = None
        
        self._init_pygame()
        self._load_audio()
        self._load_existing_beatmap()

    def _init_pygame(self):
        """Initializes Pygame, the window, and fonts."""
        pygame.init()
        pygame.mixer.init()
        self.screen = pygame.display.set_mode((WIDTH, HEIGHT))
        pygame.display.set_caption("Visual Guzheng AR Beatmapper")
        self.font = pygame.font.SysFont(None, 24)
        self.large_font = pygame.font.SysFont(None, 36)
        self.clock = pygame.time.Clock()

    def _load_audio(self):
        """Loads the MP3 and calculates its total length."""
        try:
            pygame.mixer.music.load(self.audio_file)
            pygame.mixer.music.set_volume(0.5)
            self.audio_length = pygame.mixer.Sound(self.audio_file).get_length() 
        except Exception as e:
            print(f"Error loading audio '{self.audio_file}'. Error: {e}")
            sys.exit()

    def _load_existing_beatmap(self):
        """Checks for an existing JSON and loads it if found."""
        if os.path.exists(self.output_file):
            try:
                with open(self.output_file, 'r') as f:
                    data = json.load(f)
                    if "notes" in data:
                        self.notes = data["notes"]
                        print(f">>> SUCCESS: Loaded {len(self.notes)} notes from '{self.output_file}' <<<")
            except Exception as e:
                print(f">>> ERROR: Could not read '{self.output_file}'. Starting fresh. Error: {e}")
        else:
            print(f">>> Starting a fresh beatmap for '{self.output_file}' <<<")

    def _is_overlapping(self, target_time, string_num, target_gesture, target_duration=None, ignore_idx=None):
        """Calculates if a proposed note overlaps using exact time durations."""
        if target_duration is None:
            target_duration = (15 if target_gesture in FIXED_DURATION_GESTURES else 40) / PX_PER_SEC
            
        for i, note in enumerate(self.notes):
            if i == ignore_idx:
                continue
                
            if note["string"] == string_num:
                # Use saved duration, or fallback to default if loading an older JSON
                existing_duration = note.get("duration", (15 if note["gesture"] in FIXED_DURATION_GESTURES else 40) / PX_PER_SEC)
                
                if (target_time < note["time"] + existing_duration) and (note["time"] < target_time + target_duration):
                    return True
                    
        return False

    def save_beatmap(self):
        """Sorts and saves all recorded notes to the JSON file."""
        sorted_notes = sorted(self.notes, key=lambda x: x["time"])
        beatmap = {"notes": sorted_notes}
        with open(self.output_file, 'w') as f:
            json.dump(beatmap, f, indent=4)
        print(f"\n>>> SUCCESS: Saved {len(sorted_notes)} notes to '{self.output_file}' <<<")

    def seek_audio(self, target_time):
        """Safely scrubs the audio to a new timestamp."""
        self.playhead_time = max(0.0, min(target_time, self.audio_length))
        pygame.mixer.music.play(0, start=self.playhead_time)
        if not self.is_playing:
            pygame.mixer.music.pause()

    def add_note(self, string_num, gesture):
        """Records a note at the current playhead time with a duration property."""
        if 0 <= self.playhead_time <= self.audio_length:
            new_time = round(self.playhead_time, 3)
            duration = (15 if gesture in FIXED_DURATION_GESTURES else 40) / PX_PER_SEC
            
            if self._is_overlapping(new_time, string_num, gesture, target_duration=duration):
                return
                
            self.notes.append({
                "time": new_time,
                "string": string_num,
                "gesture": gesture,
                "duration": round(duration, 3)
            })

    def handle_events(self):
        """Processes all keyboard and mouse inputs."""
        for event in pygame.event.get():
            if event.type == pygame.QUIT:
                self.running = False
                
            elif event.type == pygame.MOUSEBUTTONDOWN:
                mouse_x, mouse_y = event.pos
                clicked_note_idx = None
                clicked_edge = False
                
                for i in range(len(self.notes) - 1, -1, -1):
                    note = self.notes[i]
                    note_duration = note.get("duration", (15 if note["gesture"] in FIXED_DURATION_GESTURES else 40) / PX_PER_SEC)
                    note_height = note_duration * PX_PER_SEC
                    
                    note_start_y = PLAYHEAD_Y - ((note["time"] - self.playhead_time) * PX_PER_SEC)
                    note_end_y = note_start_y - note_height
                    
                    note_x = LANE_START_X + ((note["string"] - 1) * LANE_WIDTH) + 10
                    note_width = LANE_WIDTH - 20
                    
                    note_rect = pygame.Rect(note_x, note_end_y, note_width, note_height)
                    # The edge handle is now at the top of the note (note_end_y)
                    edge_rect = pygame.Rect(note_x, note_end_y - 5, note_width, 15) 
                    
                    if edge_rect.collidepoint(mouse_x, mouse_y) and note["gesture"] == "tremolo":
                        clicked_note_idx = i
                        clicked_edge = True
                        break
                    elif note_rect.collidepoint(mouse_x, mouse_y):
                        clicked_note_idx = i
                        break

                if clicked_note_idx is not None:
                    if event.button == 1: 
                        if clicked_edge:
                            self.dragging_note_idx = clicked_note_idx 
                        else:
                            gestures = ALL_GESTURES
                            current = self.notes[clicked_note_idx]["gesture"]
                            next_idx = (gestures.index(current) + 1) % len(gestures)
                            proposed_gesture = gestures[next_idx]
                            proposed_duration = (15 if proposed_gesture in FIXED_DURATION_GESTURES else 40) / PX_PER_SEC
                            
                            if not self._is_overlapping(self.notes[clicked_note_idx]["time"], 
                                                        self.notes[clicked_note_idx]["string"], 
                                                        proposed_gesture, 
                                                        target_duration=proposed_duration,
                                                        ignore_idx=clicked_note_idx):
                                self.notes[clicked_note_idx]["gesture"] = proposed_gesture
                                self.notes[clicked_note_idx]["duration"] = proposed_duration
                                
                    elif event.button == 3:  
                        self.notes.pop(clicked_note_idx)
                else:
                    # Only scrub if clicking in the lane area (right 2/3)
                    if event.button == 1 and mouse_x > PANEL_WIDTH: 
                        time_offset = (PLAYHEAD_Y - mouse_y) / PX_PER_SEC
                        self.seek_audio(self.playhead_time + time_offset)
            
            elif event.type == pygame.MOUSEMOTION:
                if self.dragging_note_idx is not None:
                    _, mouse_y = event.pos
                    note = self.notes[self.dragging_note_idx]
                    
                    note_start_y = PLAYHEAD_Y - ((note["time"] - self.playhead_time) * PX_PER_SEC)
                    # Moving mouse UP decreases Y, increasing height
                    new_height = max(20, note_start_y - mouse_y) 
                    new_duration = new_height / PX_PER_SEC
                    
                    max_duration = float('inf')
                    for j, other_note in enumerate(self.notes):
                        if j != self.dragging_note_idx and other_note["string"] == note["string"]:
                            if other_note["time"] > note["time"]:
                                max_duration = min(max_duration, other_note["time"] - note["time"])
                    
                    note["duration"] = round(min(new_duration, max_duration), 3)

            elif event.type == pygame.MOUSEBUTTONUP:
                if event.button == 1:
                    self.dragging_note_idx = None

            elif event.type == pygame.KEYDOWN:
                if event.key == pygame.K_ESCAPE: self.running = False
                elif event.key == pygame.K_SPACE:
                    self.is_playing = not self.is_playing
                    if self.is_playing: pygame.mixer.music.play(0, start=self.playhead_time)
                    else: pygame.mixer.music.pause()
                elif event.key == pygame.K_z: 
                    if len(self.notes) > 0: self.notes.pop()
                elif event.key == pygame.K_1: self.add_note(1, "thumb")
                elif event.key == pygame.K_2: self.add_note(2, "thumb")
                elif event.key == pygame.K_3: self.add_note(3, "thumb")
                elif event.key == pygame.K_4: self.add_note(4, "thumb")
                elif event.key == pygame.K_5: self.add_note(5, "thumb")

    def update(self, delta_time):
        """Updates the game state (like moving the playhead)."""
        if self.is_playing:
            self.playhead_time += delta_time
            if self.playhead_time >= self.audio_length:
                self.playhead_time = self.audio_length
                self.is_playing = False

    def draw_lanes(self):
        """Renders the vertical background lanes and string labels."""
        for i in range(5):
            x = LANE_START_X + (i * LANE_WIDTH)
            color = LANE_COLOR_1 if i % 2 == 0 else LANE_COLOR_2
            pygame.draw.rect(self.screen, color, (x, 0, LANE_WIDTH, HEIGHT))
            
            label = self.font.render(f"String {i+1}", True, (100, 100, 100))
            self.screen.blit(label, (x + 15, HEIGHT - 30))

    def draw_notes(self):
        """Renders the recorded notes, symbols, and drag handles falling towards the playhead."""
        for note in self.notes:
            note_duration = note.get("duration", (15 if note["gesture"] in FIXED_DURATION_GESTURES else 40) / PX_PER_SEC)
            note_height = note_duration * PX_PER_SEC
            
            # The bottom of the note hits the playhead at note["time"]
            note_start_y = PLAYHEAD_Y - ((note["time"] - self.playhead_time) * PX_PER_SEC)
            # The top of the note is higher up on the screen (lower Y value)
            note_end_y = note_start_y - note_height 
            
            if -note_height < note_start_y < HEIGHT + note_height:
                note_x = LANE_START_X + ((note["string"] - 1) * LANE_WIDTH) + 10
                note_width = LANE_WIDTH - 20
                
                if note["gesture"] == "thumb": color = COLOR_THUMB
                elif note["gesture"] == "index": color = COLOR_INDEX
                elif note["gesture"] == "middle": color = COLOR_MIDDLE
                elif note["gesture"] == "ring": color = COLOR_RING
                elif note["gesture"] == "pinky": color = COLOR_PINKY
                elif note["gesture"] == "mute": color = COLOR_MUTE
                else: color = COLOR_TREMOLO
                    
                # 1. Draw the note rectangle
                pygame.draw.rect(self.screen, color, (note_x, note_end_y, note_width, note_height), border_radius=4)
                
                # 2. Draw the symbol text
                symbol = GESTURE_SYMBOLS.get(note["gesture"], "?")
                text_surface = self.font.render(symbol, True, (30, 30, 30)) # Dark gray text for contrast
                text_rect = text_surface.get_rect()
                
                text_rect.centerx = note_x + (note_width // 2)
                
                # Center vertically for short notes, anchor near bottom for stretched tremolos
                if note_height <= 30:
                    text_rect.centery = note_end_y + (note_height // 2)
                else:
                    text_rect.bottom = note_start_y - 5
                    
                self.screen.blit(text_surface, text_rect)
                
                # 3. Draw a visual drag handle at the TOP of Tremolo notes
                if note["gesture"] == "tremolo":
                    handle_rect = (note_x + 10, note_end_y, note_width - 20, 8)
                    pygame.draw.rect(self.screen, (255, 255, 255), handle_rect, border_radius=2)

    def draw_ui(self):
        """Renders the left control panel, instructions, and horizontal playhead line."""
        # 1. Left Control Panel Background
        pygame.draw.rect(self.screen, PANEL_BG_COLOR, (0, 0, PANEL_WIDTH, HEIGHT))
        pygame.draw.line(self.screen, (60, 65, 75), (PANEL_WIDTH, 0), (PANEL_WIDTH, HEIGHT), 2)

        # 2. Playhead Line (Only across the lanes)
        pygame.draw.line(self.screen, PLAYHEAD_COLOR, (LANE_START_X - 20, PLAYHEAD_Y), (LANE_START_X + (5 * LANE_WIDTH) + 20, PLAYHEAD_Y), 3)

        # 3. Dynamic Status Elements
        status = "PLAYING" if self.is_playing else "PAUSED"
        status_color = (100, 255, 100) if self.is_playing else (255, 255, 100)
        
        self.screen.blit(self.large_font.render(f"> {status}", True, status_color), (20, 20))
        self.screen.blit(self.font.render(f"Time: {self.playhead_time:.2f}s / {self.audio_length:.2f}s", True, TEXT_COLOR), (20, 60))
        self.screen.blit(self.font.render(f"Notes Total: {len(self.notes)}", True, TEXT_COLOR), (20, 85))
        
        # 4. Instruction List (Formatted to fit the left panel)
        instructions = [
            "--- CONTROLS ---",
            "",
            "PLAYBACK:",
            "[SPACE] Play / Pause",
            "[Left Click Lane] Scrub Timeline",
            "",
            "ADDING NOTES (Drops at Playhead):",
            "[1] String 1 (Left / Highest Pitch)",
            "[2] String 2",
            "[3] String 3",
            "[4] String 4",
            "[5] String 5 (Right / Lowest Pitch)",
            "    * Defaults to Thumb style",
            "",
            "EDITING NOTES:",
            "[Left Click Note] Cycle Gesture",
            "[Right Click Note] Delete",
            "[Drag Note Top] Stretch Tremolo",
            "[Z] Undo Last Added Note",
            "",
            "SYSTEM:",
            "[ESC] Save & Quit"
        ]
        
        y_offset = 130
        for line in instructions:
            # Color headers differently from standard instructions
            color = (180, 180, 200) if line.endswith(":") or line.startswith("---") else (130, 130, 130)
            self.screen.blit(self.font.render(line, True, color), (20, y_offset))
            y_offset += 20

    def run(self):
        """The main application loop."""
        last_tick = pygame.time.get_ticks()

        while self.running:
            current_tick = pygame.time.get_ticks()
            delta_time = (current_tick - last_tick) / 1000.0
            last_tick = current_tick

            self.handle_events()
            self.update(delta_time)

            # Rendering
            self.screen.fill(BG_COLOR)
            self.draw_lanes()
            self.draw_notes()
            self.draw_ui()
            
            pygame.display.flip()
            self.clock.tick(60) 

        self.save_beatmap()
        pygame.quit()


def main():
    parser = argparse.ArgumentParser(description="Visual Guzheng AR Beatmapper")
    parser.add_argument("-i", "--input", required=True, help="Path to the input audio file")
    parser.add_argument("-o", "--output", required=True, help="Path to the output JSON file")
    args = parser.parse_args()

    # Instantiate and run our mapped application
    app = GuzhengBeatmapper(args.input, args.output)
    app.run()

if __name__ == "__main__":
    main()
