using System.Net.Sockets;
using SlaeSolverSystem.Common;

namespace SlaeSolverSystem.Master.Network;

public class GuiNotifier
{
	private readonly TcpClient _guiClient;
	private readonly NetworkStream _stream;

	public GuiNotifier(TcpClient guiClient)
	{
		_guiClient = guiClient;
		_stream = _guiClient.GetStream();
	}

	public Task SendLogAsync(string message)
	{
		if (!_guiClient.Connected) return Task.CompletedTask;
		Console.WriteLine($"[To GUI Log] {message}");
		return NetworkHelper.SendMessageAsync(_stream, CommandCodes.LogMessage, NetworkHelper.ToBytes(message));
	}

	public Task SendStatusAsync(string status)
	{
		if (!_guiClient.Connected) return Task.CompletedTask;
		return NetworkHelper.SendMessageAsync(_stream, CommandCodes.StatusUpdate, NetworkHelper.ToBytes(status));
	}

	public Task SendProgressAsync(int iteration, double error)
	{
		if (!_guiClient.Connected) return Task.CompletedTask;
		using var ms = new MemoryStream();
		using var writer = new BinaryWriter(ms);
		writer.Write(iteration);
		writer.Write(error);
		return NetworkHelper.SendMessageAsync(_stream, CommandCodes.ProgressUpdate, ms.ToArray());
	}

	public Task NotifyLinearResultAsync(long time, int iterations, int size)
	{
		if (!_guiClient.Connected) return Task.CompletedTask;
		using var ms = new MemoryStream();
		using var writer = new BinaryWriter(ms);
		writer.Write(time);
		writer.Write(iterations);
		writer.Write(size);
		return NetworkHelper.SendMessageAsync(_stream, CommandCodes.LinearResultReady, ms.ToArray());
	}

	public Task NotifyDistributedResultAsync(long time, int iterations, double[] solution, int size)
	{
		if (!_guiClient.Connected) return Task.CompletedTask;
		using var ms = new MemoryStream();
		using var writer = new BinaryWriter(ms);
		writer.Write(time);
		writer.Write(iterations);
		writer.Write(size);
		writer.Write(solution.Length);
		foreach (var val in solution) writer.Write(val);
		return NetworkHelper.SendMessageAsync(_stream, CommandCodes.ResultReady, ms.ToArray());
	}

	public Task NotifyCalculationFailedAsync()
	{
		if (!_guiClient.Connected) return Task.CompletedTask;
		return NetworkHelper.SendMessageAsync(_stream, CommandCodes.CalculationFailed, []);
	}

	public Task SendPoolStateAsync(int availableCount, int totalCount)
	{
		if (!_guiClient.Connected) return Task.CompletedTask;

		using var ms = new MemoryStream();
		using var writer = new BinaryWriter(ms);
		writer.Write(availableCount);
		writer.Write(totalCount);

		return NetworkHelper.SendMessageAsync(_stream, CommandCodes.PoolStateUpdate, ms.ToArray());
	}
}