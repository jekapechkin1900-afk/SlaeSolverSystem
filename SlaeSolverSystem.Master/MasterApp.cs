using System.Net.Sockets;
using SlaeSolverSystem.Master.Handlers;
using SlaeSolverSystem.Master.Jobs;
using SlaeSolverSystem.Master.Network;
using SlaeSolverSystem.Master.Pools;

namespace SlaeSolverSystem.Master;

public class MasterApp
{
	private readonly ConnectionManager _connectionManager;
	private readonly IWorkerPool _workerPool;
	private readonly GuiCommandHandler _guiCommandHandler;
	private readonly IJobManager _jobManager;

	public MasterApp(int workerPort, int guiPort)
	{
		_workerPool = new WorkerPool();
		_jobManager = new JobManager(_workerPool);
		_guiCommandHandler = new GuiCommandHandler(_jobManager);
		_connectionManager = new ConnectionManager(workerPort, guiPort);

		_connectionManager.WorkerConnected += OnWorkerConnected;
		_connectionManager.GuiClientConnected += OnGuiClientConnected;
	}

	public void Start()
	{
		_jobManager.StartProcessing();
		_connectionManager.StartListening();
		Console.WriteLine("[MasterApp] Master запущен и готов к работе.");
	}

	private void OnWorkerConnected(TcpClient worker)
	{
		_workerPool.Add(worker);
	}

	private void OnGuiClientConnected(TcpClient gui)
	{
		_guiCommandHandler.HandleClient(gui);
	}
}
