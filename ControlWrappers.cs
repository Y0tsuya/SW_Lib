using Color = System.Drawing.Color;
using Size = System.Drawing.Size;
using Point = System.Drawing.Point;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Text;
using CustomLibs;
using Scripting;
using System.Windows.Forms.VisualStyles;
using CustomExtensions;
using System;

// Hummingbird v2.0.2:	Disable Application.DoEvents() in ConsoleTextmanager.PushTocontrol
//						Can cause OverflowExceptionError in rapid printing
//						Add Spinner + Slider to FlexDialog
// FolderWatch v1.3.5:	Add RichTextBox capability to LogAndStatusManager
// Folderwatch v1.4.8:	Optimize RichTextBox update/insertion performance
// Folderwatch v1.5.0:	RichTextBox logic fix
// Folderwatch v1.5.2:	Timer recursion fix
// Folderwatch v1.6.0:	Add virtual mode for ListView
//						Add HyperLogView option
//						Remove RichTextBox support from LogAndStatusManager
// Folderwatch v1.6.2:	Add HyperTextBox option
// Folderwatch v1.7.7:	Change List<LogItem> to Queue<LogItem> for reentrancy problem
// Folderwatch v1.7.9:	Change Queue<LogItem> to ConcurrentQueue<LogItem> for reentrancy problem
// Cobra v1.1.1:		Change script grid launch from click to doubleclick
//						ConsoleTextManager PopLog batch process performance improvement
// FolderBot v2.3.0		Update boolean naming convention


namespace CustomControls {
	public class ConsoleTextManager {
		public class TextItem {
			public int Index;
			public string Text;
			public Color TextColor;

			public TextItem(int index, string text, Color textColor) {
				Index = index;
				Text = text;
				TextColor = textColor;
			}
		}
		public RichTextBox? TextBoxToControl;
		public RoundedRichTextBox? TextBoxToControlRounded;
		public bool CanFilter;
		public Dictionary<string, string> FilterDictionary;
		private Color TextColor;
		ConcurrentQueue<TextItem> TextFifo;
		private System.Windows.Forms.Timer FifoTimer;
		private bool IsProcessingFifo;

		public ConsoleTextManager(RichTextBox console) {
			TextBoxToControl = console;
			TextBoxToControlRounded = null;
			CommonInit();
		}

		public ConsoleTextManager(RoundedRichTextBox console) {
			TextBoxToControlRounded = console;
			TextBoxToControl = null;
			CommonInit();
		}

		private void CommonInit() {
			FilterDictionary = new Dictionary<string, string>();
			TextColor = Color.Black;
			TextFifo = new ConcurrentQueue<TextItem>();
			IsProcessingFifo = false;
			FifoTimer = new System.Windows.Forms.Timer();
			FifoTimer.Interval = 10;
			FifoTimer.Enabled = true;
			FifoTimer.Tick += FifoTimer_Tick;
			FifoTimer.Start();
		}

		~ConsoleTextManager() {
			if (FifoTimer != null) {
				FifoTimer.Stop();
				FifoTimer.Dispose();
			}
		}

		public int AddText(string text, Color textColor) {
			TextItem item = new TextItem(0, text, textColor);
			PushLog(item);
			return item.Index;
		}

		public void ClearText() {
			TextBoxToControl?.Invoke(new Action(delegate {
				TextBoxToControl.Text = "";
			}));
			TextBoxToControlRounded?.Invoke(new Action(delegate {
				TextBoxToControlRounded.Text = "";
			}));
		}

		private void FifoTimer_Tick(object? sender, EventArgs? e) {
			if (!IsProcessingFifo) {
				IsProcessingFifo = true;
				PopLog();
				IsProcessingFifo = false;
			}
		}

		private void PushLog(TextItem item) {
			TextFifo.Enqueue(item);
		}

		private void PopLog() {
			StringBuilder sb = new StringBuilder();
			Color currentColor = Color.White;
			int processed = 0;
			int currentIndex = 0;
			TextItem item;
			TextItem peek;
			while (TextFifo.Count > 0) {
				if (!TextFifo.TryDequeue(out peek)) break;
				if (processed == 0) currentColor = peek.TextColor;
				if (peek.TextColor != currentColor) {
					break;
				} else {
					currentIndex = peek.Index;
					sb.Append(peek.Text);
				}
			}
			if (sb.Length > 0) {
				item = new TextItem(currentIndex, sb.ToString(), currentColor);
				PushToControl(item);
			}
		}

		public void PushToControl(TextItem item) {
			if (CanFilter) {
				foreach (KeyValuePair<string, string> pair in FilterDictionary) {
					if (item.Text.Contains(pair.Key)) {
						item.Text = item.Text.Replace(pair.Key, pair.Value);
					}
				}
			}
			try {
				TextBoxToControl?.Invoke(new Action(delegate {
					TextBoxToControl.SelectionColor = item.TextColor;
					TextBoxToControl.AppendText(item.Text);
					TextBoxToControl.SelectionStart = TextBoxToControl.Text.Length;
					// scroll it automatically
					TextBoxToControl.ScrollToCaret();
				}));
				TextBoxToControlRounded?.Invoke(new Action(delegate {
					TextBoxToControlRounded.SelectionColor = item.TextColor;
					TextBoxToControlRounded.AppendText(item.Text);
					TextBoxToControlRounded.SelectionStart = TextBoxToControlRounded.Text.Length;
					// scroll it automatically
					TextBoxToControlRounded.ScrollToCaret();
				}));
			} catch (Exception ex) {
				string buf = ex.Message;
			}
			Application.DoEvents();
		}
	}

	public class ProgressBarManager {
		public ProgressBar ProgressControl;
		public RoundedProgressBar ProgressControlRounded;
		private System.Windows.Forms.Timer ProgressTimer;
		public int Progress;
		private int Modulo;
		private int TickPos;

		public ProgressBarManager(ProgressBar barToControl) {
			ProgressControl = barToControl;
			ProgressControlRounded = null;
			CommonInit();
		}

		public ProgressBarManager(RoundedProgressBar barToControl) {
			ProgressControl = null;
			ProgressControlRounded = barToControl;
			CommonInit();
		}

		private void CommonInit() {
			ProgressTimer = new System.Windows.Forms.Timer();
			ProgressTimer.Interval = 100;
			ProgressTimer.Enabled = true;
			ProgressTimer.Tick += ProgressTimer_Tick;
			ProgressTimer.Start();
			TickPos = 0;
			Modulo = 1;
		}

		~ProgressBarManager() {
			if (ProgressTimer != null) {
				ProgressTimer.Stop();
				ProgressTimer.Dispose();
			}
		}

		public int Max {
			set {
				//if (max == ProgressBarToControl.Maximum) return;
				if (ProgressControl != null) {
					ProgressControl.Invoke(new Action(delegate {
						if (ProgressControl.Value > value) ProgressControl.Value = value;
						ProgressControl.Maximum = value;
					}));
					Modulo = value / ProgressControl.Width;
				}
				if (ProgressControlRounded != null) {
					ProgressControlRounded.Invoke(new Action(delegate {
						if (ProgressControlRounded.Value > value) ProgressControlRounded.Value = value;
						ProgressControlRounded.Maximum = value;
					}));
					Modulo = value / ProgressControlRounded.Width;
				}
				if (Progress > value) Progress = value;
				if (Modulo == 0) Modulo = 1;
			}
			get {
				if (ProgressControl != null) {
					return ProgressControl.Maximum;
				} else if (ProgressControlRounded != null) {
					return ProgressControlRounded.Maximum;
				} else {
					return 0;
				}
			}
		}

		public void Advance() {
			Progress++;
		}

		public void Advance(int ticks) {
			Progress += ticks;
		}

		public void Set(int val) {
			Progress = val;
		}

		public void GoToEnd() {
			if (ProgressControl == null) return;
			Progress = ProgressControl.Maximum;
		}

		private void ProgressTimer_Tick(object? sender, EventArgs? e) {
			int tick = (int)Progress / Modulo;
			if (ProgressControl != null) {
				if (this.Progress <= ProgressControl.Maximum) {
					if (tick != TickPos) {
						ProgressControl.Invoke(new Action(delegate {
							ProgressControl.Value = Progress;
						}));
					}
				}
			}
			if (ProgressControlRounded != null) {
				if (this.Progress <= ProgressControlRounded.Maximum) {
					if (tick != TickPos) {
						ProgressControlRounded.Invoke(new Action(delegate {
							ProgressControlRounded.Value = Progress;
						}));
					}
				}
			}
			TickPos = tick;
			//Debug.WriteLine(String.Format("ProgressBar value={0} tick={1} max={2}", Progress, TickPos, Max));
			Application.DoEvents();
		}
	}

	public class FlexDialogManager {
		public enum ControlType { TEXT, CHECK, RADIO, SLIDER, SPINNER };
		public class ControlItem {
			public string ControlLabel;
			public ControlType UserControlType;
			public System.Windows.Forms.Control UserControl;
			public Label Label;
			public System.Windows.Forms.Control[] SubControls;
			public Label[] SubLabels;
			public Size OverrideSize;
			public int DefaultVal;
			public string DefaultText;
			public bool ShouldEnableByDefault;
			public int MinValue;
			public int MaxValue;

