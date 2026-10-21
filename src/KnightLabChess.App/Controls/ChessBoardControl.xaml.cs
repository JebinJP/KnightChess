using System.Windows;
using System.Windows.Controls;
using KnightLabChess.App.ViewModels;
using KnightLabChess.App.Views.Dialogs;

namespace KnightLabChess.App.Controls;

/// <summary>
/// Interaction logic for ChessBoardControl.xaml
/// </summary>
public partial class ChessBoardControl : UserControl
{
    public ChessBoardControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is ChessBoardViewModel vm && vm.PromptPromotion is null)
        {
            vm.PromptPromotion = (color) =>
            {
                var parentWindow = Window.GetWindow(this);
                var dlg = new PromotionDialog(color);
                if (parentWindow != null)
                {
                    dlg.Owner = parentWindow;
                }
                var result = dlg.ShowDialog();
                return result == true ? dlg.SelectedPieceType : null;
            };
        }
    }
}
