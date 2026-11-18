use clap::{ArgMatches, ValueEnum};
use std::collections::HashSet;
use std::error::Error;
use std::fs::{self, File};
use std::io::{self, Write};
use std::path::{Component, Path, PathBuf};
use std::process::Command as ProcessCommand;
use std::thread;
use std::time::Duration;
use zip::read::ZipArchive;
use zip::write::FileOptions;
use zip::{CompressionMethod, ZipWriter};

#[derive(Clone, Debug, ValueEnum)]
pub enum BackupMode {
    /// 备份
    Backup,
    /// 恢复
    Restore,
}

pub fn handle_backup_command(matches: &ArgMatches) -> Result<(), Box<dyn Error>> {
    let mode = matches.get_one::<BackupMode>("mode").unwrap();
    let archive = matches.get_one::<String>("archive").unwrap();
    let delay = *matches.get_one::<u64>("delay").unwrap();
    let verbose = matches.get_flag("verbose");
    let launch_path = matches.get_one::<String>("launch");
    let create_file = matches.get_one::<String>("create-file");
    let file_content = matches.get_one::<String>("file-content");
    let delete_file = matches.get_one::<String>("delete-file");

    if delay > 0 {
        if verbose {
            println!("⏳ 延迟 {} 秒后开始备份/恢复...", delay);
        }
        thread::sleep(Duration::from_secs(delay));
    }

    match mode {
        BackupMode::Backup => {
            let directories: Vec<&String> = matches
                .get_many::<String>("folder")
                .unwrap_or_default()
                .collect();

            if directories.is_empty() {
                return Err("备份模式下至少需要指定一个目录 (--folder)".into());
            }

            backup_directories(&directories, archive, verbose)?;
            println!("✅ 备份完成: {}", archive);
        }
        BackupMode::Restore => {
            let source_dirs: Vec<&String> = matches
                .get_many::<String>("source-folder")
                .unwrap_or_default()
                .collect();
            let targets: Vec<&String> = matches
                .get_many::<String>("target-folder")
                .unwrap_or_default()
                .collect();

            if source_dirs.is_empty() || targets.is_empty() {
                return Err("恢复模式下必须至少指定一个 --source-folder 和 --target-folder".into());
            }

            if source_dirs.len() != targets.len() {
                return Err(format!(
                    "--source-folder 与 --target-folder 的数量必须一致，当前为 {} 和 {}",
                    source_dirs.len(),
                    targets.len()
                )
                .into());
            }

            for (source, target) in source_dirs.iter().zip(targets.iter()) {
                restore_directory(archive, source, target, verbose)?;
                println!("✅ 恢复完成: {} → {}", source, target);
            }

            if let Some(file_path) = delete_file {
                if !file_path.trim().is_empty() {
                    delete_file_or_directory(file_path, verbose)?;
                }
            }
        }
    }

    if let Some(file_path) = create_file {
        if !file_path.trim().is_empty() {
            let content = file_content.map(|s| s.as_str()).unwrap_or("");
            create_file_with_content(file_path, content, verbose)?;
        }
    }

    if let Some(path) = launch_path {
        if !path.trim().is_empty() {
            launch_program(path, verbose)?;
        }
    }

    Ok(())
}

fn backup_directories(
    directories: &[&String],
    archive_path: &str,
    verbose: bool,
) -> Result<(), Box<dyn Error>> {
    let archive_path = Path::new(archive_path);

    if let Some(parent) = archive_path.parent() {
        if !parent.as_os_str().is_empty() {
            fs::create_dir_all(parent)?;
        }
    }

    let archive_file = File::create(archive_path)?;
    let archive_abs = fs::canonicalize(archive_path)?;
    let mut zip = ZipWriter::new(archive_file);

    let mut root_names = HashSet::new();

    for dir in directories {
        let dir_path = Path::new(dir);
        if !dir_path.exists() {
            return Err(format!("目录不存在: {}", dir_path.display()).into());
        }
        if !dir_path.is_dir() {
            return Err(format!("路径不是目录: {}", dir_path.display()).into());
        }

        let dir_abs = fs::canonicalize(dir_path)?;

        if archive_abs.starts_with(&dir_abs) {
            return Err(format!("输出文件位于备份目录内: {}", dir_path.display()).into());
        }

        let root_name = derive_root_name(&dir_abs)?;

        if !root_names.insert(root_name.clone()) {
            return Err(format!(
                "存在重复目录名称: {}，请确保各备份目录的末级名称唯一",
                root_name
            )
            .into());
        }

        if verbose {
            println!("📦 备份目录: {}", dir_abs.display());
        }

        let mut relative = PathBuf::new();
        relative.push(&root_name);

        add_directory_recursively(&mut zip, &dir_abs, &relative, verbose)?;
    }

    zip.finish()?;
    Ok(())
}

fn add_directory_recursively(
    zip: &mut ZipWriter<File>,
    source: &Path,
    relative: &Path,
    verbose: bool,
) -> Result<(), Box<dyn Error>> {
    let relative_str = path_to_zip_string(relative);
    if !relative_str.is_empty() {
        zip.add_directory(format!("{}/", relative_str), FileOptions::default())?;
    }

    for entry in fs::read_dir(source)? {
        let entry = entry?;
        let file_type = entry.file_type()?;
        let path = entry.path();

        if file_type.is_symlink() {
            if verbose {
                println!("⚠️  跳过符号链接: {}", path.display());
            }
            continue;
        }

        let mut next_relative = relative.to_path_buf();
        next_relative.push(entry.file_name());

        if file_type.is_dir() {
            add_directory_recursively(zip, &path, &next_relative, verbose)?;
        } else if file_type.is_file() {
            if verbose {
                println!("   ➕ 文件: {}", path.display());
            }
            let mut input = File::open(&path)?;
            zip.start_file(path_to_zip_string(&next_relative), file_options())?;
            io::copy(&mut input, zip)?;
        }
    }

    Ok(())
}

