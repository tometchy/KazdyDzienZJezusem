//! Native SQLite FTS5 control path for WebKitGTK (which may lack OPFS).
//! This is a native benchmark, NOT a claim that SQLite-WASM/SAH Pool works in Tauri.
use flate2::read::GzDecoder;
use rusqlite::{params, Connection};
use serde_json::{json, Value};
use sha2::{Digest, Sha256};
use std::{
    fs,
    io::Read,
    path::Path,
    sync::Mutex,
    time::{Instant, SystemTime, UNIX_EPOCH},
};
use tauri::Manager;

const SNAPSHOT_GZIP: &[u8] =
    include_bytes!(concat!(env!("CARGO_MANIFEST_DIR"), "/../dist/corpus-fts5.sqlite3.gz"));
const MANIFEST: &str =
    include_str!(concat!(env!("CARGO_MANIFEST_DIR"), "/../dist/corpus-manifest.json"));

const QUERIES: [(&str, &str); 10] = [
    ("Polskie OR", "pl:wiara OR pl:milosc"),
    ("Dokładna fraza", "pl:\"na poczatku\""),
    ("Grecka forma", "gr:λογοσ"),
    ("Greka i polski", "gr:αγαπη AND pl:milosc"),
    ("Tylko TR", "source:tr AND gr:λογοσ"),
    ("Pisma ojców", "source:ojcowie AND gr:αγαπη"),
    ("Trudniejsze AND/OR", "pl:zycie AND (pl:swiatlo OR pl:prawda)"),
    ("Grecka fraza", "gr:\"λογοσ ην\""),
    ("Grecka forma w słowniku", "source:slownik AND gr:λογοσ"),
    ("Tylko TNP", "source:tnp AND pl:swiatlo"),
];

fn millis(started: Instant) -> f64 {
    (started.elapsed().as_secs_f64() * 1000.0 * 1000.0).round() / 1000.0
}

fn percentile(values: &[f64], portion: f64) -> f64 {
    let mut values = values.to_vec();
    values.sort_by(|a, b| a.total_cmp(b));
    let index = ((portion * values.len() as f64).ceil() as usize).saturating_sub(1);
    values[index.min(values.len() - 1)]
}

fn ensure_db(path: &Path, manifest: &Value) -> Result<(bool, f64, f64), String> {
    if path.exists() {
        let size = fs::metadata(path).map_err(|e| e.to_string())?.len();
        if size == manifest["sqlite_bytes"].as_u64().unwrap_or(0) {
            return Ok((true, 0.0, 0.0));
        }
        fs::remove_file(path).map_err(|e| e.to_string())?;
    }
    let decompress_start = Instant::now();
    let mut bytes = Vec::new();
    GzDecoder::new(SNAPSHOT_GZIP)
        .read_to_end(&mut bytes).map_err(|e| format!("Błąd gzip: {e}"))?;
    let decompress_ms = millis(decompress_start);
    let expected_len = manifest["sqlite_bytes"].as_u64().ok_or("Brak sqlite_bytes")?;
    if bytes.len() as u64 != expected_len || !bytes.starts_with(b"SQLite format 3\0") {
        return Err("Niewłaściwa rozpakowana baza SQLite".into());
    }
    let hash = format!("{:x}", Sha256::digest(&bytes));
    if hash != manifest["sqlite_sha256"].as_str().unwrap_or_default() {
        return Err("SHA-256 wbudowanego SQLite różny od manifestu".into());
    }
    let write_start = Instant::now();
    let tmp = path.with_extension("sqlite3.tmp");
    fs::write(&tmp, &bytes).map_err(|e| format!("Nie można zapisać SQLite: {e}"))?;
    fs::rename(&tmp, path).map_err(|e| format!("Nie można zatwierdzić SQLite: {e}"))?;
    Ok((false, decompress_ms, millis(write_start)))
}

