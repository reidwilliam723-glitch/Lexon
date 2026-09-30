using Lexon.Core;
using Lexon.Input.Interfaces;
using System.Runtime.InteropServices;

namespace Lexon.Input;

/// <summary>
/// Marker to identify Lexon's own synthetic input, so KeyboardListener can filter it out.
/// This prevents feedback loops where our injected keystrokes get re-processed.
/// </summary>
internal static class InputMarkers
{
    internal const uint LexonInjectedMarker = 0x4C465753; // "LFWS" in hex
}

/// <summary>
/// Injects text into the focused application using SendInput
/// </summary>
public class TextInjector : ITextInjector
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll")]
    private static extern IntPtr GetFocus();

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_UNICODE = 0x0004;
    private const int VK_BACK = 0x08;
    private const int VK_DELETE = 0x2E;
    private const int VK_SHIFT = 0x10;
    private const int VK_CONTROL = 0x11;
    private const int VK_LEFT = 0x25;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MOUSEINPUT mi;
        [FieldOffset(0)]
        public KEYBDINPUT ki;
        [FieldOffset(0)]
        public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    public void InjectText(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        DiagnosticLog.Write($"InjectText: sending {DiagnosticLog.Escape(text)}");

        // Primary method: character-by-character SendInput with KEYEVENTF_UNICODE
        // This avoids clipboard race conditions and doesn't interfere with user's clipboard
        var inputs = new List<INPUT>();

        foreach (char c in text)
        {
            // Key down with Unicode
            var input = new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = c,
                        dwFlags = KEYEVENTF_UNICODE,
                        time = 0,
                        dwExtraInfo = new IntPtr(InputMarkers.LexonInjectedMarker)
                    }
                }
            };
            inputs.Add(input);

            // Key up with Unicode
            var inputUp = new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = c,
                        dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = new IntPtr(InputMarkers.LexonInjectedMarker)
                    }
                }
            };
            inputs.Add(inputUp);
        }

        var sent = SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<INPUT>());
        if (sent != inputs.Count)
        {
            var errorCode = Marshal.GetLastWin32Error();
            DiagnosticLog.Write($"InjectText: SendInput BLOCKED OR PARTIAL — requested={inputs.Count} actual={sent} Win32Error={errorCode}. This usually means the input was rejected by Windows (e.g. UIPI blocking a lower-privilege process from injecting into a higher-privilege window).");
        }
        else
        {
            DiagnosticLog.Write($"InjectText: SendInput accepted all {sent} events");
        }
    }

    public void DeleteBackward(int count)
    {
        if (count <= 0) return;

        DiagnosticLog.Write($"DeleteBackward: count={count}");

        var inputs = new List<INPUT>();

        for (int i = 0; i < count; i++)
        {
            // Press backspace
            inputs.Add(new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = VK_BACK,
                        wScan = 0,
                        dwFlags = 0,
                        time = 0,
                        dwExtraInfo = new IntPtr(InputMarkers.LexonInjectedMarker)
                    }
                }
            });

            // Release backspace
            inputs.Add(new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = VK_BACK,
                        wScan = 0,
                        dwFlags = KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = new IntPtr(InputMarkers.LexonInjectedMarker)
                    }
                }
            });
        }

        var sent = SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<INPUT>());
        if (sent != inputs.Count)
        {
            var errorCode = Marshal.GetLastWin32Error();
            DiagnosticLog.Write($"DeleteBackward: SendInput BLOCKED OR PARTIAL — requested={inputs.Count} actual={sent} Win32Error={errorCode}");
        }
        else
        {
            DiagnosticLog.Write($"DeleteBackward: SendInput accepted all {sent} events");
        }
    }

    public void SelectBackwardWords(int wordCount)
    {
        if (wordCount <= 0)
        {
            return;
        }

        DiagnosticLog.Write($"SelectBackwardWords: count={wordCount}");
        var inputs = new List<INPUT>
        {
            Key(VK_CONTROL, down: true),
            Key(VK_SHIFT, down: true)
        };
        for (var i = 0; i < wordCount; i++)
        {
            inputs.Add(Key(VK_LEFT, down: true, extended: true));
            inputs.Add(Key(VK_LEFT, down: false, extended: true));
        }

        inputs.Add(Key(VK_SHIFT, down: false));
        inputs.Add(Key(VK_CONTROL, down: false));
        SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<INPUT>());
        Thread.Sleep(40);
    }

    private static INPUT Key(int vk, bool down, bool extended = false)
    {
        var flags = down ? 0u : KEYEVENTF_KEYUP;
        if (extended)
        {
            flags |= KEYEVENTF_EXTENDEDKEY;
        }

        return new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = (ushort)vk,
                    wScan = 0,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = new IntPtr(InputMarkers.LexonInjectedMarker)
                }
            }
        };
    }

    public void ReplaceText(string oldText, string newText, bool selectionStillActive = false)
    {
        if (string.IsNullOrEmpty(oldText) && string.IsNullOrEmpty(newText)) return;

        var deletes = TextReplacement.BackspacesForReplace(oldText, selectionStillActive);
        if (deletes > 0)
        {
            DeleteBackward(deletes);
        }

        if (!string.IsNullOrEmpty(newText))
        {
            InjectText(newText);
        }
    }

    public void ReplaceSelection(string newText)
    {
        DeleteBackward(1);
        if (!string.IsNullOrEmpty(newText))
        {
            InjectText(newText);
        }
    }

    public void InjectTextViaClipboard(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        // Save current clipboard content
        string? originalClipboard = null;
        try
        {
            originalClipboard = System.Windows.Forms.Clipboard.GetText();
        }
        catch
        {
            // Clipboard might not be accessible
        }

        try
        {
            // Set new text to clipboard
            System.Windows.Forms.Clipboard.SetText(text);

            // Small delay to ensure clipboard is ready
            System.Threading.Thread.Sleep(10);

            // Send Ctrl+V to paste (marked as synthetic input)
            var inputs = new List<INPUT>();

            // Press Ctrl
            inputs.Add(new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = 0x11, // VK_CONTROL
                        wScan = 0,
                        dwFlags = 0,
                        time = 0,
                        dwExtraInfo = new IntPtr(InputMarkers.LexonInjectedMarker)
                    }
                }
            });

            // Press V
            inputs.Add(new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = 0x56, // VK_V
                        wScan = 0,
                        dwFlags = 0,
                        time = 0,
                        dwExtraInfo = new IntPtr(InputMarkers.LexonInjectedMarker)
                    }
                }
            });

            // Release V
            inputs.Add(new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = 0x56, // VK_V
                        wScan = 0,
                        dwFlags = KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = new IntPtr(InputMarkers.LexonInjectedMarker)
                    }
                }
            });

            // Release Ctrl
            inputs.Add(new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = 0x11, // VK_CONTROL
                        wScan = 0,
                        dwFlags = KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = new IntPtr(InputMarkers.LexonInjectedMarker)
                    }
                }
            });

            SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<INPUT>());

            // Wait a bit for paste to complete
            System.Threading.Thread.Sleep(100);

            // Restore original clipboard
            if (!string.IsNullOrEmpty(originalClipboard))
            {
                System.Windows.Forms.Clipboard.SetText(originalClipboard);
            }
            else
            {
                try
                {
                    System.Windows.Forms.Clipboard.Clear();
                }
                catch
                {
                    // Clear might fail, that's okay
                }
            }
        }
        catch
        {
            // Restore original clipboard on error
            if (!string.IsNullOrEmpty(originalClipboard))
            {
                try
                {
                    System.Windows.Forms.Clipboard.SetText(originalClipboard);
                }
                catch
                {
                    // Restore might fail, that's okay
                }
            }
        }
    }

    public void Flush()
    {
        // No-op - not needed with character-by-character SendInput
        // Kept for interface compatibility
    }
}
