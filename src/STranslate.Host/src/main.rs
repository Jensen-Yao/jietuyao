mod commands;

use clap::{Arg, ArgAction, Command};

use crate::commands::{
    BackupMode, PortableMode, StartMode, TaskAction, handle_backup_command,
    handle_portable_command, handle_start_command, handle_task_command, handle_update_command,
};

fn main() {
    let matches = Command::new("z_stranslate_host")
        .version("1.0.3")
        .author("ZGGSONG <zggsong@foxmail.com>")
        .about("程序更新和后台启动工具")
        .subcommand(
            Command::new("update")
                .about("更新程序")
                .arg(
                    Arg::new("archive")
                        .short('a')
                        .long("archive")
                        .value_name("PATH")
                        .help("缓存的压缩包路径")
                        .required(true),
                )
                .arg(
                    Arg::new("wait-time")
                        .short('w')
                        .long("wait-time")
                        .value_name("SECONDS")
                        .help("关闭进程等待时间（秒）")
                        .default_value("0")
                        .value_parser(clap::value_parser!(u64)),
                )
                .arg(
                    Arg::new("clean")
                        .short('c')
                        .long("clean")
                        .action(ArgAction::SetTrue)
                        .help("是否清理必要目录（保留 log、portable_config、tmp 目录）"),
                )
                .arg(
                    Arg::new("process-name")
                        .short('p')
                        .long("process")
                        .value_name("NAME")
                        .help("要关闭的进程名称"),
                )
                .arg(
                    Arg::new("auto-start")
                        .short('s')
                        .long("auto-start")
                        .action(ArgAction::SetTrue)
                        .help("更新完成后自动启动程序"),
                )
                .arg(
                    Arg::new("verbose")
                        .short('v')
                        .long("verbose")
                        .action(ArgAction::SetTrue)
                        .help("显示详细输出"),
                ),
        )
        .subcommand(
            Command::new("start")
                .about("后台启动程序")
                .arg(
                    Arg::new("mode")
                        .short('m')
                        .long("mode")
                        .value_name("MODE")
                        .help("启动方式")
                        .value_parser(clap::value_parser!(StartMode))
                        .default_value("elevated"),
                )
                .arg(
                    Arg::new("target")
                        .short('t')
                        .long("target")
                        .value_name("PATH_OR_TASK")
                        .help("目标程序路径或任务计划名称")
                        .required(true),
                )
                .arg(
                    Arg::new("args")
                        .short('a')
                        .long("args")
                        .value_name("ARGUMENTS")
                        .help("启动参数")
                        .action(ArgAction::Append),
                )
                .arg(
                    Arg::new("delay")
                        .short('d')
                        .long("delay")
                        .value_name("SECONDS")
                        .help("启动延迟（秒）")
                        .default_value("0")
                        .value_parser(clap::value_parser!(u64)),
                )
                .arg(
                    Arg::new("wait-pid")
                        .long("wait-pid")
                        .value_name("PID")
                        .help("启动前等待退出的进程ID")
                        .value_parser(clap::value_parser!(u32)),
                )
                .arg(
                    Arg::new("wait-timeout")
                        .long("wait-timeout")
                        .value_name("SECONDS")
                        .help("等待进程退出超时时间（秒）")
                        .default_value("10")
                        .value_parser(clap::value_parser!(u64)),
                )
                .arg(
                    Arg::new("verbose")
                        .short('v')
                        .long("verbose")
                        .action(ArgAction::SetTrue)
                        .help("显示详细输出"),
                ),
        )
        .subcommand(
            Command::new("task")
                .about("管理Windows任务计划")
                .arg(
                    Arg::new("action")
                        .short('a')
                        .long("action")
                        .value_name("ACTION")
                        .help("操作类型")
                        .value_parser(clap::value_parser!(TaskAction))
                        .required(true),
                )
                .arg(
                    Arg::new("name")
                        .short('n')
                        .long("name")
                        .value_name("TASK_NAME")
                        .help("任务计划名称")
                        .required_if_eq("action", "create")
                        .required_if_eq("action", "check")
                        .required_if_eq("action", "delete"),
                )
                .arg(
                    Arg::new("program")
                        .short('p')
                        .long("program")
                        .value_name("PATH")
                        .help("要执行的程序路径（创建任务时需要）"),
                )
                .arg(
                    Arg::new("working-dir")
                        .short('w')
                        .long("working-dir")
                        .value_name("PATH")
                        .help("工作目录（可选，默认为程序所在目录）"),
                )
                .arg(
                    Arg::new("description")
                        .short('d')
                        .long("description")
                        .value_name("TEXT")
                        .help("任务描述")
                        .default_value("just for jump uac"),
                )
                .arg(
                    Arg::new("run-level")
                        .short('r')
                        .long("run-level")
                        .value_name("LEVEL")
                        .help("运行级别")
                        .value_parser(["limited", "highest"])
                        .default_value("highest"),
                )
                .arg(
                    Arg::new("force")
                        .short('f')
                        .long("force")
                        .action(ArgAction::SetTrue)
                        .help("强制操作（覆盖已存在的任务或强制删除）"),
                )
                .arg(
                    Arg::new("verbose")
                        .short('v')
                        .long("verbose")
                        .action(ArgAction::SetTrue)
                        .help("显示详细输出"),
                ),
        )
        .subcommand(
            Command::new("backup")
                .about("备份或恢复指定目录")
                .arg(
                    Arg::new("mode")
                        .short('m')
                        .long("mode")
                        .value_name("MODE")
                        .help("选择备份或恢复")
                        .value_parser(clap::value_parser!(BackupMode))
                        .required(true),
                )
                .arg(
                    Arg::new("archive")
                        .short('a')
                        .long("archive")
                        .value_name("FILE")
                        .help("备份文件路径（zip）")
                        .required(true),
                )
                .arg(
                    Arg::new("folder")
                        .short('f')
                        .long("folder")
                        .value_name("PATH")
                        .help("需要备份的目录，支持多次重复指定")
                        .action(ArgAction::Append)
                        .required_if_eq("mode", "backup"),
                )
                .arg(
                    Arg::new("source-folder")
                        .short('s')
                        .long("source-folder")
                        .value_name("NAME")
                        .help("备份包中要恢复的目录名称，可重复指定")
                        .action(ArgAction::Append)
                        .required_if_eq("mode", "restore"),
                )
                .arg(
                    Arg::new("target-folder")
                        .short('t')
                        .long("target-folder")
                        .value_name("PATH")
                        .help("恢复后的目标目录（会覆盖原内容），可重复指定")
                        .action(ArgAction::Append)
                        .required_if_eq("mode", "restore"),
                )
                .arg(
                    Arg::new("delete-file")
                        .short('r')
                        .long("delete-file")
                        .value_name("FILE_PATH")
                        .help("恢复后删除的文件或目录路径（可选）"),
                )
                .arg(
                    Arg::new("delay")
                        .short('d')
                        .long("delay")
                        .value_name("SECONDS")
                        .help("启动延迟（秒）")
                        .default_value("0")
                        .value_parser(clap::value_parser!(u64)),
                )
                .arg(
                    Arg::new("launch")
                        .short('l')
                        .long("launch")
                        .value_name("PATH")
                        .help("操作完成后启动的程序路径（可选）"),
                )
                .arg(
                    Arg::new("create-file")
                        .short('c')
                        .long("create-file")
                        .value_name("FILE_PATH")
                        .help("创建文件的路径（可选）"),
                )
                .arg(
                    Arg::new("file-content")
                        .short('w')
                        .long("file-content")
                        .value_name("CONTENT")
                        .help("写入文件的内容（配合 --create-file 使用）"),
                )
                .arg(
                    Arg::new("verbose")
                        .short('v')
                        .long("verbose")
                        .action(ArgAction::SetTrue)
                        .help("显示详细输出"),
                ),
        )
        .subcommand(
            Command::new("portable")
                .about("切换便携模式并迁移目录")
                .arg(
                    Arg::new("mode")
                        .short('m')
                        .long("mode")
                        .value_name("MODE")
                        .help("切换模式：enable 或 disable")
                        .value_parser(clap::value_parser!(PortableMode))
                        .required(true),
                )
                .arg(
                    Arg::new("source")
                        .short('s')
                        .long("source")
                        .value_name("PATH")
                        .help("迁移源目录")
                        .required(true),
                )
                .arg(
                    Arg::new("target")
                        .short('t')
                        .long("target")
                        .value_name("PATH")
                        .help("迁移目标目录")
                        .required(true),
                )
                .arg(
                    Arg::new("delay")
                        .short('d')
                        .long("delay")
                        .value_name("SECONDS")
                        .help("执行前延迟秒数")
                        .default_value("0")
                        .value_parser(clap::value_parser!(u64)),
                )
                .arg(
                    Arg::new("restart-target")
                        .short('p')
                        .long("restart-target")
                        .value_name("PATH_OR_TASK")
                        .help("重启目标（可执行文件路径）")
                        .required(true),
                )
                .arg(
                    Arg::new("info-file")
                        .short('i')
                        .long("info-file")
                        .value_name("FILE")
                        .help("写入提示信息的文件路径")
                        .required(true),
                )
                .arg(
                    Arg::new("success-message")
                        .short('w')
                        .long("success-message")
                        .value_name("TEXT")
                        .help("迁移成功提示文案")
                        .required(true),
                )
                .arg(
                    Arg::new("failure-prefix")
                        .short('f')
                        .long("failure-prefix")
                        .value_name("TEXT")
                        .help("迁移失败前缀文案")
                        .required(true),
                )
                .arg(
                    Arg::new("verbose")
                        .short('v')
                        .long("verbose")
                        .action(ArgAction::SetTrue)
                        .help("显示详细输出"),
                ),
        )
        .get_matches();

    match matches.subcommand() {
        Some(("update", sub_matches)) => {
            if let Err(e) = handle_update_command(sub_matches) {
                eprintln!("❌ 更新失败: {}", e);
                std::process::exit(1);
            }
        }
        Some(("start", sub_matches)) => {
            if let Err(e) = handle_start_command(sub_matches) {
                eprintln!("❌ 启动失败: {}", e);
                std::process::exit(1);
            }
        }
        Some(("task", sub_matches)) => {
            if let Err(e) = handle_task_command(sub_matches) {
                eprintln!("❌ 任务操作失败: {}", e);
                std::process::exit(1);
            }
        }
        Some(("backup", sub_matches)) => {
            if let Err(e) = handle_backup_command(sub_matches) {
                eprintln!("❌ 备份操作失败: {}", e);
                std::process::exit(1);
            }
        }
        Some(("portable", sub_matches)) => {
            if let Err(e) = handle_portable_command(sub_matches) {
                eprintln!("❌ 便携模式操作失败: {}", e);
                std::process::exit(1);
            }
        }
        _ => {
            eprintln!("❌ 请指定命令: update、start、task、backup 或 portable");
            eprintln!("使用 --help 查看帮助信息");
            std::process::exit(1);
        }
    }
}