fn query_top20(connection: &Connection, query: &str) -> Result<(f64, usize), String> {
    let started = Instant::now();
    let mut stmt = connection.prepare_cached(
        "SELECT source, ref, pl, gr, en, lemma FROM docs_fts WHERE docs_fts MATCH ?1 LIMIT 20"
    ).map_err(|e| e.to_string())?;
    let mut rows = stmt.query(params![query]).map_err(|e| e.to_string())?;
    let mut count = 0;
    while let Some(row) = rows.next().map_err(|e| e.to_string())? {
        for column in 0..6 {
            let _: String = row.get(column).map_err(|e| e.to_string())?;
        }
        count += 1;
    }
    Ok((millis(started), count))
}

fn query_count(connection: &Connection, query: &str) -> Result<(f64, u64), String> {
    let started = Instant::now();
    let hits: u64 = connection.query_row(
        "SELECT COUNT(*) FROM docs_fts WHERE docs_fts MATCH ?1",
        params![query], |row| row.get(0)
    ).map_err(|e| e.to_string())?;
    Ok((millis(started), hits))
}

fn run_native(app: tauri::AppHandle, reset: bool) -> Result<Value, String> {
    // A single Tauri app may have more than one webview. Serialize native
    // reads and cold resets, never delete other users' application files.
    let guard = app.state::<Mutex<()>>();
    let _lock = guard.lock().map_err(|e| e.to_string())?;
    let started = Instant::now();
    let manifest: Value = serde_json::from_str(MANIFEST).map_err(|e| e.to_string())?;
    let sha = manifest["sqlite_sha256"].as_str().ok_or("Brak SHA bazy")?;
    let base = app.path().app_data_dir().map_err(|e| e.to_string())?;
    fs::create_dir_all(&base).map_err(|e| e.to_string())?;
    let path = base.join(format!("fts5-{}.sqlite3", &sha[..16]));
    if reset && path.exists() {
        fs::remove_file(&path).map_err(|e| format!("Nie można zresetować indeksu: {e}"))?;
    }
    let (reused, decompress_ms, write_ms) = ensure_db(&path, &manifest)?;
    let open_started = Instant::now();
    let db = Connection::open(&path).map_err(|e| format!("Nie można otworzyć SQLite: {e}"))?;
    let sqlite_version: String = db.query_row("SELECT sqlite_version()", [], |r| r.get(0))
        .map_err(|e| e.to_string())?;
    let documents: u64 = db.query_row("SELECT COUNT(*) FROM docs_fts", [], |r| r.get(0))
        .map_err(|e| e.to_string())?;
    if documents != manifest["records"].as_u64().ok_or("Brak liczby rekordów")? {
        return Err("Liczba wierszy różna od manifestu".into());
    }
    db.execute_batch("PRAGMA query_only=ON").map_err(|e| e.to_string())?;
    let db_open_ms = millis(open_started);
    let total_ready_ms = millis(started);

    let mut results = Vec::new();
    let mut typical = Vec::new();
    let mut slowest: f64 = 0.0;
    for (name, query) in QUERIES {
        let mut top20 = Vec::new();
        let mut counts = Vec::new();
        let mut hits = 0;
        for _ in 0..20 {
            let (time, _) = query_top20(&db, query)?;
            top20.push(time);
            let (time, hit_count) = query_count(&db, query)?;
            counts.push(time);
            hits = hit_count;
        }
        let p50 = percentile(&top20, 0.5);
        let p95 = percentile(&top20, 0.95);
        typical.push(p50);
        slowest = slowest.max(p95);
        results.push(json!({
            "name": name, "query": query, "hits": hits,
            "top20_p50_ms": p50, "top20_p95_ms": p95,
            "count_p50_ms": percentile(&counts, 0.5),
            "count_p95_ms": percentile(&counts, 0.95)
        }));
    }
    let timestamp = SystemTime::now().duration_since(UNIX_EPOCH)
        .map_err(|e| e.to_string())?.as_millis();
    Ok(json!({
        "version": 2, "engine": "Natywne SQLite FTS5 / Tauri 2",
        "created_at_unix_ms": timestamp,
        "device": { "platform": "Linux ARM64 / postmarketOS (weryfikacja w urządzeniu)" },
        "meta": {
            "strategy": "native-sqlite", "mode": "Natywny plik SQLite (trwały)",
            "sqlite_version": sqlite_version, "persisted": true,
            "documents": documents, "sources": manifest["sources"],
            "books_by_source": manifest["books_by_source"],
            "corpus_hash": manifest["gzip_sha256"],
            "sqlite_snapshot_sha256": sha,
            "database_bytes": fs::metadata(path).map_err(|e| e.to_string())?.len(),
            "download_ms": 0, "downloaded_bytes": 0,
            "decompress_ms": decompress_ms, "import_ms": write_ms,
            "db_open_ms": db_open_ms,
            "index_built": !reused, "index_imported": !reused,
            "index_reused": reused, "total_ready_ms": total_ready_ms,
            "memory_bytes": null,
            "cache_note": "Ten pomiar dotyczy natywnego SQLite, NIE WebKit OPFS/SAH Pool"
        },
        "rounds": 20, "results": results,
        "summary": {
            "typical_top20_p50_ms": percentile(&typical, 0.5),
            "slowest_top20_p95_ms": slowest
        }
    }))
}