fn restore_directory(
    archive_path: &str,
    source_dir: &str,
    target_dir: &str,
    verbose: bool,
) -> Result<(), Box<dyn Error>> {
    let source_in_zip = normalize_zip_path(source_dir);
    if source_in_zip.is_empty() {
        return Err("恢复目录名称不能为空".into());
    }
    if source_in_zip.split('/').any(|segment| segment == "..") {
        return Err("恢复目录名称不能包含 ..".into());
    }

    let archive_path = Path::new(archive_path);
    if !archive_path.exists() {
        return Err(format!("备份文件不存在: {}", archive_path.display()).into());
    }

    if verbose {
        println!("♻️  正在恢复目录 '{}' 到 '{}'", source_in_zip, target_dir);
    }

    let target_path = Path::new(target_dir);

    if target_path.exists() {
        if target_path.is_dir() {
            fs::remove_dir_all(target_path)?;
        } else {
            fs::remove_file(target_path)?;
        }
    }
    fs::create_dir_all(target_path)?;

    let file = File::open(archive_path)?;
    let mut archive = ZipArchive::new(file)?;
    let prefix = Path::new(&source_in_zip);
    let mut restored_any = false;

    for i in 0..archive.len() {
        let mut entry = archive.by_index(i)?;
        let enclosed = match entry.enclosed_name() {
            Some(path) => path.to_owned(),
            None => continue,
        };

        if !enclosed.starts_with(prefix) {
            continue;
        }

        restored_any = true;

        let relative = enclosed.strip_prefix(prefix)?;
        if relative.components().next().is_none() {
            continue;
        }

        let out_path = target_path.join(relative);

        if entry.is_dir() {
            fs::create_dir_all(&out_path)?;
            if verbose {
                println!("📁 创建目录: {}", out_path.display());
            }
        } else {
            if let Some(parent) = out_path.parent() {
                fs::create_dir_all(parent)?;
            }
            let mut outfile = File::create(&out_path)?;
            io::copy(&mut entry, &mut outfile)?;

            if verbose {
                println!("📝 恢复文件: {}", out_path.display());
            }
        }
    }

    if !restored_any {
        return Err(format!("在备份文件中找不到目录: {}", source_in_zip).into());
    }

    Ok(())
}

fn derive_root_name(path: &Path) -> Result<String, Box<dyn Error>> {
    if let Some(name) = path.file_name() {
        return Ok(name.to_string_lossy().to_string());
    }

    if let Some(component) = path
        .components()
        .rev()
        .find_map(|component| match component {
            Component::Normal(part) => Some(part.to_string_lossy().to_string()),
            _ => None,
        })
    {
        return Ok(component);
    }

    Err(format!("无法确定目录名称: {}", path.display()).into())
}

fn path_to_zip_string(path: &Path) -> String {
    path.components()
        .filter_map(|component| match component {
            Component::Normal(part) => Some(part.to_string_lossy().to_string()),
            _ => None,
        })
        .collect::<Vec<_>>()
        .join("/")
}

fn normalize_zip_path(value: &str) -> String {
    value
        .split(|c| c == '/' || c == '\\')
        .filter(|segment| !segment.is_empty())
        .collect::<Vec<_>>()
        .join("/")
}

fn file_options() -> FileOptions {
    FileOptions::default().compression_method(CompressionMethod::Deflated)
}

fn delete_file_or_directory(file_path: &str, verbose: bool) -> Result<(), Box<dyn Error>> {
    let path = Path::new(file_path);

    if !path.exists() {
        if verbose {
            println!("⚠️  文件或目录不存在，跳过删除: {}", file_path);
        }
        return Ok(());
    }

    if verbose {
        if path.is_dir() {
            println!("🗑️  删除目录: {}", file_path);
        } else {
            println!("🗑️  删除文件: {}", file_path);
        }
    }

    if path.is_dir() {
        fs::remove_dir_all(path).map_err(|e| format!("删除目录失败: {}", e))?;
    } else {
        fs::remove_file(path).map_err(|e| format!("删除文件失败: {}", e))?;
    }

    if verbose {
        println!("✅ 删除成功");
    }

    Ok(())
}

fn create_file_with_content(
    file_path: &str,
    content: &str,
    verbose: bool,
) -> Result<(), Box<dyn Error>> {
    let path = Path::new(file_path);

    // 如果父目录不存在，则创建
    if let Some(parent) = path.parent() {
        if !parent.as_os_str().is_empty() && !parent.exists() {
            fs::create_dir_all(parent)?;
            if verbose {
                println!("📁 创建目录: {}", parent.display());
            }
        }
    }

    if verbose {
        println!("📝 创建文件: {}", file_path);
    }

    let mut file = File::create(path).map_err(|e| format!("创建文件失败: {}", e))?;

    file.write_all(content.as_bytes())
        .map_err(|e| format!("写入文件失败: {}", e))?;

    if verbose {
        println!("✅ 文件已创建，写入 {} 字节", content.len());
    }

    Ok(())
}

fn launch_program(program_path: &str, verbose: bool) -> Result<(), Box<dyn Error>> {
    let path = Path::new(program_path);

    if !path.exists() {
        return Err(format!("程序路径不存在: {}", program_path).into());
    }

    if verbose {
        println!("🚀 正在启动程序: {}", program_path);
    }

    ProcessCommand::new(program_path)
        .spawn()
        .map_err(|e| format!("启动程序失败: {}", e))?;

    if verbose {
        println!("✅ 程序已启动");
    }

    Ok(())
}
