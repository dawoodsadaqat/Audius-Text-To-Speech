use std::process::{Child, Command, Stdio};
use std::sync::Mutex;
use tauri::Manager;

struct BackendProcess(Mutex<Option<Child>>);

#[cfg(target_os = "macos")]
fn kill_existing_backend() {
    let _ = Command::new("pkill")
        .args(["-f", "audius-backend"])
        .output();

    let _ = Command::new("bash")
        .args([
            "-c",
            "lsof -ti:5055 | xargs kill -9 2>/dev/null || true",
        ])
        .output();
}

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
        .manage(BackendProcess(Mutex::new(None)))
        .invoke_handler(tauri::generate_handler![save_video_from_url])
        .setup(|app| {
            kill_existing_backend();

            let current_exe = std::env::current_exe()
                .map_err(|error| format!("Could not get current executable: {error}"))?;

            let app_dir = current_exe
                .parent()
                .ok_or("Could not get app executable directory")?;

            let backend_path = app_dir.join("audius-backend");

            if !backend_path.exists() {
                return Err(format!("Backend not found: {:?}", backend_path).into());
            }

            println!("Starting backend from: {:?}", backend_path);
            println!("Backend working directory: {:?}", app_dir);

            let child = Command::new(&backend_path)
                .current_dir(app_dir)
                .args(["--urls", "http://127.0.0.1:5055"])
                .stdout(Stdio::inherit())
                .stderr(Stdio::inherit())
                .spawn()
                .map_err(|error| format!("Failed to start backend: {error}"))?;

            let backend_process = app.state::<BackendProcess>();
            *backend_process.0.lock().unwrap() = Some(child);

            Ok(())
        })
       .on_window_event(|window, event| {
    if let tauri::WindowEvent::CloseRequested { .. } = event {
        if let Some(state) = window.app_handle().try_state::<BackendProcess>() {
            if let Some(mut child) = state.0.lock().unwrap().take() {
                let _ = child.kill();
            }
        }

        kill_existing_backend();

        window.app_handle().exit(0);
    }
})
        .plugin(tauri_plugin_dialog::init())
        .plugin(tauri_plugin_fs::init())
        .plugin(tauri_plugin_opener::init())
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}