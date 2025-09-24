using SlaeSolverSystem.Worker.Core;
using SlaeSolverSystem.Worker.Interfaces;

namespace SlaeSolverSystem.Worker;

public class WorkerApp
{
	private readonly string _masterIp;
	private readonly int _masterPort;
	private readonly IMasterClient _masterClient;
	private readonly MessageHandler _messageHandler;

	public WorkerApp(string masterIp, int masterPort)
	{
		_masterIp = masterIp;
		_masterPort = masterPort;

		IWorkerTask workerTask = new WorkerTask();
		_masterClient = new MasterClient();
		_messageHandler = new MessageHandler(_masterClient, workerTask);
	}

	public async Task StartAsync()
	{
		try
		{
			Console.WriteLine($"Подключение к Master: {_masterIp}:{_masterPort}...");
			await _masterClient.ConnectAsync(_masterIp, _masterPort);
			Console.WriteLine("Подключено.");

			while (true)
			{
				Console.WriteLine("Worker: Ожидание сообщения от Master...");
				var (command, payload) = await _masterClient.ReadMessageAsync();
				await _messageHandler.HandleMessageAsync(command, payload);
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Критическая ошибка: {ex.Message}. Работа Worker'а будет прекращена.");
		}
		finally
		{
			_masterClient.Disconnect();
		}
	}
}