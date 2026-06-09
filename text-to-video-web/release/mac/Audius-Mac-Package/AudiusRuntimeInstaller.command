#!/bin/bash

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

cd "$SCRIPT_DIR"

chmod +x install-runtime.sh

./install-runtime.sh

echo ""
echo "Audius Runtime Installation Complete"
echo ""

read -p "Press Enter to close..."
