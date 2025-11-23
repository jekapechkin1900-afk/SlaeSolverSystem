using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SlaeSolverSystem.Common;

public class LocalCalculator
{
	private readonly int _startRow;
	private readonly int _rowCount;
	private readonly int _matrixSize;
	private readonly double[,] _localMatrix;
	private readonly double[] _localB;

	public LocalCalculator(int startRow, int rowCount, int matrixSize, double[,] localMatrix, double[] localB)
	{
		_startRow = startRow;
		_rowCount = rowCount;
		_matrixSize = matrixSize;
		_localMatrix = localMatrix;
		_localB = localB;
	}

	private void CalculateSingleRow(int i, double[] fullX, double[] result)
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
	}

	public double[] CalculatePartSingleThread(double[] fullX)
	{
		var result = new double[_rowCount];
		for (int i = 0; i < _rowCount; i++)
		{
			CalculateSingleRow(i, fullX, result);
		}
		return result;
	}

	public double[] CalculatePartMultiThreadWithPool(double[] fullX)
	{
		var result = new double[_rowCount];
		Parallel.For(0, _rowCount, i =>
		{
			CalculateSingleRow(i, fullX, result);
		});
		return result;
	}

	public double[] CalculatePartMultiThreadWithoutPool(double[] fullX)
	{
		var result = new double[_rowCount];
		int threadCount = Math.Min(Environment.ProcessorCount, _rowCount);
		if (threadCount == 0) return result;

		var threads = new List<Thread>();
		int rowsPerThread = _rowCount / threadCount;
		int extraRows = _rowCount % threadCount;

		int currentRow = 0;
		for (int t = 0; t < threadCount; t++)
		{
			int start = currentRow;
			int rowsForThisThread = rowsPerThread + (t < extraRows ? 1 : 0);
			int end = start + rowsForThisThread;

			var thread = new Thread(() =>
			{
				for (int i = start; i < end; i++)
				{
					CalculateSingleRow(i, fullX, result);
				}
			});
			threads.Add(thread);
			thread.Start();
			currentRow = end;
		}

		foreach (var thread in threads)
		{
			thread.Join();
		}
		return result;
	}

	public async Task<double[]> CalculatePartMultiThreadAsync(double[] fullX)
	{
		var result = new double[_rowCount];
		int taskCount = Environment.ProcessorCount;
		if (taskCount == 0 || _rowCount == 0) return result;

		var tasks = new List<Task>();
		int rowsPerTask = _rowCount / taskCount;
		int extraRows = _rowCount % taskCount;

		int currentRow = 0;
		for (int t = 0; t < taskCount; t++)
		{
			int start = currentRow;
			int rowsForThisTask = rowsPerTask + (t < extraRows ? 1 : 0);
			int end = start + rowsForThisTask;

			tasks.Add(Task.Run(() =>
			{
				for (int i = start; i < end; i++)
				{
					CalculateSingleRow(i, fullX, result);
				}
			}));
			currentRow = end;
		}
		await Task.WhenAll(tasks);
		return result;
	}
}
