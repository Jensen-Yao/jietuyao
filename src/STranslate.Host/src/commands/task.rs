use chrono::Local;
use clap::{ArgMatches, ValueEnum};
use std::error::Error;
use std::fs;
use std::path::Path;
use std::process::Command as ProcessCommand;

#[derive(Clone, Debug, ValueEnum)]
pub enum TaskAction {
    /// 检查任务是否存在
    Check,
    /// 创建新任务
    Create,
    /// 删除任务
    Delete,
    /// 列出所有任务
    List,
}

pub fn handle_task_command(matches: &ArgMatches) -> Result<(), Box<dyn Error>> {
    let action = matches.get_one::<TaskAction>("action").unwrap();
    let verbose = matches.get_flag("verbose");

    #[cfg(target_os = "windows")]
    {
        match action {
            TaskAction::Check => {
                let task_name = matches.get_one::<String>("name").unwrap();
                check_task_exists(task_name, verbose)?;
            }
            TaskAction::Create => {
                let task_name = matches.get_one::<String>("name").unwrap();
                let program = matches
                    .get_one::<String>("program")
                    .ok_or("创建任务时必须指定程序路径 --program")?;
                let working_dir = matches.get_one::<String>("working-dir");
                let description = matches.get_one::<String>("description").unwrap();
                let run_level = matches.get_one::<String>("run-level").unwrap();
                let force = matches.get_flag("force");

                create_task(
                    task_name,
                    program,
                    working_dir,
                    description,
                    run_level,
                    force,
                    verbose,
                )?;
            }
            TaskAction::Delete => {
                let task_name = matches.get_one::<String>("name").unwrap();
                delete_task(task_name, verbose)?;
            }
            TaskAction::List => {
                list_tasks(verbose)?;
            }
        }
    }

    Ok(())
}

#[cfg(target_os = "windows")]
fn check_task_exists(task_name: &str, verbose: bool) -> Result<(), Box<dyn Error>> {
    if verbose {
        println!("🔍 检查任务计划是否存在: {}", task_name);
    }

    let output = ProcessCommand::new("schtasks")
        .args(&["/Query", "/TN", task_name])
        .output()?;

    if output.status.success() {
        println!("✅ 任务计划存在: {}", task_name);
        if verbose {
            let info = String::from_utf8_lossy(&output.stdout);
            println!("📋 任务信息:");
            println!("{}", info);
        }
    } else {
        println!("❌ 任务计划不存在: {}", task_name);
    }

    Ok(())
}

#[cfg(target_os = "windows")]
fn create_task(
    task_name: &str,
    program: &str,
    working_dir: Option<&String>,
    description: &str,
    run_level: &str,
    force: bool,
    verbose: bool,
) -> Result<(), Box<dyn Error>> {
    if verbose {
        println!("📝 创建任务计划: {}", task_name);
        println!("   程序路径: {}", program);
        if let Some(wd) = working_dir {
            println!("   工作目录: {}", wd);
        }
        println!("   运行级别: {}", run_level);
    }

    if !Path::new(program).exists() {
        return Err(format!("程序文件不存在: {}", program).into());
    }

    let work_dir = if let Some(wd) = working_dir {
        wd.clone()
    } else {
        Path::new(program)
            .parent()
            .ok_or("无法确定程序所在目录")?
            .to_string_lossy()
            .to_string()
    };

    let check_output = ProcessCommand::new("schtasks")
        .args(&["/Query", "/TN", task_name])
        .output()?;

    if check_output.status.success() && !force {
        println!("✅ 任务计划已存在: {}，使用 --force 强制覆盖", task_name);
        return Ok(());
    }

    let xml_content = generate_task_xml(task_name, program, &work_dir, description, run_level)?;

    let temp_xml_path = format!("temp_task_{}.xml", task_name);
    fs::write(&temp_xml_path, xml_content)?;

    if verbose {
        println!("📄 已生成临时XML文件: {}", temp_xml_path);
    }

    let create_args = vec!["/Create", "/XML", &temp_xml_path, "/TN", task_name, "/F"];

    let output = ProcessCommand::new("schtasks")
        .args(&create_args)
        .output()?;

    let _ = fs::remove_file(&temp_xml_path);
    if verbose {
        println!("🗑️ 已删除临时XML文件: {}", temp_xml_path);
    }

    if output.status.success() {
        println!("✅ 任务计划创建成功: {}", task_name);
        if verbose {
            let result = String::from_utf8_lossy(&output.stdout);
            println!("📋 创建结果: {}", result);
        }
    } else {
        let error = String::from_utf8_lossy(&output.stderr);
        return Err(format!("创建任务计划失败: {}", error).into());
    }

    Ok(())
}