			// initializer for textbox
			public ControlItem(string label, ControlType controlType, Size overrideSize, string defaultText) {
				ControlLabel = label;
				UserControlType = controlType;
				OverrideSize = overrideSize;
				DefaultText = defaultText;
			}

			// initializer for checkbox
			public ControlItem(string label, ControlType controlType, bool shouldEnableByDefault) {
				ControlLabel = label;
				UserControlType = controlType;
				ShouldEnableByDefault = shouldEnableByDefault;
			}

			// initializer for radiobutton
			public ControlItem(string label, ControlType controlType, int defaultVal) {
				ControlLabel = label;
				UserControlType = controlType;
				DefaultVal = defaultVal;
			}

			// initializer for spinner/slider
			public ControlItem(string label, ControlType controlType, int defaultVal, int minVal, int maxVal) {
				ControlLabel = label;
				UserControlType = controlType;
				DefaultVal = defaultVal;
				MinValue = minVal;
				MaxValue = maxVal;
			}

		}

		public List<ControlItem> ControlItems;
		private bool IsDialogInitialized;
		public Form FlexForm;
		public Action MoveEventCallback;
		public const int SLIDER_TICK = 10;

		public FlexDialogManager() {
			IsDialogInitialized = false;
			FlexForm = new Form();
			FlexForm.MinimizeBox = false;
			FlexForm.MaximizeBox = false;
			FlexForm.Resize += FlexForm_Resize;
			FlexForm.FormClosing += FlexForm_Closing;
			FlexForm.Move += FlexForm_Move;
		}

		private void FlexForm_Move(object sender, EventArgs e) {
			if (MoveEventCallback != null) MoveEventCallback();
		}

		private void FlexForm_UpdateControl(object sender, EventArgs e) {
			string label = ((Control)sender).Tag.ToString();
			ControlItem item = FindControlByLabel(label);
			if (item.UserControlType == ControlType.SLIDER) {
				int sliderVal = (int)(((HScrollBar)item.UserControl).Value);
				if (sliderVal % SLIDER_TICK > 0) {
					sliderVal = ((sliderVal / SLIDER_TICK) + 1) * SLIDER_TICK;
					((HScrollBar)item.UserControl).Value = sliderVal;
				}
				item.SubControls[0].Text = (sliderVal / SLIDER_TICK).ToString();
			}
			if (item.UserControlType == ControlType.SPINNER) {
				item.SubControls[0].Text = ((int)(((NumericUpDown)item.UserControl).Value)).ToString();
			}
		}

		private void ResizeLabel(Label lbl) {
			Size textSize = TextRenderer.MeasureText(lbl.Text, lbl.Font);
			lbl.Width = textSize.Width;
		}

		private void ResizeRadio(RadioButton radio) {
			Size textSize = TextRenderer.MeasureText(radio.Text, radio.Font);
			radio.Width = textSize.Width + 20;
		}
		private void ResizeControl(System.Windows.Forms.Control con, int adjustment) {
			Size textSize = TextRenderer.MeasureText(con.Text, con.Font);
			con.Width = textSize.Width + adjustment;
		}

		private void FlexForm_Resize(object sender, EventArgs e) {
			if (ControlItems == null) return;
			if (!IsDialogInitialized) return;
			foreach (ControlItem item in ControlItems) {
				if (item.UserControlType == ControlType.TEXT) {
					if (item.OverrideSize.Width == 0) {
						item.UserControl.Width = FlexForm.Width - item.Label.Width - 35;
					}
				}
				if (item.UserControlType == ControlType.SLIDER) {
					item.UserControl.Width = FlexForm.Width - item.Label.Width - item.SubControls[0].Width - 40;
					item.SubControls[0].Left = item.UserControl.Right + 5;
				}
			}
		}

		private void FlexForm_Closing(object sender, FormClosingEventArgs e) {
			e.Cancel = true;
		}

		public void Position(Point location) {
			FlexForm.Invoke(new Action(delegate {
				FlexForm.Location = location;
			}));
		}

		public void BuildDialog(List<ControlItem> itemList) {
			if (IsDialogInitialized) DestroyDialog();
			ControlItems = itemList;
			string[] sublabels;
			char[] delims = { ',', '/', '\\' };
			int subCount;
			Point XY = new Point(5, 5);
			Point subXY = new Point(5, 5);
			Size extant = new Size();
			int itemWidth;
			FlexForm.Invoke(new Action(delegate {
				foreach (ControlItem item in ControlItems) {
					itemWidth = 0;
					switch (item.UserControlType) {
						case ControlType.TEXT:
							// init
							item.Label = new Label();
							item.UserControl = new TextBox();
							FlexForm.Controls.Add(item.Label);
							FlexForm.Controls.Add(item.UserControl);
							// value
							item.Label.Text = item.ControlLabel;
							item.UserControl.Text = item.DefaultText;
							item.UserControl.Tag = item.ControlLabel;
							// position
							ResizeLabel(item.Label);
							if (item.OverrideSize.Width > 0) item.UserControl.Width = item.OverrideSize.Width;
							item.Label.Location = new Point(XY.X, XY.Y);
							item.UserControl.Location = new(item.Label.Right + 5, XY.Y);
							itemWidth = item.UserControl.Right + 5;
							break;
						case ControlType.RADIO:
							// init
							sublabels = item.ControlLabel.Split(delims);
							item.ControlLabel = sublabels[0];
							subCount = sublabels.Count() - 1;
							item.UserControl = new GroupBox();
							item.SubControls = new System.Windows.Forms.Control[subCount];
							FlexForm.Controls.Add(item.UserControl);
							for (int i = 0; i < subCount; i++) {
								item.SubControls[i] = new RadioButton();
								item.UserControl.Controls.Add(item.SubControls[i]);
							}
							if (item.DefaultVal < subCount) {
								((RadioButton)item.SubControls[item.DefaultVal]).Checked = true;
							}
							// value
							item.UserControl.Text = sublabels[0];
							item.UserControl.Tag = item.ControlLabel;
							for (int i = 0; i < subCount; i++) {
								item.SubControls[i].Text = sublabels[i + 1];
							}
							// position
							item.UserControl.Location = XY;
							item.UserControl.Height = 45;
							//item.SubLabels = new Label[subCount];
							subXY.X = 5; subXY.Y = 5;
							for (int i = 0; i < subCount; i++) {
								ResizeControl(item.SubControls[i], 20);
								item.SubControls[i].Top = 15;
								item.SubControls[i].Left = subXY.X;
								subXY.X += item.SubControls[i].Width + 5;
							}
							item.UserControl.Width = subXY.X;
							itemWidth = item.UserControl.Right + 5;
							break;
						case ControlType.CHECK:
							// init
							item.UserControl = new CheckBox();
							FlexForm.Controls.Add(item.UserControl);
							// value
							item.UserControl.Text = item.ControlLabel;
							item.UserControl.Tag = item.ControlLabel;
							((CheckBox)item.UserControl).Checked = item.ShouldEnableByDefault;
							// position
							item.UserControl.Location = XY;
							itemWidth = item.UserControl.Right + 5;
							break;
						case ControlType.SLIDER:
							// init
							item.UserControl = new HScrollBar();
							item.Label = new Label();
							item.SubControls = new System.Windows.Forms.Control[1];
							item.SubControls[0] = new TextBox();
							FlexForm.Controls.Add(item.Label);
							FlexForm.Controls.Add(item.UserControl);
							FlexForm.Controls.Add(item.SubControls[0]);
							((HScrollBar)item.UserControl).ValueChanged += FlexForm_UpdateControl;
							// value
							item.UserControl.Tag = item.ControlLabel;
							item.Label.Text = item.ControlLabel;
							((TextBox)item.SubControls[0]).ReadOnly = true;
							item.SubControls[0].Text = item.DefaultVal.ToString();
							((HScrollBar)item.UserControl).Maximum = item.MaxValue * SLIDER_TICK;
							((HScrollBar)item.UserControl).Minimum = item.MinValue * SLIDER_TICK;
							((HScrollBar)item.UserControl).Value = item.DefaultVal * SLIDER_TICK;
							((HScrollBar)item.UserControl).LargeChange = SLIDER_TICK;
							((HScrollBar)item.UserControl).SmallChange = SLIDER_TICK;
							// position
							item.UserControl.Location = XY;
							item.Label.Location = new Point(XY.X, XY.Y);
							ResizeLabel(item.Label);
							item.UserControl.Location = new Point(item.Label.Right + 5, XY.Y);
							item.SubControls[0].Location = new Point(item.UserControl.Right + 5, XY.Y);
							item.SubControls[0].Width = item.MaxValue.ToString().Length * 5 + 10;
							itemWidth = item.SubControls[0].Right + 10;
							break;
						case ControlType.SPINNER:
							// init
							item.UserControl = new NumericUpDown();
							item.Label = new Label();
							item.SubControls = new System.Windows.Forms.Control[1];
							item.SubControls[0] = new TextBox();
							FlexForm.Controls.Add(item.Label);
							FlexForm.Controls.Add(item.UserControl);
							FlexForm.Controls.Add(item.SubControls[0]);
							((NumericUpDown)item.UserControl).ValueChanged += FlexForm_UpdateControl;
							// value
							item.UserControl.Tag = item.ControlLabel;
							item.Label.Text = item.ControlLabel;
							((TextBox)item.SubControls[0]).ReadOnly = true;
							item.SubControls[0].Text = item.DefaultVal.ToString();
							((NumericUpDown)item.UserControl).Maximum = item.MaxValue;
							((NumericUpDown)item.UserControl).Minimum = item.MinValue;
							((NumericUpDown)item.UserControl).Value = item.DefaultVal;
							// position
							item.UserControl.Location = XY;
							item.Label.Location = new Point(XY.X, XY.Y);
							ResizeLabel(item.Label);
							item.UserControl.Location = new Point(item.Label.Right + 5, XY.Y);
							item.UserControl.Width = item.MaxValue.ToString().Length * 5 + 30;
							item.SubControls[0].Location = new Point(item.UserControl.Right + 5, XY.Y);
							item.SubControls[0].Width = item.MaxValue.ToString().Length * 5 + 10;
							itemWidth = item.SubControls[0].Right + 10;
							break;
					}
					if (itemWidth > extant.Width) extant.Width = itemWidth;
					XY.Y = item.UserControl.Bottom + 5;
				}
				extant.Width += 20;
				extant.Height = XY.Y + 5;
				IsDialogInitialized = true;
				FlexForm.Width = extant.Width;
				FlexForm.Height = extant.Height + 40;
			}));
		}

