#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

fn main() {
    if std::env::args().any(|argument| argument == "--self-test") {
        if let Err(message) = kdzj_tauri_benchmark_lib::self_test() {
            eprintln!("Native FTS5 self-test failed: {message}");
            std::process::exit(1);
        }
        return;
    }
    kdzj_tauri_benchmark_lib::run();
}
