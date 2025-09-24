using System.Text;

namespace SlaeSolverSystem.Tests.Infrastructure;

public static class TestDataGenerator
{
	public static async Task GenerateSlaeFilesAsync(int size, string matrixFile, string vectorFile)
	{
		var rand = new Random();
		var x_true = Enumerable.Range(1, size).Select(i => (double)i).ToArray();

		using var matrixWriter = new StreamWriter(matrixFile, false, Encoding.UTF8);
		using var vectorWriter = new StreamWriter(vectorFile, false, Encoding.UTF8);

		for (int i = 0; i < size; i++)
		{
			var row = new double[size];
			double rowSumOfAbs = 0.0;

			for (int j = 0; j < size; j++)
			{
				if (i == j) continue;
				row[j] = rand.NextDouble() * 2 - 1; 
				rowSumOfAbs += Math.Abs(row[j]);
			}

			row[i] = rowSumOfAbs + rand.NextDouble() * 5 + 1;
			if (rand.NextDouble() < 0.5) row[i] *= -1;

			double b_i = row.Zip(x_true, (a, x) => a * x).Sum();

			await matrixWriter.WriteLineAsync(string.Join(" ", row.Select(v => v.ToString("F8"))));
			await vectorWriter.WriteLineAsync(b_i.ToString("F8"));
		}
	}

	public static async Task GenerateNodesFileAsync(string nodesFile, int count)
	{
		var nodes = Enumerable.Repeat("127.0.0.1", count);
		await File.WriteAllLinesAsync(nodesFile, nodes);
	}
}
