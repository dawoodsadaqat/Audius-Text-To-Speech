#!/bin/bash

set -e

echo "Installing Audius Runtime..."

if ! command -v python3 >/dev/null 2>&1; then
  echo "Python3 is required. Install it first."
  exit 1
fi

if ! command -v ffmpeg >/dev/null 2>&1; then
  echo "Installing FFmpeg..."
  brew install ffmpeg
fi

python3 -m venv "$HOME/.audius/runtime/venv"

source "$HOME/.audius/runtime/venv/bin/activate"

pip install --upgrade pip
pip install -r requirements.txt

echo "Audius Runtime installed successfully."