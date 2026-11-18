use clap::{ArgMatches, ValueEnum};
use std::error::Error;
use std::fs::{self, File};
use std::io::Write;
use std::path::{Path, PathBuf};
use std::process::Command as ProcessCommand;
use std::thread;
use std::time::Duration;

#[derive(Clone, Debug, ValueEnum)]
pub enum PortableMode {
    /// 启用便携模式（漫游 -> 便携）
    Enable,
    /// 关闭便携模式（便携 -> 漫游）
    Disable,
}

pub fn handle_portable_command(matches: &ArgMatches) -> Result<(), Box<dyn Error>> {
    let mode = matches.get_one::<PortableMode>("mode").unwrap();
    let source = matches.get_one::<String>("source").unwrap();
    let target = matches.get_one::<String>("target").unwrap();
    let delay = *matches.get_one::<u64>("delay").unwrap();
    let restart_target = matches.get_one::<String>("restart-target").unwrap();
    let info_file = matches.get_one::<String>("info-file").unwrap();
    let success_message = matches.get_one::<String>("success-message").unwrap();
    let failure_prefix = matches.get_one::<String>("failure-prefix").unwrap();
    let verbose = matches.get_flag("verbose");

    if verbose {
        println!("🚚 准备执行便携模式迁移...");
        println!("   模式: {:?}", mode);
        println!("   源目录: {}", source);
        println!("   目标目录: {}", target);
        println!("   重启目标: {}", restart_target);
    }

    if delay > 0 {
        if verbose {
            println!("⏳ 延迟 {} 秒后开始迁移...", delay);
        }
        thread::sleep(Duration::from_secs(delay));
    }

    let migration_result = execute_portable_migration(source, target, verbose);
    let info_message = match &migration_result {
        Ok(_) => success_message.to_string(),
        Err(err) => format!("{}: {}", failure_prefix, err),
    };

    if let Err(err) = write_info_file(info_file, &info_message) {
        eprintln!("⚠️ 写入提示文件失败: {}", err);
    }

    let restart_result = restart_application(restart_target, verbose);
    if let Err(err) = restart_result {
        eprintln!("⚠️ 重启失败: {}", err);
        if migration_result.is_ok() {
            return Err(format!("迁移成功，但重启失败: {}", err).into());
        }
    }

    migration_result?;

    if verbose {
        println!("✅ 便携模式迁移完成");
    }

    Ok(())
}

fn execute_portable_migration(
    source: &str,
    target: &str,
    verbose: bool,
) -> Result<(), Box<dyn Error>> {
    let source_path = Path::new(source);
    let target_path = Path::new(target);

    if source_path == target_path {
        return Err("源目录和目标目录不能相同".into());
    }

    if !source_path.exists() {
        return Err(format!("源目录不存在: {}", source).into());
    }

    if !source_path.is_dir() {
        return Err(format!("源路径不是目录: {}", source).into());
    }

    if target_path.exists() {
        return Err(format!("目标目录已存在: {}", target).into());
    }

    let staging_path = build_staging_path(target_path);
    cleanup_staging(&staging_path)?;

    if verbose {
        println!("📦 正在复制目录到临时路径: {}", staging_path.display());
    }
    if let Err(err) = copy_directory_recursive(source_path, &staging_path) {
        let _ = cleanup_staging(&staging_path);
        return Err(format!("复制目录失败: {}", err).into());
    }

    if verbose {
        println!("🔁 原子切换目录: {} -> {}", staging_path.display(), target_path.display());
    }
    if let Err(err) = fs::rename(&staging_path, target_path) {
        let _ = cleanup_staging(&staging_path);
        return Err(format!("切换目录失败: {}", err).into());
    }

    if verbose {
        println!("🗑️ 删除源目录: {}", source_path.display());
    }
    fs::remove_dir_all(source_path).map_err(|err| format!("删除源目录失败: {}", err))?;

    Ok(())
}

fn copy_directory_recursive(source: &Path, target: &Path) -> Result<(), Box<dyn Error>> {
    fs::create_dir_all(target)?;

    for entry in fs::read_dir(source)? {
        let entry = entry?;
        let source_path = entry.path();
        let target_path = target.join(entry.file_name());
        let file_type = entry.file_type()?;

        if file_type.is_dir() {
            copy_directory_recursive(&source_path, &target_path)?;
        } else if file_type.is_file() {
            fs::copy(&source_path, &target_path)?;
        }
    }

    Ok(())
}

fn build_staging_path(target: &Path) -> PathBuf {
    let folder_name = target
        .file_name()
        .and_then(|name| name.to_str())
        .unwrap_or("portable");

    target.with_file_name(format!("{}.__migrating", folder_name))
}

fn cleanup_staging(staging_path: &Path) -> Result<(), Box<dyn Error>> {
    if staging_path.exists() {
        fs::remove_dir_all(staging_path)?;
    }

    if let Some(parent) = staging_path.parent() {
        if !parent.as_os_str().is_empty() {
            fs::create_dir_all(parent)?;
        }
    }

    Ok(())
}

fn write_info_file(path: &str, content: &str) -> Result<(), Box<dyn Error>> {
    let info_path = Path::new(path);

    if let Some(parent) = info_path.parent() {
        if !parent.as_os_str().is_empty() {
            fs::create_dir_all(parent)?;
        }
    }

    let mut file = File::create(info_path)?;
    file.write_all(content.as_bytes())?;
    Ok(())
}

fn restart_application(target: &str, verbose: bool) -> Result<(), Box<dyn Error>> {
    restart_direct(target, verbose)
}

fn restart_direct(target: &str, verbose: bool) -> Result<(), Box<dyn Error>> {
    if verbose {
        println!("🚀 直接启动应用: {}", target);
    }

    ProcessCommand::new(target)
        .spawn()
        .map_err(|err| format!("直接启动失败: {}", err))?;

    Ok(())
}
