// MsgPackBuilderForm.cs
// Drop into a .NET 6+ WinForms project.
// Add NuGet package: MessagePack (by neuecc / Cysharp)
//   dotnet add package MessagePack
//
// Set as startup form in Program.cs:
//   Application.Run(new MsgPackBuilderForm());

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using MessagePack;

public class MsgPackBuilderForm : Form
{
    // ── State ────────────────────────────────────────────────────────────
    private readonly List<MsgPackField> _fields = new();

    // ── Controls ─────────────────────────────────────────────────────────
    private ComboBox   _typeCombo      = null!;
    private TextBox    _keyBox         = null!;
    private TextBox    _valueBox       = null!;
    private Button     _addBtn         = null!;
    private Button     _removeBtn      = null!;
    private Button     _buildBtn       = null!;
    private Button     _clearBtn       = null!;
    private ListView   _fieldList      = null!;
    private RichTextBox _hexOutput     = null!;
    private RichTextBox _previewOutput = null!;
    private Label      _statusLabel    = null!;

    // ── MsgPack types ────────────────────────────────────────────────────
    private static readonly string[] Types =
    {
        "nil", "bool", "int8", "int16", "int32", "int64",
        "uint8", "uint16", "uint32", "uint64",
        "float32", "float64",
        "str", "bin", "array_start", "map_start", "ext", "timestamp"
    };

    public MsgPackBuilderForm()
    {
        InitUI();
        WireEvents();
    }

    // ─────────────────────────────────────────────────────────────────────
    // UI Construction
    // ─────────────────────────────────────────────────────────────────────
    private void InitUI()
    {
        Text            = "MessagePack Builder";
        Size            = new Size(1000, 720);
        MinimumSize     = new Size(900, 620);
        Font            = new Font("Consolas", 9.5f);
        BackColor       = Color.FromArgb(30, 30, 30);
        ForeColor       = Color.FromArgb(220, 220, 220);

        // ── Left panel (input) ───────────────────────────────────────────
        var leftPanel = new Panel
        {
            Dock    = DockStyle.Left,
            Width   = 320,
            Padding = new Padding(10)
        };
        leftPanel.BackColor = Color.FromArgb(38, 38, 38);

        int y = 10;

        leftPanel.Controls.Add(Label("TYPE", 10, y)); y += 20;
        _typeCombo = new ComboBox
        {
            Left = 10, Top = y, Width = 290,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(50, 50, 50),
            ForeColor = Color.FromArgb(200, 230, 255),
            FlatStyle = FlatStyle.Flat
        };
        _typeCombo.Items.AddRange(Types);
        _typeCombo.SelectedIndex = 0;
        leftPanel.Controls.Add(_typeCombo); y += 32;

        leftPanel.Controls.Add(Label("KEY (optional — for map values)", 10, y)); y += 20;
        _keyBox = InputBox(10, y, 290);
        leftPanel.Controls.Add(_keyBox); y += 32;

        leftPanel.Controls.Add(Label("VALUE", 10, y)); y += 20;
        _valueBox = InputBox(10, y, 290, 60, true);
        leftPanel.Controls.Add(_valueBox); y += 72;

        // Hint label
        var hint = new Label
        {
            Left = 10, Top = y, Width = 290, Height = 110,
            Text =
                "Hints:\r\n" +
                "  nil        — no value needed\r\n" +
                "  bool       — true / false\r\n" +
                "  int*/uint* — integer\r\n" +
                "  float*     — decimal number\r\n" +
                "  str        — text\r\n" +
                "  bin        — hex bytes: 0A FF 3C\r\n" +
                "  ext        — typeId,hex: 5,DEADBEEF\r\n" +
                "  timestamp  — yyyy-MM-ddTHH:mm:ssZ\r\n" +
                "  array/map  — value = element count",
            ForeColor = Color.FromArgb(130, 130, 130),
            Font = new Font("Consolas", 8.5f)
        };
        leftPanel.Controls.Add(hint); y += 120;

        _addBtn = Btn("＋ Add Field", 10, y, 135, Color.FromArgb(40, 100, 60));
        _removeBtn = Btn("－ Remove", 155, y, 135, Color.FromArgb(100, 40, 40));
        leftPanel.Controls.Add(_addBtn);
        leftPanel.Controls.Add(_removeBtn);
        y += 40;

        _buildBtn = Btn("▶ Build Bytes", 10, y, 135, Color.FromArgb(30, 80, 140));
        _clearBtn = Btn("✕ Clear All",  155, y, 135, Color.FromArgb(70, 70, 40));
        leftPanel.Controls.Add(_buildBtn);
        leftPanel.Controls.Add(_clearBtn);

        // ── Right panel (field list + output) ───────────────────────────
        var rightPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
        rightPanel.BackColor = Color.FromArgb(30, 30, 30);

        _fieldList = new ListView
        {
            Left = 0, Top = 0, Width = rightPanel.Width - 20, Height = 220,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            View = View.Details,
            FullRowSelect = true,
            GridLines = false,
            BackColor = Color.FromArgb(24, 24, 24),
            ForeColor = Color.FromArgb(200, 220, 255),
            BorderStyle = BorderStyle.FixedSingle
        };
        _fieldList.Columns.Add("#",     40);
        _fieldList.Columns.Add("Type",  80);
        _fieldList.Columns.Add("Key",  110);
        _fieldList.Columns.Add("Value",300);
        rightPanel.Controls.Add(_fieldList);

        rightPanel.Controls.Add(Label("HEX OUTPUT", 0, 232));
        _hexOutput = new RichTextBox
        {
            Left = 0, Top = 250, Width = rightPanel.Width - 20, Height = 130,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Color.FromArgb(18, 18, 18),
            ForeColor = Color.FromArgb(100, 255, 150),
            Font = new Font("Consolas", 9f),
            ReadOnly = true,
            BorderStyle = BorderStyle.FixedSingle,
            WordWrap = true
        };
        rightPanel.Controls.Add(_hexOutput);

        rightPanel.Controls.Add(Label("STRUCTURED PREVIEW", 0, 392));
        _previewOutput = new RichTextBox
        {
            Left = 0, Top = 410,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            BackColor = Color.FromArgb(18, 18, 18),
            ForeColor = Color.FromArgb(220, 200, 120),
            Font = new Font("Consolas", 9f),
            ReadOnly = true,
            BorderStyle = BorderStyle.FixedSingle
        };
        rightPanel.Controls.Add(_previewOutput);

        _statusLabel = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(130, 200, 130),
            Padding = new Padding(4, 0, 0, 0)
        };

