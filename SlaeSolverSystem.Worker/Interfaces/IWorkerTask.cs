namespace SlaeSolverSystem.Worker.Interfaces;

public interface IWorkerTask
{
	bool IsSet { get; }
	void SetData(int startRow, int rowCount, int matrixSize, double[,] localMatrix, double[] localB);
	double[] CalculatePart(double[] fullX);
	void Reset();
}
