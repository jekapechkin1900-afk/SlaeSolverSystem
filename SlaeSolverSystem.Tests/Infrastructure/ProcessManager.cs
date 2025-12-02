using System.Diagnostics;

namespace SlaeSolverSystem.Tests.Infrastructure;

public class ProcessManager : IDisposable
{
	private Process _masterProcess;
	private readonly List<Process> _workerProcesses = new();
	private readonly string _solutionRoot;

	public ProcessManager()
	{
		KillAllSlaeProcesses();
		var currentDir = Directory.GetCurrentDirectory();
		_solutionRoot = Path.GetFullPath(Path.Combine(currentDir, "../../../../"));
	}

	public void StartMaster()
	{
		var path = Path.Combine(_solutionRoot, "SlaeSolverSystem.Master", "bin", "Debug", "net9.0", "SlaeSolverSystem.Master.exe");
		if (!File.Exists(path)) throw new FileNotFoundException($"Master exe not found at {path}");

		_masterProcess = Process.Start(new ProcessStartInfo
		{
			FileName = path,
			UseShellExecute = true,
			CreateNoWindow = false
		});

		Thread.Sleep(2000);
	}

	public void StartWorker()
	{
		var path = Path.Combine(_solutionRoot, "SlaeSolverSystem.Worker", "bin", "Debug", "net9.0", "SlaeSolverSystem.Worker.exe");
		if (!File.Exists(path)) throw new FileNotFoundException($"Worker exe not found at {path}");

		var p = Process.Start(new ProcessStartInfo
		{
			FileName = path,
			UseShellExecute = true,
			CreateNoWindow = false
		});
		_workerProcesses.Add(p);
	}

	public void Dispose()
	{
		try
		{
			if (_masterProcess != null && !_masterProcess.HasExited) _masterProcess.Kill();
			foreach (var w in _workerProcesses)
			{
				if (!w.HasExited) w.Kill();
			}
		}
		catch { }
	}

	public static void KillAllSlaeProcesses()
	{
		foreach (var p in Process.GetProcessesByName("SlaeSolverSystem.Master"))
		{
			try { p.Kill(); } catch { }
		}
		foreach (var p in Process.GetProcessesByName("SlaeSolverSystem.Worker"))
		{
			try { p.Kill(); } catch { }
		}
	}
}