using System.Text;
using System.Globalization;
using System.Text.RegularExpressions;
using CustomControls;

// RoboRename v1.2.2:	Add a testing field to rule editor form
// FolderBot v1.9.8:	Enable checkbox function

namespace CustomForms {

	public partial class RuleEditForm : Form {

		public class RulePair {
			public bool IsEnabled;
			public string Search;
			public string Replace;

			public RulePair(string search, string replace) {
				IsEnabled = true;
				Search = search;
				Replace = replace;
			}
		}

		private Rectangle DragBoxFromMouseDown;
		private int RowIndexFromMouseDown;
		private int RowIndexOfItemUnderMouseToDrop;
		private bool IsEditingCellsNow = false;
		const int COL_INDEX = 0;
		const int COL_ENABLE = 1;
		const int COL_ORIG = 2;
		const int COL_SPACE = 3;
		const int COL_NEW = 4;
		public LogAndStatusManager LogAndStatus;
		public VisualParams Visuals;

		public RuleEditForm() {
			InitializeComponent();
			Visuals = new VisualParams();
		}

		private void ReNumber() {
			int c = RuleDataGridView.RowCount;
			for (int i = 0; i < c; i++) {
				RuleDataGridView.Rows[i].Cells[COL_INDEX].Value = i.ToString();
				if (RuleDataGridView.Rows[i].Cells[COL_ENABLE].Value == null) {
					RuleDataGridView.Rows[i].Cells[COL_ENABLE].Value = true;
				}
			}
		}

		private void RuleEditForm_Load(object sender, EventArgs e) {
			RuleToolTip.SetToolTip(RuleDataGridView,
				"[pre] - prefix" + Environment.NewLine +
				"[post] - postfix" + Environment.NewLine +
				"[cap] - capitalize" + Environment.NewLine +
				 "[regex] - regular expression");
			RuleDataGridView.ClipboardCopyMode = DataGridViewClipboardCopyMode.Disable;
			RuleDataGridView.Visuals = Visuals;
		}

		public List<RulePair> GetRules() {
			int i, c;
			string search, replace;
			RulePair rule;

			List<RulePair> ruleList = new List<RulePair>();
			c = RuleDataGridView.RowCount;
			for (i = 0; i < c; i++) {
				if (RuleDataGridView.Rows[i].Cells[COL_ORIG].Value == null) continue;
				else search = RuleDataGridView.Rows[i].Cells[COL_ORIG].Value.ToString();
				if (RuleDataGridView.Rows[i].Cells[COL_NEW].Value == null) replace = "";
				else replace = RuleDataGridView.Rows[i].Cells[COL_NEW].Value.ToString();
				rule = new RulePair(search, replace);
				if (RuleDataGridView.Rows[i].Cells[COL_ENABLE].Value == null) rule.IsEnabled = false;
				else rule.IsEnabled = (bool)RuleDataGridView.Rows[i].Cells[COL_ENABLE].Value;
				ruleList.Add(rule);
			}
			return ruleList;
		}

		public void SetRules(List<RulePair> rules) {
			int i, c;
			object[] row;

			RuleDataGridView.Rows.Clear();
			c = rules.Count;

			for (i = 0; i < c; i++) {
				row = new object[] { i.ToString(), rules[i].IsEnabled, rules[i].Search, null, rules[i].Replace };
				RuleDataGridView.Rows.Add(row);
			}
			UpdateRegexText();
		}

		private void RuleDataGridView_MouseMove(object sender, MouseEventArgs e) {
			if ((e.Button & MouseButtons.Left) == MouseButtons.Left) {
				// If the mouse moves outside the rectangle, start the drag.
				if (DragBoxFromMouseDown != Rectangle.Empty &&
				!DragBoxFromMouseDown.Contains(e.X, e.Y)) {
					// Proceed with the drag and drop, passing in the list item.					
					DragDropEffects dropEffect = RuleDataGridView.DoDragDrop(
						  RuleDataGridView.Rows[RowIndexFromMouseDown],
						  DragDropEffects.Move);
				}
			}
		}

		private void RuleDataGridView_MouseDown(object sender, MouseEventArgs e) {
			// Get the index of the item the mouse is below.
			RowIndexFromMouseDown = RuleDataGridView.HitTest(e.X, e.Y).RowIndex;

			if (RowIndexFromMouseDown != -1) {
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
			} else
				// Reset the rectangle if the mouse is not over an item in the ListBox.
				DragBoxFromMouseDown = Rectangle.Empty;
		}

