"use client";

import { ChangeEvent, DragEvent, FormEvent, useEffect, useMemo, useState } from "react";
import { invoke } from "@tauri-apps/api/core";
import { save } from "@tauri-apps/plugin-dialog";
import { readTextFile } from "@tauri-apps/plugin-fs";
import { getCurrentWindow } from "@tauri-apps/api/window";

const API_BASE_URL = "http://localhost:5055";

const voices = [
  { value: "en-US-JennyNeural", label: "Jenny - US English" },
  { value: "en-US-GuyNeural", label: "Guy - US English" },
  { value: "en-GB-SoniaNeural", label: "Sonia - British English" },
  { value: "en-AU-NatashaNeural", label: "Natasha - Australian English" },
  { value: "pt-BR-FranciscaNeural", label: "Francisca - Brazilian Portuguese" },
  { value: "pt-BR-AntonioNeural", label: "Antonio - Brazilian Portuguese" },
];

const simulatedSteps = [
  "Reading legal notes",
  "Creating voice narration",
  "Syncing word highlights",
  "Finalizing MP4",
];

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

  const [runtimeReady, setRuntimeReady] = useState(false);
  const [runtimeMessage, setRuntimeMessage] = useState("Checking Audius runtime...");

  const [licenseActive, setLicenseActive] = useState(false);
  const [licenseMessage, setLicenseMessage] = useState("Checking Audius license...");
  const [isCheckingLicense, setIsCheckingLicense] = useState(false);

  const statusMessage = useMemo(
    () => simulatedSteps[statusIndex] ?? simulatedSteps[0],
    [statusIndex]
  );

  async function checkRuntime() {
    try {
      const response = await fetch(`${API_BASE_URL}/api/system/check`);
      const data = await response.json();

      setRuntimeReady(Boolean(data.runtimeReady));
      setRuntimeMessage(
        data.runtimeReady
          ? "Audius runtime is ready."
          : data.message ?? "Audius runtime is missing."
      );
    } catch (requestError) {
      setRuntimeReady(false);
      setRuntimeMessage(
        requestError instanceof Error
          ? `Runtime check failed: ${requestError.message}`
          : "Runtime check failed."
      );
    }
  }

  async function validateLicense() {
    setIsCheckingLicense(true);
    setLicenseMessage("Checking Audius license...");

    try {
      const response = await fetch(`${API_BASE_URL}/api/license/validate`);
      const data = await response.json();

      setLicenseActive(Boolean(data.active));
      setLicenseMessage(
        data.message ?? (data.active ? "License active." : "License inactive.")
      );
    } catch (requestError) {
      setLicenseActive(false);
      setLicenseMessage(
        requestError instanceof Error
          ? `License validation failed: ${requestError.message}`
          : "License validation failed."
      );
    } finally {
      setIsCheckingLicense(false);
    }
  }

  useEffect(() => {
    checkRuntime();
  }, []);

  useEffect(() => {
    if (runtimeReady) {
      validateLicense();
    }
  }, [runtimeReady]);

  useEffect(() => {
    if (!isGenerating) return;

    const interval = window.setInterval(() => {
      setStatusIndex((current) =>
        Math.min(current + 1, simulatedSteps.length - 1)
      );
    }, 4500);

    return () => window.clearInterval(interval);
  }, [isGenerating]);

  useEffect(() => {
    let unlisten: (() => void) | undefined;

    async function setupTauriDrop() {
      unlisten = await getCurrentWindow().onDragDropEvent(async (event) => {
        const payload: any = event.payload;

        if (payload.type !== "drop") return;

        const droppedPath = payload.paths?.[0];

        if (!droppedPath) {
          setError("No file was dropped.");
          return;
        }

        if (!droppedPath.toLowerCase().endsWith(".txt")) {
          setError("Only .txt files are supported.");
          return;
        }

        try {
          const text = await readTextFile(droppedPath);
          const fileName = droppedPath.split("/").pop() ?? "notes.txt";

          const droppedFile = new File([text], fileName, {
            type: "text/plain",
          });

          setSelectedFile(droppedFile);
        } catch {
          setError("Could not read dropped file.");
        }
      });
    }

    setupTauriDrop();

    return () => {
      if (unlisten) unlisten();
    };
  }, []);

  function setSelectedFile(selectedFile: File | null) {
    setFile(selectedFile);
    setError(null);
    setVideoUrl(null);
  }

  function handleFileChange(event: ChangeEvent<HTMLInputElement>) {
    const selectedFile = event.target.files?.[0] ?? null;
    setSelectedFile(selectedFile);
  }

  function handleDrop(event: DragEvent<HTMLLabelElement>) {
    event.preventDefault();

    const droppedFile = event.dataTransfer.files?.[0] ?? null;

    if (!droppedFile) return;

    if (!droppedFile.name.toLowerCase().endsWith(".txt")) {
      setError("Only .txt files are supported right now.");
      return;
    }

    setSelectedFile(droppedFile);
  }

  function handleDragOver(event: DragEvent<HTMLLabelElement>) {
    event.preventDefault();
  }

  async function downloadVideoFromTauri() {
    if (!videoUrl) {
      alert("No generated video is available.");
      return;
    }

    try {
      const savePath = await save({
        defaultPath: `audius-video-${Date.now()}.mp4`,
        filters: [{ name: "MP4 Video", extensions: ["mp4"] }],
      });

      if (!savePath) return;

      await invoke("save_video_from_url", {
        videoUrl,
        savePath,
      });

      alert(`Video saved successfully:\n${savePath}`);
    } catch (saveError) {
      alert(
        saveError instanceof Error
          ? `Save failed: ${saveError.message}`
          : `Save failed: ${String(saveError)}`
      );
    }
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!runtimeReady) {
      setError("Audius runtime is missing. Please install runtime first.");
      return;
    }

    if (!licenseActive) {
      setError("Audius license is inactive. Please contact support.");
      return;
    }

    if (!file) {
      setError("Please upload a .txt file containing your legal notes.");
      return;
    }

    if (!file.name.toLowerCase().endsWith(".txt")) {
      setError("Only .txt files are supported right now.");
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
        throw new Error(data.error ?? "Audius could not generate the video.");
      }

      setStatusIndex(simulatedSteps.length - 1);
      setVideoUrl(`${API_BASE_URL}${data.videoUrl}`);
    } catch (requestError) {
      setError(
        requestError instanceof Error
          ? requestError.message
          : "An unexpected error occurred."
      );
    } finally {
      setIsGenerating(false);
    }
  }

  return (
    <main className="min-h-screen bg-[#f7f4ee] text-slate-950">
      <section className="relative overflow-hidden bg-slate-950 px-6 py-8 text-white">
        <div className="absolute inset-0 bg-[radial-gradient(circle_at_top_left,rgba(245,158,11,0.28),transparent_35%),radial-gradient(circle_at_bottom_right,rgba(59,130,246,0.16),transparent_30%)]" />

        <div className="relative mx-auto flex max-w-7xl items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="flex h-11 w-11 items-center justify-center rounded-2xl bg-amber-400 text-xl font-black text-slate-950">
              A
            </div>
            <div>
              <p className="text-xl font-black tracking-tight">Audius</p>
              <p className="text-xs uppercase tracking-[0.28em] text-amber-200">
                Legal Study Videos
              </p>
            </div>
          </div>

          <a
            href="#generator"
            className="hidden rounded-full bg-white px-5 py-3 text-sm font-bold text-slate-950 transition hover:bg-amber-200 sm:inline-flex"
          >
            Generate video
          </a>
        </div>

        <div className="relative mx-auto grid max-w-7xl gap-10 py-16 lg:grid-cols-[1.05fr_0.95fr] lg:py-24">
          <div>
            <p className="mb-5 inline-flex rounded-full border border-amber-300/30 bg-amber-300/10 px-4 py-2 text-sm font-bold text-amber-200">
              Built for law notes, case summaries, and exam revision
            </p>

            <h1 className="max-w-4xl text-5xl font-black leading-[1.02] tracking-tight sm:text-6xl lg:text-7xl">
              Turn legal notes into highlighted voice videos.
            </h1>

            <p className="mt-6 max-w-2xl text-lg leading-8 text-slate-300">
              Audius converts plain law text into narrated MP4 videos with
              synchronized word highlighting, helping students, trainers, and
              legal teams revise faster and retain more.
            </p>
          </div>

          <div className="rounded-[2rem] border border-white/10 bg-white/10 p-5 shadow-2xl backdrop-blur">
            <div className="rounded-[1.5rem] bg-white p-5 text-slate-950">
              <div className="rounded-2xl bg-slate-100 p-5">
                <p className="text-xs font-black uppercase tracking-[0.25em] text-slate-500">
                  Video preview concept
                </p>
                <div className="mt-5 aspect-[9/16] rounded-[1.5rem] bg-white p-6 shadow-inner">
                  <div className="flex h-full flex-col justify-center text-center">
                    <p className="text-sm font-bold uppercase tracking-[0.18em] text-slate-400">
                      Constitutional Law
                    </p>
                    <p className="mt-6 text-4xl font-black leading-tight">
                      The <span className="rounded-xl bg-amber-300 px-2">burden</span>{" "}
                      of proof remains on the prosecution.
                    </p>
                    <p className="mt-8 text-sm text-slate-500">
                      Synchronized narration + word highlighting
                    </p>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>

      <section
        id="how-it-works"
        className="mx-auto grid max-w-7xl gap-5 px-6 py-16 md:grid-cols-4"
      >
        {[
          ["01", "Upload notes", "Add plain text from law lectures, case briefs, or exam summaries."],
          ["02", "Choose voice", "Select a clear narration voice for your legal study material."],
          ["03", "Sync highlights", "Audius aligns words with narration for focused revision."],
          ["04", "Export MP4", "Download a vertical video ready for mobile viewing or sharing."],
        ].map(([number, title, description]) => (
          <div
            key={number}
            className="rounded-3xl bg-white p-6 shadow-sm ring-1 ring-slate-200"
          >
            <p className="text-sm font-black text-amber-600">{number}</p>
            <h3 className="mt-4 text-xl font-black">{title}</h3>
            <p className="mt-3 text-sm leading-6 text-slate-600">
              {description}
            </p>
          </div>
        ))}
      </section>

      <section
        id="generator"
        className="mx-auto grid max-w-7xl gap-8 px-6 pb-20 lg:grid-cols-[1fr_420px]"
      >
        <div className="rounded-[2rem] bg-white p-8 shadow-xl shadow-slate-200/70 ring-1 ring-slate-200">
          <p className="mb-3 inline-flex rounded-full bg-amber-100 px-4 py-1 text-sm font-bold text-amber-800">
            Audius Generator
          </p>

          <h2 className="text-4xl font-black tracking-tight">
            Create a highlighted law video
          </h2>

          <p className="mt-4 max-w-2xl text-lg leading-8 text-slate-600">
            Upload your legal notes, select a narration voice, and Audius will
            generate a vertical study video with synchronized highlighting.
          </p>

          <div className="mt-8 rounded-3xl border border-slate-200 bg-slate-50 p-5">
            <div className="flex items-start justify-between gap-4">
              <div>
                <h3 className="text-lg font-black text-slate-950">
                  Audius Runtime
                </h3>
                <p className="mt-1 text-sm text-slate-600">
                  Local runtime is checked automatically.
                </p>
              </div>

              <span
                className={`rounded-full px-3 py-1 text-xs font-black ${
                  runtimeReady
                    ? "bg-green-100 text-green-700"
                    : "bg-red-100 text-red-700"
                }`}
              >
                {runtimeReady ? "Ready" : "Missing"}
              </span>
            </div>

            <p
              className={`mt-3 text-sm font-semibold ${
                runtimeReady ? "text-green-700" : "text-red-700"
              }`}
            >
              {runtimeMessage}
            </p>

            <button
              type="button"
              onClick={checkRuntime}
              className="mt-3 rounded-xl bg-slate-900 px-4 py-2 text-sm font-bold text-white"
            >
              Recheck Runtime
            </button>
          </div>

          <div className="mt-5 rounded-3xl border border-slate-200 bg-slate-50 p-5">
            <div className="flex items-start justify-between gap-4">
              <div>
                <h3 className="text-lg font-black text-slate-950">
                  Audius License
                </h3>
                <p className="mt-1 text-sm text-slate-600">
                  Audius validates the installed license automatically.
                </p>
              </div>

              <span
                className={`rounded-full px-3 py-1 text-xs font-black ${
                  licenseActive
                    ? "bg-green-100 text-green-700"
                    : "bg-red-100 text-red-700"
                }`}
              >
                {licenseActive ? "Active" : "Inactive"}
              </span>
            </div>

            <p
              className={`mt-3 text-sm font-semibold ${
                licenseActive ? "text-green-700" : "text-red-700"
              }`}
            >
              {isCheckingLicense ? "Checking license..." : licenseMessage}
            </p>

            <button
              type="button"
              onClick={validateLicense}
              disabled={!runtimeReady || isCheckingLicense}
              className="mt-3 rounded-xl bg-slate-900 px-4 py-2 text-sm font-bold text-white disabled:bg-slate-400"
            >
              {isCheckingLicense ? "Checking..." : "Recheck License"}
            </button>
          </div>

          <form className="mt-8 space-y-6" onSubmit={handleSubmit}>
            <label
              onDrop={handleDrop}
              onDragOver={handleDragOver}
              className="block cursor-pointer rounded-3xl border-2 border-dashed border-slate-300 bg-slate-50 p-6 text-center transition hover:border-amber-400 hover:bg-amber-50"
            >
              <span className="block text-sm font-black text-slate-700">
                Drag & drop your legal notes here
              </span>
              <span className="mt-2 block text-sm text-slate-500">
                or click to browse a .txt file
              </span>

              {file && (
                <span className="mt-4 block rounded-2xl bg-white px-4 py-3 text-sm font-bold text-slate-950 ring-1 ring-slate-200">
                  Selected: {file.name}
                </span>
              )}

              <input
                type="file"
                accept=".txt,text/plain"
                onChange={handleFileChange}
                className="hidden"
              />
            </label>

            <label className="block">
              <span className="text-sm font-bold text-slate-700">
                Narration voice
              </span>
              <select
                value={voiceName}
                onChange={(event) => setVoiceName(event.target.value)}
                className="mt-2 w-full rounded-2xl border border-slate-300 bg-white px-4 py-3 text-sm outline-none ring-amber-400 focus:ring-2"
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
              disabled={!file || isGenerating || !runtimeReady || !licenseActive}
              className="w-full rounded-2xl bg-amber-400 px-6 py-4 text-base font-black text-slate-950 shadow-lg shadow-amber-200 transition hover:bg-amber-300 disabled:cursor-not-allowed disabled:bg-slate-300 disabled:shadow-none"
            >
              {isGenerating ? "Generating legal video..." : "Generate video"}
            </button>
          </form>
        </div>

        <aside className="rounded-[2rem] bg-slate-950 p-8 text-white shadow-xl">
          <h2 className="text-2xl font-black">Output</h2>
          <p className="mt-2 text-sm leading-6 text-slate-300">
            Audius creates AI voice narration, aligns words with audio, and
            exports a mobile-ready MP4.
          </p>

          {isGenerating && (
            <div className="mt-8 rounded-2xl bg-white/10 p-5">
              <div className="mb-4 flex items-center justify-between text-sm">
                <span>{statusMessage}</span>
                <span>
                  {Math.round(((statusIndex + 1) / simulatedSteps.length) * 100)}
                  %
                </span>
              </div>
              <div className="h-3 overflow-hidden rounded-full bg-white/10">
                <div
                  className="h-full rounded-full bg-amber-400 transition-all duration-700"
                  style={{
                    width: `${((statusIndex + 1) / simulatedSteps.length) * 100}%`,
                  }}
                />
              </div>
              <p className="mt-4 text-xs leading-5 text-slate-400">
                Longer legal notes may take more time. Keep demo files short for
                faster generation.
              </p>
            </div>
          )}

          {error && (
            <div className="mt-8 rounded-2xl border border-red-400/40 bg-red-500/10 p-4 text-sm text-red-100">
              {error}
            </div>
          )}

          {videoUrl && (
            <div className="mt-8 space-y-4">
              <button
                type="button"
                onClick={downloadVideoFromTauri}
                className="block w-full rounded-2xl bg-white px-5 py-4 text-center text-sm font-black text-slate-950 transition hover:bg-amber-100"
              >
                Save generated video
              </button>

              <button
                type="button"
                onClick={() => window.open(videoUrl, "_blank")}
                className="block w-full rounded-2xl border border-white/10 px-5 py-4 text-center text-sm font-black text-white transition hover:bg-white/10"
              >
                Open video in browser
              </button>

              <p className="text-xs leading-5 text-slate-400">
                Save is handled directly by Audius.
              </p>
            </div>
          )}

          {!isGenerating && !error && !videoUrl && (
            <div className="mt-8 rounded-2xl border border-white/10 p-5 text-sm leading-6 text-slate-300">
              Runtime and license are checked automatically. Upload a .txt legal
              note file and create your first Audius video.
            </div>
          )}
        </aside>
      </section>
    </main>
  );
}