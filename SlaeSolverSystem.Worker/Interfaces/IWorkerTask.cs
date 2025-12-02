namespace SlaeSolverSystem.Worker.Interfaces;

public interface IWorkerTask
{
	bool IsSet { get; }
	void SetData(int startRow, int rowCount, int matrixSize, double[,] localMatrix, double[] localB);
	double[] CalculatePartSingleThread(double[] fullX);
	double[] CalculatePartMultiThreadWithPool(double[] fullX);
	double[] CalculatePartMultiThreadWithoutPool(double[] fullX);
	Task<double[]> CalculatePartMultiThreadAsync(double[] fullX);
	void Reset();
}
