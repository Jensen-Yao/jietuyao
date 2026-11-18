use clap::ArgMatches;
use std::error::Error;
use std::fs;
use std::io::{self};
use std::path::Path;
use std::process::Command as ProcessCommand;
use std::thread;
use std::time::Duration;
use zip::read::ZipArchive;

pub fn handle_update_command(matches: &ArgMatches) -> Result<(), Box<dyn Error>> {
    let archive_path = matches.get_one::<String>("archive").unwrap();
    let wait_time = *matches.get_one::<u64>("wait-time").unwrap();
    let should_clean = matches.get_flag("clean");
    let process_name = matches.get_one::<String>("process-name");
    let auto_start = matches.get_flag("auto-start");
    let verbose = matches.get_flag("verbose");

    if verbose {
        println!("🔧 开始更新程序...");
        println!("   压缩包路径: {}", archive_path);
        if wait_time > 0 {
            println!("   等待时间: {} 秒", wait_time);
        }
        println!("   清理目录: {}", should_clean);
        println!("   自动启动: {}", auto_start);
    }

    if !Path::new(archive_path).exists() {
        return Err(format!("压缩包不存在: {}", archive_path).into());
    }

    if let Some(process) = process_name {
        if verbose {
            println!("🔄 正在关闭进程: {}", process);
        }
        close_process(process, verbose)?;
    }

    if wait_time > 0 {
        if verbose {
            println!("⏳ 等待 {} 秒...", wait_time);
        }
        thread::sleep(Duration::from_secs(wait_time));
    }

    unzip_file_to_parent_dir(archive_path, should_clean)?;

    if verbose {
        println!("✅ 解压完成");
    }

    if auto_start {
        let parent = Path::new(archive_path)
            .parent()
            .and_then(|p| p.parent())
            .ok_or("无法确定程序目录")?;

        let exe_path = parent.join("STranslate.exe");

        if exe_path.exists() {
            if verbose {
                println!("🚀 启动 STranslate.exe...");
            }
            std::process::Command::new(&exe_path).spawn()?;
            println!("✅ 程序已启动");
        } else if verbose {
            println!("⚠️  STranslate.exe 不存在，跳过自动启动");
        }
    }

    println!("✅ 更新完成!");
    Ok(())
}

/// 解压缩打包内容到父目录，可选择清理保留白名单之外的文件夹
fn unzip_file_to_parent_dir(zip_path: &str, clear_dir: bool) -> io::Result<()> {
    let zip_path = Path::new(zip_path);

    if !zip_path.exists() || zip_path.extension().unwrap_or_default() != "zip" {
        return Err(io::Error::new(
            io::ErrorKind::InvalidInput,
            "提供的路径不存在或不是ZIP文件",
        ));
    }

    let grand_parent_dir = match zip_path.parent().and_then(|dir| dir.parent()) {
        Some(grand_parent) => grand_parent,
        None => {
            return Err(io::Error::new(
                io::ErrorKind::NotFound,
                "无法确定上上级目录",
            ));
        }
    };

    if clear_dir {
        let skip_dirs = ["log", "portable_config", "tmp"];

        if let Ok(entries) = fs::read_dir(grand_parent_dir) {
            for entry in entries.flatten() {
                let path = entry.path();
                let name = path.file_name().and_then(|n| n.to_str()).unwrap_or("");

                if skip_dirs.contains(&name) {
                    continue;
                }

                if path.is_dir() {
                    fs::remove_dir_all(&path)?;
                } else {
                    fs::remove_file(&path)?;
                }
            }
        }
    }

    let file = fs::File::open(zip_path)?;
    let mut archive = ZipArchive::new(file)?;

    for i in 0..archive.len() {
        let mut file = archive.by_index(i)?;
        let outpath = grand_parent_dir.join(file.name());

        if file.name().ends_with('/') {
            fs::create_dir_all(&outpath)?;
        } else {
            if let Some(p) = outpath.parent() {
                if !p.exists() {
                    fs::create_dir_all(p)?;
                }
            }
            let mut outfile = fs::File::create(&outpath)?;
            io::copy(&mut file, &mut outfile)?;
        }
    }

    Ok(())
}

fn close_process(process_name: &str, verbose: bool) -> Result<(), Box<dyn Error>> {
    if verbose {
        println!("🔄 正在关闭进程: {}", process_name);
    }

    #[cfg(target_os = "windows")]
    {
        let output = ProcessCommand::new("taskkill")
            .args(&["/F", "/IM", process_name])
            .output()?;

        if !output.status.success() {
            let error = String::from_utf8_lossy(&output.stderr);
            if verbose {
                println!("⚠️  进程可能已经关闭或不存在: {}", error);
            }
        } else if verbose {
            println!("✅ 进程已关闭: {}", process_name);
        }
    }

    Ok(())
}
