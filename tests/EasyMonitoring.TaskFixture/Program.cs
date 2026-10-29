var pidFile = Path.Combine(AppContext.BaseDirectory, "task-pid.txt");
File.WriteAllText(pidFile + ".tmp",
    Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
File.Move(pidFile + ".tmp", pidFile);
var marker = Path.Combine(AppContext.BaseDirectory, "task-ran.txt");
File.WriteAllText(marker + ".tmp", "ok");
File.Move(marker + ".tmp", marker);
// A marker is deliberately observable before process exit; cleanup must wait for the PID.
Thread.Sleep(200);
