using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Autodesk.AutoCAD.DatabaseServices;
using WinForms = System.Windows.Forms;
using YangCAD.AutoCADHost.Infrastructure;
using YangCAD.AutoCADHost.Toolbox;

namespace YangCAD.AutoCADHost.UI
{
    public partial class ToolboxParameterPanel : UserControl
    {
        private sealed class EditorEntry
        {
            public ToolboxParameterShape Shape { get; set; } = null!;
            public FrameworkElement Editor { get; set; } = null!;
        }

        private readonly List<EditorEntry> _editors = new List<EditorEntry>();

        public ToolboxParameterPanel()
        {
            InitializeComponent();
        }

        public void SetParameters(IEnumerable<ToolboxParameterShape>? shapes)
        {
            _editors.Clear();
            FieldsHost.Children.Clear();
            ValidationText.Visibility = System.Windows.Visibility.Collapsed;

            var list = (shapes ?? Array.Empty<ToolboxParameterShape>()).ToList();
            EmptyText.Visibility = list.Count == 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            foreach (var shape in list) AddField(shape);
        }

        public bool TryGetValues(out Dictionary<string, object?> values, out string error)
        {
            values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            var errors = new List<string>();
            foreach (var entry in _editors)
            {
                if (TryRead(entry, out var value, out var fieldError)) values[entry.Shape.Key] = value;
                else errors.Add(fieldError);
            }

            error = string.Join("\n", errors);
            ValidationText.Text = error;
            ValidationText.Visibility = errors.Count == 0 ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
            return errors.Count == 0;
        }

        public void ClearValidation()
        {
            ValidationText.Text = "";
            ValidationText.Visibility = System.Windows.Visibility.Collapsed;
        }

        private void AddField(ToolboxParameterShape shape)
        {
            var container = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            var title = new TextBlock
            {
                Text = shape.Label + (shape.Required ? "  *" : ""),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold
            };
            container.Children.Add(title);
            if (!string.IsNullOrWhiteSpace(shape.Description))
            {
                var description = new TextBlock
                {
                    Text = shape.Description,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 3, 0, 6)
                };
                description.SetResourceReference(StyleProperty, "MutedText");
                container.Children.Add(description);
            }

            FrameworkElement editor = CreateEditor(shape);
            container.Children.Add(editor);
            FieldsHost.Children.Add(container);
            _editors.Add(new EditorEntry { Shape = shape, Editor = editor });
        }

        private FrameworkElement CreateEditor(ToolboxParameterShape shape)
        {
            switch (shape.Kind)
            {
                case ToolboxParameterKind.Bool:
                    var check = new CheckBox { IsChecked = ToBool(shape.DefaultValue), Content = "启用" };
                    check.SetResourceReference(StyleProperty, "ElegantCheckBox");
                    return check;

                case ToolboxParameterKind.Choice:
                    var choice = NewComboBox(false);
                    foreach (var option in shape.Options ?? Array.Empty<string>()) choice.Items.Add(option);
                    if (shape.DefaultValue != null) choice.SelectedItem = Convert.ToString(shape.DefaultValue, CultureInfo.CurrentCulture);
                    if (choice.SelectedIndex < 0 && choice.Items.Count > 0 && shape.Required) choice.SelectedIndex = 0;
                    return choice;

                case ToolboxParameterKind.Layer:
                    var layer = NewComboBox(true);
                    foreach (var name in LoadLayerNames()) layer.Items.Add(name);
                    layer.Text = Convert.ToString(shape.DefaultValue, CultureInfo.CurrentCulture) ?? "";
                    return layer;

                case ToolboxParameterKind.File:
                    return CreatePathEditor(shape, allowFile: true, allowFolder: false);

                case ToolboxParameterKind.Folder:
                    return CreatePathEditor(shape, allowFile: false, allowFolder: true);

                case ToolboxParameterKind.FileOrFolder:
                    return CreatePathEditor(shape, allowFile: true, allowFolder: true);

                case ToolboxParameterKind.Text:
                case ToolboxParameterKind.Number:
                case ToolboxParameterKind.ObjectSelection:
                case ToolboxParameterKind.Point:
                default:
                    return NewTextBox(DefaultText(shape));
            }
        }


