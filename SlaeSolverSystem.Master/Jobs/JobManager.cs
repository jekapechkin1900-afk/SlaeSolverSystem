using SlaeSolverSystem.Master.Pools;

namespace SlaeSolverSystem.Master.Jobs;

public interface IJobManager
{
	IWorkerPool WorkerPool { get; }

	void EnqueueJob(IJob job);

	void StartProcessing();

	void StopProcessing();
}
