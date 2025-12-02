namespace SlaeSolverSystem.Client.Wpf.ViewModels;

public class TestResultViewModel : BaseViewModel 
{
	public string TestType { get; set; }
	public int MatrixSize { get; set; }
	public long TimeMs { get; set; }
	public int Iterations { get; set; }
	public double Speedup { get; set; }

	public int Resources { get; set; }
	public string ResourceType { get; set; } 
	public double Efficiency { get; set; } 
}