        private FrameworkElement CreatePathEditor(ToolboxParameterShape shape, bool allowFile, bool allowFolder)
        {
            var panel = new Grid();
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var box = NewTextBox(Convert.ToString(shape.DefaultValue, CultureInfo.CurrentCulture) ?? "");
            box.Tag = "Path";
            panel.Children.Add(box);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 0, 0, 0) };
            if (allowFile)
            {
                var fileButton = new Button { Content = allowFolder ? "选择 DWG…" : "浏览…", Tag = box };
                fileButton.SetResourceReference(StyleProperty, "SecondaryButton");
                fileButton.Click += BrowseFile;
                buttons.Children.Add(fileButton);
            }
            if (allowFolder)
            {
                var folderButton = new Button { Content = allowFile ? "选择文件夹…" : "浏览…", Tag = box, Margin = allowFile ? new Thickness(6, 0, 0, 0) : new Thickness(0) };
                folderButton.SetResourceReference(StyleProperty, "SecondaryButton");
                folderButton.Click += BrowseFolder;
                buttons.Children.Add(folderButton);
            }
            Grid.SetColumn(buttons, 1);
            panel.Children.Add(buttons);
            return panel;
        }

        private TextBox NewTextBox(string value)
        {
            var box = new TextBox { Text = value };
            box.SetResourceReference(StyleProperty, "SearchBox");
            return box;
        }

        private ComboBox NewComboBox(bool editable)
        {
            var combo = new ComboBox { IsEditable = editable, MinHeight = 34 };
            combo.SetResourceReference(StyleProperty, "ParameterComboBox");
            return combo;
        }

        private static string DefaultText(ToolboxParameterShape shape)
        {
            if (shape.DefaultValue == null) return "";
            if (shape.Kind == ToolboxParameterKind.Number && shape.DefaultValue is IFormattable f)
                return f.ToString(null, CultureInfo.InvariantCulture);
            return Convert.ToString(shape.DefaultValue, CultureInfo.CurrentCulture) ?? "";
        }

        private bool TryRead(EditorEntry entry, out object? value, out string error)
        {
            var shape = entry.Shape;
            value = null;
            error = "";

            switch (shape.Kind)
            {
                case ToolboxParameterKind.Bool:
                    value = ((CheckBox)entry.Editor).IsChecked == true;
                    return true;

                case ToolboxParameterKind.Choice:
                    var choice = (ComboBox)entry.Editor;
                    var selected = Convert.ToString(choice.SelectedItem ?? choice.Text, CultureInfo.CurrentCulture) ?? "";
                    if (shape.Required && string.IsNullOrWhiteSpace(selected)) return Fail(shape, "请选择一个值。", out error);
                    if (!string.IsNullOrWhiteSpace(selected) && (shape.Options?.Length ?? 0) > 0 && !shape.Options.Contains(selected))
                        return Fail(shape, "值不在允许选项中。", out error);
                    value = selected;
                    return true;

                case ToolboxParameterKind.Layer:
                    var layer = ((ComboBox)entry.Editor).Text.Trim();
                    if (shape.Required && layer.Length == 0) return Fail(shape, "请输入或选择图层。", out error);
                    value = layer;
                    return true;

                case ToolboxParameterKind.File:
                case ToolboxParameterKind.Folder:
                case ToolboxParameterKind.FileOrFolder:
                    var pathGrid = (Grid)entry.Editor;
                    var path = ((TextBox)pathGrid.Children[0]).Text.Trim();
                    if (shape.Required && path.Length == 0) return Fail(shape, "请选择路径。", out error);
                    if (path.Length > 0 && shape.Kind == ToolboxParameterKind.File && !File.Exists(Environment.ExpandEnvironmentVariables(path.Trim('"'))))
                        return Fail(shape, "文件不存在。", out error);
                    if (path.Length > 0 && shape.Kind == ToolboxParameterKind.FileOrFolder)
                    {
                        var expanded = Environment.ExpandEnvironmentVariables(path.Trim('"'));
                        if (!File.Exists(expanded) && !Directory.Exists(expanded)) return Fail(shape, "文件或文件夹不存在。", out error);
                    }
                    value = path;
                    return true;

                case ToolboxParameterKind.Number:
                    var numberText = ((TextBox)entry.Editor).Text.Trim();
                    if (numberText.Length == 0)
                    {
                        if (shape.Required) return Fail(shape, "请输入数值。", out error);
                        value = null;
                        return true;
                    }
                    if (!TryParseDouble(numberText, out var number)) return Fail(shape, "不是有效数值。", out error);
                    if (shape.Min.HasValue && number < shape.Min.Value) return Fail(shape, $"不能小于 {shape.Min.Value}。", out error);
                    if (shape.Max.HasValue && number > shape.Max.Value) return Fail(shape, $"不能大于 {shape.Max.Value}。", out error);
                    value = number;
                    return true;

                case ToolboxParameterKind.ObjectSelection:
                    var handlesText = ((TextBox)entry.Editor).Text.Trim();
                    var handles = SplitTokens(handlesText);
                    if (shape.Required && handles.Length == 0) return Fail(shape, "请输入至少一个对象 Handle。", out error);
                    value = handles;
                    return true;

                case ToolboxParameterKind.Point:
                    var pointText = ((TextBox)entry.Editor).Text.Trim();
                    if (pointText.Length == 0)
                    {
                        if (shape.Required) return Fail(shape, "请输入点坐标。", out error);
                        value = null;
                        return true;
                    }
                    if (!TryParsePoint(pointText, out var point)) return Fail(shape, "点格式应为 X,Y 或 X,Y,Z。", out error);
                    value = point;
                    return true;

                default:
                    var text = ((TextBox)entry.Editor).Text.Trim();
                    if (shape.Required && text.Length == 0) return Fail(shape, "不能为空。", out error);
                    value = text;
                    return true;
            }
        }

        private static bool Fail(ToolboxParameterShape shape, string message, out string error)
        {
            error = shape.Label + "：" + message;
            return false;
        }

        private static bool TryParseDouble(string text, out double value)
        {
            return double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
                   double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryParsePoint(string text, out double[] point)
        {
            point = Array.Empty<double>();
            var cleaned = text.Trim().Trim('(', ')', '[', ']');
            var parts = cleaned.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 && parts.Length != 3) return false;
            var values = new double[3];
            for (var i = 0; i < parts.Length; i++)
                if (!TryParseDouble(parts[i], out values[i])) return false;
            point = values;
            return true;
        }

        private static string[] SplitTokens(string text)
            => string.IsNullOrWhiteSpace(text)
                ? Array.Empty<string>()
                : text.Split(new[] { ',', ';', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        private static bool ToBool(object? value)
        {
            if (value is bool b) return b;
            return value != null && bool.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var parsed) && parsed;
        }

        private static string[] LoadLayerNames()
        {
            try
            {
                var doc = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return Array.Empty<string>();
                return CadTransactionRunner.ReadContext(doc, ctx =>
                {
                    var table = (LayerTable)ctx.Transaction.GetObject(ctx.Database.LayerTableId, OpenMode.ForRead);
                    var names = new List<string>();
                    foreach (ObjectId id in table)
                    {
                        var record = (LayerTableRecord)ctx.Transaction.GetObject(id, OpenMode.ForRead);
                        names.Add(record.Name);
                    }
                    return names.OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToArray();
                });
            }
            catch (Exception ex)
            {
                RuntimeLogger.Error("Toolbox layer parameter load failed.", ex);
                return Array.Empty<string>();
            }
        }

        private static void BrowseFile(object sender, RoutedEventArgs e)
        {
            if (!((sender as Button)?.Tag is TextBox target)) return;
            var dialog = new OpenFileDialog
            {
                CheckFileExists = true,
                Multiselect = false,
                Filter = "AutoCAD 图纸 (*.dwg)|*.dwg|所有文件 (*.*)|*.*"
            };
            if (dialog.ShowDialog() == true) target.Text = dialog.FileName;
        }

        private static void BrowseFolder(object sender, RoutedEventArgs e)
        {
            if (!((sender as Button)?.Tag is TextBox target)) return;
            using (var dialog = new WinForms.FolderBrowserDialog
            {
                Description = "选择文件夹",
                ShowNewFolderButton = true
            })
            {
                var current = Environment.ExpandEnvironmentVariables((target.Text ?? "").Trim().Trim('"'));
                if (Directory.Exists(current)) dialog.SelectedPath = current;
                if (dialog.ShowDialog() == WinForms.DialogResult.OK) target.Text = dialog.SelectedPath;
            }
        }
    }
}
