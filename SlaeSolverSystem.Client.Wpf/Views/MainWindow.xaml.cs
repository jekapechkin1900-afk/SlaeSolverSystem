using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using SlaeSolverSystem.Client.Wpf.ViewModels;

namespace SlaeSolverSystem.Client.Wpf.Views;

public partial class MainWindow : MetroWindow
{
	public MainViewModel ViewModel { get; }

	public MainWindow()
	{
		InitializeComponent();
		ViewModel = new MainViewModel(DialogCoordinator.Instance);
		DataContext = ViewModel;
	}

	#region Drag-and-Drop Handlers

	private void TextBox_PreviewDragOver(object sender, DragEventArgs e)
	{
		e.Handled = true; 

		if (e.Data.GetDataPresent(DataFormats.FileDrop))
		{
			e.Effects = DragDropEffects.Copy;
		}
		else
		{
			e.Effects = DragDropEffects.None;
		}
	}

	private void TextBox_DragEnter(object sender, DragEventArgs e)
	{
		if (sender is TextBox textBox && e.Data.GetDataPresent(DataFormats.FileDrop))
		{
			textBox.BorderBrush = FindResource("MahApps.Brushes.Accent") as Brush;
			textBox.BorderThickness = new Thickness(2);
		}
	}

	private void TextBox_DragLeave(object sender, DragEventArgs e)
	{
		if (sender is TextBox textBox)
		{
			textBox.ClearValue(BorderBrushProperty);
			textBox.ClearValue(BorderThicknessProperty);
		}
	}

	private void MatrixTextBox_Drop(object sender, DragEventArgs e)
	{
		HandleFileDrop(sender as TextBox, path => ViewModel.MatrixFilePath = path, e);
	}

	private void VectorTextBox_Drop(object sender, DragEventArgs e)
	{
		HandleFileDrop(sender as TextBox, path => ViewModel.VectorFilePath = path, e);
	}

	private void NodesTextBox_Drop(object sender, DragEventArgs e)
	{
		HandleFileDrop(sender as TextBox, path => ViewModel.NodesFilePath = path, e);
	}

	private void HandleFileDrop(TextBox textBox, Action<string> setPathAction, DragEventArgs e)
	{
		textBox?.ClearValue(BorderBrushProperty);
		textBox?.ClearValue(BorderThicknessProperty);

		if (e.Data.GetDataPresent(DataFormats.FileDrop))
		{
			string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

			if (files != null && files.Length > 0)
			{
				setPathAction(files[0]);
			}
		}
	}

	#endregion
}
