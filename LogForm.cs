using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using CustomControls;


// Beowulf v3.3.5:	Change log+status display to deferred queue
//					Add update of existing line in listview
//					Add subline to ERRORLEVEL for follow-up errors
//					Remove wrapping mode
//					Speed up clipboard copy
// Beowulf v3.4.5:	UpdateEntry now scans from 0 so it can udpate the parent
//					Pixel-precise placement of bottom-row buttons
// Hummingbird v1.4.7:	Handle control box hiding size
// Beowulf v3.4.6:	Remove -1 from list count so UpdateEntry of last entry will work
// Mustang v1.8.8:	Put -1 back to AddSubEntry so last entry will work
// FolderWatch v1.0.0:	Move ListView handling functions into controlwrappers
// RoboRename v1.1.0:	Add LOG_ALERT handling
// FolderWatch v1.6.0:	Change from ListView to HyperLogView

namespace CustomForms {

	public partial class LogForm : Form {

		public Action? MoveEventCallback;
		public LogAndStatusManager LogAndStatus;

		public LogForm() {
			InitializeComponent();
		}

		private void LogForm_Resize(object sender, EventArgs e) {
			LogListView.Size = ClientSize;
			//LogListView.Columns[0].Width = ClientSize.Width - 4;
			LogListView.Height = ClientSize.Height - 40;
			ClearButton.Top = ClientSize.Height - 34;
			CopyButton.Top = ClearButton.Top;
			SaveButton.Top = ClearButton.Top;
			CloseButton.Top = ClearButton.Top;
			CloseButton.Left = ClientSize.Width - CloseButton.Width - 6;
			SaveButton.Left = CloseButton.Left - CloseButton.Width - 6;
			CopyButton.Left = SaveButton.Left - SaveButton.Width - 6;
			ClearButton.Left = CopyButton.Left - CopyButton.Width - 6;
		}

		private void LogForm_Load(object sender, EventArgs e) {
			LogListView.Top = 0;
			LogListView.Left = 0;
			LogListView.Size = ClientSize;
			//LogListView.Columns[0].Width = ClientSize.Width - 4;
			LogListView.Height = ClientSize.Height - 40;
			this.ControlBox = false;
			//LogListView.ImageList = IconList;
		}

		public HyperLogView InternalListView {
			get {
				return LogListView;
			}
		}

		private void CloseButton_Click(object sender, EventArgs e) {
			this.Hide();
		}

		private void SaveButton_Click(object sender, EventArgs e) {
			StreamWriter? tw = null;

			if (saveLogDialog.ShowDialog() != DialogResult.OK) return;
			if (saveLogDialog.FileName != "") {
				try {
					tw = new StreamWriter(saveLogDialog.FileName);
				} catch (IOException ex) {
					if (LogAndStatus != null) LogAndStatus.AddLog(ex.Message, ErrorLevel.LOG_ERROR);
				}
				if (tw == null) return;
				foreach (HyperLogView.HyperLogItem item in LogListView.Items) {
					tw.WriteLine(item.Text);
				}
				tw.Close();
				if (LogAndStatus != null) LogAndStatus.AddLog("Log file saved", "", ErrorLevel.LOG_INFO);
			}

		}

		private void ClearButton_Click(object sender, EventArgs e) {
			LogListView.Items.Clear();
		}

		public string GetAllLog() {
			int vCount = LogListView.Items.Count;
			int i;
			StringBuilder sb = new StringBuilder();

			for (i = 0; i < vCount; i++) {
				sb.Append(LogListView.Items[i].Text + "\n");
			}
			return sb.ToString();
		}

		private void CopyButton_Click(object sender, EventArgs e) {
			Clipboard.SetText(GetAllLog());
		}

		private void LogForm_Move(object sender, EventArgs e) {
			if (MoveEventCallback != null) MoveEventCallback();
		}
	}
}
