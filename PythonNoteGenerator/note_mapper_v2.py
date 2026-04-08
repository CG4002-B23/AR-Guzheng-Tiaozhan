import pygame
import json
import sys
import argparse
import os
import tempfile

# --- Visual Layout Constants ---
SCALE = 2.0
WIDTH, HEIGHT = 1000, 600
PANEL_WIDTH = 400        # The left 1/3 UI panel
LANE_WIDTH = 100         # Width of each vertical string lane

SLIDER_WIDTH = 15
SLIDER_X = WIDTH - SLIDER_WIDTH - 20     # Positions it neatly in the right margin
SLIDER_Y_MARGIN = 48                     # Padding from top and bottom
SLIDER_TRACK_HEIGHT = HEIGHT - (SLIDER_Y_MARGIN * 2)

VOL_SLIDER_WIDTH = 300
VOL_SLIDER_X = 20
VOL_SLIDER_Y = HEIGHT - 40
VOL_SLIDER_HEIGHT = 10

# Centers the 4 lanes (400px) within the right 2/3 of the screen
LANE_START_X = PANEL_WIDTH + ((WIDTH - PANEL_WIDTH) - (4 * LANE_WIDTH)) // 2
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
        self.dragging_slider = False
        self.was_playing_before_drag = False
        self.volume = 0.5            
        self.dragging_volume = False

        # Speed state
        self.playback_speeds = [0.25, 0.5, 0.75, 1.0, 1.25, 1.5]
        self.speed_idx = 3 # Defaults to 1.0x
        self.playback_speed = self.playback_speeds[self.speed_idx]
        self.temp_audio_files = {}  
        
        self._init_pygame()
        self._prepare_audio_speeds() 
        self._load_audio()
        self._load_existing_beatmap()

    def _prepare_audio_speeds(self):
        """Uses pydub to physically stretch the audio and caches temporary WAV files."""
        print(">>> Pre-processing audio speeds (this may take a few seconds)... <<<")
        try:
            from pydub import AudioSegment
            base_audio = AudioSegment.from_file(self.audio_file)
            original_fr = base_audio.frame_rate
            
            for speed in self.playback_speeds:
                if speed == 1.0:
                    self.temp_audio_files[speed] = self.audio_file
                else:
                    # Alter frame rate to change speed/pitch, then resample back to standard
                    new_fr = int(original_fr * speed)
                    altered_audio = base_audio._spawn(base_audio.raw_data, overrides={"frame_rate": new_fr})
                    stretched_audio = altered_audio.set_frame_rate(original_fr)
                    
                    # Save as a temporary WAV file
                    temp_fd, temp_path = tempfile.mkstemp(suffix=".wav")
                    os.close(temp_fd)
                    stretched_audio.export(temp_path, format="wav")
                    self.temp_audio_files[speed] = temp_path
                    
            self.audio_length = len(base_audio) / 1000.0 
            print(">>> Audio pre-processing complete! <<<")
            
        except ImportError:
            print(">>> ERROR: 'pydub' is not installed. Run: pip install pydub <<<")
            sys.exit()
        except Exception as e:
            print(f">>> ERROR processing audio: {e} <<<")
            print("Ensure you have ffmpeg installed on your system to read MP3 files.")
            sys.exit()

    def _load_audio(self):
        """Loads the pre-processed audio file for the current speed."""
        try:
            current_file = self.temp_audio_files[self.playback_speed]
            pygame.mixer.music.load(current_file)
            pygame.mixer.music.set_volume(self.volume)
        except Exception as e:
            print(f"Error loading audio: {e}")
            sys.exit()

    def _update_slider_visual(self, mouse_y):
        """Updates the playhead time visually without restarting the audio stream."""
        clamped_y = max(SLIDER_Y_MARGIN, min(mouse_y, SLIDER_Y_MARGIN + SLIDER_TRACK_HEIGHT))
        progress = 1.0 - ((clamped_y - SLIDER_Y_MARGIN) / SLIDER_TRACK_HEIGHT)
        self.playhead_time = progress * self.audio_length

    def _update_volume_visual(self, mouse_x):
        """Updates the volume level based on the slider position."""
        clamped_x = max(VOL_SLIDER_X, min(mouse_x, VOL_SLIDER_X + VOL_SLIDER_WIDTH))
        self.volume = (clamped_x - VOL_SLIDER_X) / VOL_SLIDER_WIDTH
        pygame.mixer.music.set_volume(self.volume)

    def draw_volume_slider(self):
        """Renders the horizontal volume slider in the bottom left panel."""
        vol_text = self.font.render(f"Volume: {int(self.volume * 100)}%", True, TEXT_COLOR)
        self.screen.blit(vol_text, (VOL_SLIDER_X, VOL_SLIDER_Y - 25))
        
        track_rect = (VOL_SLIDER_X, VOL_SLIDER_Y, VOL_SLIDER_WIDTH, VOL_SLIDER_HEIGHT)
        pygame.draw.rect(self.screen, (50, 50, 50), track_rect, border_radius=5)
        
        knob_x = VOL_SLIDER_X + (self.volume * VOL_SLIDER_WIDTH)
        knob_rect = (knob_x - 5, VOL_SLIDER_Y - 5, 10, VOL_SLIDER_HEIGHT + 10)
        knob_color = (200, 200, 200) if self.dragging_volume else (120, 120, 120)
        pygame.draw.rect(self.screen, knob_color, knob_rect, border_radius=3)

    def draw_slider(self):
        """Renders the vertical scrollbar on the right margin."""
        track_rect = (SLIDER_X, SLIDER_Y_MARGIN, SLIDER_WIDTH, SLIDER_TRACK_HEIGHT)
        pygame.draw.rect(self.screen, (50, 50, 50), track_rect, border_radius=8)
        
        progress = self.playhead_time / self.audio_length if self.audio_length > 0 else 0
        knob_y = SLIDER_Y_MARGIN + ((1.0 - progress) * SLIDER_TRACK_HEIGHT)
        
        knob_rect = (SLIDER_X - 2, knob_y - 15, SLIDER_WIDTH + 4, 30)
        knob_color = (200, 200, 200) if self.dragging_slider else (120, 120, 120)
        pygame.draw.rect(self.screen, knob_color, knob_rect, border_radius=5)

    def seek_audio(self, target_time):
        """Safely scrubs the audio to a new timestamp, adjusting for physical file length."""
        self.playhead_time = max(0.0, min(target_time, self.audio_length))
        scaled_start_time = self.playhead_time / self.playback_speed
        
        pygame.mixer.music.play(0, start=scaled_start_time)
        if not self.is_playing:
            pygame.mixer.music.pause()

    def change_speed(self, direction):
        """Switches to the pre-processed audio file for the new speed."""
        new_idx = max(0, min(len(self.playback_speeds) - 1, self.speed_idx + direction))
        
        if new_idx != self.speed_idx:
            self.speed_idx = new_idx
            self.playback_speed = self.playback_speeds[self.speed_idx]
            
            was_playing = self.is_playing
            self._load_audio() 
            
            if was_playing:
                self.seek_audio(self.playhead_time)
            elif self.playhead_time > 0:
                self.seek_audio(self.playhead_time)
                pygame.mixer.music.pause()

    def clean_up(self):
        """Deletes the temporary WAV files generated for playback speed."""
        print("\nCleaning up temporary audio files...")
        for speed, path in self.temp_audio_files.items():
            if speed != 1.0 and os.path.exists(path):
                try:
                    os.remove(path)
                except Exception as e:
                    pass

    def _init_pygame(self):
        """Initializes Pygame, the window, and fonts."""
        pygame.init()
        pygame.mixer.init()
        # self.screen = pygame.display.set_mode((WIDTH, HEIGHT))
        self.window = pygame.display.set_mode((int(WIDTH * SCALE), int(HEIGHT * SCALE)))
        self.screen = pygame.Surface((WIDTH, HEIGHT))
        pygame.display.set_caption("Visual Guzheng AR Beatmapper")
        self.font = pygame.font.SysFont(None, 24)
        self.large_font = pygame.font.SysFont(None, 36)
        self.clock = pygame.time.Clock()

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
                existing_duration = note.get("duration", (15 if note["gesture"] in FIXED_DURATION_GESTURES else 40) / PX_PER_SEC)
                if (target_time < note["time"] + existing_duration) and (note["time"] < target_time + target_duration):
                    return True
                    
        return False

    def save_beatmap(self):
        """Sorts, standardizes, and saves all recorded notes to the JSON file."""
        standardized_notes = []
        
        for note in self.notes:
            gesture = note.get("gesture", "thumb")
            default_duration = (15 if gesture in FIXED_DURATION_GESTURES else 40) / PX_PER_SEC
            standardized_note = {
                "time": note.get("time", 0.0),
                "string": note.get("string", 1),
                "gesture": gesture,
                "duration": round(note.get("duration", default_duration), 3),
                "vibrato": note.get("vibrato", "none")
            }
            standardized_notes.append(standardized_note)

        sorted_notes = sorted(standardized_notes, key=lambda x: x["time"])
        beatmap = {"notes": sorted_notes}
        
        with open(self.output_file, 'w') as f:
            json.dump(beatmap, f, indent=4)
            
        print(f"\n>>> SUCCESS: Saved {len(sorted_notes)} notes to '{self.output_file}' <<<")

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
                "duration": round(duration, 3),
                "vibrato": "none"
            })

    def handle_events(self):
        """Processes all keyboard and mouse inputs."""
        for event in pygame.event.get():
            if event.type == pygame.QUIT:
                self.running = False
                
            elif event.type == pygame.MOUSEBUTTONDOWN:
                mouse_x = int(event.pos[0] / SCALE)
                mouse_y = int(event.pos[1] / SCALE)
                
                if event.button == 1:
                    slider_rect = pygame.Rect(SLIDER_X - 10, SLIDER_Y_MARGIN, SLIDER_WIDTH + 20, SLIDER_TRACK_HEIGHT)
                    if slider_rect.collidepoint(mouse_x, mouse_y):
                        self.dragging_slider = True
                        self.was_playing_before_drag = self.is_playing
                        self.is_playing = False
                        pygame.mixer.music.pause()
                        self._update_slider_visual(mouse_y)
                        continue

                    vol_rect = pygame.Rect(VOL_SLIDER_X - 10, VOL_SLIDER_Y - 10, VOL_SLIDER_WIDTH + 20, VOL_SLIDER_HEIGHT + 20)
                    if vol_rect.collidepoint(mouse_x, mouse_y):
                        self.dragging_volume = True
                        self._update_volume_visual(mouse_x)
                        continue

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
                    edge_rect = pygame.Rect(note_x, note_end_y - 10, note_width, 30) 
                    
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
                            mods = pygame.key.get_mods()
                            if mods & pygame.KMOD_SHIFT:
                                # --- Cycle vibrato ---
                                vib_states = ["none", "light", "heavy"]
                                current_vib = self.notes[clicked_note_idx].get("vibrato", "none")
                                next_vib = vib_states[(vib_states.index(current_vib) + 1) % len(vib_states)]
                                self.notes[clicked_note_idx]["vibrato"] = next_vib
                            else:
                                # --- Cycle gesture ---
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
                    if event.button == 1 and PANEL_WIDTH < mouse_x < SLIDER_X - 10: 
                        time_offset = (PLAYHEAD_Y - mouse_y) / PX_PER_SEC
                        self.seek_audio(self.playhead_time + time_offset)
            
            elif event.type == pygame.MOUSEMOTION:
                mouse_x = int(event.pos[0] / SCALE)
                mouse_y = int(event.pos[1] / SCALE)

                if self.dragging_slider:
                    self._update_slider_visual(event.pos[1])
                elif self.dragging_volume:               
                    self._update_volume_visual(event.pos[0])
                elif self.dragging_note_idx is not None:
                    note = self.notes[self.dragging_note_idx]
                    
                    note_start_y = PLAYHEAD_Y - ((note["time"] - self.playhead_time) * PX_PER_SEC)
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
                    if self.dragging_slider:
                        self.dragging_slider = False
                        self.is_playing = self.was_playing_before_drag
                        self.seek_audio(self.playhead_time)
                    self.dragging_volume = False
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
                elif event.key == pygame.K_UP: self.change_speed(1)
                elif event.key == pygame.K_DOWN: self.change_speed(-1)

    def update(self, delta_time):
        """Updates the game state (like moving the playhead)."""
        if self.is_playing:
            self.playhead_time += (delta_time * self.playback_speed)
            
            if self.playhead_time >= self.audio_length:
                self.playhead_time = self.audio_length
                self.is_playing = False

    def draw_lanes(self):
        """Renders the vertical background lanes and string labels."""
        for i in range(4):
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
            
            note_start_y = PLAYHEAD_Y - ((note["time"] - self.playhead_time) * PX_PER_SEC)
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
                    
                # 1. Draw the main note rectangle
                rect_tuple = (note_x, note_end_y, note_width, note_height)
                pygame.draw.rect(self.screen, color, rect_tuple, border_radius=4)
                
                vibrato = note.get("vibrato", "none")
                if vibrato == "light":
                    pygame.draw.rect(self.screen, (170, 170, 170), rect_tuple, width=2, border_radius=4)
                elif vibrato == "heavy":
                    pygame.draw.rect(self.screen, (255, 255, 255), rect_tuple, width=3, border_radius=4)

                # 2. Draw the symbol text
                symbol = GESTURE_SYMBOLS.get(note["gesture"], "?")
                text_surface = self.font.render(symbol, True, (30, 30, 30)) 
                text_rect = text_surface.get_rect()
                text_rect.centerx = note_x + (note_width // 2)
                
                if note_height <= 30:
                    text_rect.centery = note_end_y + (note_height // 2)
                else:
                    text_rect.bottom = note_start_y - 5
                    
                self.screen.blit(text_surface, text_rect)
                
                # 3. Draw a visual drag handle at the TOP of Tremolo notes
                if note["gesture"] == "tremolo":
                    handle_rect = (note_x + 10, note_end_y, note_width - 20, 15)
                    pygame.draw.rect(self.screen, (255, 255, 255), handle_rect, border_radius=4)

    def draw_ui(self):
        """Renders the left control panel, instructions, and horizontal playhead line."""
        pygame.draw.rect(self.screen, PANEL_BG_COLOR, (0, 0, PANEL_WIDTH, HEIGHT))
        pygame.draw.line(self.screen, (60, 65, 75), (PANEL_WIDTH, 0), (PANEL_WIDTH, HEIGHT), 2)

        pygame.draw.line(self.screen, PLAYHEAD_COLOR, (LANE_START_X - 20, PLAYHEAD_Y), (LANE_START_X + (4 * LANE_WIDTH) + 20, PLAYHEAD_Y), 3)

        status = "PLAYING" if self.is_playing else "PAUSED"
        status_color = (100, 255, 100) if self.is_playing else (255, 255, 100)
        
        self.screen.blit(self.large_font.render(f"> {status}", True, status_color), (20, 20))
        self.screen.blit(self.font.render(f"Time: {self.playhead_time:.2f}s / {self.audio_length:.2f}s", True, TEXT_COLOR), (20, 60))
        self.screen.blit(self.font.render(f"Notes Total: {len(self.notes)}", True, TEXT_COLOR), (20, 80))

        speed_color = (150, 255, 255) if self.playback_speed != 1.0 else TEXT_COLOR
        self.screen.blit(self.font.render(f"Speed: {self.playback_speed}x", True, speed_color), (20, 100))
        
        instructions = [
            "PLAYBACK:",
            "[SPACE] Play / Pause",
            "[UP / DOWN] Change Speed",
            "[Left Click Lane] Scrub Timeline",
            "",
            "ADDING NOTES (Drops at Playhead):",
            "[1] String 1 (Left / Lowest Pitch)",
            "[2] String 2",
            "[3] String 3",
            "[4] String 4 (Right / Highest Pitch)",
            "    * Defaults to Thumb style",
            "",
            "EDITING NOTES:",
            "[Left Click Note] Cycle Gesture",
            "[Shift + Left Click] Cycle Vibrato", 
            "[Right Click Note] Delete",
            "[Drag Note Top] Stretch Tremolo",
            "[Z] Undo Last Added Note",
            "",
            "SYSTEM:",
            "[ESC] Save & Quit"
        ]
        
        y_offset = 130
        for line in instructions:
            color = (180, 180, 200) if line.endswith(":") or line.startswith("---") else (130, 130, 130)
            self.screen.blit(self.font.render(line, True, color), (20, y_offset))
            y_offset += 18

    def run(self):
        """The main application loop."""
        last_tick = pygame.time.get_ticks()

        while self.running:
            current_tick = pygame.time.get_ticks()
            delta_time = (current_tick - last_tick) / 1000.0
            last_tick = current_tick

            self.handle_events()
            self.update(delta_time)

            self.screen.fill(BG_COLOR)
            self.draw_lanes()
            self.draw_notes()
            self.draw_ui()
            self.draw_slider()
            self.draw_volume_slider()

            scaled_surface = pygame.transform.scale(self.screen, (int(WIDTH * SCALE), int(HEIGHT * SCALE)))
            self.window.blit(scaled_surface, (0, 0))
            
            pygame.display.flip()
            self.clock.tick(60) 

        self.save_beatmap()
        self.clean_up()
        pygame.quit()


def main():
    parser = argparse.ArgumentParser(description="Visual Guzheng AR Beatmapper")
    parser.add_argument("-i", "--input", required=True, help="Path to the input audio file")
    parser.add_argument("-o", "--output", required=True, help="Path to the output JSON file")
    args = parser.parse_args()

    app = GuzhengBeatmapper(args.input, args.output)
    app.run()

if __name__ == "__main__":
    main()