		private void RuleDataGridView_DragOver(object sender, DragEventArgs e) {
			e.Effect = DragDropEffects.Move;
		}

		private void RuleDataGridView_DragDrop(object sender, DragEventArgs e) {
			if (e.Data.GetDataPresent(DataFormats.FileDrop, false)) {
			} else if (e.Data.GetDataPresent(typeof(DataGridViewRow))) {
				// The mouse locations are relative to the screen, so they must be 
				// converted to client coordinates.
				Point clientPoint = RuleDataGridView.PointToClient(new Point(e.X, e.Y));
				if (RuleDataGridView.RowCount < 3) return;

				// Get the row index of the item the mouse is below. 
				RowIndexOfItemUnderMouseToDrop = RuleDataGridView.HitTest(clientPoint.X, clientPoint.Y).RowIndex;
				if (RowIndexOfItemUnderMouseToDrop < 0) {
					RowIndexOfItemUnderMouseToDrop = 0;
				} else if (RowIndexOfItemUnderMouseToDrop > RuleDataGridView.RowCount - 2) {
					RowIndexOfItemUnderMouseToDrop = RuleDataGridView.RowCount - 2;
				}

				// If the drag operation was a move then remove and insert the row.
				if (e.Effect == DragDropEffects.Move) {
					DataGridViewRow rowToMove = e.Data.GetData(typeof(DataGridViewRow)) as DataGridViewRow;
					RuleDataGridView.Rows.RemoveAt(RowIndexFromMouseDown);
					RuleDataGridView.Rows.Insert(RowIndexOfItemUnderMouseToDrop, rowToMove);
				}
				ReNumber();
				UpdateRegexText();
			}
		}

		private void InputTextBox_DragOver(object sender, DragEventArgs e) {
			if (e.Data == null) return;
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
				e.Effect = DragDropEffects.Copy;
			else
				e.Effect = DragDropEffects.None;
		}

		private void InputTextBox_DragDrop(object sender, DragEventArgs e) {
			if (e.Data == null) return;
			string[] paths = (string[])e.Data.GetData(DataFormats.FileDrop);
			((RoundedTextBox)sender).Text = paths[0];
		}


		private void CopyToClipboard(bool isTabbed) {
			//Clipboard.Clear();
			StringBuilder sb = new StringBuilder();
			string buf = "";
			int c, i;
			c = RuleDataGridView.RowCount - 1;
			for (i = 0; i < c; i++) {
				sb.Append(RuleDataGridView.Rows[i].Cells[COL_ORIG].Value.ToString());
				if (isTabbed) sb.Append("\t");
				else sb.Append(Environment.NewLine);
				sb.Append(RuleDataGridView.Rows[i].Cells[COL_NEW].Value.ToString() + Environment.NewLine);
			}
			try {
				//Clipboard.SetText("test");
				Clipboard.SetText(sb.ToString());
			} catch (Exception ex) {
				string msg = ex.Message;
			}
		}

		private void PasteFromClipboard(bool isTabbed) {
			string search, replace;
			object[] row;
			string[] tablist;
			int i;
			List<RulePair> pairs = new List<RulePair>();
			TextReader tr = new StringReader(Clipboard.GetText());
			while ((search = tr.ReadLine()) != null) {
				if (search == "") continue;
				if (isTabbed) {
					tablist = search.Split('\t');
					pairs.Add(new RulePair(tablist[0], tablist[1]));
				} else {
					if ((replace = tr.ReadLine()) != null) {
						pairs.Add(new RulePair(search, replace));
					}
				}
			}
			if (pairs.Count > 0) {
				RuleDataGridView.Rows.Clear();
				for (i = 0; i < pairs.Count; i++) {
					row = new object[] { i.ToString(), null, pairs[i].Search, null, pairs[i].Replace };
					RuleDataGridView.Rows.Add(row);
				}
			}
			ReNumber();
			UpdateRegexText();
		}

		private void RuleDataGridView_KeyDown(object sender, KeyEventArgs e) {
			if (!IsEditingCellsNow) {
				switch (e.KeyCode) {
					case Keys.C:
						if (e.Control) {
							CopyToClipboard(e.Shift);
							e.Handled = true;
						}
						break;
					case Keys.V:
						if (e.Control) {
							PasteFromClipboard(e.Shift);
							e.Handled = true;
						}
						break;
					case Keys.Insert:
						int index = RuleDataGridView.SelectedRows[0].Index;
						object[] row = new object[] { null, null, null, null, null };
						RuleDataGridView.Rows.Insert(index, row);
						ReNumber();
						break;
					default: break;
				}
			}
		}