		private void FlexDialogManager_ValueChanged(object? sender, EventArgs e) {
			throw new NotImplementedException();
		}

		private void DestroyDialog() {
			FlexForm.Invoke(new Action(delegate {
				for (int i = 0; i < ControlItems.Count(); i++) {
					if (ControlItems[i].SubControls != null) {
						for (int j = 0; j < ControlItems[i].SubControls.Count(); j++) {
							FlexForm.Controls.Remove(ControlItems[i].SubControls[j]);
							ControlItems[i].SubControls[j].Dispose();
						}
					}
					FlexForm.Controls.Remove(ControlItems[i].UserControl);
					ControlItems[i].UserControl.Dispose();
					if (ControlItems[i].Label != null) {
						FlexForm.Controls.Remove(ControlItems[i].Label);
						ControlItems[i].Label.Dispose();
					}
				}
			}));
			IsDialogInitialized = false;
		}

		public void ShowDialog() {
			FlexForm.Invoke(new Action(delegate {
				FlexForm.Show();
			}));
		}

		public void HideDialog() {
			FlexForm.Invoke(new Action(delegate {
				FlexForm.Hide();
			}));
		}

		public ControlItem FindControlByLabel(string label) {
			for (int i = 0; i < ControlItems.Count(); i++) {
				if (ControlItems[i].ControlLabel == label) {
					return ControlItems[i];
				}
			}
			return null;
		}

		public int GetRadio(string label) {
			int idx = -1;
			ControlItem item = FindControlByLabel(label);
			if (item != null) {
				FlexForm.Invoke(new Action(delegate {
					for (int j = 0; j < item.SubControls.Count(); j++) {
						if (((RadioButton)item.SubControls[j]).Checked) {
							idx = j;
						}
					}
				}));
			}
			return idx;
		}

		public void SetRadio(string label, int val) {
			ControlItem item = FindControlByLabel(label);
			if (item != null) {
				if (val < item.SubControls.Count()) {
					FlexForm.Invoke(new Action(delegate {
						((RadioButton)item.SubControls[val]).Checked = true;
					}));
				}
			}
		}

		public string GetText(string label) {
			string text = "";
			ControlItem item = FindControlByLabel(label);
			if (item != null) {
				FlexForm.Invoke(new Action(delegate {
					text = item.UserControl.Text;
				}));
			}
			return text;
		}

		public void SetText(string label, string text) {
			ControlItem item = FindControlByLabel(label);
			if (item != null) {
				FlexForm.Invoke(new Action(delegate {
					item.UserControl.Text = text;
				}));
			}
		}

		public bool GetCheck(string label) {
			bool check = false;
			ControlItem item = FindControlByLabel(label);
			if (item != null) {
				FlexForm.Invoke(new Action(delegate {
					check = ((CheckBox)item.UserControl).Checked;
				}));
			}
			return check;
		}

		public void SetCheck(string label, bool check) {
			ControlItem item = FindControlByLabel(label);
			if (item != null) {
				FlexForm.Invoke(new Action(delegate {
					((CheckBox)item.UserControl).Checked = check;
				}));
			}
		}

		public int GetSlide(string label) {
			int val = -1;
			ControlItem item = FindControlByLabel(label);
			if (item != null) {
				FlexForm.Invoke(new Action(delegate {
					val = ((HScrollBar)item.UserControl).Value / SLIDER_TICK;
				}));
			}
			return val;
		}

		public void SetSlide(string label, int val) {
			ControlItem item = FindControlByLabel(label);
			if (item != null) {
				HScrollBar scroll = item.UserControl as HScrollBar;
				if (val * SLIDER_TICK > scroll.Maximum) {
					// do some error handling here
				} else {
					FlexForm.Invoke(new Action(delegate {
						scroll.Value = val * SLIDER_TICK;
					}));
				}
			}
		}

		public int GetSpin(string label) {
			int val = -1;
			ControlItem item = FindControlByLabel(label);
			if (item != null) {
				FlexForm.Invoke(new Action(delegate {
					val = (int)((NumericUpDown)item.UserControl).Value;
				}));
			}
			return val;
		}

		public void SetSpin(string label, int val) {
			ControlItem item = FindControlByLabel(label);
			if (item != null) {
				NumericUpDown updown = item.UserControl as NumericUpDown;
				if (val > updown.Maximum) {
				} else {
					FlexForm.Invoke(new Action(delegate {
						updown.Value = val;
					}));
				}
			}
		}

	}

	public class LogAndStatusManager {

		public class LogItem {
			public int Index;
			public int Parent;
			public string Text;
			public string ToolTip;
			public ErrorLevel Level;
			public Color ExtendedColor;
			public bool IsSubItem;
			public bool ShouldUpdate;

			public LogItem() {
				Text = "";
				ToolTip = "";
				Parent = -1;
			}

			public LogItem(string text, string tooltip, ErrorLevel level, Color extendedColor, bool isSubItem, bool shouldUpdate) {
				Index = 0;
				Text = text;
				ToolTip = tooltip;
				Level = level;
				ExtendedColor = extendedColor;
				IsSubItem = isSubItem;
				ShouldUpdate = shouldUpdate;
			}
		}

		public ListView ListViewToControl;
		public TextBox LogStripToControl;
		public HyperLogView HyperViewToControl;
		public HyperTextBox HyperStripToControl;
		public bool CanFilter;
		public Dictionary<string, string> FilterDictionary;
		private string LastMessage;
		private Color TextColor;
		private string PopupMessage;
		ConcurrentQueue<LogItem> LogQueue;
		private System.Windows.Forms.Timer FifoTimer;
		private int Index;
		private object IndexLock;
		private bool IsProcessingFifo;
		private bool IsVirtualListViewEnabled;
		List<ListViewItem> VirtualListItems;

		public LogAndStatusManager(HyperTextBox hyperStrip, TextBox logStrip, HyperLogView hyperView, ListView logView, bool isVirtualListViewEnabled = false) {
			HyperStripToControl = hyperStrip;
			LogStripToControl = logStrip;
			HyperViewToControl = hyperView;
			ListViewToControl = logView;
			FilterDictionary = new Dictionary<string, string>();
			PopupMessage = "";
			TextColor = Color.Black;
			LogQueue = new ConcurrentQueue<LogItem>();
			IsProcessingFifo = false;
			FifoTimer = new System.Windows.Forms.Timer();
			FifoTimer.Interval = 10;
			FifoTimer.Enabled = true;
			FifoTimer.Tick += FifoTimer_Tick;
			FifoTimer.Start();
			Index = 0;
			IndexLock = new object();
			LastMessage = "";
			if (ListViewToControl != null) {
				IsVirtualListViewEnabled = isVirtualListViewEnabled;
				if (IsVirtualListViewEnabled) {
					ListViewToControl.VirtualMode = true;
					ListViewToControl.VirtualListSize = 0;
					ListViewToControl.RetrieveVirtualItem += RetrieveVirtualItem;
					VirtualListItems = new List<ListViewItem>();
				}
				ListViewToControl.Resize += ListView_Resize;
			}
		}

		~LogAndStatusManager() {
			if (FifoTimer != null) {
				FifoTimer.Stop();
				FifoTimer.Dispose();
				//FifoTimer = null;
			}
		}

		//////////////////////  Top-Level User Interface ////////////////////////
		public int AddLog(string text) {
			return AddLog(text, "", ErrorLevel.LOG_INFO);
		}

