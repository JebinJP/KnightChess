using System.Windows;
using System.Windows.Input;
using KnightLabChess.Core.Models;

namespace KnightLabChess.App.Views.Dialogs;

/// <summary>
/// Interaction logic for PromotionDialog.xaml
/// </summary>
public partial class PromotionDialog : Window
{
    public PieceType SelectedPieceType { get; private set; } = PieceType.Queen;

    public PromotionDialog() : this(PieceColor.White)
    {
    }

    public PromotionDialog(PieceColor color)
    {
        InitializeComponent();
        SetPieceColor(color);
    }

    public void SetPieceColor(PieceColor color)
    {
        if (color == PieceColor.Black)
        {
            QueenGlyph.Text = "♛";
            RookGlyph.Text = "♜";
            BishopGlyph.Text = "♝";
            KnightGlyph.Text = "♞";
        }
        else
        {
            QueenGlyph.Text = "♕";
            RookGlyph.Text = "♖";
            BishopGlyph.Text = "♗";
            KnightGlyph.Text = "♘";
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
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
