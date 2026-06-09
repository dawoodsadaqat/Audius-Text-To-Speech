#!/bin/bash
set -e

RUNTIME_DIR="$HOME/.audius/runtime"
VENV_DIR="$RUNTIME_DIR/venv"

echo "Creating Audius runtime..."
mkdir -p "$RUNTIME_DIR"

echo "Checking Python..."
python3 --version

echo "Creating Python virtual environment..."
python3 -m venv "$VENV_DIR"

echo "Installing Python packages..."
"$VENV_DIR/bin/pip" install --upgrade pip
"$VENV_DIR/bin/pip" install edge-tts stable-ts openai-whisper

echo "Checking FFmpeg..."
if ! command -v ffmpeg >/dev/null 2>&1; then
  echo "FFmpeg is missing. Install it with Homebrew:"
  echo "brew install ffmpeg"
  exit 1
fi

echo "Audius runtime installed successfully."
echo "Python: $VENV_DIR/bin/python3"
