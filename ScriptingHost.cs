using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CustomLibs;
using CustomExtensions;
//using static System.Net.Mime.MediaTypeNames;

// UnifiedProxyControlBase is shared by both the parent application
// and the ScriptingHost

namespace Scripting {
	public class UnifiedProxyControlBase {
		public string[]? ScriptArgs;
		public Func<int, string, int, int, int> LogCallBack;
		public Action<int, int> ProgressCallBack;
		public Action? AbortOnError; // used by script to indicate error
		public bool ShouldAbortAndCleanuping;
		public int LogParentIndex;

		/////////////////////////////////////////////////
		///////////    General Text and UI    ///////////
		/////////////////////////////////////////////////

		// Get script arguments from the UI
		public string[]? Args {
			get {
				return ScriptArgs;
			}
		}

		// Add a message to the application logger
		public void AddLogInfo(string text) {
			if (LogCallBack != null) LogCallBack(CallBacks.LOG_SUB, text, CallBacks.LOG_INFO, LogParentIndex);
		}

		public void AddLogWarning(string text) {
			if (LogCallBack != null) LogCallBack(CallBacks.LOG_SUB, text, CallBacks.LOG_WARNING, LogParentIndex);
		}

		public void AddLogError(string text) {
			if (LogCallBack != null) LogCallBack(CallBacks.LOG_SUB, text, CallBacks.LOG_ERROR, LogParentIndex);
		}

		// Manipulate the progress bar
		public void SetProgressIndicator(int value, int max) {
			if (ProgressCallBack != null) ProgressCallBack(value, max);
		}

	}


	// ScriptingHostBase needs to be inherited by a class to fill out the
	// CsScriptWorker abstract method
	public abstract class ScriptingHostBase {
		public bool HasScriptingError;
		private Thread? ScriptThread;
		public bool IsScriptBusy = false;
		protected string? ScriptText;
		//public string[]? ScriptFile;
		protected string[]? ScriptArgs;
		protected string[]? ScriptSearchPaths;
		protected string? ScriptBaseDirectory;
		protected UnifiedProxyControlBase? ProxyControl;
		protected EventHandler? ClickHandler;
		public Func<int, string, int, int, int> LogCallBack;
		public int LogParentIndex;
		protected System.Timers.Timer ThreadMonitorTimer;
		public EventHandler? ScriptExitHandler;

		public ScriptingHostBase() {
			ThreadMonitorTimer = new System.Timers.Timer();
			ThreadMonitorTimer.AutoReset = true;
			ThreadMonitorTimer.Enabled = true;
			ThreadMonitorTimer.Elapsed += ThreadMonitorTimer_Tick;
			ThreadMonitorTimer.Interval = 1000;
		}

		//private void ThreadMonitorTimer_Tick(object sender, EventArgs e) {
		private void ThreadMonitorTimer_Tick(object sender, System.Timers.ElapsedEventArgs e) {
			if (ScriptThread == null) return;
			if (!ScriptThread.IsAlive) {
				if (HasScriptingError) {
					if (LogCallBack != null) LogCallBack(CallBacks.LOG_SUB, "Script ended with errors", CallBacks.LOG_WARNING, LogParentIndex); 
				} else {
					if (LogCallBack != null) LogCallBack(CallBacks.LOG_SUB, "Script completed", CallBacks.LOG_INFO, LogParentIndex);
				}
				ThreadMonitorTimer.Stop();
				IsScriptBusy = false;
				ScriptExitHandler?.Invoke(this, new EventArgs());
			}
		}

		public bool RunScriptByNameBlocking(string? fileName, string args) {
			if (RunScriptByName(fileName, args)) {
				WaitForWorker();
				return true;
			} else return false;
		}

		public bool RunScriptByName(string? fileName, string args) {
			char[] argd = new char[] { ' ' };
			Debug.Assert(ProxyControl != null);
			Debug.Assert(fileName != null);
			IsScriptBusy = true;
			HasScriptingError = false;
			ProxyControl.ShouldAbortAndCleanuping = false;
			ScriptArgs = args.Split(argd);
			ProxyControl.ScriptArgs = ScriptArgs;
			ProxyControl.AbortOnError = AbortScriptWithError;
			// start script here
			ScriptSearchPaths = new string[] { };
			ScriptBaseDirectory = Path.GetDirectoryName(fileName);
			try {
				ScriptText = File.ReadAllText(fileName);
			} catch (IOException ex) {
				if (LogCallBack != null) LogCallBack(CallBacks.LOG_SUB, ex.Message, CallBacks.LOG_ERROR, LogParentIndex);
				HasScriptingError = true;
				return false;
			}
			ScriptThread = new Thread(CsScriptWorker);
			ScriptThread.Start();
			ThreadMonitorTimer.Start();
			return true;
		}

		public bool RunScriptByTextBlocking(string scriptText, string args) {
			if (RunScriptByText(scriptText, args)) {
				WaitForWorker();
				return true;
			}
			return false;
		}

		public bool RunScriptByText(string scriptText, string args) {
			char[] argd = new char[] { ' ' };
			IsScriptBusy = true;
			HasScriptingError = false;
			ProxyControl.ShouldAbortAndCleanuping = false;
			ScriptArgs = args.Split(argd);
			ProxyControl.ScriptArgs = ScriptArgs;
			ProxyControl.AbortOnError = AbortScriptWithError;
			ScriptText = scriptText;
			ScriptThread = new Thread(CsScriptWorker);
			ScriptThread.Start();
			return true;
		}

		public void WaitForWorker() {
			if (ScriptThread != null) {
				if (ScriptThread.IsAlive) {
					while (ScriptThread.IsAlive) {
						Thread.Sleep(100);
						Application.DoEvents();
					}

				}
			}
		}

