namespace Lexon.SettingsModel;

/// <summary>
/// When an edit touches a list another open page has already changed,
/// flush that earlier page first and reload the clean pages so both edits
/// are applied in order instead of one being dropped at the combined flush.
/// </summary>
public sealed class OverlappingEditGuard
{
    private readonly IReadOnlyList<IOwnedSettingsPage> _pages;
    private readonly Action<IReadOnlyList<IOwnedSettingsPage>> _flushEarlier;
    private readonly Action<IOwnedSettingsPage> _reload;
    private bool _busy;

    public OverlappingEditGuard(
        IReadOnlyList<IOwnedSettingsPage> pages,
        Action<IReadOnlyList<IOwnedSettingsPage>> flushEarlier,
        Action<IOwnedSettingsPage> reload)
    {
        _pages = pages ?? throw new ArgumentNullException(nameof(pages));
        _flushEarlier = flushEarlier ?? throw new ArgumentNullException(nameof(flushEarlier));
        _reload = reload ?? throw new ArgumentNullException(nameof(reload));
    }

    public void BeforeEdit(IOwnedSettingsPage editor)
    {
        if (_busy)
        {
            return;
        }

        var earlier = _pages
            .Where(page => !ReferenceEquals(page, editor) && page.IsDirty && Shares(editor, page))
            .ToArray();
        if (earlier.Length == 0)
        {
            return;
        }

        _busy = true;
        try
        {
            _flushEarlier(earlier);
            foreach (var page in _pages)
            {
                if (page.IsDirty)
                {
                    continue;
                }

                if (ReferenceEquals(page, editor) || Shares(editor, page))
                {
                    _reload(page);
                }
            }
        }
        finally
        {
            _busy = false;
        }
    }

    public static bool Shares(IOwnedSettingsPage left, IOwnedSettingsPage right)
        => left.OwnedKeys.Intersect(right.OwnedKeys, StringComparer.Ordinal).Any();
}