#[tauri::command]
async fn native_benchmark(app: tauri::AppHandle, reset: bool) -> Result<Value, String> {
    tauri::async_runtime::spawn_blocking(move || run_native(app, reset))
        .await.map_err(|error| error.to_string())?
}

#[tauri::command]
fn save_report_json(app: tauri::AppHandle, contents: String) -> Result<String, String> {
    if contents.len() > 2_000_000 {
        return Err("Raport przekracza dopuszczalny rozmiar 2 MB".into());
    }
    let parsed: Value = serde_json::from_str(&contents).map_err(|e| e.to_string())?;
    if parsed["kind"] != "tauri-postmarketos-native-sqlite" {
        return Err("Niepoprawny typ raportu".into());
    }
    let directory = app.path().download_dir()
        .or_else(|_| app.path().app_data_dir())
        .map_err(|e| e.to_string())?;
    fs::create_dir_all(&directory).map_err(|e| e.to_string())?;
    let millis = SystemTime::now().duration_since(UNIX_EPOCH)
        .map_err(|e| e.to_string())?.as_millis();
    let output = directory.join(format!("kdzj-tauri-postmarketos-{millis}.json"));
    fs::write(&output, contents).map_err(|e| e.to_string())?;
    Ok(output.display().to_string())
}

/// Headless verification of the exact executable and its embedded corpus.
pub fn self_test() -> Result<(), String> {
    let manifest: Value = serde_json::from_str(MANIFEST).map_err(|e| e.to_string())?;
    let directory = std::env::temp_dir().join(format!("kdzj-native-selftest-{}", std::process::id()));
    fs::create_dir_all(&directory).map_err(|e| e.to_string())?;
    let file = directory.join("snapshot.sqlite3");
    let result = (|| {
        let (_reused, decompress_ms, write_ms) = ensure_db(&file, &manifest)?;
        let db = Connection::open(&file).map_err(|e| e.to_string())?;
        let total: u64 = db.query_row("SELECT COUNT(*) FROM docs_fts", [], |row| row.get(0))
            .map_err(|e| e.to_string())?;
        if total != manifest["records"].as_u64().ok_or("Brak records")? {
            return Err(format!("Nieprawidłowa liczba dokumentów: {total}"));
        }
        for (name, query) in QUERIES {
            let (_time, hits) = query_count(&db, query)?;
            if hits == 0 {
                return Err(format!("Pusty wynik FTS5: {name} / {query}"));
            }
        }
        println!("PASS native musl SQLite FTS5: {total} documents, 10 queries, gzip {decompress_ms} ms, disk {write_ms} ms");
        Ok(())
    })();
    let _ = fs::remove_dir_all(directory);
    result
}

pub fn run() {
    tauri::Builder::default()
        .manage(Mutex::new(()))
        .invoke_handler(tauri::generate_handler![native_benchmark, save_report_json])
        .run(tauri::generate_context!())
        .expect("Nie udało się uruchomić aplikacji Tauri");
}
