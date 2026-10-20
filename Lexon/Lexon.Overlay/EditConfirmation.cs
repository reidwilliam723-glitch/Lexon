using Lexon.Core;
using Lexon.Overlay.Interfaces;

namespace Lexon.Overlay;

public sealed class EditConfirmation : IEditConfirmation
{
    private readonly IPreviewOverlay _preview;
    private readonly GlanceOverlay? _glance;
    private readonly SessionEditTrust _trust = new();
    private Action? _pendingApply;
    private Action? _pendingCancel;
    private string? _pendingTrustKey;
    private string _liveAfter = string.Empty;
    private Action<string>? _liveApply;

    public EditConfirmation(IPreviewOverlay preview, GlanceOverlay? glance = null)
    {
        _preview = preview;
        _glance = glance;
        _preview.Confirmed += (_, _) =>
        {
            var live = Interlocked.Exchange(ref _liveApply, null);
            var apply = Interlocked.Exchange(ref _pendingApply, null);
            Interlocked.Exchange(ref _pendingCancel, null);
            var key = Interlocked.Exchange(ref _pendingTrustKey, null);
            var after = _liveAfter;
            live?.Invoke(after);
            apply?.Invoke();
            _trust.RecordAccepted(key);
        };
        _preview.Cancelled += (_, _) =>
        {
            Interlocked.Exchange(ref _liveApply, null);
            Interlocked.Exchange(ref _pendingApply, null);
            var cancel = Interlocked.Exchange(ref _pendingCancel, null);
            var key = Interlocked.Exchange(ref _pendingTrustKey, null);
            _trust.RecordRejected(key);
            cancel?.Invoke();
        };
    }

    public bool RequireConfirmation { get; set; } = true;
    public bool IsPreviewVisible => _preview.IsVisible;

    public void RequestEdit(string before, string after, int x, int y, Action apply, Action? onCancel = null, string? trustKey = null)
    {
        if (apply == null)
        {
            return;
        }

        if (!RequireConfirmation)
        {
            apply();
            return;
        }

        var lowRisk = EditRisk.IsLowRisk(before, after);
        var trusted = _trust.IsTrusted(trustKey);
        if (lowRisk || trusted)
        {
            apply();
            _trust.RecordAccepted(trustKey);
            _glance?.ShowGlance("Edit applied · Ctrl+Shift+Z undoes this", x, y);
            return;
        }

        _liveApply = null;
        _pendingApply = apply;
        _pendingCancel = onCancel;
        _pendingTrustKey = trustKey;
        _preview.ShowPreview(before, after, x, y);
    }

    public void BeginLiveRewrite(string before, int x, int y, Action<string> apply, Action? onCancel = null, string? trustKey = null)
    {
        _pendingApply = null;
        _liveApply = apply;
        _liveAfter = string.Empty;
        _pendingCancel = onCancel;
        _pendingTrustKey = trustKey;
        _preview.ShowLivePreview(before, x, y);
    }

    public void UpdateLiveRewrite(string after, bool complete)
    {
        _liveAfter = after ?? string.Empty;
        if (!_preview.IsVisible)
        {
            return;
        }

        _preview.UpdateAfter(_liveAfter, complete);
        if (complete && !RequireConfirmation && !string.IsNullOrWhiteSpace(_liveAfter))
        {
            _preview.Confirm();
        }
    }

    public void DismissLiveRewrite()
    {
        Interlocked.Exchange(ref _liveApply, null);
        Interlocked.Exchange(ref _pendingApply, null);
        Interlocked.Exchange(ref _pendingCancel, null);
        Interlocked.Exchange(ref _pendingTrustKey, null);
        _preview.Hide();
    }

    public bool TryHandleKey(int virtualKey, bool shift, bool control, bool alt)
    {
        if (!_preview.IsVisible || shift || control || alt)
        {
            return false;
        }

        if (virtualKey == 13)
        {
            _preview.Confirm();
            return true;
        }

        if (virtualKey == 27)
        {
            _preview.Cancel();
            return true;
        }

        return false;
    }
}