        // ── Layout ───────────────────────────────────────────────────────
        Controls.Add(rightPanel);
        Controls.Add(leftPanel);
        Controls.Add(_statusLabel);

        // Anchor right-panel resizable controls
        Resize += (_, _) =>
        {
            int w = rightPanel.Width - 20;
            _fieldList.Width      = w;
            _hexOutput.Width      = w;
            _previewOutput.Width  = w;
            _previewOutput.Height = rightPanel.Height - 410 - 10;
        };
    }

    // ─────────────────────────────────────────────────────────────────────
    // Events
    // ─────────────────────────────────────────────────────────────────────
    private void WireEvents()
    {
        _addBtn.Click    += AddField;
        _removeBtn.Click += RemoveField;
        _buildBtn.Click  += BuildBytes;
        _clearBtn.Click  += ClearAll;
        _typeCombo.SelectedIndexChanged += (_, _) => UpdateValueHint();
        UpdateValueHint();
    }

    private void AddField(object? s, EventArgs e)
    {
        var type  = _typeCombo.SelectedItem?.ToString() ?? "nil";
        var key   = _keyBox.Text.Trim();
        var value = _valueBox.Text.Trim();

        string? err = ValidateField(type, value);
        if (err != null) { Status(err, error: true); return; }

        var field = new MsgPackField(type, key, value);
        _fields.Add(field);

        var item = new ListViewItem((_fields.Count).ToString());
        item.SubItems.Add(type);
        item.SubItems.Add(key);
        item.SubItems.Add(value);
        item.ForeColor = TypeColor(type);
        _fieldList.Items.Add(item);

        _keyBox.Clear();
        _valueBox.Clear();
        Status($"Added [{type}] field #{_fields.Count}");
    }

    private void RemoveField(object? s, EventArgs e)
    {
        if (_fieldList.SelectedIndices.Count == 0) return;
        int idx = _fieldList.SelectedIndices[0];
        _fields.RemoveAt(idx);
        _fieldList.Items.RemoveAt(idx);
        // Renumber
        for (int i = 0; i < _fieldList.Items.Count; i++)
            _fieldList.Items[i].Text = (i + 1).ToString();
        Status("Field removed.");
    }

    private void BuildBytes(object? s, EventArgs e)
    {
        if (_fields.Count == 0) { Status("No fields to build.", error: true); return; }
        try
        {
            var writer = new MessagePackWriter(new System.Buffers.ArrayBufferWriter<byte>());
            var buf    = new System.Buffers.ArrayBufferWriter<byte>();
            writer     = new MessagePackWriter(buf);

            WriteFields(ref writer, _fields);
            writer.Flush();

            byte[] bytes = buf.WrittenSpan.ToArray();

            // Hex display
            _hexOutput.Text = FormatHex(bytes);

            // Structured preview
            _previewOutput.Text = BuildPreview(_fields);

            Status($"Built {bytes.Length} byte(s) across {_fields.Count} field(s). Copied to hex output.");
        }
        catch (Exception ex)
        {
            Status($"Build error: {ex.Message}", error: true);
        }
    }

    private void ClearAll(object? s, EventArgs e)
    {
        _fields.Clear();
        _fieldList.Items.Clear();
        _hexOutput.Clear();
        _previewOutput.Clear();
        Status("Cleared.");
    }

    // ─────────────────────────────────────────────────────────────────────
    // MessagePack writing
    // ─────────────────────────────────────────────────────────────────────
    private static void WriteFields(ref MessagePackWriter w, List<MsgPackField> fields)
    {
        foreach (var f in fields)
            WriteOne(ref w, f);
    }

    private static void WriteOne(ref MessagePackWriter w, MsgPackField f)
    {
        switch (f.Type)
        {
            case "nil":
                w.WriteNil();
                break;
            case "bool":
                w.Write(bool.Parse(f.Value));
                break;
            case "int8":
                w.WriteInt8(sbyte.Parse(f.Value));
                break;
            case "int16":
                w.WriteInt16(short.Parse(f.Value));
                break;
            case "int32":
                w.WriteInt32(int.Parse(f.Value));
                break;
            case "int64":
                w.WriteInt64(long.Parse(f.Value));
                break;
            case "uint8":
                w.WriteUInt8(byte.Parse(f.Value));
                break;
            case "uint16":
                w.WriteUInt16(ushort.Parse(f.Value));
                break;
            case "uint32":
                w.WriteUInt32(uint.Parse(f.Value));
                break;
            case "uint64":
                w.WriteUInt64(ulong.Parse(f.Value));
                break;
            case "float32":
                w.Write(float.Parse(f.Value));
                break;
            case "float64":
                w.Write(double.Parse(f.Value));
                break;
            case "str":
                w.Write(f.Value);
                break;
            case "bin":
                w.WriteBinHeader(ParseHexBytes(f.Value).Length);
                w.WriteRaw(ParseHexBytes(f.Value));
                break;
            case "array_start":
                w.WriteArrayHeader(int.Parse(f.Value));
                break;
            case "map_start":
                w.WriteMapHeader(int.Parse(f.Value));
                break;
            case "ext":
            {
                var (typeId, data) = ParseExt(f.Value);
                w.WriteExtensionFormat(new ExtensionResult((sbyte)typeId, data));
                break;
            }
            case "timestamp":
            {
                var dt = DateTime.Parse(f.Value, null,
                    System.Globalization.DateTimeStyles.RoundtripKind);
                MessagePackSerializer.Serialize(ref w, dt);
                break;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Validation
    // ─────────────────────────────────────────────────────────────────────
    private static string? ValidateField(string type, string value)
    {
        try
        {
            switch (type)
            {
                case "nil":   return null;
                case "bool":
                    if (!bool.TryParse(value, out _)) return "bool: enter true or false";
                    return null;
                case "int8":   sbyte.Parse(value);  return null;
                case "int16":  short.Parse(value);  return null;
                case "int32":  int.Parse(value);    return null;
                case "int64":  long.Parse(value);   return null;
                case "uint8":  byte.Parse(value);   return null;
                case "uint16": ushort.Parse(value); return null;
                case "uint32": uint.Parse(value);   return null;
                case "uint64": ulong.Parse(value);  return null;
                case "float32": float.Parse(value);  return null;
                case "float64": double.Parse(value); return null;
                case "str": return null;
                case "bin":
                    ParseHexBytes(value); return null;
                case "array_start":
                case "map_start":
                    if (!int.TryParse(value, out int n) || n < 0)
                        return $"{type}: value must be a non-negative integer count";
                    return null;
                case "ext":
                    ParseExt(value); return null;
                case "timestamp":
                    DateTime.Parse(value, null,
                        System.Globalization.DateTimeStyles.RoundtripKind);
                    return null;
            }
        }
        catch (Exception ex)
        {
            return $"{type}: {ex.Message}";
        }
        return null;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────
    private static byte[] ParseHexBytes(string hex)
    {
        var clean = Regex.Replace(hex, @"\s+", "");
        if (clean.Length % 2 != 0) throw new Exception("Odd hex digit count");
        var bytes = new byte[clean.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = Convert.ToByte(clean.Substring(i * 2, 2), 16);
        return bytes;
    }

    private static (int typeId, byte[] data) ParseExt(string value)
    {
        // Format: "typeId,hexbytes"  e.g. "5,DEADBEEF"
        var parts = value.Split(',', 2);
        if (parts.Length != 2) throw new Exception("ext format: typeId,hexbytes  e.g. 5,DEADBEEF");
        int id = int.Parse(parts[0].Trim());
        if (id < -128 || id > 127) throw new Exception("ext typeId must be -128..127");
        return (id, ParseHexBytes(parts[1].Trim()));
    }

    private static string FormatHex(byte[] bytes)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < bytes.Length; i++)
        {
            if (i > 0 && i % 16 == 0) sb.AppendLine();
            sb.Append($"{bytes[i]:X2} ");
        }
        return sb.ToString().TrimEnd();
    }

    private static string BuildPreview(List<MsgPackField> fields)
    {
        var sb = new StringBuilder();
        foreach (var f in fields)
        {
            string line = f.Key.Length > 0
                ? $"[{f.Type,-12}] key={f.Key,-20} value={f.Value}"
                : $"[{f.Type,-12}]                          value={f.Value}";
            sb.AppendLine(line);
        }
        sb.AppendLine();
        sb.AppendLine($"Total fields : {fields.Count}");
        sb.AppendLine($"Types used   : {string.Join(", ", fields.Select(f => f.Type).Distinct())}");
        return sb.ToString();
    }

    private void UpdateValueHint()
    {
        var t = _typeCombo.SelectedItem?.ToString() ?? "";
        _valueBox.PlaceholderText = t switch
        {
            "nil"         => "(no value needed)",
            "bool"        => "true or false",
            "bin"         => "hex bytes: 0A FF 3C ...",
            "ext"         => "typeId,hexbytes  e.g. 5,DEADBEEF",
            "timestamp"   => "2024-01-15T12:00:00Z",
            "array_start" => "element count (integer)",
            "map_start"   => "pair count (integer)",
            _             => "numeric value"
        };
    }

    private void Status(string msg, bool error = false)
    {
        _statusLabel.ForeColor = error
            ? Color.FromArgb(255, 100, 100)
            : Color.FromArgb(100, 220, 130);
        _statusLabel.Text = "  " + msg;
    }

    private static Color TypeColor(string type) => type switch
    {
        "nil"                    => Color.FromArgb(160, 160, 160),
        "bool"                   => Color.FromArgb(200, 160, 255),
        "str"                    => Color.FromArgb(150, 230, 150),
        "bin"                    => Color.FromArgb(230, 180, 100),
        "float32" or "float64"   => Color.FromArgb(100, 200, 255),
        "ext" or "timestamp"     => Color.FromArgb(255, 160, 160),
        "array_start"            => Color.FromArgb(255, 220, 100),
        "map_start"              => Color.FromArgb(255, 200, 150),
        _ /* int/uint */         => Color.FromArgb(180, 255, 200),
    };

    // ── Control factory helpers ──────────────────────────────────────────
    private Label Label(string text, int x, int y) => new Label
    {
        Text = text, Left = x, Top = y, AutoSize = true,
        ForeColor = Color.FromArgb(140, 140, 160),
        Font = new Font("Consolas", 7.5f, FontStyle.Bold)
    };

    private TextBox InputBox(int x, int y, int w, int h = 24, bool multiline = false) => new TextBox
    {
        Left = x, Top = y, Width = w, Height = h,
        Multiline = multiline,
        BackColor = Color.FromArgb(45, 45, 45),
        ForeColor = Color.FromArgb(220, 220, 220),
        BorderStyle = BorderStyle.FixedSingle
    };

    private Button Btn(string text, int x, int y, int w, Color back) => new Button
    {
        Text = text, Left = x, Top = y, Width = w, Height = 30,
        BackColor = back,
        ForeColor = Color.White,
        FlatStyle = FlatStyle.Flat,
        Cursor = Cursors.Hand,
        Font = new Font("Consolas", 9f, FontStyle.Bold)
    };
}

// ── Data model ───────────────────────────────────────────────────────────
public record MsgPackField(string Type, string Key, string Value);