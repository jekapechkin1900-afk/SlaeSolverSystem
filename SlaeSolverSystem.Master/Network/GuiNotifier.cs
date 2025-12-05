using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using SlaeSolverSystem.Common;

namespace SlaeSolverSystem.Master.Network;

public class GuiNotifier
{
	private readonly TcpClient _guiClient;
	private readonly NetworkStream _stream;
	private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

	public GuiNotifier(TcpClient guiClient)
	{
		_guiClient = guiClient;
		if (_guiClient.Connected)
		{
			_stream = _guiClient.GetStream();
		}
	}

	public async Task SendLogAsync(string message)
	{
		if (_guiClient?.Connected != true) return;
		await _lock.WaitAsync();
		try
		{
			await NetworkHelper.SendMessageAsync(_stream, CommandCodes.LogMessage, NetworkHelper.ToBytes(message));
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[GuiNotifier] Ошибка отправки лога: {ex.Message}");
		}
		finally { _lock.Release(); }
	}

	public async Task SendStatusAsync(string status)
	{
		if (_guiClient?.Connected != true) return;
		await _lock.WaitAsync();
		try
		{
			await NetworkHelper.SendMessageAsync(_stream, CommandCodes.StatusUpdate, NetworkHelper.ToBytes(status));
		}
		catch { /* Ignore send errors */ }
		finally { _lock.Release(); }
	}

	public async Task SendProgressAsync(int iteration, double error)
	{
		if (_guiClient?.Connected != true) return;

		using var ms = new MemoryStream();
		using var writer = new BinaryWriter(ms);
		writer.Write(iteration);
		writer.Write(error);
		var data = ms.ToArray();

		await _lock.WaitAsync();
		try
		{
			await NetworkHelper.SendMessageAsync(_stream, CommandCodes.ProgressUpdate, data);
		}
		catch { }
		finally { _lock.Release(); }
	}

	public async Task NotifyLinearResultAsync(long time, int iterations, int size)
	{
		if (_guiClient?.Connected != true) return;

		using var ms = new MemoryStream();
		using var writer = new BinaryWriter(ms);
		writer.Write(time);
		writer.Write(iterations);
		writer.Write(size);
		var data = ms.ToArray();

		await _lock.WaitAsync();
		try
		{
			await NetworkHelper.SendMessageAsync(_stream, CommandCodes.LinearResultReady, data);
		}
		catch { }
		finally { _lock.Release(); }
	}

	public async Task NotifyDistributedResultAsync(long time, int iterations, double[] solution, int size, int resources)
	{
		if (_guiClient?.Connected != true) return;

		using var ms = new MemoryStream();
		using var writer = new BinaryWriter(ms);
		writer.Write(time);
		writer.Write(iterations);
		writer.Write(size);
		writer.Write(resources);
		writer.Write(solution.Length);
		foreach (var val in solution) writer.Write(val);
		var data = ms.ToArray();

		await _lock.WaitAsync();
		try
		{
			await NetworkHelper.SendMessageAsync(_stream, CommandCodes.ResultReady, data);
		}
		catch { }
		finally { _lock.Release(); }
	}

	public async Task NotifyCalculationFailedAsync()
	{
		if (_guiClient?.Connected != true) return;

		await _lock.WaitAsync();
		try
		{
			await NetworkHelper.SendMessageAsync(_stream, CommandCodes.CalculationFailed, []);
		}
		catch { }
		finally { _lock.Release(); }
	}

	public async Task SendPoolStateAsync(int availableCount, int totalCount)
	{
		if (_guiClient?.Connected != true) return;

		using var ms = new MemoryStream();
		using var writer = new BinaryWriter(ms);
		writer.Write(availableCount);
		writer.Write(totalCount);
		var data = ms.ToArray();

		await _lock.WaitAsync();
		try
		{
			await NetworkHelper.SendMessageAsync(_stream, CommandCodes.PoolStateUpdate, data);
		}
		catch { }
		finally { _lock.Release(); }
	}
}