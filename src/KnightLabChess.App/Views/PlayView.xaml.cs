using System.Windows;
using System.Windows.Controls;
using KnightLabChess.App.ViewModels;
using KnightLabChess.App.Views.Dialogs;

namespace KnightLabChess.App.Views;

/// <summary>
/// Interaction logic for PlayView.xaml
/// </summary>
public partial class PlayView : UserControl
{
    public PlayView()
    {
        InitializeComponent();
    }

    private void PlayView_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is PlayViewModel vm)
        {
            vm.Board.PromptPromotion = (color) =>
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
