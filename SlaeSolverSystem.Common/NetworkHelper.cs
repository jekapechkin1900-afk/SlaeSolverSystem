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
		var commandByte = new byte[1];
		await ReadExactlyAsync(stream, commandByte, 1);

		var lengthBytes = new byte[4];
		await ReadExactlyAsync(stream, lengthBytes, 4);
		var length = BitConverter.ToInt32(lengthBytes, 0);

		var payload = new byte[length];
		if (length > 0)
		{
			await ReadExactlyAsync(stream, payload, length);
		}

		return (commandByte[0], payload);
	}

	private static async Task ReadExactlyAsync(NetworkStream stream, byte[] buffer, int bytesToRead)
	{
		int offset = 0;
		while (offset < bytesToRead)
		{
			int read = await stream.ReadAsync(buffer, offset, bytesToRead - offset);
			if (read == 0) throw new EndOfStreamException("Соединение было закрыто.");
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
