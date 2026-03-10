using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkylarkTerminal.Models;
using SkylarkTerminal.Services;
using SkylarkTerminal.Services.Mock;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SkylarkTerminal.ViewModels.RightPanelModes;

public sealed partial class SnippetsModeViewModel : ObservableObject, IRightPanelModeViewModel
{
    private readonly ISnippetRepository repository;
    private readonly IClipboardService clipboardService;
    private readonly ITerminalCommandBridge terminalBridge;
    private readonly IAppDialogService dialogService;
    private readonly Func<WorkspaceTabItemViewModel?> selectedTabAccessor;
    private readonly Func<IReadOnlyList<WorkspaceTabItemViewModel>> allTabsAccessor;

    public SnippetsModeViewModel()
        : this(
            new MockSnippetRepository(),
            new MockClipboardService(),
            new MockTerminalCommandBridge(),
            new MockAppDialogService(),
            static () => null,
            static () => [])
    {
    }

    public SnippetsModeViewModel(
        ISnippetRepository repository,
        IClipboardService clipboardService,
        ITerminalCommandBridge terminalBridge,
        IAppDialogService dialogService,
        Func<WorkspaceTabItemViewModel?> selectedTabAccessor,
        Func<IReadOnlyList<WorkspaceTabItemViewModel>> allTabsAccessor)
    {
        this.repository = repository;
        this.clipboardService = clipboardService;
        this.terminalBridge = terminalBridge;
        this.dialogService = dialogService;
        this.selectedTabAccessor = selectedTabAccessor;
        this.allTabsAccessor = allTabsAccessor;

        BeginCreateCommand = new RelayCommand(BeginCreate);
        BeginCreateCategoryCommand = new RelayCommand(BeginCreateCategory);
        BeginCreateFromClipboardCommand = new AsyncRelayCommand(BeginCreateFromClipboardAsync);
        ClearFilterCommand = new RelayCommand(ClearFilter);
        FocusSearchCommand = new RelayCommand(() => { });
        OpenEditCommand = new RelayCommand<SnippetItem?>(OpenEdit);
        OpenViewMoreCommand = new RelayCommand<SnippetItem?>(OpenViewMore);
        CancelEditCommand = new RelayCommand(CancelEdit);
        StartNewCategoryCommand = new RelayCommand(StartNewCategory);

        Actions =
        [
            new("snippet.new", "\uE710", "新建代码块", "新建代码块", BeginCreateCommand),
            new("snippet.search-focus", "\uE721", "聚焦搜索", "聚焦搜索", FocusSearchCommand),
        ];
    }

    public RightToolsViewKind Kind => RightToolsViewKind.Snippets;

    public string Title => SnippetsText.ModeTitle;

    public string Glyph => "\uE8D2";

    public RightPanelHeaderNode HeaderNode { get; } = new ActionStripRightPanelHeader();

    public RightToolsContentNode ContentNode { get; } = new SnippetsRightToolsContent();

    public IReadOnlyList<ModeActionDescriptor> Actions { get; }

    public ObservableCollection<SnippetCategory> Categories { get; } = [];

    public ObservableCollection<SnippetCategory> VisibleCategories { get; } = [];

    [ObservableProperty]
    private string filterText = string.Empty;

    [ObservableProperty]
    private SnippetPanelState panelState = SnippetPanelState.Browse;

    [ObservableProperty]
    private SnippetEditDraft draft = SnippetEditDraft.Empty();

    [ObservableProperty]
    private SnippetItem? selectedSnippet;

    public IRelayCommand BeginCreateCommand { get; }

    public IRelayCommand BeginCreateCategoryCommand { get; }

    public IAsyncRelayCommand BeginCreateFromClipboardCommand { get; }

    public IRelayCommand ClearFilterCommand { get; }

    public IRelayCommand FocusSearchCommand { get; }

    public IRelayCommand<SnippetItem?> OpenEditCommand { get; }

    public IRelayCommand<SnippetItem?> OpenViewMoreCommand { get; }

    public IRelayCommand CancelEditCommand { get; }

    public IRelayCommand StartNewCategoryCommand { get; }

    public bool IsBrowseState => PanelState == SnippetPanelState.Browse;

    public bool IsCreateState => PanelState == SnippetPanelState.Create;

    public bool IsEditState => PanelState == SnippetPanelState.Edit;

