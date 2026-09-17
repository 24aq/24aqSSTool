using System.Windows;

namespace TwentyFourAqSSTool;

public partial class DisclaimerWindow : Window
{
    public DisclaimerWindow()
    {
        InitializeComponent();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

    private void Accept_Click(object sender, RoutedEventArgs e)
    {
        var main = new MainWindow();
        main.Show();
        Close();
    }
}