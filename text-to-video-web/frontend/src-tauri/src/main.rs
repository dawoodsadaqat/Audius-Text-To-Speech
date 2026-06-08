use std::sync::Mutex;
use tauri::Manager;
use tauri_plugin_shell::process::{CommandChild, CommandEvent};
use tauri_plugin_shell::ShellExt;

struct BackendProcess(Mutex<Option<CommandChild>>);

#[tauri::command]
fn save_video_from_url(video_url: String, save_path: String) -> Result<(), String> {
    let response = ureq::get(&video_url)
        .call()
        .map_err(|error| format!("Failed to fetch video: {error}"))?;

    let mut reader = response.into_reader();

    let mut file = std::fs::File::create(&save_path)
        .map_err(|error| format!("Failed to create file: {error}"))?;

    std::io::copy(&mut reader, &mut file)
        .map_err(|error| format!("Failed to write file: {error}"))?;

    Ok(())
}

fn main() {
    tauri::Builder::default()
        .plugin(tauri_plugin_shell::init())
        .manage(BackendProcess(Mutex::new(None)))
        .invoke_handler(tauri::generate_handler![save_video_from_url])
        .setup(|app| {
            let shell = app.shell();

            let sidecar_command = shell.sidecar("audius-backend")?;

            let (mut rx, child) = sidecar_command
                .args(["--urls", "http://127.0.0.1:5055"])
                .spawn()?;

            let backend_process = app.state::<BackendProcess>();
            *backend_process.0.lock().unwrap() = Some(child);

            tauri::async_runtime::spawn(async move {
                while let Some(event) = rx.recv().await {
                    match event {
                        CommandEvent::Stdout(line) => {
                            println!("BACKEND STDOUT: {}", String::from_utf8_lossy(&line));
                        }
                        CommandEvent::Stderr(line) => {
                            eprintln!("BACKEND STDERR: {}", String::from_utf8_lossy(&line));
                        }
                        CommandEvent::Error(error) => {
                            eprintln!("BACKEND ERROR: {}", error);
                        }
                        CommandEvent::Terminated(payload) => {
                            eprintln!("BACKEND TERMINATED: {:?}", payload);
                        }
                        _ => {}
                    }
                }
            });

            Ok(())
        })
        .plugin(tauri_plugin_dialog::init())
        .plugin(tauri_plugin_fs::init())
        .plugin(tauri_plugin_opener::init())
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}