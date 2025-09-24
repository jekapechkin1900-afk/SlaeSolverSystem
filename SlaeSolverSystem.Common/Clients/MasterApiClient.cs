using System.Net.Sockets;
using SlaeSolverSystem.Common.Contracts;

namespace SlaeSolverSystem.Common.Clients;

public class MasterApiClient
{
	private TcpClient _client;
	private NetworkStream _stream;
	private readonly string _masterIp;
	private readonly int _masterPort;
	private Task _listenerTask; 

	public bool IsConnected => _client?.Connected == true;

	public event Action<string> LogReceived;
	public event Action<int, double> ProgressReceived;
	public event Action<string> StatusReceived;
	public event Action<CalculationResult> CalculationFinished;
	public event Action Disconnected;
	public event Action<long, int> LinearCalculationFinished;
	public event Action CalculationFailed;

	public MasterApiClient(string masterIp, int masterPort)
	{
		_masterIp = masterIp;
		_masterPort = masterPort;
	}

	public async Task ConnectAsync()
	{
		if (IsConnected && _listenerTask != null && !_listenerTask.IsCompleted)
		{
			return; 
		}

		Disconnect();

		_client = new TcpClient();
		await _client.ConnectAsync(_masterIp, _masterPort);
		_stream = _client.GetStream();

		_listenerTask = Task.Run(ListenForMessagesAsync);
	}

	public void Disconnect()
	{
		_stream?.Dispose();
		_client?.Close();
	}

	public Task StartLinearCalculationAsync(string matrixFile, string vectorFile, string nodesFile, double epsilon, int maxIterations)
	{
		if (!IsConnected) throw new InvalidOperationException("Клиент не подключен к Master-серверу.");

		using var ms = new MemoryStream();
		using var writer = new BinaryWriter(ms);

		writer.Write(matrixFile);
		writer.Write(vectorFile);
		writer.Write(nodesFile);
		writer.Write(epsilon);
		writer.Write(maxIterations);

		byte[] payload = ms.ToArray();
		return NetworkHelper.SendMessageAsync(_stream, CommandCodes.StartLinearCalculation, payload);
	}

	public Task StartDistributedCalculationAsync(string matrixFile, string vectorFile, string nodesFile, double epsilon, int maxIterations)
	{
		if (!IsConnected) throw new InvalidOperationException("Клиент не подключен к Master-серверу.");

		using var ms = new MemoryStream();
		using var writer = new BinaryWriter(ms);

		writer.Write(matrixFile);
		writer.Write(vectorFile);
		writer.Write(nodesFile);
		writer.Write(epsilon);
		writer.Write(maxIterations);

		byte[] payload = ms.ToArray();
		return NetworkHelper.SendMessageAsync(_stream, CommandCodes.StartDistributedCalculation, payload);
	}

	private async Task ListenForMessagesAsync()
	{
		try
		{
			while (_client.Connected)
			{
				var (command, payload) = await NetworkHelper.ReadMessageAsync(_stream);
				switch (command)
				{
					case CommandCodes.LogMessage:
						LogReceived?.Invoke(NetworkHelper.ToString(payload));
						break;
					case CommandCodes.StatusUpdate:
						StatusReceived?.Invoke(NetworkHelper.ToString(payload));
						break;
					case CommandCodes.ProgressUpdate:
						using (var reader = new BinaryReader(new MemoryStream(payload)))
						{
							int iter = reader.ReadInt32();
							double error = reader.ReadDouble();
							ProgressReceived?.Invoke(iter, error);
						}
						break;
					case CommandCodes.ResultReady:
						using (var reader = new BinaryReader(new MemoryStream(payload)))
						{
							long time = reader.ReadInt64();
							int iter = reader.ReadInt32();
							int size = reader.ReadInt32();
							int vectorLength = reader.ReadInt32();
							var vector = new double[vectorLength];
							for (int i = 0; i < vectorLength; i++)
							{
								vector[i] = reader.ReadDouble();
							}
							var result = new CalculationResult(time, iter, vector, size);
							CalculationFinished?.Invoke(result);
						}
						break;
					case CommandCodes.LinearResultReady:
						using (var reader = new BinaryReader(new MemoryStream(payload)))
						{
							long time = reader.ReadInt64();
							int iter = reader.ReadInt32();
							int size = reader.ReadInt32();
							LinearCalculationFinished?.Invoke(time, size);
						}
						break;

					case CommandCodes.CalculationFailed:
						CalculationFailed?.Invoke();
						break;
				}
			}
		}
		catch (Exception) { }
		finally { Disconnected?.Invoke(); }
	}
}