		public int AddLog(string text, ErrorLevel level) {
			return AddLog(text, "", level);
		}

		public int AddLog(string text, string tooltip, ErrorLevel level) {
			return AddLog(text, tooltip, level, Color.Blue);
		}

		public int AddLog(string text, string tooltip, ErrorLevel level, Color extendedColor) {
			LogItem item = new LogItem();
			item.Text = text;
			item.ToolTip = tooltip;
			item.Level = level;
			item.ExtendedColor = extendedColor;
			lock (IndexLock) {
				item.Index = Index;
				Index++;
			}
			item.Parent = -1;   // no parent
			PushLog(item);
			return item.Index;
		}

		public void UpdateLog(int index, string text) {
			UpdateLog(index, text, "", ErrorLevel.LOG_INFO, Color.Blue);
		}

		public void UpdateLog(int index, string text, ErrorLevel level) {
			UpdateLog(index, text, "", level, Color.Blue);
		}

		public void UpdateLog(int index, string text, string tooltip, ErrorLevel level) {
			UpdateLog(index, text, tooltip, level, Color.Blue);
		}

		public void UpdateLog(int index, string text, string tooltip, ErrorLevel level, Color extendedColor) {
			LogItem item = new LogItem();
			item.Text = text;
			item.ToolTip = tooltip;
			item.Level = level;
			item.ExtendedColor = extendedColor;
			item.Index = index;
			item.ShouldUpdate = true;
			PushLog(item);
		}

		public int AddSubLog(int parent, string text) {
			return AddSubLog(parent, text, "", ErrorLevel.LOG_INFO, Color.Blue);
		}

		public int AddSubLog(int parent, string text, ErrorLevel level) {
			return AddSubLog(parent, text, "", level, Color.Blue);
		}

		public int AddSubLog(int parent, string text, string tooltip, ErrorLevel level) {
			return AddSubLog(parent, text, tooltip, level, Color.Blue);
		}

		public int AddSubLog(int parent, string text, string tooltip, ErrorLevel level, Color extendedColor) {
			LogItem item = new LogItem();
			item.Text = text;
			item.ToolTip = tooltip;
			item.Level = level;
			item.ExtendedColor = extendedColor;
			lock (IndexLock) {
				item.Index = Index;
				Index++;
			}
			item.Parent = parent;
			item.IsSubItem = true;
			PushLog(item);
			return item.Index;
		}

		public void ClearListView() {
			if (ListViewToControl != null) {
				ListViewToControl.Clear();
				if (IsVirtualListViewEnabled) {
					ListViewToControl.VirtualListSize = 0;
					VirtualListItems.Clear();
				}
			}
			HyperViewToControl?.ClearLog();

		}


		//////////////////////  FIFO to ListView  ////////////////////////
		private void FifoTimer_Tick(object? sender, EventArgs? e) {
			if (!IsProcessingFifo) {
				IsProcessingFifo = true;
				if (LogQueue.Count > 0) PopLog();
				IsProcessingFifo = false;
			}
		}

		private void PushLog(LogItem item) {
			LogQueue.Enqueue(item);
			//Debug.WriteLine("Pushed " + item.Text);
		}

		private void PopLog() {
			LogItem item;
			string[] lines;
			ListViewToControl?.BeginUpdate();
			while (LogQueue.TryDequeue(out item)) {
				//Debug.WriteLine("Popped " + item.Text);
				PushToControls(item);
			}
			ListViewToControl?.EndUpdate();
			Application.DoEvents();
		}

		private void PushToControls(LogItem item) {
			//int widthLimit = ListViewToControl.Width;
			string prefix;
			string[] lines;

			if (item == null) return;
			switch (item.Level) {
				case ErrorLevel.LOG_INFO: TextColor = Color.Green; prefix = "[I]"; break;
				case ErrorLevel.LOG_ALERT: TextColor = Color.DarkCyan; prefix = "[A]"; break;
				case ErrorLevel.LOG_WARNING: TextColor = Color.DarkOrange; prefix = "[W]"; break;
				case ErrorLevel.LOG_ERROR: TextColor = Color.Red; prefix = "[E]"; break;
				default: TextColor = Color.Black; prefix = ""; break;
			}
			if (CanFilter) {
				foreach (KeyValuePair<string, string> pair in FilterDictionary) {
					if (item.Text.Contains(pair.Key)) {
						item.Text = item.Text.Replace(pair.Key, pair.Value);
					}
					if (item.ToolTip.Contains(pair.Key)) {
						item.ToolTip = item.ToolTip.Replace(pair.Key, pair.Value);
					}
				}
			}
			try {
				lines = item.Text.Split('\n');
				if (LogStripToControl != null) {
					LogStripToControl.Invoke(new Action(delegate {
						LogStripToControl.ForeColor = TextColor;
						//LogStripToControl.Text = String.Format("{0}-{1}--", prefix, item.Text);
					}));
				}
				if (ListViewToControl != null) {
					ListViewToControl.Invoke(new Action(delegate {
						if (item.ShouldUpdate) {
							UpdateListEntry(item.Index, item.Text, item.ToolTip, item.Level, Color.DarkGray);
						} else if (item.IsSubItem) {
							AddListSubEntry(item.Parent, item.Text, item.ToolTip, item.Level, Color.DarkGray);
						} else {
							AddListEntry(item.Index, item.Text, item.ToolTip, item.Level, Color.DarkGray);
						}
					}));
				}
				if (HyperStripToControl != null) {
					HyperStripToControl.Invoke(new Action(delegate {
						HyperTextBox.HyperTextItem hyperText = new HyperTextBox.HyperTextItem(item.Text, item.Level, item.ExtendedColor);
						HyperStripToControl.Item = hyperText;
					}));
				}
				if (HyperViewToControl != null) {
					HyperViewToControl.Invoke(new Action(delegate {
						foreach (string line in lines) {
							if (item.ShouldUpdate) {
								HyperViewToControl.UpdateLog(item.Index, line, item.ToolTip, item.Level, Color.DarkGray);
							} else if (item.IsSubItem) {
								HyperViewToControl.AddSubLog(item.Parent, line, item.ToolTip, item.Level, Color.DarkGray);
							} else {
								HyperViewToControl.AddLog(line, item.ToolTip, item.Level, Color.DarkGray);
							}
						}
					}));
				}
			} catch (Exception ex) {
				string buf = ex.Message;
			}
			LastMessage = String.Format("{0} {1}", prefix, item.Text);
			Popup = "";
			Application.DoEvents();
		}

		private string Popup {
			set {
				if (LogStripToControl != null) {
					LogStripToControl.Invoke(new Action(delegate {
						if (value != "") {
							LogStripToControl.ForeColor = Color.Black;
							LogStripToControl.Text = value;
						} else {
							LogStripToControl.ForeColor = TextColor;
							LogStripToControl.Text = LastMessage;
						}
					}));
				}
				PopupMessage = value;
			}
			get {
				return PopupMessage;
			}
		}

		//////////////////////  Direct ListView Control ////////////////////////

		private void ListView_Resize(object sender, EventArgs e) {
			ListViewToControl.Columns[0].Width = ListViewToControl.Width - 24;
		}

		private void AddListEntry(int index, string text, string tooltip, ErrorLevel level) {
			AddListEntry(index, text, tooltip, level, Color.Blue);
		}

		private void AddListEntry(int index, string text, string tooltip, ErrorLevel level, Color extendedColor) {
			ListViewItem lvItem = FormatListItem(text, tooltip, level, extendedColor, false);
			lvItem.Tag = index;
			lvItem.EnsureVisible();
			if (IsVirtualListViewEnabled) {
				VirtualListItems.Add(lvItem);
				ListViewToControl.VirtualListSize++;
				ListViewToControl.EnsureVisible(ListViewToControl.Items.Count - 1);
			} else {
				ListViewToControl.Items.Add(lvItem);
				ListViewToControl.EnsureVisible(ListViewToControl.Items.Count - 1);
			}
		}

		private void UpdateListEntry(int index, string text, string tooltip, ErrorLevel level, Color extendedColor) {
			ListViewItem lvItem = FormatListItem(text, tooltip, level, extendedColor, false);
			int i, c, j;

			j = -1;
			if (IsVirtualListViewEnabled) {
				c = VirtualListItems.Count;
				for (i = 0; i < c; i++) {
					if (index == (int)VirtualListItems[i].Tag) {
						j = i;
						break;
					}
				}
				if (j >= 0) {
					lvItem.Tag = index;
					VirtualListItems[j] = lvItem;
					ListViewToControl.EnsureVisible(j);
				}
			} else {
				c = ListViewToControl.Items.Count;
				for (i = 0; i < c; i++) {
					if (index == (int)ListViewToControl.Items[i].Tag) {
						j = i;
						break;
					}
				}
				if (j >= 0) {
					lvItem.Tag = index;
					ListViewToControl.Items[j] = lvItem;
					ListViewToControl.EnsureVisible(j);
				}
			}
		}

