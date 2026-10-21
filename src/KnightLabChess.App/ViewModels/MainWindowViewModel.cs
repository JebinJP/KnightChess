using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace KnightLabChess.App.ViewModels;

/// <summary>
/// Represents a navigation item in the sidebar.
/// </summary>
public class NavigationItem
{
    public string Title { get; init; } = string.Empty;
    public string Icon { get; init; } = string.Empty;
    public Type ViewModelType { get; init; } = typeof(HomeViewModel);
}

/// <summary>
/// Main window ViewModel — controls navigation and the application shell.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly IServiceProvider _serviceProvider;

    [ObservableProperty]
    private PageViewModelBase? _currentPage;

    [ObservableProperty]
    private NavigationItem? _selectedNavItem;

    [ObservableProperty]
    private string _windowTitle = "KnightLab Chess";

    public List<NavigationItem> NavigationItems { get; } =
    [
        new() { Title = "Home", Icon = "⌂", ViewModelType = typeof(HomeViewModel) },
        new() { Title = "Play Chess", Icon = "♟", ViewModelType = typeof(PlayViewModel) },
        new() { Title = "Learn", Icon = "📖", ViewModelType = typeof(LearnViewModel) },
        new() { Title = "Puzzles", Icon = "🧩", ViewModelType = typeof(PuzzlesViewModel) },
        new() { Title = "Analysis", Icon = "🔍", ViewModelType = typeof(AnalysisViewModel) },
        new() { Title = "Opening Explorer", Icon = "📚", ViewModelType = typeof(OpeningExplorerViewModel) },
        new() { Title = "Progress", Icon = "📊", ViewModelType = typeof(ProgressViewModel) },
        new() { Title = "Settings", Icon = "⚙", ViewModelType = typeof(SettingsViewModel) },
    ];

    public MainWindowViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    [RelayCommand]
    public async Task NavigateTo(NavigationItem item)
    {
        if (item == SelectedNavItem) return;

        try
        {
            // Navigate away from current
            if (CurrentPage is not null)
            {
                try { await CurrentPage.OnNavigatedFromAsync(); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Nav] OnNavigatedFromAsync error: {ex}"); }
            }

            SelectedNavItem = item;
            var page = _serviceProvider.GetService(item.ViewModelType) as PageViewModelBase;
            if (page is null)
            {
                System.Diagnostics.Debug.WriteLine($"[Nav] Failed to resolve ViewModel: {item.ViewModelType.Name}");
                return;
            }
            CurrentPage = page;

            try { await page.OnNavigatedToAsync(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Nav] OnNavigatedToAsync error for {item.Title}: {ex}"); }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Nav] NavigateTo error: {ex}");
        }
    }

    /// <summary>
    /// Navigate to a specific page type.
    /// </summary>
    public async Task NavigateToPage<T>() where T : PageViewModelBase
    {
        var item = NavigationItems.FirstOrDefault(n => n.ViewModelType == typeof(T));
        if (item is not null)
        {
            await NavigateTo(item);
        }
    }

    /// <summary>
    /// Called when the app starts to navigate to the home page.
    /// </summary>
    public async Task InitializeAsync()
    {
        var home = NavigationItems[0];
        await NavigateTo(home);
    }
}
