use clap::{ArgMatches, ValueEnum};
use std::error::Error;
use std::process::{Command as ProcessCommand, Stdio};
use std::thread;
use std::time::{Duration, Instant};

#[derive(Clone, Debug, ValueEnum)]
pub enum StartMode {
    /// 直接启动进程
    Direct,
    /// 直接提权启动进程
    Elevated,
    /// 执行指定名称的任务计划程序
    Task,
}

#[derive(Debug, Default)]
struct WaitKillReport {
    wait_pid: Option<u32>,
    wait_timeout_sec: u64,
    wait_elapsed_ms: u128,
    kill_attempted: bool,
    kill_success: bool,
    kill_exit_code: Option<i32>,
    kill_stdout: String,
    kill_stderr: String,
}

impl WaitKillReport {
    fn new(wait_pid: Option<u32>, wait_timeout_sec: u64) -> Self {
        Self {
            wait_pid,
            wait_timeout_sec,
            ..Default::default()
        }
    }
}

pub fn handle_start_command(matches: &ArgMatches) -> Result<(), Box<dyn Error>> {
    let mode = matches.get_one::<StartMode>("mode").unwrap();
    let target = matches.get_one::<String>("target").unwrap();
    let args: Vec<&String> = matches
        .get_many::<String>("args")
        .unwrap_or_default()
        .collect();
    let delay = *matches.get_one::<u64>("delay").unwrap();
    let wait_pid = matches.get_one::<u32>("wait-pid").copied();
    let wait_timeout_sec = (*matches.get_one::<u64>("wait-timeout").unwrap()).max(1);
    let verbose = matches.get_flag("verbose");

    if verbose {
        println!("🚀 准备启动程序...");
        println!("   启动方式: {:?}", mode);
        println!("   目标: {}", target);
        if !args.is_empty() {
            println!("   参数: {:?}", args);
        }
        if delay > 0 {
            println!("   延迟: {} 秒", delay);
        }
        if let Some(pid) = wait_pid {
            println!("   等待退出PID: {}", pid);
            println!("   等待超时: {} 秒", wait_timeout_sec);
        }
    }

    if delay > 0 {
        if verbose {
            println!("⏳ 延迟 {} 秒后启动...", delay);
        }
        thread::sleep(Duration::from_secs(delay));
    }

    let report = wait_and_cleanup_previous_process(wait_pid, wait_timeout_sec, verbose);
    let launch_mode = mode_name(mode);
    let launch_result = match mode {
        StartMode::Direct => {
            start_direct_process(target, &args, verbose)
        }
        StartMode::Elevated => {
            start_elevated_process(target, &args, verbose)
        }
        StartMode::Task => {
            start_task_scheduler(target, verbose)
        }
    };

    let launch_success = launch_result.is_ok();
    print_start_report(&report, launch_mode, launch_success);
    launch_result?;

    println!("✅ 启动完成!");
    Ok(())
}

fn mode_name(mode: &StartMode) -> &'static str {
    match mode {
        StartMode::Direct => "direct",
        StartMode::Elevated => "elevated",
        StartMode::Task => "task",
    }
}

fn wait_and_cleanup_previous_process(
    wait_pid: Option<u32>,
    wait_timeout_sec: u64,
    verbose: bool,
) -> WaitKillReport {
    let mut report = WaitKillReport::new(wait_pid, wait_timeout_sec);
    let Some(pid) = wait_pid else {
        return report;
    };

    if verbose {
        println!("⏳ 启动前等待旧进程退出: pid={}", pid);
    }

    let started_at = Instant::now();
    let timeout = Duration::from_secs(wait_timeout_sec);

    while started_at.elapsed() < timeout {
        if !is_process_running(pid) {
            report.wait_elapsed_ms = started_at.elapsed().as_millis();
            if verbose {
                println!("✅ 旧进程已退出: pid={}", pid);
            }
            return report;
        }

        thread::sleep(Duration::from_millis(100));
    }

    report.wait_elapsed_ms = started_at.elapsed().as_millis();
    report.kill_attempted = true;

    if verbose {
        println!("⚠️ 旧进程等待超时，尝试强制结束: pid={}", pid);
    }

    let (kill_success, kill_exit_code, kill_stdout, kill_stderr) = try_kill_process(pid, verbose);
    report.kill_success = kill_success;
    report.kill_exit_code = kill_exit_code;
    report.kill_stdout = kill_stdout;
    report.kill_stderr = kill_stderr;

    if kill_success {
        // 强杀成功后再短暂等待，尽量确保系统完成进程清理。
        let settle_started_at = Instant::now();
        while settle_started_at.elapsed() < Duration::from_secs(2) {
            if !is_process_running(pid) {
                break;
            }
            thread::sleep(Duration::from_millis(100));
        }
        report.wait_elapsed_ms = started_at.elapsed().as_millis();
    }

    report
}

