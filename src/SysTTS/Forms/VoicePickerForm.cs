using System.Windows.Forms;
using SysTTS.Models;

namespace SysTTS.Forms;

/// <summary>
/// Compact borderless WinForms popup for voice selection.
///
/// Displays available voices in a ListBox, positioned near the cursor,
/// pre-selects the last-used voice, and dismisses on voice selection, Escape, or click-away.
/// </summary>
public partial class VoicePickerForm : Form
{
    private const int PopupWidth = 200;
    private const int PopupHeight = 300;
    private const int CursorOffsetX = -10;
    private const int CursorOffsetY = -10;

    private readonly ListBox _voiceListBox;
    private readonly string? _lastUsedVoiceId;
    private bool _isReady;

    public string? SelectedVoiceId { get; private set; }

    /// <summary>
    /// Creates a new VoicePickerForm.
    /// </summary>
    /// <param name="voices">List of available voices to display</param>
    /// <param name="lastUsedVoiceId">Voice ID to pre-select (if it exists in the list)</param>
    public VoicePickerForm(IReadOnlyList<VoiceInfo> voices, string? lastUsedVoiceId)
    {
        _lastUsedVoiceId = lastUsedVoiceId;

        // Configure form properties
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(PopupWidth, PopupHeight);
        BackColor = SystemColors.Window;
        ForeColor = SystemColors.WindowText;

        // Create and configure ListBox
        _voiceListBox = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
            SelectionMode = SelectionMode.One
        };

        // Populate ListBox with voice names, storing voice ID as tag
        foreach (var voice in voices)
        {
            var index = _voiceListBox.Items.Add(new VoiceListItem(voice.Id, voice.Name));

            // Pre-select the last-used voice if it matches
            if (voice.Id == lastUsedVoiceId)
            {
                _voiceListBox.SelectedIndex = index;
            }
        }

        // If no pre-selection yet, select the first voice
        if (_voiceListBox.SelectedIndex < 0 && _voiceListBox.Items.Count > 0)
        {
            _voiceListBox.SelectedIndex = 0;
        }

        // Add ListBox to form
        Controls.Add(_voiceListBox);

        // Wire up event handlers
        _voiceListBox.DoubleClick += VoiceListBox_DoubleClick;
        _voiceListBox.KeyDown += VoiceListBox_KeyDown;
        KeyDown += VoicePickerForm_KeyDown;
        Deactivate += VoicePickerForm_Deactivate;
        Shown += VoicePickerForm_Shown;

        // Position near cursor
        PositionNearCursor();
    }

    /// <summary>
    /// Positions the form near the cursor, clamped to screen working area.
    /// </summary>
    private void PositionNearCursor()
    {
        var cursorPos = Cursor.Position;
        var screen = Screen.FromPoint(cursorPos);
        var workingArea = screen.WorkingArea;

        // Calculate initial position (cursor offset)
        int left = cursorPos.X + CursorOffsetX;
        int top = cursorPos.Y + CursorOffsetY;

        // Clamp to working area
        if (left < workingArea.Left)
            left = workingArea.Left;
        if (left + Width > workingArea.Right)
            left = workingArea.Right - Width;

        if (top < workingArea.Top)
            top = workingArea.Top;
        if (top + Height > workingArea.Bottom)
            top = workingArea.Bottom - Height;

        Location = new Point(left, top);
    }

    /// <summary>
    /// Handles double-click on a voice in the ListBox.
    /// </summary>
    private void VoiceListBox_DoubleClick(object? sender, EventArgs e)
    {
        SelectCurrentVoice();
    }

    /// <summary>
    /// Handles KeyDown on the ListBox.
    /// </summary>
    private void VoiceListBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Return)
        {
            e.Handled = true;
            SelectCurrentVoice();
        }
        else if (e.KeyCode == Keys.Escape)
        {
            e.Handled = true;
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }

    /// <summary>
    /// Handles KeyDown on the form itself (for Escape at the form level).
    /// </summary>
    private void VoicePickerForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            e.Handled = true;
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }

    /// <summary>
    /// Handles form deactivation (click outside).
    /// Only close if the form has been fully shown (_isReady is true) to avoid
    /// closing the form prematurely during initialization when other windows may take focus.
    /// </summary>
    private void VoicePickerForm_Deactivate(object? sender, EventArgs e)
    {
        // Only dismiss if the form is ready AND no explicit result was already set
        // (e.g., by SelectCurrentVoice setting DialogResult.OK before Close()).
        // Without this check, Deactivate fires during Close() and overwrites OK → Cancel.
        if (_isReady && DialogResult == DialogResult.None)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }

    /// <summary>
    /// Handles the Shown event to mark the form as ready for deactivation handling.
    /// </summary>
    private void VoicePickerForm_Shown(object? sender, EventArgs e)
    {
        _isReady = true;
    }

    /// <summary>
    /// Selects the currently highlighted voice and closes the form.
    /// </summary>
    private void SelectCurrentVoice()
    {
        if (_voiceListBox.SelectedItem is VoiceListItem item)
        {
            SelectedVoiceId = item.VoiceId;
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    /// <summary>
    /// Helper class to pair voice ID with display name in ListBox.
    /// </summary>
    private class VoiceListItem
    {
        public string VoiceId { get; }
        public string DisplayName { get; }

        public VoiceListItem(string voiceId, string displayName)
        {
            VoiceId = voiceId;
            DisplayName = displayName;
        }

        public override string ToString() => DisplayName;
    }
}
