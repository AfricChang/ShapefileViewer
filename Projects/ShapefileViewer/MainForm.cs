using DotSpatial.Controls;
using DotSpatial.Data;
using DotSpatial.Symbology;
using System;
using System.Data;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ShapefileViewer
{
    public class MainForm : Form
    {
        // Members
        private Map m_Map;
        private PropertyGrid m_PropertyGrid;
        private StatusStrip m_StatusStrip;
        private ToolStripStatusLabel m_StatusLabel;
        private MenuStrip m_Menu;
        private ToolStrip m_Toolbar;

        public MainForm()
        {
            InitializeComponents();
        }

        // Initialize UI components and layout
        private void InitializeComponents()
        {
            Text = "Shapefile Viewer";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(900, 600);

            m_Menu = BuildMenu();
            m_Toolbar = BuildToolbar();

            m_Map = new Map
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };

            m_PropertyGrid = new PropertyGrid
            {
                Dock = DockStyle.Fill,
                ToolbarVisible = false,
                HelpVisible = false
            };

            var rightPanel = new Panel
            {
                Dock = DockStyle.Right,
                Width = 360
            };
            rightPanel.Controls.Add(m_PropertyGrid);

            var container = new Panel
            {
                Dock = DockStyle.Fill
            };
            container.Controls.Add(m_Map);
            container.Controls.Add(rightPanel);

            m_StatusStrip = new StatusStrip();
            m_StatusLabel = new ToolStripStatusLabel
            {
                Text = "Ready"
            };
            m_StatusStrip.Items.Add(m_StatusLabel);

            Controls.Add(container);
            Controls.Add(m_Toolbar);
            Controls.Add(m_Menu);
            Controls.Add(m_StatusStrip);

            m_Map.FunctionMode = FunctionMode.Pan;
            m_Map.GeoMouseMove += OnMapMouseMove;
            m_Map.SelectionChanged += MapSelectionChanged;
            //m_Map.MouseUp += M_Map_MouseUp;

            KeyPreview = true;
            KeyDown += OnMainFormKeyDown;
        }

        private MenuStrip BuildMenu()
        {
            var menu = new MenuStrip();

            var file = new ToolStripMenuItem("File");
            var open = new ToolStripMenuItem("Open Shapefile", null, OnOpenShapefileClicked)
            {
                ShortcutKeys = Keys.Control | Keys.O
            };
            var exit = new ToolStripMenuItem("Exit", null, (s, e) => Close());
            file.DropDownItems.Add(open);
            file.DropDownItems.Add(new ToolStripSeparator());
            file.DropDownItems.Add(exit);

            var view = new ToolStripMenuItem("View");
            var full = new ToolStripMenuItem("Full Extent", null, (s, e) => ZoomToFullExtent())
            {
                ShortcutKeys = Keys.Control | Keys.F
            };
            var pan = new ToolStripMenuItem("Pan", null, (s, e) =>
            {
                m_Map.FunctionMode = FunctionMode.Pan;
            });
            var zoomIn = new ToolStripMenuItem("Zoom In", null, (s, e) =>
            {
                m_Map.FunctionMode = FunctionMode.ZoomIn;
            });
            var zoomOut = new ToolStripMenuItem("Zoom Out", null, (s, e) =>
            {
                m_Map.FunctionMode = FunctionMode.ZoomOut;
            });
            var select = new ToolStripMenuItem("Select", null, (s, e) => SetSelectMode(true));
            view.DropDownItems.Add(full);
            view.DropDownItems.Add(new ToolStripSeparator());
            view.DropDownItems.Add(pan);
            view.DropDownItems.Add(zoomIn);
            view.DropDownItems.Add(zoomOut);
            view.DropDownItems.Add(select);

            menu.Items.Add(file);
            menu.Items.Add(view);

            return menu;
        }

        private void SetSelectMode(bool enable)
        {
            m_Map.FunctionMode = enable ? FunctionMode.Select : FunctionMode.None;
        }

        private ToolStrip BuildToolbar()
        {
            var tb = new ToolStrip
            {
                GripStyle = ToolStripGripStyle.Hidden,
                ImageScalingSize = new Size(20, 20)
            };

            tb.Items.Add(new ToolStripButton("Open", null, OnOpenShapefileClicked));
            tb.Items.Add(new ToolStripSeparator());
            tb.Items.Add(new ToolStripButton("Pan", null, (s, e) =>
            {
                m_Map.FunctionMode = FunctionMode.Pan;
            }));
            tb.Items.Add(new ToolStripButton("Zoom In", null, (s, e) =>
            {
                m_Map.FunctionMode = FunctionMode.ZoomIn;
            }));
            tb.Items.Add(new ToolStripButton("Zoom Out", null, (s, e) =>
            {
                m_Map.FunctionMode = FunctionMode.ZoomOut;
            }));
            tb.Items.Add(new ToolStripButton("Full", null, (s, e) => ZoomToFullExtent()));
            tb.Items.Add(new ToolStripButton("Select", null, (s, e) => SetSelectMode(true)));

            return tb;
        }

        private void OnOpenShapefileClicked(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "Shapefile (*.shp)|*.shp";
                ofd.Title = "Open Shapefile";
                ofd.Multiselect = false;
                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        var path = ofd.FileName;

                        // Check sibling files (.shx and .dbf)
                        var shxPath = Path.ChangeExtension(path, ".shx");
                        var dbfPath = Path.ChangeExtension(path, ".dbf");
                        if (!File.Exists(shxPath))
                        {
                            ShowWarn("Index file (.shx) is missing. Performance or selection may be affected.");
                        }
                        if (!File.Exists(dbfPath))
                        {
                            ShowWarn("Attribute file (.dbf) is missing. Attributes may not be available.");
                        }

                        var fs = Utils.ShapefileLoader.Load(path);
                        if (fs == null)
                        {
                            MessageBox.Show(this, "Failed to open shapefile.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }

                        // Post-load validations
                        if (fs.Projection == null)
                        {
                            ShowWarn("Projection (.prj) is missing or unknown.");
                        }

                        if (fs.DataTable == null || fs.DataTable.Rows.Count == 0)
                        {
                            ShowInfo("No attributes found in DBF.");
                        }

                        if (fs.Projection != null)
                        {
                            m_Map.Projection = fs.Projection;
                        }

                        var layer = m_Map.Layers.Add(fs) as IMapFeatureLayer;
                        if (layer != null)
                        {
                            layer.IsVisible = true;

                            // Ensure base symbolizer is visible
                            var ftVis = layer.DataSet?.FeatureType ?? FeatureType.Unspecified;
                            switch (ftVis)
                            {
                                case FeatureType.Point:
                                case FeatureType.MultiPoint:
                                    {
                                        layer.Symbolizer = new PointSymbolizer(Color.Red, DotSpatial.Symbology.PointShape.Ellipse, 6);
                                        break;
                                    }
                                case FeatureType.Line:
                                    {
                                        layer.Symbolizer = new LineSymbolizer(Color.Blue, 2);
                                        break;
                                    }
                                case FeatureType.Polygon:
                                    {
                                        var outline = new LineSymbolizer(Color.ForestGreen, 2);
                                        var fill = new PolygonSymbolizer(Color.FromArgb(80, Color.LightGreen));
                                        fill.OutlineSymbolizer = outline;
                                        layer.Symbolizer = fill;
                                        break;
                                    }
                                default:
                                    {
                                        layer.Symbolizer = new LineSymbolizer(Color.DarkGray, 2);
                                        break;
                                    }
                            }

                            if (layer.Selection != null)
                            {
                                layer.Selection.Clear();
                            }

                            // Fallback zoom to layer extent to ensure view jumps to data
                            var extent = layer.Extent;
                            if (extent != null)
                            {
                                m_Map.ViewExtents = extent;
                            }
                        }

                        ApplySelectionStyle(layer as IFeatureLayer);
                        ZoomToFullExtent();
                        UpdateZoomStatus();

                        // Force redraw to avoid blank view
                        m_Map.ResetBuffer();
                        m_Map.Refresh();
                        m_Map.Invalidate();

                        m_StatusLabel.Text = $"Loaded: {Path.GetFileName(path)}";
                        m_Map.FunctionMode = FunctionMode.Select;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, $"Open failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void ZoomToFullExtent()
        {
            try
            {
                m_Map.ZoomToMaxExtent();
                UpdateZoomStatus();
            }
            catch
            {
                // ignore
            }
        }

        private void OnMapMouseMove(object sender, GeoMouseArgs e)
        {
            var coordText = $"X: {e.GeographicLocation.X:F6}, Y: {e.GeographicLocation.Y:F6}";
            var zoomText = GetZoomText();
            m_StatusLabel.Text = string.IsNullOrEmpty(zoomText) ? coordText : $"{coordText} | {zoomText}";
        }

        private void MapSelectionChanged(object sender, EventArgs e)
        {
            try
            {
                IFeature? first = null;
                int total = 0;

                foreach (var l in m_Map.Layers)
                {
                    var fl = l as IMapFeatureLayer;
                    if (fl == null)
                    {
                        continue;
                    }

                    var count = fl.Selection.Count;
                    if (count > 0)
                    {
                        total += count;
                        var list = fl.Selection.ToFeatureList();
                        if (first == null && list != null && list.Count > 0)
                        {
                            first = list[0];
                        }
                    }
                }

                if (total > 0)
                {
                    if (first != null)
                    {
                        LoadAttributes(first);
                    }
                    m_StatusLabel.Text = $"Selected: {total}";
                }
                else
                {
                    LoadAttributes(null);
                    UpdateZoomStatus();
                }
            }
            catch
            {
                LoadAttributes(null);
                UpdateZoomStatus();
            }
        }

        private void LoadAttributes(in IFeature feature)
        {
            if (feature == null)
            {
                if (m_PropertyGrid != null)
                {
                    m_PropertyGrid.SelectedObject = null;
                    m_PropertyGrid.Refresh();
                }
                return;
            }

            var row = feature.DataRow;
            if (row == null || row.Table == null)
            {
                var fallback = new System.Collections.Generic.Dictionary<string, object>();
                fallback["FeatureType"] = feature.FeatureType.ToString();
                if (m_PropertyGrid != null)
                {
                    m_PropertyGrid.SelectedObject = new DictionaryPropertyGridAdapter(fallback);
                    m_PropertyGrid.Refresh();
                }
                return;
            }

            var dict = new System.Collections.Generic.Dictionary<string, object>();
            var cols = row.Table.Columns;
            for (int i = 0; i < cols.Count; i++)
            {
                var col = cols[i];
                object val = row[col.ColumnName];
                dict[col.ColumnName] = val ?? string.Empty;
            }

            if (m_PropertyGrid != null)
            {
                m_PropertyGrid.SelectedObject = new DictionaryPropertyGridAdapter(dict);
                m_PropertyGrid.Refresh();
            }
        }

        private void ApplySelectionStyle(in IFeatureLayer layer)
        {
            if (layer == null)
            {
                return;
            }

            var ft = layer?.DataSet?.FeatureType ?? FeatureType.Unspecified;

            switch (ft)
            {
                case FeatureType.Point:
                case FeatureType.MultiPoint:
                    {
                        var sym = new PointSymbolizer(Color.LimeGreen, DotSpatial.Symbology.PointShape.Ellipse, 10);
                        layer.SelectionSymbolizer = sym;
                        break;
                    }
                case FeatureType.Line:
                    {
                        var sym = new LineSymbolizer(Color.OrangeRed, 3);
                        layer.SelectionSymbolizer = sym;
                        break;
                    }
                case FeatureType.Polygon:
                    {
                        var outline = new LineSymbolizer(Color.DodgerBlue, 3);
                        var fill = new PolygonSymbolizer(Color.FromArgb(64, Color.DodgerBlue));
                        fill.OutlineSymbolizer = outline;
                        layer.SelectionSymbolizer = fill;
                        break;
                    }
                default:
                    {
                        var sym = new LineSymbolizer(Color.Gold, 2);
                        layer.SelectionSymbolizer = sym;
                        break;
                    }
            }
        }

        private void ClearSelection()
        {
            try
            {
                foreach (var l in m_Map.Layers)
                {
                    var fl = l as IMapFeatureLayer;
                    if (fl != null)
                    {
                        fl.Selection.Clear();
                    }
                }
                LoadAttributes(null);
                UpdateZoomStatus();
                m_Map.Invalidate();
            }
            catch
            {
                LoadAttributes(null);
            }
        }

        private void OnMainFormKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.O)
            {
                OnOpenShapefileClicked(this, EventArgs.Empty);
                e.Handled = true;
                return;
            }

            if (!e.Control && e.KeyCode == Keys.F)
            {
                ZoomToFullExtent();
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.Escape)
            {
                ClearSelection();
                e.Handled = true;
                return;
            }
        }

        private string GetZoomText()
        {
            try
            {
                if (m_Map.MapFrame != null && m_Map.MapFrame.ViewExtents != null)
                {
                    var w = m_Map.MapFrame.ViewExtents.Width;
                    return $"Extent Width: {w:F2}";
                }
            }
            catch
            {
            }
            return string.Empty;
        }

        private void UpdateZoomStatus()
        {
            var zoomText = GetZoomText();
            if (!string.IsNullOrEmpty(zoomText))
            {
                m_StatusLabel.Text = zoomText;
            }
        }

        private void ShowInfo(string message)
        {
            try
            {
                MessageBox.Show(this, message, "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch
            {
            }
        }

        private void ShowWarn(string message)
        {
            try
            {
                MessageBox.Show(this, message, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch
            {
            }
        }

        internal class DictionaryPropertyGridAdapter : System.ComponentModel.ICustomTypeDescriptor
        {
            private readonly System.Collections.Generic.IDictionary<string, object> m_Data;

            public DictionaryPropertyGridAdapter(System.Collections.Generic.IDictionary<string, object> data)
            {
                m_Data = data;
            }

            public System.ComponentModel.AttributeCollection GetAttributes()
            {
                return System.ComponentModel.AttributeCollection.Empty;
            }

            public string GetClassName()
            {
                return "Attributes";
            }

            public string GetComponentName()
            {
                return "Attributes";
            }

            public System.ComponentModel.TypeConverter GetConverter()
            {
                return new System.ComponentModel.TypeConverter();
            }

            public System.ComponentModel.EventDescriptor GetDefaultEvent()
            {
                return null;
            }

            public System.ComponentModel.PropertyDescriptor GetDefaultProperty()
            {
                return null;
            }

            public object GetEditor(System.Type editorBaseType)
            {
                return null;
            }

            public System.ComponentModel.EventDescriptorCollection GetEvents()
            {
                return System.ComponentModel.EventDescriptorCollection.Empty;
            }

            public System.ComponentModel.EventDescriptorCollection GetEvents(System.Attribute[] attributes)
            {
                return System.ComponentModel.EventDescriptorCollection.Empty;
            }

            public System.ComponentModel.PropertyDescriptorCollection GetProperties()
            {
                return GetProperties(null);
            }

            public System.ComponentModel.PropertyDescriptorCollection GetProperties(System.Attribute[] attributes)
            {
                var props = new System.Collections.Generic.List<System.ComponentModel.PropertyDescriptor>();

                foreach (var kv in m_Data)
                {
                    props.Add(new DictionaryEntryPropertyDescriptor(kv.Key, m_Data));
                }

                return new System.ComponentModel.PropertyDescriptorCollection(props.ToArray(), true);
            }

            public object GetPropertyOwner(System.ComponentModel.PropertyDescriptor pd)
            {
                return m_Data;
            }

            private class DictionaryEntryPropertyDescriptor : System.ComponentModel.PropertyDescriptor
            {
                private readonly string m_Key;
                private readonly System.Collections.Generic.IDictionary<string, object> m_Data;

                public DictionaryEntryPropertyDescriptor(string key, System.Collections.Generic.IDictionary<string, object> data)
                    : base(key, null)
                {
                    m_Key = key;
                    m_Data = data;
                }

                public override bool CanResetValue(object component)
                {
                    return false;
                }

                public override System.Type ComponentType
                {
                    get
                    {
                        return typeof(System.Collections.Generic.IDictionary<string, object>);
                    }
                }

                public override object GetValue(object component)
                {
                    object value;
                    if (!m_Data.TryGetValue(m_Key, out value))
                    {
                        return string.Empty;
                    }
                    return value;
                }

                public override bool IsReadOnly
                {
                    get
                    {
                        return true;
                    }
                }

                public override System.Type PropertyType
                {
                    get
                    {
                        var value = GetValue(null);
                        return value != null ? value.GetType() : typeof(string);
                    }
                }

                public override void ResetValue(object component)
                {
                }

                public override void SetValue(object component, object value)
                {
                }

                public override bool ShouldSerializeValue(object component)
                {
                    return false;
                }
            }
        }

    }
}