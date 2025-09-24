using System.Diagnostics;
using SlaeSolverSystem.Common.Contracts;

namespace SlaeSolverSystem.Common;

public class LinearSlaeSolver
{
	public static Task<LinearSolveResult> SolveAsync(double[,] A, double[] b)
	{
		return Task.Run(() =>
		{
			var stopwatch = Stopwatch.StartNew();
			int size = b.Length;

			var matrix = (double[,])A.Clone();
			var vector = (double[])b.Clone();

			for (int k = 0; k < size - 1; k++)
			{
				int maxRowIndex = k;
				for (int i = k + 1; i < size; i++)
				{
					if (Math.Abs(matrix[i, k]) > Math.Abs(matrix[maxRowIndex, k]))
					{
						maxRowIndex = i;
					}
				}

				if (maxRowIndex != k)
				{
					for (int j = 0; j < size; j++)
					{
						(matrix[k, j], matrix[maxRowIndex, j]) = (matrix[maxRowIndex, j], matrix[k, j]);
					}
					(vector[k], vector[maxRowIndex]) = (vector[maxRowIndex], vector[k]);
				}

				if (Math.Abs(matrix[k, k]) < 1e-12)
					throw new InvalidOperationException($"Матрица вырождена или близка к вырожденной. Решение невозможно.");

				for (int i = k + 1; i < size; i++)
				{
					double factor = matrix[i, k] / matrix[k, k];
					for (int j = k; j < size; j++)
					{
						matrix[i, j] -= factor * matrix[k, j];
					}
					vector[i] -= factor * vector[k];
				}
			}

			var x = new double[size];
			for (int i = size - 1; i >= 0; i--)
			{
				double sum = 0;
				for (int j = i + 1; j < size; j++)
				{
					sum += matrix[i, j] * x[j];
				}

				if (Math.Abs(matrix[i, i]) < 1e-12)
					throw new InvalidOperationException($"Матрица вырождена. Решение невозможно.");

				x[i] = (vector[i] - sum) / matrix[i, i];
			}

			stopwatch.Stop();

			return new LinearSolveResult(stopwatch.ElapsedMilliseconds, x, size);
		});
	}
}
