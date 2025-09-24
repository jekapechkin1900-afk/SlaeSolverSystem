using System.Net;
using System.Net.Sockets;

namespace SlaeSolverSystem.Master.Network;

public class ConnectionManager
{
	private readonly int _workerPort;
	private readonly int _guiPort;

	public event Action<TcpClient> WorkerConnected;
	public event Action<TcpClient> GuiClientConnected;

	public ConnectionManager(int workerPort, int guiPort)
	{
		_workerPort = workerPort;
		_guiPort = guiPort;
	}

	public void StartListening()
	{
		var workerListener = new TcpListener(IPAddress.Any, _workerPort);
		var guiListener = new TcpListener(IPAddress.Any, _guiPort);

		workerListener.Start();
		guiListener.Start();

		Console.WriteLine($"[ConnectionManager] Прослушивание Worker'ов на порту {_workerPort}...");
		Console.WriteLine($"[ConnectionManager] Прослушивание GUI на порту {_guiPort}...");

		Task.Run(() => AcceptLoopAsync(workerListener, (client) => WorkerConnected?.Invoke(client)));
		Task.Run(() => AcceptLoopAsync(guiListener, (client) => GuiClientConnected?.Invoke(client)));
	}

	private async Task AcceptLoopAsync(TcpListener listener, Action<TcpClient> onConnect)
	{
		while (true)
		{
			try
			{
				var client = await listener.AcceptTcpClientAsync();
				Console.WriteLine($"[ConnectionManager] Принято подключение от {client.Client.RemoteEndPoint}");
				onConnect(client);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[ConnectionManager] Ошибка при приеме подключения: {ex.Message}");
			}
		}
	}
}
