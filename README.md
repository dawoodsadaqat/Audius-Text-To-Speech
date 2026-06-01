# Text To Highlight Video Generator MVP

A local MVP web app that turns an uploaded `.txt` file into a vertical 1080x1920 MP4 with synchronized word-by-word highlighting.

## Stack

- Frontend: Next.js 15, TypeScript, Tailwind CSS
- Backend: ASP.NET Core .NET 8 Web API
- Text-to-speech: Azure Cognitive Services Speech SDK
- Rendering: SkiaSharp PNG frames
- Video export: FFmpeg
- Storage: local `text-to-video-web/backend/outputs` folder

## Project structure

```text
text-to-video-web/
  frontend/
  backend/
```

## Prerequisites

Install these locally:

1. .NET 8 SDK
2. Node.js 20+
3. FFmpeg available on your PATH, or set the full path in `backend/appsettings.json`
4. Azure Speech resource key and region

## Backend setup

Edit `text-to-video-web/backend/appsettings.json`:

```json
{
  "AzureSpeech": {
    "Key": "YOUR_AZURE_SPEECH_KEY",
    "Region": "YOUR_AZURE_REGION"
  },
  "Ffmpeg": {
    "Path": "ffmpeg"
  }
}
```

Run the API:

```bash
cd text-to-video-web/backend
dotnet restore
dotnet run
```

The backend runs at `http://localhost:5000` and exposes:

- `POST /api/video/generate`
- static generated files under `/outputs/{jobId}/final.mp4`

## Frontend setup

```bash
cd text-to-video-web/frontend
npm install
npm run dev
```

The frontend runs at `http://localhost:3000`.

If your API is not on `http://localhost:5000`, set `NEXT_PUBLIC_API_BASE_URL` before starting the frontend.

## MVP flow

1. Upload a `.txt` file.
2. Pick an Azure neural voice.
3. The backend validates and reads the text.
4. Azure Speech SDK writes `audio.wav` and returns word-boundary timestamps.
5. SkiaSharp renders 1080x1920 PNG frames at 30 FPS with an orange highlight behind the active word.
6. FFmpeg merges frames and audio into `final.mp4`.
7. The frontend shows a video preview and download link.

## Notes

- This MVP intentionally has no database, authentication, payments, templates, background music, PDF support, or DOCX support.
- Generated job files are kept in `backend/outputs` for local inspection and troubleshooting.
