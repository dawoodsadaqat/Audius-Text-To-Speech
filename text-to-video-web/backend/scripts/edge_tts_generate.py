import asyncio
import json
import re
import sys
import edge_tts
import stable_whisper
import os

os.environ["PATH"] = (
    "/opt/homebrew/bin:"
    "/usr/local/bin:"
    "/usr/bin:"
    "/bin:"
    "/usr/sbin:"
    "/sbin:"
    + os.environ.get("PATH", "")
)

def clean_word(word):
    return word.strip().replace("\n", " ")


async def generate_audio(text, audio_output, voice_name):
    communicate = edge_tts.Communicate(text, voice_name)

    with open(audio_output, "wb") as audio_file:
        async for chunk in communicate.stream():
            if chunk.get("type") == "audio":
                audio_file.write(chunk["data"])


def align_with_whisper(audio_output, json_output):
    model = stable_whisper.load_model("base")

    result = model.transcribe(
        audio_output,
        word_timestamps=True,
        regroup=False
    )

    data = result.to_dict()
    word_timings = []

    for segment in data.get("segments", []):
        for word in segment.get("words", []):
            text = clean_word(word.get("word", ""))

            if not text:
                continue

            start = word.get("start")
            end = word.get("end")

            if start is None or end is None:
                continue

            word_timings.append({
                "word": text,
                "startSeconds": float(start),
                "endSeconds": float(end)
            })

    if not word_timings:
        raise Exception("Whisper did not generate word-level timestamps.")

    with open(json_output, "w", encoding="utf-8") as f:
        json.dump(word_timings, f, ensure_ascii=False, indent=2)


async def main():
    if len(sys.argv) < 5:
        print("Usage: edge_tts_generate.py <textFile> <audioOutputPath> <jsonOutputPath> <voiceName>")
        sys.exit(1)

    text_file = sys.argv[1]
    audio_output = sys.argv[2]
    json_output = sys.argv[3]
    voice_name = sys.argv[4]

    with open(text_file, "r", encoding="utf-8") as f:
        text = f.read().strip()

    if not text:
        print("Input text is empty.")
        sys.exit(1)

    await generate_audio(text, audio_output, voice_name)

    align_with_whisper(audio_output, json_output)


if __name__ == "__main__":
    asyncio.run(main())