using System.Net.Sockets;
using System.Text;

namespace SlaeSolverSystem.Common;

public static class NetworkHelper
{
	public static async Task SendMessageAsync(NetworkStream stream, byte command, byte[] payload)
	{
		var lengthBytes = BitConverter.GetBytes(payload.Length);
		await stream.WriteAsync([command], 0, 1);
		await stream.WriteAsync(lengthBytes, 0, 4);
		if (payload.Length > 0)
		{
			await stream.WriteAsync(payload, 0, payload.Length);
		}
	}

	public static async Task<(byte Command, byte[] Payload)> ReadMessageAsync(NetworkStream stream)
	{
		var cmdBuffer = new byte[1];
		await ReadExactlyAsync(stream, cmdBuffer, 1); // 1 байт команды
		byte cmd = cmdBuffer[0];

		var lenBuffer = new byte[4];
		await ReadExactlyAsync(stream, lenBuffer, 4); // 4 байта длины
		int len = BitConverter.ToInt32(lenBuffer, 0);

		var payload = new byte[len];
		if (len > 0)
		{
			await ReadExactlyAsync(stream, payload, len);
		}

		return (cmd, payload);
	}

	private static async Task ReadExactlyAsync(NetworkStream stream, byte[] buffer, int count)
	{
		int offset = 0;
		while (offset < count)
		{
			int read = await stream.ReadAsync(buffer, offset, count - offset);
			if (read == 0) throw new EndOfStreamException();
			offset += read;
		}
	}

	public static byte[] ToBytes(double[] array)
	{
		var buffer = new byte[array.Length * 8];
		Buffer.BlockCopy(array, 0, buffer, 0, buffer.Length);
		return buffer;
	}

	public static double[] ToDoubleArray(byte[] bytes)
	{
		var array = new double[bytes.Length / 8];
		Buffer.BlockCopy(bytes, 0, array, 0, bytes.Length);
		return array;
	}

	public static byte[] ToBytes(string str) => Encoding.UTF8.GetBytes(str);
	public static string ToString(byte[] bytes) => Encoding.UTF8.GetString(bytes);
}