		private void AddListSubEntry(int index, string text, string tooltip, ErrorLevel level, Color extendedColor) {
			ListViewItem lvItem = FormatListItem(text, tooltip, level, extendedColor, true);
			int i, c, j;

			j = -1;
			if (IsVirtualListViewEnabled) {
				c = VirtualListItems.Count;
				for (i = c - 1; i >= 0; i--) {
					if (index == (int)VirtualListItems[i].Tag) {
						j = i;
						break;
					}
				}
				if (j >= 0) {
					lvItem.Tag = index;
					VirtualListItems.Insert(j + 1, lvItem);
					ListViewToControl.VirtualListSize++;
					ListViewToControl.EnsureVisible(j + 1);
				}
			} else {
				c = ListViewToControl.Items.Count;
				for (i = c - 1; i >= 0; i--) {
					if (index == (int)ListViewToControl.Items[i].Tag) {
						j = i;
						break;
					}
				}
				if (j >= 0) {
					lvItem.Tag = index;
					ListViewToControl.Items.Insert(j + 1, lvItem);
					ListViewToControl.EnsureVisible(j + 1);
				}
			}
		}

		private ListViewItem FormatListItem(string text, string tooltip, ErrorLevel level, Color extendedColor, bool sub) {
			Color textColor;
			ListViewItem lvItem = new ListViewItem();
			int image = 0;
			switch (level) {
				case ErrorLevel.LOG_INFO:
					textColor = Color.Green;
					if (sub) image = 6;
					else image = 1;
					break;
				case ErrorLevel.LOG_ALERT:
					textColor = Color.DarkCyan;
					if (sub) image = 7;
					else image = 2;
					break;
				case ErrorLevel.LOG_WARNING:
					textColor = Color.DarkOrange;
					if (sub) image = 8;
					else image = 3;
					break;
				case ErrorLevel.LOG_ERROR:
					textColor = Color.Red;
					if (sub) image = 9;
					else image = 4;
					break;
				case ErrorLevel.LOG_EXTENDED:
					textColor = extendedColor;
					if (sub) image = 10;
					else image = 5;
					break;
				default: textColor = Color.Black; break;
			}
			lvItem.Text = text;
			if (tooltip != "") lvItem.ToolTipText = tooltip;    // use same tootip for wrapped lines
			lvItem.ForeColor = textColor;
			lvItem.ImageIndex = image;
			return lvItem;
		}

		//////////////////////  Virtual ListView Control ////////////////////////

