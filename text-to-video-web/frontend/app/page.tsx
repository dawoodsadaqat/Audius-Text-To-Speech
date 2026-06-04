"use client";

import { ChangeEvent, FormEvent, useEffect, useMemo, useState } from "react";

const API_BASE_URL = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5000";

const voices = [
  { value: "en-US-JennyNeural", label: "Jenny - friendly US English" },
  { value: "en-US-GuyNeural", label: "Guy - warm US English" },
  { value: "en-GB-SoniaNeural", label: "Sonia - British English" },
  { value: "en-AU-NatashaNeural", label: "Natasha - Australian English" },
  { value: "pt-BR-FranciscaNeural",label: "Portuguese (Brazil) - Francisca"},
  { value: "pt-BR-AntonioNeural",label: "Portuguese (Brazil) - Antonio"}
];

const simulatedSteps = ["Uploading", "Generating voice", "Rendering video", "Finalizing video"];

type GenerateResponse = {
  success: boolean;
  videoUrl?: string;
  error?: string;
};

export default function Home() {
  const [file, setFile] = useState<File | null>(null);
  const [voiceName, setVoiceName] = useState(voices[0].value);
  const [isGenerating, setIsGenerating] = useState(false);
  const [statusIndex, setStatusIndex] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [videoUrl, setVideoUrl] = useState<string | null>(null);

  const statusMessage = useMemo(() => simulatedSteps[statusIndex] ?? simulatedSteps[0], [statusIndex]);

  useEffect(() => {
    if (!isGenerating) {
      return;
    }

    // The backend MVP returns one response at the end, so this safely simulates progress while the request is pending.
    const interval = window.setInterval(() => {
      setStatusIndex((current) => Math.min(current + 1, simulatedSteps.length - 1));
    }, 3500);

    return () => window.clearInterval(interval);
  }, [isGenerating]);

  function handleFileChange(event: ChangeEvent<HTMLInputElement>) {
    const selectedFile = event.target.files?.[0] ?? null;
    setFile(selectedFile);
    setError(null);
    setVideoUrl(null);
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!file) {
      setError("Please select a .txt file before generating a video.");
      return;
    }

    if (!file.name.toLowerCase().endsWith(".txt")) {
      setError("Only .txt files are supported for this MVP.");
      return;
    }

    setIsGenerating(true);
    setStatusIndex(0);
    setError(null);
    setVideoUrl(null);

    try {
      const formData = new FormData();
      formData.append("file", file);
      formData.append("voiceName", voiceName);

      const response = await fetch(`${API_BASE_URL}/api/video/generate`, {
        method: "POST",
        body: formData,
      });

      const data = (await response.json()) as GenerateResponse;

      if (!response.ok || !data.success || !data.videoUrl) {
        throw new Error(data.error ?? "The backend could not generate the video.");
      }

      setStatusIndex(simulatedSteps.length - 1);
      setVideoUrl(`${API_BASE_URL}${data.videoUrl}`);
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : "An unexpected error occurred.");
    } finally {
      setIsGenerating(false);
    }
  }

  return (
    <main className="min-h-screen bg-slate-50 px-6 py-10 text-slate-900">
      <section className="mx-auto flex max-w-5xl flex-col gap-8 lg:flex-row">
        <div className="flex-1 rounded-3xl bg-white p-8 shadow-xl shadow-slate-200/70 ring-1 ring-slate-200">
          <p className="mb-3 inline-flex rounded-full bg-orange-100 px-4 py-1 text-sm font-semibold text-orange-700">
            MVP generator
          </p>
          <h1 className="text-4xl font-bold tracking-tight sm:text-5xl">Text To Highlight Video Generator</h1>
          <p className="mt-4 text-lg leading-8 text-slate-600">
            Upload a plain text script, choose an Azure neural voice, and generate a vertical 1080×1920 MP4 with synchronized word-by-word highlighting.
          </p>

          <form className="mt-8 space-y-6" onSubmit={handleSubmit}>
            <label className="block">
              <span className="text-sm font-semibold text-slate-700">Text file (.txt)</span>
              <input
                type="file"
                accept=".txt,text/plain"
                onChange={handleFileChange}
                className="mt-2 block w-full rounded-2xl border border-dashed border-slate-300 bg-slate-50 p-4 text-sm file:mr-4 file:rounded-full file:border-0 file:bg-slate-900 file:px-5 file:py-2 file:text-sm file:font-semibold file:text-white hover:border-orange-400"
              />
            </label>

            <label className="block">
              <span className="text-sm font-semibold text-slate-700">Voice</span>
              <select
                value={voiceName}
                onChange={(event) => setVoiceName(event.target.value)}
                className="mt-2 w-full rounded-2xl border border-slate-300 bg-white px-4 py-3 text-sm outline-none ring-orange-400 focus:ring-2"
              >
                {voices.map((voice) => (
                  <option key={voice.value} value={voice.value}>
                    {voice.label}
                  </option>
                ))}
              </select>
            </label>

            <button
              type="submit"
              disabled={!file || isGenerating}
              className="w-full rounded-2xl bg-orange-500 px-6 py-4 text-base font-bold text-white shadow-lg shadow-orange-200 transition hover:bg-orange-600 disabled:cursor-not-allowed disabled:bg-slate-300 disabled:shadow-none"
            >
              {isGenerating ? "Generating..." : "Generate Video"}
            </button>
          </form>
        </div>

        <aside className="w-full rounded-3xl bg-slate-950 p-8 text-white shadow-xl lg:w-[380px]">
          <h2 className="text-2xl font-bold">Output</h2>
          <p className="mt-2 text-sm text-slate-300">Backend rendering only: Azure Speech timestamps, SkiaSharp frames, and FFmpeg MP4 export.</p>

          {isGenerating && (
            <div className="mt-8 rounded-2xl bg-white/10 p-5">
              <div className="mb-4 flex items-center justify-between text-sm">
                <span>{statusMessage}</span>
                <span>{Math.round(((statusIndex + 1) / simulatedSteps.length) * 100)}%</span>
              </div>
              <div className="h-3 overflow-hidden rounded-full bg-white/10">
                <div
                  className="h-full rounded-full bg-orange-400 transition-all duration-700"
                  style={{ width: `${((statusIndex + 1) / simulatedSteps.length) * 100}%` }}
                />
              </div>
            </div>
          )}

          {error && <div className="mt-8 rounded-2xl border border-red-400/40 bg-red-500/10 p-4 text-sm text-red-100">{error}</div>}

          {videoUrl && (
            <div className="mt-8 space-y-4">
              <video src={videoUrl} controls className="aspect-[9/16] w-full rounded-2xl bg-black object-contain" />
              <a
                href={videoUrl}
                download
                className="block rounded-2xl bg-white px-5 py-3 text-center text-sm font-bold text-slate-950 transition hover:bg-orange-100"
              >
                Download video
              </a>
            </div>
          )}

          {!isGenerating && !error && !videoUrl && (
            <div className="mt-8 rounded-2xl border border-white/10 p-5 text-sm text-slate-300">
              Your video preview and download button will appear here after generation succeeds.
            </div>
          )}
        </aside>
      </section>
    </main>
  );
}
