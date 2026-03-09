using CommunityToolkit.Mvvm.ComponentModel;

namespace SkylarkTerminal.Models;

public sealed partial class SnippetEditDraft : ObservableObject
{
    private string? snippetId;
    private string? categoryId;
    private string categoryName = string.Empty;
    private bool createNewCategory;
    private string title = string.Empty;
    private string content = string.Empty;
    private string tagsText = string.Empty;

    public string? SnippetId
    {
        get => snippetId;
        set => SetProperty(ref snippetId, value);
    }

    public string? CategoryId
    {
        get => categoryId;
        set => SetProperty(ref categoryId, value);
    }

    public string CategoryName
    {
        get => categoryName;
        set => SetProperty(ref categoryName, value);
    }

    public bool CreateNewCategory
    {
        get => createNewCategory;
        set => SetProperty(ref createNewCategory, value);
    }

    public string Title
    {
        get => title;
        set => SetProperty(ref title, value);
    }

    public string Content
    {
        get => content;
        set => SetProperty(ref content, value);
    }

    public string TagsText
    {
        get => tagsText;
        set => SetProperty(ref tagsText, value);
    }

    public static SnippetEditDraft Empty()
    {
        return new SnippetEditDraft();
    }
}
