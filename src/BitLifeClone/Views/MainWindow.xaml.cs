using System.Windows;
using BitLifeClone.ViewModels;

namespace BitLifeClone.Views;

/// <summary>
/// Solo ensambla: los servicios los crea <see cref="MainViewModel"/> en su constructor.
/// Toda la logica va en el ViewModel.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        DataContext = new MainViewModel();
    }
}
