Write-Host "Installing Audius Runtime..."

if (-not (Get-Command python -ErrorAction SilentlyContinue)) {
    Write-Host "Python is required. Please install Python 3.11+ first."
    exit 1
}

if (-not (Get-Command ffmpeg -ErrorAction SilentlyContinue)) {
    Write-Host "FFmpeg is required. Please install FFmpeg and add it to PATH."
    exit 1
}

$runtimePath = "$env:USERPROFILE\.audius\runtime\venv"

python -m venv $runtimePath

& "$runtimePath\Scripts\Activate.ps1"

pip install --upgrade pip
pip install -r requirements.txt

Write-Host "Audius Runtime installed successfully."