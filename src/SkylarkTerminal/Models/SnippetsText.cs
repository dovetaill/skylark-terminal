namespace SkylarkTerminal.Models;

public static class SnippetsText
{
    public const string ModeTitle = "代码块";
    public const string SearchWatermark = "搜索代码块";
    public const string NewSnippet = "新建代码块";
    public const string NewCategory = "新建分类";
    public const string CreateFromClipboard = "从剪贴板创建";
    public const string ClearSearch = "清空搜索";
    public const string Run = "运行";
    public const string Edit = "编辑";
    public const string Copy = "复制";
    public const string Paste = "粘贴";
    public const string ViewMore = "查看详情";
    public const string RunInAllTabs = "在全部标签页运行";
    public const string CreateSnippet = "新建代码块";
    public const string EditSnippet = "编辑代码块";
    public const string TitleWatermark = "标题";
    public const string CategoryWatermark = "选择或新建分类";
    public const string TagsWatermark = "标签，使用逗号分隔";
    public const string ContentWatermark = "代码块内容";
    public const string Save = "保存";
    public const string Cancel = "取消";
    public const string Delete = "删除";
    public const string DeleteCategory = "删除分类";
    public const string Back = "返回";
    public const string RunInAllTabsDialogTitle = "在全部标签页运行";
    public const string DeleteDialogTitle = "删除代码块";
    public const string DeleteCategoryDialogTitle = "删除分类";

    public static string BuildRunInAllTabsMessage(string snippetTitle, int targetCount)
    {
        return $"要在 {targetCount} 个已连接 SSH 标签页中运行“{snippetTitle}”吗？";
    }

    public static string BuildDeleteMessage(string snippetTitle)
    {
        return $"确定删除代码块“{snippetTitle}”吗？";
    }

    public static string BuildDeleteCategoryMessage(string categoryName, int snippetCount)
    {
        return snippetCount > 0
            ? $"确定删除分类“{categoryName}”吗？这会同时删除该分类下的 {snippetCount} 个代码块。"
            : $"确定删除分类“{categoryName}”吗？";
    }
}