		private void RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e) {
			e.Item = VirtualListItems[e.ItemIndex];
		}

	}

	public class ScriptButtonManager {
		public ScriptingHostBase ScriptingBase;
		public bool HasScriptingError;
		public bool IsScriptBusy = false;
		public string? ScriptText;
		public int ScriptCount = 4;
		protected int ArgListWidth = 300;
		public string[]? ScriptFile;
		public TextBox[]? ScriptArgsTextBox;
		public Button[]? ScriptButton;
		public Button[]? ScriptArgButton;
		public string[]? ScriptArgs;
		public string[]? ScriptSearchPaths;
		public string? ScriptBaseDirectory;
		protected GroupBox? ScriptGroupBox;
		protected XmlSettings? SettingsStore;
		protected ToolTip? ButtonToolTip;
		protected Form BaseParent;
		protected Image Chevron;
		public Func<int, string, int, int, int> LogCallBack;
		public int LogIndex;
		protected System.Windows.Forms.Timer UiTimer;
		public Action? SettingsChangedCallBack;

		public ScriptButtonManager(Form baseParent, ScriptingHostBase scriptingBase, XmlSettings settingsStore, Image chevron) {
			BaseParent = baseParent;
			ScriptingBase = scriptingBase;
			SettingsStore = settingsStore;
			Chevron = chevron;
			UiTimer = new System.Windows.Forms.Timer();
			UiTimer.Interval = 250;
			UiTimer.Tick += UiTimer_Tick;
			ScriptingBase.ScriptExitHandler += ScriptExitHandler;
			SettingsChangedCallBack = null;
		}

		public void Construct(GroupBox scriptGroupBox, int scriptCount, Font globalFont) {
			int i;
			char[] delims = new char[] { '~' };
			string[] args;
			string[] scripts;
			string key;

			float textHeight = TextRenderer.MeasureText("Text", globalFont).Height;
			float addHeight = (float)Math.Max(8, textHeight * 0.25);
			int buttonHeight = (int)(textHeight + addHeight);
			int vertSpacing = buttonHeight / 5;

			ScriptGroupBox = scriptGroupBox;
			Point soff = ScriptGroupBox.Location;
			ScriptCount = scriptCount;
			ScriptButton = new Button[ScriptCount];
			ScriptArgButton = new Button[ScriptCount];
			ScriptArgsTextBox = new TextBox[ScriptCount];
			ScriptFile = new string[ScriptCount];
			args = new string[ScriptCount];
			scripts = new string[ScriptCount];
			for (i = 0; i < ScriptCount; i++) {
				key = "Script" + i;
				scripts[i] = SettingsStore.Read(key, "");
				key = "Args" + i;
				args[i] = SettingsStore.Read(key, "");
			}
			for (i = 0; i < ScriptCount; i++) {
				ScriptButton[i] = new Button();
				ScriptButton[i].Location = new Point(10, 25 + i * (buttonHeight + vertSpacing));
				ScriptButton[i].Size = new Size(ScriptGroupBox.Width - 49, buttonHeight);
				ScriptButton[i].Font = globalFont;
				ScriptButton[i].Name = "ScriptButton_" + i;
				ScriptButton[i].AllowDrop = true;
				ScriptArgButton[i] = new Button();
				ScriptArgButton[i].Location = new Point(ScriptButton[i].Right + 5, ScriptButton[i].Top);
				ScriptArgButton[i].Size = new Size(buttonHeight, buttonHeight);
				ScriptArgButton[i].Image = Chevron;
				ScriptArgButton[i].Name = "ScriptArgButton_" + i;
				ScriptArgsTextBox[i] = new TextBox();
				ScriptArgsTextBox[i].Left = ScriptGroupBox.Right + 5;
				ScriptArgsTextBox[i].Top = ScriptArgButton[i].Top + soff.Y + 2;
				ScriptArgsTextBox[i].Size = new Size(ArgListWidth, buttonHeight);
				ScriptArgsTextBox[i].Font = globalFont;
				ScriptArgsTextBox[i].Visible = false;
				ScriptGroupBox.Controls.Add(ScriptButton[i]);
				ScriptGroupBox.Controls.Add(ScriptArgButton[i]);
				BaseParent.Controls.Add(ScriptArgsTextBox[i]);
				ScriptArgsTextBox[i].BringToFront();
				//ScriptButton[i].Click += ScriptButton_Click;
				ScriptButton[i].DoubleClick += ScriptButton_Click;
				ScriptArgButton[i].Click += ScriptArgButton_Click;
				ScriptButton[i].DragDrop += ScriptButton_DragDrop;
				ScriptButton[i].DragEnter += Button_DragEnter;
				if (i < args.Length) ScriptArgsTextBox[i].Text = args[i];
				if (i < scripts.Length) {
					if (scripts[i] == "") {
						ScriptButton[i].Tag = "";
						ScriptButton[i].Text = "script" + i;
						ScriptFile[i] = "";
						ButtonToolTip?.SetToolTip(ScriptButton[i], "");
					} else {
						ScriptButton[i].Tag = scripts[i];
						ScriptButton[i].Text = Path.GetFileName(scripts[i]);
						ScriptFile[i] = scripts[i];
						ButtonToolTip?.SetToolTip(ScriptButton[i], scripts[i]);
					}
				} else {
					ScriptButton[i].Tag = "";
					ScriptButton[i].Text = "script" + i;
					ScriptFile[i] = "";
					ButtonToolTip?.SetToolTip(ScriptButton[i], "");
				}
			}
			UiTimer.Start();
		}

		public void Deconstruct() {
			string key;
			int i;
			Debug.Assert((SettingsStore != null) && (ScriptButton != null) && (ScriptArgButton != null) && (ScriptArgsTextBox != null));
			for (i = 0; i < ScriptCount; i++) {
				key = "Script" + i;
				SettingsStore.Write(key, ScriptButton[i].Tag.ToString());
				key = "Args" + i;
				SettingsStore.Write(key, ScriptArgsTextBox[i].Text);
				ScriptButton[i].Click -= ScriptButton_Click;
				ScriptArgButton[i].Click -= ScriptArgButton_Click;
				ScriptButton[i].DragDrop -= ScriptButton_DragDrop;
				ScriptButton[i].DragEnter -= Button_DragEnter;
			}
			UiTimer.Stop();
		}

		public void GetSettings() {
			string key;
			int i;
			Debug.Assert((SettingsStore != null) && (ScriptButton != null) && (ScriptArgButton != null) && (ScriptArgsTextBox != null));
			for (i = 0; i < ScriptCount; i++) {
				key = "Script" + i;
				SettingsStore.Write(key, ScriptButton[i].Tag.ToString());
				key = "Args" + i;
				SettingsStore.Write(key, ScriptArgsTextBox[i].Text);
			}
		}

		public void HideOccluded() {
			for (int i = 0; i < ScriptCount; i++) {
				if (ScriptButton[i].Bottom < ScriptGroupBox.Height) {
					ScriptButton[i].Visible = true;
					ScriptArgButton[i].Visible = true;
				} else {
					ScriptButton[i].Visible = false;
					ScriptArgButton[i].Visible = false;
				}
			}
		}

		private void UiTimer_Tick(object sender, EventArgs e) {
			UpdateButtonStates();
		}

		public void EnableButtons() {
			int i;
			Debug.Assert((ScriptButton != null) && (ScriptArgButton != null));
			ScriptGroupBox.Invoke(new Action(delegate {
				for (i = 0; i < ScriptCount; i++) {
					ScriptButton[i].Enabled = true;
					ScriptArgButton[i].Enabled = true;
					if (ScriptButton[i].Tag != null) {
						if (ScriptButton[i].Tag.ToString() != "") {
							ScriptButton[i].Text = Path.GetFileName(ScriptButton[i].Tag.ToString());
						} else {
							ScriptButton[i].Text = "script" + i;
						}
					} else {
						ScriptButton[i].Text = "script" + i;
					}
				}
			}));
		}

		public void DisableButtons() {
			int i;
			Debug.Assert((ScriptButton != null) && (ScriptArgButton != null));
			for (i = 0; i < ScriptCount; i++) {
				ScriptButton[i].Enabled = false;
				ScriptArgButton[i].Enabled = false;
			}
		}

		public void UpdateButtonStates() {
			int i;
			Debug.Assert(ScriptButton != null);
			for (i = 0; i < ScriptCount; i++) {
				if ((ScriptButton[i].Tag == null) || (ScriptButton[i].Tag.ToString() == "")) {
					ScriptButton[i].ForeColor = Color.Red;
				} else if (!File.Exists(ScriptButton[i].Tag.ToString())) {
					ScriptButton[i].ForeColor = Color.Red;
				} else {
					ScriptButton[i].ForeColor = Color.Green;
				}
				if (ScriptButton[i].Text == "Abort") ScriptButton[i].Enabled = true;
			}
		}

		protected void ScriptButton_DragDrop(object? sender, DragEventArgs? e) {
			Debug.Assert(e != null && e.Data != null);
			string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
			Button? btn = sender as Button;
			Debug.Assert(btn != null);

			ScriptButton_DragDrop_Handler(btn, files[0]);
		}

		protected void ScriptButton_DragDrop_Handler(Button btn, string fileName) {
			string shortName, longName;
			longName = fileName;
			shortName = Path.GetFileName(longName);
			btn.Text = shortName;
			btn.Tag = longName;
			ButtonToolTip?.SetToolTip(btn, longName);
			if (SettingsChangedCallBack != null) SettingsChangedCallBack();
		}

		private void ScriptButton_Click(object sender, EventArgs e) {
			Button btn = sender as Button;
			if (!IsScriptBusy) {
				if (RunScriptInButton(btn)) {
					IsScriptBusy = true;
					string scriptName = Path.GetFileName((string)btn.Tag);
					LogIndex = LogCallBack(CallBacks.LOG_ADD, String.Format("Start Script {0}", scriptName), CallBacks.LOG_INFO, 0);
					DisableButtons();
				}
			} else {
				// kill script here
				LogCallBack(CallBacks.LOG_SUB, "Requesting script abort", CallBacks.LOG_WARNING, 0);
				ScriptingBase.AbortScript();
			}
		}

		protected void ScriptExitHandler(object? sender, EventArgs? e) {
			EnableButtons();
			IsScriptBusy = false;
		}

		protected void ScriptArgButton_Click(object? sender, EventArgs? e) {
			Button? btn = sender as Button;
			Debug.Assert(ScriptArgsTextBox != null);
			Debug.Assert(btn != null);
			char[] delims = new char[] { '_' };
			string[] tokens = btn.Name.Split(delims);
			int btnNum = Int32.Parse(tokens[1]);
			ScriptArgsTextBox[btnNum].Visible = !ScriptArgsTextBox[btnNum].Visible;
			if (SettingsChangedCallBack != null) SettingsChangedCallBack();
		}

		protected void Button_DragEnter(object? sender, DragEventArgs? e) {
			Debug.Assert(e != null && e.Data != null);
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
				e.Effect = DragDropEffects.Copy;
			else
				e.Effect = DragDropEffects.None;
		}

		public bool RunScriptInButton(Button btn) {
			char[] delims = new char[] { '_' };
			char[] argd = new char[] { ' ' };

			Debug.Assert(ScriptArgsTextBox != null);
			string[] tokens = btn.Name.Split(delims);
			int btnNum = Int32.Parse(tokens[1]);
			if (btn.Tag == null) return false;
			if (btn.Tag.ToString() == "") return false;
			btn.Text = "Abort";
			return ScriptingBase.RunScriptByName(btn.Tag.ToString(), ScriptArgsTextBox[btnNum].Text);
		}
	}



	public class ScriptGridManager {
		public ScriptingHostBase ScriptingBase;
		public bool HasScriptingError;
		public bool IsScriptBusy = false;
		public string? ScriptText;
		public string ScriptPath;
		public int ScriptCount = 4;
		public string[]? ScriptFile;
		public string[]? ScriptArgs;
		public string[]? ScriptSearchPaths;
		public string? ScriptBaseDirectory;
		protected DataGridView ScriptGridView;
		protected XmlSettings? SettingsStore;
		protected ToolTip? ButtonToolTip;
		protected Form BaseParent;
		public Image BlankImage, ClearImage;
		public Func<int, string, int, int, int> LogCallBack;
		public int LogIndex;
		protected System.Windows.Forms.Timer UiTimer;
		public Action? SettingsChangedCallBack;
		public const int COL_DRAG = 0;
		public const int COL_NAME = 1;
		public const int COL_CLR = 2;
		public const int COL_ARGS = 3;
		public const int COL_PATH = 4;
		public const int COL_ACT = 5;

		public ScriptGridManager(Form baseParent, ScriptingHostBase scriptingBase, XmlSettings settingsStore) {
			BaseParent = baseParent;
			ScriptingBase = scriptingBase;
			SettingsStore = settingsStore;
			UiTimer = new System.Windows.Forms.Timer();
			UiTimer.Interval = 250;
			UiTimer.Tick += UiTimer_Tick;
			ScriptingBase.ScriptExitHandler += ScriptExitHandler;
			SettingsChangedCallBack = null;
			ScriptPath = "";
		}

		public void Construct(DataGridView gridView, int scriptCount) {
			int i;
			char[] delims = new char[] { '~' };
			string[] args;
			string[] scripts;
			string key;
			string[] rowTexts;

			ScriptGridView = gridView;
			ScriptGridView.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
			ScriptGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
			ScriptGridView.RowHeadersVisible = false;
			ScriptGridView.DoubleBuffered(true);
			ScriptGridView.AllowUserToAddRows = false;
			ScriptGridView.AllowUserToDeleteRows = false;
			ScriptGridView.AllowUserToResizeColumns = false;
			ScriptGridView.AllowUserToResizeRows = false;
			ScriptGridView.BackgroundColor = Color.White;
			ScriptGridView.DefaultCellStyle.BackColor = Color.White;
			ScriptGridView.DefaultCellStyle.ForeColor = Color.Black;
			ScriptGridView.DefaultCellStyle.SelectionBackColor = Color.WhiteSmoke;
			ScriptGridView.DefaultCellStyle.SelectionForeColor = Color.DarkCyan;
			DataGridViewImageColumn idxCol = new DataGridViewImageColumn();
			DataGridViewTextBoxColumn nameCol = new DataGridViewTextBoxColumn();
			DataGridViewImageColumn clrCol = new DataGridViewImageColumn();
			DataGridViewTextBoxColumn argsCol = new DataGridViewTextBoxColumn();
			DataGridViewTextBoxColumn pathCol = new DataGridViewTextBoxColumn();
			DataGridViewTextBoxColumn runCol = new DataGridViewTextBoxColumn();
			idxCol.ReadOnly = true;
			idxCol.Width = 20;
			idxCol.Resizable = DataGridViewTriState.False;
			idxCol.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
			clrCol.ReadOnly = true;
			clrCol.Width = 20;
			clrCol.Resizable = DataGridViewTriState.False;
			clrCol.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
			nameCol.Name = "Script";
			nameCol.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
			pathCol.Visible = false;
			argsCol.Visible = false;
			runCol.Visible = false;
			ScriptGridView.Columns.Add(idxCol);
			ScriptGridView.Columns.Add(nameCol);
			ScriptGridView.Columns.Add(clrCol);
			ScriptGridView.Columns.Add(argsCol);
			ScriptGridView.Columns.Add(pathCol);
			ScriptGridView.Columns.Add(runCol);
			ScriptCount = scriptCount;
			ScriptFile = new string[ScriptCount];
			args = new string[ScriptCount];
			scripts = new string[ScriptCount];
			for (i = 0; i < ScriptCount; i++) {
				key = "Script" + i;
				scripts[i] = SettingsStore.Read(key, "");
				key = "Args" + i;
				args[i] = SettingsStore.Read(key, "");
			}
			for (i = 0; i < ScriptCount; i++) {
				rowTexts = new string[] { null, Path.GetFileName(scripts[i]), null, args[i], scripts[i], "F" };
				ScriptGridView.Rows.Add(rowTexts);
				((DataGridViewImageCell)ScriptGridView.Rows[i].Cells[COL_DRAG]).Value = BlankImage;
			}
			ScriptGridView.AllowDrop = true;
			ScriptGridView.DragDrop += ScriptGridView_DragDrop;
			ScriptGridView.DragOver += ScriptGridView_DragOver;
			ScriptGridView.MouseDown += ScriptGridView_MouseDown;
			ScriptGridView.MouseMove += ScriptGridView_MouseMove;
			ScriptGridView.MouseUp += ScriptGridView_MouseUp;
			ScriptGridView.SortCompare += ScriptGridView_SortCompare;
			ScriptGridView.CellMouseClick += ScriptGridView_CellMouseClick;
			ScriptGridView.CellMouseDoubleClick += ScriptGridView_CellMouseDoubleClick;
			UiTimer.Start();
		}

		public void Deconstruct() {
			string key;
			int i;
			Debug.Assert(SettingsStore != null);
			for (i = 0; i < ScriptCount; i++) {
				key = "Script" + i;
				SettingsStore.Write(key, ScriptGridView.Rows[i].Cells[COL_PATH].Value.ToString());
				key = "Args" + i;
				SettingsStore.Write(key, ScriptGridView.Rows[i].Cells[COL_ARGS].Value.ToString());
			}
			UiTimer.Stop();
		}

		public void GetSettings() {
			string key;
			int i;
			Debug.Assert(SettingsStore != null);
			for (i = 0; i < ScriptCount; i++) {
				key = "Script" + i;
				SettingsStore.Write(key, ScriptGridView.Rows[i].Cells[COL_PATH].Value.ToString());
				key = "Args" + i;
				SettingsStore.Write(key, ScriptGridView.Rows[i].Cells[COL_ARGS].Value.ToString());
			}
		}

		private void UiTimer_Tick(object sender, EventArgs e) {
			UpdateButtonStates();
		}

		public void UpdateIfChanged(DataGridViewCell cell, string text, Color fore, Color back) {
			if (cell.Value != text) { cell.Value = text; }
			if (cell.Style.ForeColor != fore) { cell.Style.ForeColor = fore; }
			if (cell.Style.BackColor != back) { cell.Style.BackColor = back; }
		}
	
		public void UpdateButtonStates() {
			int i;
			DataGridViewRow row;
			string name;
			for (i = 0; i < ScriptCount; i++) {
				row = ScriptGridView.Rows[i];
				name = Path.GetFileName(row.Cells[COL_PATH].Value.ToString());
				if (IsScriptBusy) {
					if (row.Cells[COL_ACT].Value == "T") {
						UpdateIfChanged(row.Cells[COL_NAME], "Abort", Color.Red, Color.White);
					} else {
						UpdateIfChanged(row.Cells[COL_NAME], name, Color.Black, Color.LightGray);
					}
				} else {
					if ((row.Cells[COL_PATH].Value == null) || (row.Cells[COL_PATH].Value.ToString() == "")) {
						UpdateIfChanged(row.Cells[COL_NAME], name, Color.Red, Color.White);
					} else if (!File.Exists(row.Cells[COL_PATH].Value.ToString())) {
						UpdateIfChanged(row.Cells[COL_NAME], name, Color.Red, Color.White);
					} else {
						UpdateIfChanged(row.Cells[COL_NAME], name, Color.Green, Color.White);
					}
				}
				if (row.Cells[COL_PATH].Value != "") ((DataGridViewImageCell)ScriptGridView.Rows[i].Cells[COL_CLR]).Value = ClearImage;
				else ((DataGridViewImageCell)ScriptGridView.Rows[i].Cells[COL_CLR]).Value = BlankImage;
			}
		}

		protected void ScriptButton_DragDrop(object? sender, DragEventArgs? e) {
			Debug.Assert(e != null && e.Data != null);
			string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
			Button? btn = sender as Button;
			Debug.Assert(btn != null);

			ScriptButton_DragDrop_Handler(btn, files[0]);
		}

		protected void ScriptButton_DragDrop_Handler(Button btn, string fileName) {
			string shortName, longName;
			longName = fileName;
			shortName = Path.GetFileName(longName);
			btn.Text = shortName;
			btn.Tag = longName;
			ButtonToolTip?.SetToolTip(btn, longName);
			if (SettingsChangedCallBack != null) SettingsChangedCallBack();
		}

		protected void ScriptExitHandler(object? sender, EventArgs? e) {
			IsScriptBusy = false;
			for (int i = 0; i < ScriptCount; i++) {
				DataGridViewRow row = ScriptGridView.Rows[i];
				if (row.Cells[COL_ACT].Value == "T") row.Cells[COL_ACT].Value = "F";
			}
		}

		private void ScriptButton_Clicked(int index) {
			DataGridViewRow row = ScriptGridView.Rows[index];
			if (row.Cells[COL_PATH].Value == "") return;
			if (!IsScriptBusy) {
				if (RunScriptIndex(index)) {
					IsScriptBusy = true;
					LogIndex = LogCallBack(CallBacks.LOG_ADD, String.Format("Start Script {0}", row.Cells[COL_NAME].Value), CallBacks.LOG_INFO, 0);
					row.Cells[COL_ACT].Value = "T";
				}
			} else {
                if (row.Cells[COL_ACT].Value == "T") {
					// kill script here
					LogCallBack(CallBacks.LOG_SUB, "Requesting script abort", CallBacks.LOG_WARNING, 0);
					ScriptingBase.AbortScript();
				}
			}
		}

		public bool RunScriptIndex(int index) {
			bool success;
			char[] delims = new char[] { '_' };
			char[] argd = new char[] { ' ' };

			DataGridViewRow row = ScriptGridView.Rows[index];
			if (row.Cells[COL_PATH] == null) return false;
			if (row.Cells[COL_PATH].ToString() == "") return false;
			row.Cells[COL_ACT].Value = "T";
			success = ScriptingBase.RunScriptByName(row.Cells[COL_PATH].Value.ToString(), row.Cells[COL_ARGS].Value.ToString());
			if (!success) row.Cells[COL_ACT].Value = "F";
			return success;
		}

		private void ClearButton_Clicked(int index) {
			DataGridViewRow row = ScriptGridView.Rows[index];
			row.Cells[COL_NAME].Value = "";
			row.Cells[COL_PATH].Value = "";
			row.Cells[COL_ARGS].Value = "";
		}


		private void ScriptGridView_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e) {
			DataGridView gv = (DataGridView)sender;
			if (e.RowIndex < 0) return;
			switch (e.ColumnIndex) {
				case COL_DRAG: break;
				case COL_NAME: break;
				case COL_CLR:
					ClearButton_Clicked(e.RowIndex);
					break;
				default: break;
			}
		}

		private void ScriptGridView_CellMouseDoubleClick(object sender, DataGridViewCellMouseEventArgs e) {
			DataGridView gv = (DataGridView)sender;
			if (e.RowIndex < 0) return;
			switch (e.ColumnIndex) {
				case COL_DRAG: break;
				case COL_NAME:
					ScriptButton_Clicked(e.RowIndex);
					break;
				case COL_CLR: break;
				default: break;
			}
		}

		private Rectangle DragBoxFromMouseDown;
		private int RowIndexFromMouseDown;
		private int RowIndexOfItemUnderMouseToDrop;
		public bool PossibleDrag;
		public bool DragDropRearrange;

		private void ScriptGridView_MouseMove(object sender, MouseEventArgs e) {
			if ((e.Button & MouseButtons.Left) == MouseButtons.Left) {
				// If the mouse moves outside the rectangle, start the drag.
				if (PossibleDrag &&
					(DragBoxFromMouseDown != Rectangle.Empty) &&
					!DragBoxFromMouseDown.Contains(e.X, e.Y)) {
					// Proceed with the drag and drop, passing in the list item.					
					DragDropRearrange = true;
					DragDropEffects dropEffect = ScriptGridView.DoDragDrop(
						  ScriptGridView.Rows[RowIndexFromMouseDown],
						  DragDropEffects.Move);
				}
			}
		}
		private void ScriptGridView_MouseDown(object sender, MouseEventArgs e) {
			// Get the index of the item the mouse is below.
			RowIndexFromMouseDown = ScriptGridView.HitTest(e.X, e.Y).RowIndex;
			PossibleDrag = ScriptGridView.HitTest(e.X, e.Y).ColumnIndex == COL_DRAG;
			if (PossibleDrag && (RowIndexFromMouseDown != -1)) {
				// Remember the point where the mouse down occurred. 
				// The DragSize indicates the size that the mouse can move 
				// before a drag event should be started.				
				Size dragSize = SystemInformation.DragSize;

				// Create a rectangle using the DragSize, with the mouse position being
				// at the center of the rectangle.
				DragBoxFromMouseDown = new Rectangle(
						  new Point(
							e.X - (dragSize.Width / 2),
							e.Y - (dragSize.Height / 2)),
					  dragSize);
			} else {
				// Reset the rectangle if the mouse is not over an item in the ListBox.
				DragBoxFromMouseDown = Rectangle.Empty;
			}
		}

		private void ScriptGridView_MouseUp(object sender, MouseEventArgs e) {
			PossibleDrag = false;
		}

		private void ScriptGridView_DragOver(object sender, DragEventArgs e) {
			if (DragDropRearrange) {
				e.Effect = DragDropEffects.Move;
			} else {
				Point clientPoint = ScriptGridView.PointToClient(new Point(e.X, e.Y));
				DataGridView.HitTestInfo hitTest = ScriptGridView.HitTest(clientPoint.X, clientPoint.Y);
				if ((hitTest.ColumnIndex == COL_NAME) &&
					(hitTest.RowIndex >= 0) &&
					(hitTest.RowIndex < ScriptCount)) {
					e.Effect = DragDropEffects.Copy;
				} else {
					e.Effect = DragDropEffects.None;
				}
			}
		}

		private void ScriptGridView_DragDrop(object sender, DragEventArgs e) {
			Point clientPoint = ScriptGridView.PointToClient(new Point(e.X, e.Y));
			DataGridView.HitTestInfo hitTest = ScriptGridView.HitTest(clientPoint.X, clientPoint.Y);
			if (e.Data.GetDataPresent(DataFormats.FileDrop, false)) {
				if (hitTest.ColumnIndex == COL_NAME) {
					string[] paths = (string[])e.Data.GetData(DataFormats.FileDrop);
					if (File.Exists(paths[0])) {
						ScriptGridView.Rows[hitTest.RowIndex].Cells[COL_PATH].Value = paths[0];
					}
				}
			} else if (e.Data.GetDataPresent(typeof(DataGridViewRow))) {
				// The mouse locations are relative to the screen, so they must be 
				// converted to client coordinates.
				string[] rowItem;
				if (ScriptGridView.RowCount < 3) return;
				// Get the row index of the item the mouse is below. 
				RowIndexOfItemUnderMouseToDrop = hitTest.RowIndex;
				if (RowIndexOfItemUnderMouseToDrop < 0) {
					RowIndexOfItemUnderMouseToDrop = 0;
				} else if (RowIndexOfItemUnderMouseToDrop > ScriptGridView.RowCount - 2) {
					RowIndexOfItemUnderMouseToDrop = ScriptGridView.RowCount - 2;
				}
				// If the drag operation was a move then remove and insert the row.
				DragDropRearrange = true;
				if (e.Effect == DragDropEffects.Move) {
					DataGridViewRow rowToMove = e.Data.GetData(typeof(DataGridViewRow)) as DataGridViewRow;
					ScriptGridView.Rows.RemoveAt(RowIndexFromMouseDown);
					ScriptGridView.Rows.Insert(RowIndexOfItemUnderMouseToDrop, rowToMove);
				}
				DragDropRearrange = false;
			}
		}

		private void ScriptGridView_SortCompare(object sender, DataGridViewSortCompareEventArgs e) {
			int order = 0;
			if (ScriptGridView.SortOrder == SortOrder.Ascending) order = 1;
			else if (ScriptGridView.SortOrder == SortOrder.Descending) order = -1;
			else order = 0;
			if (e.Column.Index == COL_NAME) {
				if ((e.CellValue1.ToString() == "")&& (e.CellValue2.ToString() == "")) e.SortResult = 0;
				else if (e.CellValue1.ToString() == "") e.SortResult = 1 * order;
				else if (e.CellValue2.ToString() == "") e.SortResult = -1 * order;
				else e.SortResult = String.Compare(e.CellValue1.ToString(), e.CellValue2.ToString());
			} else e.SortResult = 0;
			e.Handled = true;
		}
	}

	public class AsciiBox {
		RichTextBox ClientTextBox;
		RoundedRichTextBox ClientTextBoxRounded;
		Size Extent;
		char[,] TextBuffer;
		Color[,] ColorBuffer;
		public Font DisplayFont = new Font("Lucida Console", 12);

		public class AsciiChar {
			public char Char;
			public Color Color;
			public AsciiChar() {
				Char = ' ';
				Color = Color.Black;
			}
		}

		public AsciiBox(RichTextBox targetBox) {
			ClientTextBox = targetBox;
			ClientTextBoxRounded = null;
			ClientTextBox.Font = DisplayFont;
		}

		public AsciiBox(RoundedRichTextBox targetBox) {
			ClientTextBox = null;
			ClientTextBoxRounded = targetBox;
			ClientTextBoxRounded.Font = DisplayFont;
		}

		public void InitArray() {
			int x, y;
			for (y = 0; y < Extent.Height; y++) {
				for (x = 0; x < Extent.Width; x++) {
					//TextBuffer[y, x] = new AsciiChar();
					TextBuffer[y, x] = ' ';
				}
			}
		}

		public void ClearText() {
			int x, y;
			for (y = 0; y < Extent.Height; y++) {
				for (x = 0; x < Extent.Width; x++) {
					//TextBuffer[y, x].Char = ' ';
					TextBuffer[y, x] = ' ';
				}
			}
		}

		public void ClearColor(Color clr) {
			int x, y;
			for (y = 0; y < Extent.Height; y++) {
				for (x = 0; x < Extent.Width; x++) {
					//TextBuffer[y, x].Color = clr;
				}
			}
		}

		public Size BoxSize {
			get {
				return Extent;
			}
			set {
				Extent = value;
				TextBuffer = new char[Extent.Height, Extent.Width];
				InitArray();
			}
		}

		public void WriteXY(int x, int y, char c) {
			//TextBuffer[y, x].Char = c;
			TextBuffer[y, x] = c;
		}


		public void WriteXY(int x, int y, string s) {
			int k;
			int len = s.Length;
			if (y >= Extent.Height) return;
			for (k = 0; k < len; k++) {
				if ((k + x) >= Extent.Width) continue;
				TextBuffer[y, k + x] = s[k];
			}
		}

		public void Update() {
			char[] lineArray;
			int x, y;
			StringBuilder sb = new StringBuilder();
			ClientTextBox?.Invoke(new Action(delegate {
				for (y = 0; y < Extent.Height; y++) {
					lineArray = new char[Extent.Width];
					for (x = 0; x < Extent.Width; x++) {
						lineArray[x] = TextBuffer[y, x];
					}
					sb.Append(new string(lineArray));
					sb.Append('\n');
				}
				ClientTextBox.Text = sb.ToString();
			}));
			ClientTextBoxRounded?.Invoke(new Action(delegate {
				for (y = 0; y < Extent.Height; y++) {
					lineArray = new char[Extent.Width];
					for (x = 0; x < Extent.Width; x++) {
						lineArray[x] = TextBuffer[y, x];
					}
					sb.Append(new string(lineArray));
					sb.Append('\n');
				}
				ClientTextBoxRounded.Text = sb.ToString();
			}));
		}
	}

	public class DataTable {
		DataGridView GridView;
		RoundedDataGridView GridViewRounded;
		Size Extent;

		public DataTable(DataGridView targetGrid) {
			GridView = targetGrid;
			GridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
			GridViewRounded = null;
		}

		public DataTable(RoundedDataGridView targetGrid) {
			GridViewRounded = targetGrid;
			GridViewRounded.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
			GridView = null;
		}

		public void GridSize(int rows, int cols) {
			int j;
			string[] entries;
			GridView?.Invoke(new Action(delegate {
				GridView.Rows.Clear();
				GridView.ColumnCount = cols;
				for (j = 0; j < rows; j++) {
					entries = new string[cols];
					GridView.Rows.Add(entries);
				}
			}));
			GridViewRounded?.Invoke(new Action(delegate {
				GridViewRounded.Rows.Clear();
				GridViewRounded.ColumnCount = cols;
				for (j = 0; j < rows; j++) {
					entries = new string[cols];
					GridViewRounded.Rows.Add(entries);
				}
			}));
			Extent = new Size(cols, rows);
		}

		public void SetText(int row, int col, string text, Color fore, Color back) {
			if (row >= Extent.Height) return;
			if (col >= Extent.Width) return;
			GridView?.Invoke(new Action(delegate {
				GridView.Rows[row].Cells[col].Value = text;
				GridView.Rows[row].Cells[col].Style = new DataGridViewCellStyle { ForeColor = fore, BackColor = back };
			}));
			GridViewRounded?.Invoke(new Action(delegate {
				GridViewRounded.Rows[row].Cells[col].Value = text;
				GridViewRounded.Rows[row].Cells[col].Style = new DataGridViewCellStyle { ForeColor = fore, BackColor = back };
			}));
		}
	}

}
