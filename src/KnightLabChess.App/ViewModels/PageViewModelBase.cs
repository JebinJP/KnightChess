using CommunityToolkit.Mvvm.ComponentModel;

namespace KnightLabChess.App.ViewModels;

/// <summary>
/// Base class for all ViewModels that represent a navigation page.
/// </summary>
public abstract partial class PageViewModelBase : ObservableObject
{
    [ObservableProperty]
    private string _pageTitle = string.Empty;

    [ObservableProperty]
    private string _pageIcon = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// Called when the page is navigated to.
    /// </summary>
    public virtual Task OnNavigatedToAsync() => Task.CompletedTask;

    /// <summary>
    /// Called when the page is navigated away from.
    /// </summary>
    public virtual Task OnNavigatedFromAsync() => Task.CompletedTask;
}