#[cfg(target_os = "windows")]
fn delete_task(task_name: &str, verbose: bool) -> Result<(), Box<dyn Error>> {
    if verbose {
        println!("🗑️  删除任务计划: {}", task_name);
    }

    let check_output = ProcessCommand::new("schtasks")
        .args(&["/Query", "/TN", task_name])
        .output()?;

    if !check_output.status.success() {
        println!("✅ 任务计划不存在: {}", task_name);
        return Ok(());
    }

    let args = vec!["/Delete", "/TN", task_name, "/F"];
    let output = ProcessCommand::new("schtasks").args(&args).output()?;

    if output.status.success() {
        println!("✅ 任务计划删除成功: {}", task_name);
    } else {
        let error = String::from_utf8_lossy(&output.stderr);
        return Err(format!("删除任务计划失败: {}", error).into());
    }

    Ok(())
}

#[cfg(target_os = "windows")]
fn list_tasks(verbose: bool) -> Result<(), Box<dyn Error>> {
    if verbose {
        println!("📋 列出所有任务计划...");
    }

    let output = ProcessCommand::new("schtasks")
        .args(&["/Query", "/FO", "TABLE"])
        .output()?;

    if output.status.success() {
        let tasks = String::from_utf8_lossy(&output.stdout);
        println!("📋 任务计划列表:");
        println!("{}", tasks);
    } else {
        let error = String::from_utf8_lossy(&output.stderr);
        return Err(format!("获取任务列表失败: {}", error).into());
    }

    Ok(())
}

#[cfg(target_os = "windows")]
fn generate_task_xml(
    task_name: &str,
    program: &str,
    working_dir: &str,
    description: &str,
    run_level: &str,
) -> Result<String, Box<dyn Error>> {
    let current_time = Local::now().format("%Y-%m-%dT%H:%M:%S").to_string();
    let run_level_value = if run_level == "highest" {
        "HighestAvailable"
    } else {
        "LeastPrivilege"
    };

    let user_sid = get_current_user_sid().unwrap_or_else(|_| "S-1-5-32-544".to_string());

    let xml_content = format!(
        r#"<?xml version="1.0" encoding="UTF-16"?>
<Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
  <RegistrationInfo>
    <Date>{}</Date>
    <Author>stranslate - zggsong</Author>
    <Description>{}</Description>
    <URI>\{}</URI>
  </RegistrationInfo>
  <Triggers />
  <Principals>
    <Principal id="Author">
      <UserId>{}</UserId>
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>{}</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>false</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <IdleSettings>
      <StopOnIdleEnd>true</StopOnIdleEnd>
      <RestartOnIdle>false</RestartOnIdle>
    </IdleSettings>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <WakeToRun>false</WakeToRun>
    <ExecutionTimeLimit>PT72H</ExecutionTimeLimit>
    <Priority>4</Priority>
  </Settings>
  <Actions Context="Author">
    <Exec>
      <Command>{}</Command>
      <WorkingDirectory>{}</WorkingDirectory>
    </Exec>
  </Actions>
</Task>"#,
        current_time, description, task_name, user_sid, run_level_value, program, working_dir
    );

    Ok(xml_content)
}

#[cfg(target_os = "windows")]
fn get_current_user_sid() -> Result<String, Box<dyn Error>> {
    let output = ProcessCommand::new("whoami")
        .args(&["/user", "/fo", "csv", "/nh"])
        .output()?;

    if output.status.success() {
        let result = String::from_utf8_lossy(&output.stdout);
        if let Some(sid_part) = result.split(',').nth(1) {
            let sid = sid_part.trim().trim_matches('"');
            return Ok(sid.to_string());
        }
    }

    Err("无法获取当前用户SID".into())
}
