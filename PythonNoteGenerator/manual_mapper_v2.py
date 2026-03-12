import pygame
import json
import sys
import argparse
import os

# --- Visual Layout Constants ---
WIDTH, HEIGHT = 1000, 600
LANE_HEIGHT = 60
LANE_START_Y = 150
PLAYHEAD_X = 300      
PX_PER_SEC = 200      

# --- Colors ---
BG_COLOR = (30, 33, 36)
LANE_COLOR_1 = (40, 44, 52)
LANE_COLOR_2 = (45, 50, 58)
PLAYHEAD_COLOR = (255, 50, 50)
TEXT_COLOR = (220, 220, 220)

COLOR_TUO = (50, 255, 50)       
COLOR_MUO = (50, 150, 255)      
COLOR_TREMOLO = (255, 150, 0)   


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

    def _is_overlapping(self, target_time, string_num, target_gesture, ignore_idx=None):
        """Calculates if a proposed note overlaps with existing notes on the same string."""
        # Convert pixel width to time duration
        target_duration = (15 if target_gesture in ["tuo", "muo"] else 40) / PX_PER_SEC
        
        for i, note in enumerate(self.notes):
            if i == ignore_idx:
                continue
                
            if note["string"] == string_num:
                existing_duration = (15 if note["gesture"] in ["tuo", "muo"] else 40) / PX_PER_SEC
                
                # Overlap logic: Start A < End B AND Start B < End A
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
        """Records a note at the current playhead time if it doesn't overlap with an e xisting note."""
        if 0 <= self.playhead_time <= self.audio_length:
            new_time = round(self.playhead_time, 3)
            
            if self._is_overlapping(new_time, string_num, gesture):
                print(f"Blocked overlap on String {string_num} at {new_time}s")
                return
                
            self.notes.append({
                "time": new_time,
                "string": string_num,
                "gesture": gesture
            })

    def handle_events(self):
        """Processes all keyboard and mouse inputs."""
        for event in pygame.event.get():
            if event.type == pygame.QUIT:
                self.running = False
                
            elif event.type == pygame.MOUSEBUTTONDOWN:
                mouse_x, mouse_y = event.pos
                clicked_note_idx = None
                
                # 1. Check for collisions with existing notes 
                # (Iterating backwards ensures we click the top note if they overlap)
                for i in range(len(self.notes) - 1, -1, -1):
                    note = self.notes[i]
                    note_x = PLAYHEAD_X + ((note["time"] - self.playhead_time) * PX_PER_SEC)
                    note_y = LANE_START_Y + ((note["string"] - 1) * LANE_HEIGHT) + 10
                    
                    # Recreate the bounding box width from draw_notes
                    note_width = 15 if note["gesture"] in ["tuo", "muo"] else 40
                    note_height = LANE_HEIGHT - 20
                    
                    note_rect = pygame.Rect(note_x, note_y, note_width, note_height)
                    
                    if note_rect.collidepoint(mouse_x, mouse_y):
                        clicked_note_idx = i
                        break

                # 2. Handle the click based on whether a note was hit
                if clicked_note_idx is not None:
                    # We clicked a note!
                    if event.button == 1:  # Left Click: Cycle the gesture
                        gestures = ["tuo", "muo", "tremolo"]
                        current = self.notes[clicked_note_idx]["gesture"]
                        next_idx = (gestures.index(current) + 1) % len(gestures)
                        self.notes[clicked_note_idx]["gesture"] = gestures[next_idx]
                        
                    elif event.button == 3:  # Right Click: Delete the note
                        self.notes.pop(clicked_note_idx)
                else:
                    # We clicked empty space
                    if event.button == 1:  # Left Click: Scrub the timeline
                        time_offset = (mouse_x - PLAYHEAD_X) / PX_PER_SEC
                        self.seek_audio(self.playhead_time + time_offset)

            elif event.type == pygame.KEYDOWN:
                if event.key == pygame.K_ESCAPE:
                    self.running = False
                    
                elif event.key == pygame.K_SPACE:
                    self.is_playing = not self.is_playing
                    if self.is_playing:
                        pygame.mixer.music.play(0, start=self.playhead_time)
                    else:
                        pygame.mixer.music.pause()

                elif event.key == pygame.K_z: 
                    if len(self.notes) > 0:
                        self.notes.pop()

                # 3. Simplified Gestures: Just drop the default "tuo"
                elif event.key == pygame.K_1: self.add_note(1, "tuo")
                elif event.key == pygame.K_2: self.add_note(2, "tuo")
                elif event.key == pygame.K_3: self.add_note(3, "tuo")
                elif event.key == pygame.K_4: self.add_note(4, "tuo")
                elif event.key == pygame.K_5: self.add_note(5, "tuo")

    def update(self, delta_time):
        """Updates the game state (like moving the playhead)."""
        if self.is_playing:
            self.playhead_time += delta_time
            if self.playhead_time >= self.audio_length:
                self.playhead_time = self.audio_length
                self.is_playing = False

    def draw_lanes(self):
        """Renders the background lanes and string labels."""
        for i in range(5):
            y = LANE_START_Y + (i * LANE_HEIGHT)
            color = LANE_COLOR_1 if i % 2 == 0 else LANE_COLOR_2
            pygame.draw.rect(self.screen, color, (0, y, WIDTH, LANE_HEIGHT))
            
            label = self.font.render(f"String {i+1}", True, (100, 100, 100))
            self.screen.blit(label, (10, y + 20))

    def draw_notes(self):
        """Renders the recorded notes on the timeline."""
        for note in self.notes:
            note_x = PLAYHEAD_X + ((note["time"] - self.playhead_time) * PX_PER_SEC)
            
            # Only draw if it's visible on screen
            if -50 < note_x < WIDTH + 50:
                note_y = LANE_START_Y + ((note["string"] - 1) * LANE_HEIGHT) + 10
                
                if note["gesture"] == "tuo":
                    color, note_width = COLOR_TUO, 15
                elif note["gesture"] == "muo":
                    color, note_width = COLOR_MUO, 15
                else: 
                    color, note_width = COLOR_TREMOLO, 40 
                    
                pygame.draw.rect(self.screen, color, (note_x, note_y, note_width, LANE_HEIGHT - 20), border_radius=4)

    def draw_ui(self):
        """Renders the text, playhead line, and status indicators."""
        # The Red Playhead Line
        pygame.draw.line(self.screen, PLAYHEAD_COLOR, (PLAYHEAD_X, LANE_START_Y - 20), (PLAYHEAD_X, LANE_START_Y + (5 * LANE_HEIGHT) + 20), 3)

        status = "> PLAYING" if self.is_playing else "|| PAUSED"
        status_color = (100, 255, 100) if self.is_playing else (255, 255, 100)
        
        self.screen.blit(self.large_font.render(status, True, status_color), (20, 20))
        self.screen.blit(self.font.render(f"Time: {self.playhead_time:.2f}s / {self.audio_length:.2f}s", True, TEXT_COLOR), (20, 60))
        self.screen.blit(self.font.render(f"Notes Total: {len(self.notes)}", True, TEXT_COLOR), (20, 85))
        
        controls_intuition = "String 1 (Highest Pitch) to String 5 (Lowest Pitch)"
        controls_general = "General: [SPACE] Play/Pause | [Left Click Empty] Scrub | [Z] Undo | [ESC] Save & Quit"
        controls_notes = "Notes: [1-5] Add Tuo | [Left Click Note] Cycle Style | [Right Click Note] Delete"
        
        self.screen.blit(self.font.render(controls_intuition, True, (150, 150, 150)), (20, HEIGHT - 80))
        self.screen.blit(self.font.render(controls_general, True, (150, 150, 150)), (20, HEIGHT - 55))
        self.screen.blit(self.font.render(controls_notes, True, (150, 150, 150)), (20, HEIGHT - 30))

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
