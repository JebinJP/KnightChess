using System.Windows;
using KnightLabChess.Core.Models;

namespace KnightLabChess.App.Views.Dialogs;

/// <summary>
/// Interaction logic for PromotionDialog.xaml
/// </summary>
public partial class PromotionDialog : Window
{
    public PieceType SelectedPieceType { get; private set; } = PieceType.Queen;

    public PromotionDialog()
    {
        InitializeComponent();
    }

    private void Queen_Click(object sender, RoutedEventArgs e)
    {
        SelectedPieceType = PieceType.Queen;
        DialogResult = true;
    }

    private void Rook_Click(object sender, RoutedEventArgs e)
    {
        SelectedPieceType = PieceType.Rook;
        DialogResult = true;
    }

    private void Bishop_Click(object sender, RoutedEventArgs e)
    {
        SelectedPieceType = PieceType.Bishop;
        DialogResult = true;
    }

    private void Knight_Click(object sender, RoutedEventArgs e)
    {
        SelectedPieceType = PieceType.Knight;
        DialogResult = true;
    }
}
