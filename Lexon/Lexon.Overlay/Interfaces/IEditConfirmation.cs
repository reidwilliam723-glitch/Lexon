namespace Lexon.Overlay.Interfaces;

public interface IEditConfirmation
{
    bool RequireConfirmation { get; set; }
    void RequestEdit(string before, string after, int x, int y, Action apply, Action? onCancel = null, string? trustKey = null);
    void BeginLiveRewrite(string before, int x, int y, Action<string> apply, Action? onCancel = null, string? trustKey = null);
    void UpdateLiveRewrite(string after, bool complete);
    void DismissLiveRewrite();
    bool IsPreviewVisible { get; }
    bool TryHandleKey(int virtualKey, bool shift, bool control, bool alt);
}
