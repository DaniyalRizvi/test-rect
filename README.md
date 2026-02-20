# Memory Game (Unity 6.3)

This project is made with Unity 6.3.

## Game Overview
A classic memory matching game where you flip cards to find pairs. Each level increases the grid size and difficulty, with limited moves and combo-based scoring.

## How It Works
- The game spawns a grid of face-down cards with paired faces.
- Tap two cards to reveal them; matching pairs stay revealed and score points.
- A mismatch consumes moves and flips the cards back after a short delay.
- Complete the grid within the move limit to advance to the next level.

## Scripting API

### `AudioManager`
Handles playback of UI, match, win, and game-over audio cues.
Centralizes audio access for gameplay events.

### `CardView`
Controls card visuals, flip animation, input handling, and match effects.
Exposes card state and click events to the game controller.

### `MemoryGameController`
Drives level setup, scoring, move limits, and progression logic.
Manages saving/loading, UI updates, and game state transitions.

### `JellyButtonFX`
Applies a jelly-style scale animation to buttons on interaction.
Enhances UI feedback on presses.

### `JellyDialogPop`
Animates dialog pop-in/out effects for UI panels.
Provides an elastic-style UI entrance.