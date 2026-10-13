using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Linq;
//using CustomExtensions;

// FreeSpaceMonitor v2.0.5	Fix ListBox ItemHeight on scaled display

namespace CustomForms {
	public partial class ColorPickerForm : Form {
		private class NamedColorPair {
			public string Name;
			public Color Value;
		}

		private List<Color> SystemColorList;
		private List<Color> WebColorList;
		private Color InnerSelectedColor;
		private bool IsInitFinished;

		public Color SelectedColor {
			get {
				return InnerSelectedColor;
			}
			set {
				InnerSelectedColor = value;
				bool found = false;
				foreach (Color c in WebColorList) {
					if (c.ToArgb() == InnerSelectedColor.ToArgb()) {
						InnerSelectedColor = c;
						found = true;
						break;
					}
				}
				if (!found) {
					foreach (Color c in SystemColorList) {
						if (c.ToArgb() == InnerSelectedColor.ToArgb()) {
							InnerSelectedColor = c;
							found = true;
							break;
						}
					}
				}
			}
		}

		public ColorPickerForm() {
			InitializeComponent();
			IsInitFinished = false;
			InnerSelectedColor = Color.Transparent;
			SystemColorList = new List<Color>();
			WebColorList = new List<Color>();
			Color clr;
			foreach (System.Reflection.PropertyInfo propInfo in typeof(SystemColors).GetProperties(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)) {
				SystemColorList.Add((Color)propInfo.GetValue(null));
			}
			foreach (System.Reflection.PropertyInfo propInfo in typeof(Color).GetProperties(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)) {
				clr = (Color)propInfo.GetValue(null);
				if (!SystemColorList.Contains(clr)) {
					WebColorList.Add(clr);
				}
			}
		}

		private void ColorPickerForm_Load(object sender, EventArgs e) {
			int select;
			int itemHeight = TextRenderer.MeasureText("Text", Font).Height + 4;
			SystemColorList = SystemColorList.OrderBy(x => x.Name).ToList();
			WebColorList = WebColorList.OrderBy(x => x.GetHue()).ToList();
			SystemListBox.Items.Clear();
			WebListBox.Items.Clear();
			SystemListBox.ItemHeight = itemHeight;
			WebListBox.ItemHeight = itemHeight;
			foreach (Color color in SystemColorList) {
				SystemListBox.Items.Add(color.Name);
			}
			foreach (Color color in WebColorList) {
				WebListBox.Items.Add(color.Name);
			}
			if (SystemColorList.Contains(InnerSelectedColor)) {
				ColorTabs.SelectTab(0);
				SystemListBox.SelectedIndex = SystemColorList.IndexOf(InnerSelectedColor);

			} else if (WebColorList.Contains(InnerSelectedColor)) {
				ColorTabs.SelectTab(1);
				WebListBox.SelectedIndex = WebColorList.IndexOf(InnerSelectedColor);
			}
			IsInitFinished = true;
		}

		private void ColorPickerForm_FormClosing(object sender, FormClosingEventArgs e) {

		}

		protected override bool ProcessCmdKey(ref Message msg, Keys keyData) {
			if (keyData == Keys.Escape) {
				this.DialogResult = DialogResult.Cancel;
				this.Close();
				return true;
			}
			return base.ProcessCmdKey(ref msg, keyData);
		}

		private void SystemListBox_DrawItem(object sender, DrawItemEventArgs e) {
			if ((e.State & DrawItemState.Selected) == DrawItemState.Selected) {
				e = new DrawItemEventArgs(e.Graphics, e.Font, e.Bounds, e.Index,
					e.State ^ DrawItemState.Selected, e.ForeColor, Color.PaleGoldenrod);
			}
			e.DrawBackground();
			Color drawColor = SystemColorList[e.Index];
			Rectangle outlineRect = new Rectangle(e.Bounds.X + 2, e.Bounds.Y + 2, e.Bounds.Height * 3 / 2, e.Bounds.Height - 5);
			Rectangle fillRect = outlineRect;
			e.Graphics.FillRectangle(new SolidBrush(drawColor), fillRect);
			e.Graphics.DrawRectangle(new Pen(Color.Gray), outlineRect);
			e.Graphics.DrawString(drawColor.Name, Font, new SolidBrush(Color.Black), new Point(outlineRect.Width + 5, e.Bounds.Y - 1));
		}

		private void WebListBox_DrawItem(object sender, DrawItemEventArgs e) {
			if ((e.State & DrawItemState.Selected) == DrawItemState.Selected) {
				e = new DrawItemEventArgs(e.Graphics, e.Font, e.Bounds, e.Index,
					e.State ^ DrawItemState.Selected, e.ForeColor, Color.PaleGoldenrod);
			}
			e.DrawBackground();
			Color drawColor = WebColorList[e.Index];
			Rectangle outlineRect = new Rectangle(e.Bounds.X + 2, e.Bounds.Y + 2, e.Bounds.Height * 3 / 2, e.Bounds.Height - 5);
			Rectangle fillRect = outlineRect;
			e.Graphics.FillRectangle(new SolidBrush(drawColor), fillRect);
			e.Graphics.DrawRectangle(new Pen(Color.Gray), outlineRect);
			e.Graphics.DrawString(drawColor.Name, Font, new SolidBrush(Color.Black), new Point(outlineRect.Width + 5, e.Bounds.Y - 1));
		}

		private void SystemListBox_SelectedIndexChanged(object sender, EventArgs e) {
			InnerSelectedColor = SystemColorList[SystemListBox.SelectedIndex];
			if (IsInitFinished) {
				this.DialogResult = DialogResult.OK;
				this.Close();
			}
		}

		private void WebListBox_SelectedIndexChanged(object sender, EventArgs e) {
			InnerSelectedColor = WebColorList[WebListBox.SelectedIndex];
			if (IsInitFinished) {
				this.DialogResult = DialogResult.OK;
				this.Close();
			}
		}
	}
}
