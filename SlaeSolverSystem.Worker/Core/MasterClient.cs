using System.Net.Sockets;
using SlaeSolverSystem.Common;
using SlaeSolverSystem.Worker.Interfaces;

namespace SlaeSolverSystem.Worker.Core;

public class MasterClient : IMasterClient
{
	private TcpClient _client;
	private NetworkStream _stream;

	public async Task ConnectAsync(string ip, int port)
	{
		_client = new TcpClient();
		await _client.ConnectAsync(ip, port);
		_stream = _client.GetStream();
	}

	public Task<(byte Command, byte[] Payload)> ReadMessageAsync()
	{
		if (_stream == null) throw new InvalidOperationException("Клиент не подключен.");
		return NetworkHelper.ReadMessageAsync(_stream);
	}

	public Task SendMessageAsync(byte command, byte[] payload)
	{
		if (_stream == null) throw new InvalidOperationException("Клиент не подключен.");
		return NetworkHelper.SendMessageAsync(_stream, command, payload);
	}

	public void Disconnect()
	{
		_stream?.Dispose();
		_client?.Close();
	}
}
