using System.Net.Sockets;

namespace SlaeSolverSystem.Master.Pools;

public interface IWorkerPool
{
	int AvailableCount { get; }
	int TotalCount { get; }
	void Add(TcpClient worker);
	Task<List<TcpClient>> GetWorkersAsync(int count, CancellationToken cancellationToken);
	void ReturnWorkers(IEnumerable<TcpClient> workers);
	void PruneDisconnected();
}
