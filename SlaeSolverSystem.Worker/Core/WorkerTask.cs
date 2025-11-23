using SlaeSolverSystem.Worker.Interfaces;

namespace SlaeSolverSystem.Worker.Core;

public class WorkerTask : IWorkerTask
{
	private int _startRow;
	private int _rowCount;
	private int _matrixSize;
	private double[,] _localMatrix;
	private double[] _localB;

	public bool IsSet { get; private set; }

	public void SetData(int startRow, int rowCount, int matrixSize, double[,] localMatrix, double[] localB)
	{
		_startRow = startRow;
		_rowCount = rowCount;
		_matrixSize = matrixSize;
		_localMatrix = localMatrix;
		_localB = localB;
		IsSet = true;
		Console.WriteLine("Задача успешно установлена.");
	}

	public double[] CalculatePart(double[] fullX)
	{
		if (!IsSet) throw new InvalidOperationException("Задача не установлена.");
		var result = new double[_rowCount];

		Parallel.For(0, _rowCount, i =>
		{
			double sum = 0;
			int globalRowIndex = _startRow + i;
			for (int j = 0; j < _matrixSize; j++)
			{
				if (j != globalRowIndex)
				{
					sum += _localMatrix[i, j] * fullX[j];
				}
			}
			result[i] = (Math.Abs(_localMatrix[i, globalRowIndex]) < 1e-12)
						? 0
						: (_localB[i] - sum) / _localMatrix[i, globalRowIndex];
		});

		return result;
	}

	public void Reset()
	{
		IsSet = false;
		_localMatrix = null;
		_localB = null;
		Console.WriteLine("[WorkerTask] Состояние сброшено.");
	}
}