    public bool IsViewMoreState => PanelState == SnippetPanelState.ViewMore;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var categories = await repository.LoadAsync(cancellationToken).ConfigureAwait(false);

        Categories.Clear();
        foreach (var category in categories)
        {
            Categories.Add(category);
        }

        RebuildVisibleCategories();
    }

    public void RebuildVisibleCategories()
    {
        var keyword = FilterText.Trim();
        var filteredCategories = string.IsNullOrWhiteSpace(keyword)
            ? Categories.Select(CloneCategory)
            : Categories
                .Select(category => FilterCategory(category, keyword))
                .Where(category => category is not null)
                .Select(category => category!);

        VisibleCategories.Clear();
        foreach (var category in filteredCategories)
        {
            VisibleCategories.Add(category);
        }
    }

    public Task SaveDraftAsync(CancellationToken cancellationToken = default)
    {
        var title = Draft.Title.Trim();
        var content = Draft.Content.Trim();
        var categoryName = Draft.CategoryName.Trim();
        if (string.IsNullOrWhiteSpace(title) ||
            string.IsNullOrWhiteSpace(content) ||
            string.IsNullOrWhiteSpace(categoryName))
        {
            return Task.CompletedTask;
        }

        var category = Categories.FirstOrDefault(
            item => string.Equals(item.Name, categoryName, StringComparison.OrdinalIgnoreCase));
        if (category is null)
        {
            category = new SnippetCategory
            {
                Name = categoryName,
                SortOrder = Categories.Count,
            };
            Categories.Add(category);
        }

        var tags = Draft.TagsText
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (PanelState == SnippetPanelState.Edit && SelectedSnippet is not null)
        {
            var previousCategory = Categories.FirstOrDefault(item => item.Items.Contains(SelectedSnippet));
            if (previousCategory is not null && !ReferenceEquals(previousCategory, category))
            {
                previousCategory.Items.Remove(SelectedSnippet);
                category.Items.Add(SelectedSnippet);
            }

            SelectedSnippet.Title = title;
            SelectedSnippet.Content = content;
            SelectedSnippet.Tags = tags;
            SelectedSnippet.UpdatedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            category.Items.Add(new SnippetItem
            {
                Title = title,
                Content = content,
                Tags = tags,
                SortOrder = category.Items.Count,
            });
        }

        return PersistAndReturnToBrowseAsync(cancellationToken);
    }

    public async Task DeleteSelectedSnippetAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedSnippet is null)
        {
            return;
        }

        if (!await dialogService.ShowDeleteSnippetConfirmAsync(SelectedSnippet.Title).ConfigureAwait(false))
        {
            return;
        }

        var owner = Categories.FirstOrDefault(category => category.Items.Contains(SelectedSnippet));
        if (owner is null)
        {
            return;
        }

        owner.Items.Remove(SelectedSnippet);
        SelectedSnippet = null;
        await PersistAndReturnToBrowseAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task DeleteSnippetAsync(SnippetItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        SelectedSnippet = item;
        return DeleteSelectedSnippetAsync(cancellationToken);
    }

    public async Task DeleteCategoryAsync(SnippetCategory category, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(category);

        var owner = Categories.FirstOrDefault(item => item.Id == category.Id);
        if (owner is null)
        {
            return;
        }

        if (!await dialogService
                .ShowDeleteSnippetCategoryConfirmAsync(owner.Name, owner.Items.Count)
                .ConfigureAwait(false))
        {
            return;
        }

        if (SelectedSnippet is not null && owner.Items.Contains(SelectedSnippet))
        {
            SelectedSnippet = null;
        }

        Categories.Remove(owner);
        await PersistAndReturnToBrowseAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task PasteAsync(SnippetItem item, CancellationToken cancellationToken = default)
    {
        await terminalBridge
            .PasteToActiveAsync(selectedTabAccessor(), item.Content, cancellationToken)
            .ConfigureAwait(false);
    }

    public Task CopyAsync(SnippetItem item, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        return clipboardService.SetTextAsync(item.Content);
    }

    public async Task RunAsync(SnippetItem item, CancellationToken cancellationToken = default)
    {
        await terminalBridge
            .RunInActiveAsync(selectedTabAccessor(), item.Content, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task RunInAllTabsAsync(SnippetItem item, CancellationToken cancellationToken = default)
    {
        var tabs = allTabsAccessor()
            .Where(tab => tab.ConnectionConfig is not null)
            .ToArray();
        if (!await dialogService
                .ShowRunSnippetInAllTabsConfirmAsync(item.Title, tabs.Length)
                .ConfigureAwait(false))
        {
            return;
        }

        await terminalBridge.RunInAllTabsAsync(tabs, item.Content, cancellationToken).ConfigureAwait(false);
    }

    partial void OnFilterTextChanged(string value)
    {
        _ = value;
        RebuildVisibleCategories();
    }

    partial void OnPanelStateChanged(SnippetPanelState value)
    {
        _ = value;
        OnPropertyChanged(nameof(IsBrowseState));
        OnPropertyChanged(nameof(IsCreateState));
        OnPropertyChanged(nameof(IsEditState));
        OnPropertyChanged(nameof(IsViewMoreState));
    }

    private void BeginCreate()
    {
        SelectedSnippet = null;
        Draft = SnippetEditDraft.Empty();
        PanelState = SnippetPanelState.Create;
    }

    private void BeginCreateCategory()
    {
        BeginCreate();
        Draft.CreateNewCategory = true;
        Draft.CategoryName = string.Empty;
    }

    private async Task BeginCreateFromClipboardAsync()
    {
        BeginCreate();
        Draft.Content = await clipboardService.GetTextAsync().ConfigureAwait(false) ?? string.Empty;
    }

    private void OpenEdit(SnippetItem? item)
    {
        if (item is null)
        {
            return;
        }

        var owner = Categories.FirstOrDefault(category => category.Items.Contains(item));
        SelectedSnippet = item;
        Draft = new SnippetEditDraft
        {
            SnippetId = item.Id,
            CategoryId = owner?.Id,
            CategoryName = owner?.Name ?? string.Empty,
            Title = item.Title,
            Content = item.Content,
            TagsText = string.Join(", ", item.Tags),
        };
        PanelState = SnippetPanelState.Edit;
    }

    private void OpenViewMore(SnippetItem? item)
    {
        if (item is null)
        {
            return;
        }

        var owner = Categories.FirstOrDefault(category => category.Items.Contains(item));
        SelectedSnippet = item;
        Draft = new SnippetEditDraft
        {
            SnippetId = item.Id,
            CategoryId = owner?.Id,
            CategoryName = owner?.Name ?? string.Empty,
            Title = item.Title,
            Content = item.Content,
            TagsText = string.Join(", ", item.Tags),
        };
        PanelState = SnippetPanelState.ViewMore;
    }

    private void CancelEdit()
    {
        Draft = SnippetEditDraft.Empty();
        PanelState = SnippetPanelState.Browse;
    }

    private void ClearFilter()
    {
        FilterText = string.Empty;
    }

    private void StartNewCategory()
    {
        Draft.CreateNewCategory = true;
        Draft.CategoryId = null;
        Draft.CategoryName = string.Empty;
    }

    private static SnippetCategory CloneCategory(SnippetCategory category)
    {
        return new SnippetCategory
        {
            Id = category.Id,
            Name = category.Name,
            SortOrder = category.SortOrder,
            IsExpanded = category.IsExpanded,
            Items = new ObservableCollection<SnippetItem>(category.Items),
        };
    }

    private static SnippetCategory? FilterCategory(SnippetCategory category, string keyword)
    {
        var categoryMatches = category.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase);
        var items = categoryMatches
            ? category.Items
            : new ObservableCollection<SnippetItem>(category.Items.Where(item => Matches(item, keyword)));

        if (items.Count == 0)
        {
            return null;
        }

        return new SnippetCategory
        {
            Id = category.Id,
            Name = category.Name,
            SortOrder = category.SortOrder,
            IsExpanded = category.IsExpanded,
            Items = items,
        };
    }

    private static bool Matches(SnippetItem item, string keyword)
    {
        return item.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
               item.Content.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    private async Task PersistAndReturnToBrowseAsync(CancellationToken cancellationToken)
    {
        await repository.SaveAsync(Categories.ToArray(), cancellationToken).ConfigureAwait(false);
        RebuildVisibleCategories();
        Draft = SnippetEditDraft.Empty();
        PanelState = SnippetPanelState.Browse;
    }
}