		private void RuleDataGridView_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e) {
			IsEditingCellsNow = true;
		}

		private void RuleDataGridView_CellEndEdit(object sender, DataGridViewCellEventArgs e) {
			IsEditingCellsNow = false;
			UpdateRegexText();
		}

		private void RuleDataGridView_RowsRemoved(object sender, DataGridViewRowsRemovedEventArgs e) {
			ReNumber();
		}

		private void RuleDataGridView_RowsAdded(object sender, DataGridViewRowsAddedEventArgs e) {
			ReNumber();
		}

		private void RuleEditForm_Resize(object sender, EventArgs e) {
			InputTextBox.Top = TestLabel.Bottom + 5;
			ArrowLabel.Top = InputTextBox.Top + 2;
			OutputTextBox.Top = InputTextBox.Top;
			InputTextBox.Width = (RuleDataGridView.Width - ArrowLabel.Width - 10) / 2;
			OutputTextBox.Width = InputTextBox.Width;
			InputTextBox.Left = RuleDataGridView.Left;
			ArrowLabel.Left = InputTextBox.Right + 5;
			OutputTextBox.Left = ArrowLabel.Right + 5;
		}

		public static List<string> ProcessName(string oldName, List<RulePair> ruleList, LogAndStatusManager logAndStatus = null) {
			char[] delimChars = { ' ' };
			char[] trimChars = { ' ', '　' };
			string newName, copy;
			string pattern, replace;
			TextInfo ti = CultureInfo.CurrentCulture.TextInfo;
			List<string> history = new List<string>();
			Regex rgx;

			newName = oldName;
			foreach (RuleEditForm.RulePair rule in ruleList) {
				if (!rule.IsEnabled) continue;
				string[] tokens = rule.Search.Split(delimChars);
				switch (tokens[0].ToLower()) {
					case "[pre]":
						if (newName.IndexOf(rule.Replace) != 0) newName = rule.Replace + newName;
						break;
					case "[post]": newName = newName + rule.Replace; break;
					case "[cap]": newName = ti.ToTitleCase(newName.ToLower()); break;
					case "[trim]":
						rgx = new Regex("[ ]{2,10}");
						newName = rgx.Replace(newName, " ");
						newName = newName.Trim(trimChars);
						break;
					case "[regex]":
						try {
							pattern = rule.Search.Substring(8, rule.Search.Length - 8);
							rgx = new Regex(pattern);
							string[] tokenr = rule.Replace.Split(delimChars);
							if (tokenr[0].ToLower() == "[regex]") {
								replace = rule.Replace.Substring(8, rule.Replace.Length - 8);
							} else {
								replace = rule.Replace;
							}
							newName = rgx.Replace(newName, replace);
						} catch (Exception ex) {
							logAndStatus?.AddLog(ex.Message, ErrorLevel.LOG_WARNING);
						}
						break;
					default:
						try {
							newName = newName.Replace(rule.Search, rule.Replace);
						} catch (Exception ex) {
							logAndStatus?.AddLog(ex.Message, ErrorLevel.LOG_WARNING);
						}
						break;
				}
				history.Add(newName);
			}
			return history;
		}

		private void UpdateRegexText() {
			if (InputTextBox.Text.Trim().Length == 0) {
				OutputTextBox.Text = "";
				return;
			}
			string stub = Path.GetFileNameWithoutExtension(InputTextBox.Text);
			string ext = Path.GetExtension(InputTextBox.Text);
			List<string> newNames = ProcessName(stub, GetRules());
			if (newNames.Count > 0) {
				OutputTextBox.Text = newNames.Last() + ext;
			} else {
				OutputTextBox.Text = "Regex error";
			}
		}

		private void InputTextBox_TextChanged(object sender, EventArgs e) {
			UpdateRegexText();
		}

		private void RuleDataGridView_CellContentClick(object sender, DataGridViewCellEventArgs e) {
			if (e.RowIndex < 0) return;
			if (e.ColumnIndex == COL_ENABLE) {
				//sequence.IsEnabled = (bool)senderGrid.Rows[e.RowIndex].Cells[COL_ENABLE].EditedFormattedValue;
			}
		}
	}
}