fn try_kill_process(pid: u32, verbose: bool) -> (bool, Option<i32>, String, String) {
    #[cfg(target_os = "windows")]
    {
        let output = ProcessCommand::new("taskkill")
            .args(&["/PID", &pid.to_string(), "/T", "/F"])
            .output();

        return match output {
            Ok(output) => {
                let success = output.status.success();
                let exit_code = output.status.code();
                let stdout = String::from_utf8_lossy(&output.stdout).trim().to_string();
                let stderr = String::from_utf8_lossy(&output.stderr).trim().to_string();
                if verbose {
                    println!(
                        "🔧 taskkill执行完成: pid={} success={} exit_code={}",
                        pid,
                        success,
                        exit_code
                            .map(|x| x.to_string())
                            .unwrap_or_else(|| "none".to_string())
                    );
                }
                (success, exit_code, stdout, stderr)
            }
            Err(e) => {
                if verbose {
                    println!("❌ taskkill执行异常: pid={} error={}", pid, e);
                }
                (false, None, String::new(), e.to_string())
            }
        };
    }

    #[allow(unreachable_code)]
    (false, None, String::new(), "taskkill is only available on Windows".to_string())
}

fn is_process_running(pid: u32) -> bool {
    #[cfg(target_os = "windows")]
    {
        let output = ProcessCommand::new("tasklist")
            .args(&["/FI", &format!("PID eq {}", pid), "/FO", "CSV", "/NH"])
            .output();

        let Ok(output) = output else {
            // 无法访问进程信息时按“已退出”处理，避免无限等待。
            return false;
        };

        if !output.status.success() {
            return false;
        }

        let stdout = String::from_utf8_lossy(&output.stdout);
        let first_line = stdout.lines().map(str::trim).find(|line| !line.is_empty());
        return first_line.is_some_and(|line| line.starts_with('"'));
    }

    #[allow(unreachable_code)]
    false
}

fn print_start_report(report: &WaitKillReport, launch_mode: &str, launch_success: bool) {
    let wait_pid = report
        .wait_pid
        .map(|pid| pid.to_string())
        .unwrap_or_else(|| "none".to_string());
    let kill_exit_code = report
        .kill_exit_code
        .map(|x| x.to_string())
        .unwrap_or_else(|| "none".to_string());

    println!(
        "handover wait_pid={} wait_timeout_sec={} wait_elapsed_ms={} kill_attempted={} kill_success={} kill_exit_code={} launch_mode={} launch_success={}",
        wait_pid,
        report.wait_timeout_sec,
        report.wait_elapsed_ms,
        report.kill_attempted,
        report.kill_success,
        kill_exit_code,
        launch_mode,
        launch_success
    );

    if report.kill_attempted && !report.kill_success {
        eprintln!(
            "handover-kill-error wait_pid={} kill_exit_code={} kill_stdout=\"{}\" kill_stderr=\"{}\"",
            wait_pid, kill_exit_code, report.kill_stdout, report.kill_stderr
        );
    }
}

fn start_direct_process(
    target: &str,
    args: &[&String],
    verbose: bool,
) -> Result<(), Box<dyn Error>> {
    if verbose {
        println!("🚀 直接启动进程: {}", target);
    }

    #[cfg(target_os = "windows")]
    {
        let mut cmd_args = vec![
            "-Command".to_string(),
            format!("Start-Process '{}'", target),
        ];

        if !args.is_empty() {
            let args_str = args
                .iter()
                .map(|s| s.as_str())
                .collect::<Vec<_>>()
                .join(" ");
            cmd_args[1] = format!("Start-Process '{}' -ArgumentList '{}' ", target, args_str);
        }

        let mut command = ProcessCommand::new("powershell");
        command.args(&cmd_args);

        let output = command.output()?;

        if !output.status.success() {
            let error = String::from_utf8_lossy(&output.stderr).trim().to_string();
            let stdout = String::from_utf8_lossy(&output.stdout).trim().to_string();
            return Err(format!("直接启动失败: stderr={} stdout={}", error, stdout).into());
        } else if verbose {
            println!("✅ 进程已启动: {}", target);
        }
    }

    Ok(())
}

fn start_elevated_process(
    target: &str,
    args: &[&String],
    verbose: bool,
) -> Result<(), Box<dyn Error>> {
    if verbose {
        println!("🔑 以提权方式启动进程: {}", target);
    }

    #[cfg(target_os = "windows")]
    {
        let mut cmd_args = vec![
            "-Command".to_string(),
            format!("Start-Process '{}' -Verb RunAs", target),
        ];

        if !args.is_empty() {
            let args_str = args
                .iter()
                .map(|s| s.as_str())
                .collect::<Vec<_>>()
                .join(" ");
            cmd_args[1] = format!(
                "Start-Process '{}' -ArgumentList '{}' -Verb RunAs",
                target, args_str
            );
        }

        let mut command = ProcessCommand::new("powershell");
        command.args(&cmd_args);

        if !verbose {
            command.stdout(Stdio::null()).stderr(Stdio::null());
        }

        command.spawn()?;
    }

    Ok(())
}

fn start_task_scheduler(task_name: &str, verbose: bool) -> Result<(), Box<dyn Error>> {
    if verbose {
        println!("📅 启动任务计划: {}", task_name);
    }

    #[cfg(target_os = "windows")]
    {
        let output = ProcessCommand::new("schtasks")
            .args(&["/Run", "/TN", task_name])
            .output()?;

        if !output.status.success() {
            let error = String::from_utf8_lossy(&output.stderr);
            return Err(format!("启动任务计划失败: {}", error).into());
        }

        if verbose {
            println!("✅ 任务计划已启动: {}", task_name);
        }
    }

    Ok(())
}