		public void AbortScript() {
			Debug.Assert(ProxyControl != null);
			ProxyControl.ShouldAbortAndCleanuping = true;
			if (ScriptThread != null) {
				if (ScriptThread.IsAlive) {
					try {
						ScriptThread.Abort();
					} catch (PlatformNotSupportedException ex) {
						// nothing doing
						string buf = ex.Message;
					}
					while (ScriptThread.IsAlive) {
						Thread.Sleep(100);
						Application.DoEvents();
					}

				}
			}
			IsScriptBusy = false;
		}

		public void AbortScriptWithError() {
			HasScriptingError = true;
		}

		protected string FindCodeFragmentFromError(string errorMsg) {
			string linenumtxt;
			int c, linenum;

			Debug.Assert(ScriptText != null);
			if (errorMsg[0] != '(') return "";
			for (c = 1; c < errorMsg.Length; c++) {
				if (errorMsg[c] == ',') break;
			}
			if (c == errorMsg.Length) return "";
			linenumtxt = errorMsg.Substring(1, c - 1);
			linenum = Convert.ToInt32(linenumtxt);
			string[] scriptlines = ScriptText.Split(new char[] { '\n' });
			return "Line " + linenum + ": " + scriptlines[linenum - 1].Trim();
		}

		protected string ParseStackTrace(string trace) {
			Debug.Assert(ScriptText != null);
			string ns = GetString(@"in :line ([0-9]{1,5})\r\n", ref trace);
			if (ns != "") {
				int linenum = Int32.Parse(ns);
				string[] scriptlines = ScriptText.Split(new char[] { '\n' });
				return "Line " + linenum + ": " + scriptlines[linenum - 1].Trim();
			} else {
				return "";
			}
		}

		public static string GetString(string searchstring, ref string blob) {
			string value;

			Regex rgx = new Regex(searchstring);
			Match match = rgx.Match(blob);
			if (match.Success) {
				value = rgx.Replace(match.Value, "$1");
				return value;
			} else {
				return "";
			}
		}

		public abstract void CsScriptWorker();

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
				ScriptButton[i].Click += ScriptButton_Click;
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


		private void ScriptGridView_CellClick(object sender, DataGridViewCellEventArgs e) {
			DataGridView gv = (DataGridView)sender;
			switch (e.ColumnIndex) {
				case COL_NAME:
					ScriptButton_Clicked(e.RowIndex);
					break;
				case COL_CLR:
					ClearButton_Clicked(e.RowIndex);
					break;
				default: break;
			}
		}

		private void ScriptGridView_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e) {
			DataGridView gv = (DataGridView)sender;
			if (e.RowIndex < 0) return;
			switch (e.ColumnIndex) {
				case COL_NAME:
					ScriptButton_Clicked(e.RowIndex);
					break;
				case COL_CLR:
					ClearButton_Clicked(e.RowIndex);
					break;
				default: break;
			}
		}

		private Rectangle DragBoxFromMouseDown;
		private int RowIndexFromMouseDown;
		private int RowIndexOfItemUnderMouseToDrop;
		public bool IsPossibleDrag;
		public bool IsDragDropRearrange;

		private void ScriptGridView_MouseMove(object sender, MouseEventArgs e) {
			if ((e.Button & MouseButtons.Left) == MouseButtons.Left) {
				// If the mouse moves outside the rectangle, start the drag.
				if (IsPossibleDrag &&
					(DragBoxFromMouseDown != Rectangle.Empty) &&
					!DragBoxFromMouseDown.Contains(e.X, e.Y)) {
					// Proceed with the drag and drop, passing in the list item.					
					IsDragDropRearrange = true;
					DragDropEffects dropEffect = ScriptGridView.DoDragDrop(
						  ScriptGridView.Rows[RowIndexFromMouseDown],
						  DragDropEffects.Move);
				}
			}
		}
		private void ScriptGridView_MouseDown(object sender, MouseEventArgs e) {
			// Get the index of the item the mouse is below.
			RowIndexFromMouseDown = ScriptGridView.HitTest(e.X, e.Y).RowIndex;
			IsPossibleDrag = ScriptGridView.HitTest(e.X, e.Y).ColumnIndex == COL_DRAG;
			if (IsPossibleDrag && (RowIndexFromMouseDown != -1)) {
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
			IsPossibleDrag = false;
		}

		private void ScriptGridView_DragOver(object sender, DragEventArgs e) {
			if (IsDragDropRearrange) {
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
				IsDragDropRearrange = true;
				if (e.Effect == DragDropEffects.Move) {
					DataGridViewRow rowToMove = e.Data.GetData(typeof(DataGridViewRow)) as DataGridViewRow;
					ScriptGridView.Rows.RemoveAt(RowIndexFromMouseDown);
					ScriptGridView.Rows.Insert(RowIndexOfItemUnderMouseToDrop, rowToMove);
				}
				IsDragDropRearrange = false;
			}
		}

		private void ScriptGridView_SortCompare(object sender, DataGridViewSortCompareEventArgs e) {
			int order = 0;
			if (ScriptGridView.SortOrder == SortOrder.Ascending) order = 1;
			else if (ScriptGridView.SortOrder == SortOrder.Descending) order = -1;
			else order = 0;
			if (e.Column.Index == COL_NAME) {
				if ((e.CellValue1.ToString() == "") && (e.CellValue2.ToString() == "")) e.SortResult = 0;
				else if (e.CellValue1.ToString() == "") e.SortResult = 1 * order;
				else if (e.CellValue2.ToString() == "") e.SortResult = -1 * order;
				else e.SortResult = String.Compare(e.CellValue1.ToString(), e.CellValue2.ToString());
			} else e.SortResult = 0;
			e.Handled = true;
		}
	}

}