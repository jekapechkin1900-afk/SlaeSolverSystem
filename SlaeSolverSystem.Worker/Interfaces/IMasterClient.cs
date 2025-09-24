namespace SlaeSolverSystem.Worker.Interfaces;

public interface IMasterClient
{
	Task ConnectAsync(string ip, int port);
	Task<(byte Command, byte[] Payload)> ReadMessageAsync();
	Task SendMessageAsync(byte command, byte[] payload);
	void Disconnect();
}
