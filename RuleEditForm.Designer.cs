namespace CustomForms {
	partial class RuleEditForm {
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing) {
			if (disposing && (components != null)) {
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent() {
			components = new System.ComponentModel.Container();
			RuleDataGridView = new CustomControls.RoundedDataGridView();
			IndexColumn = new DataGridViewTextBoxColumn();
			StatusColumn = new DataGridViewCheckBoxColumn();
			SearchColumn = new DataGridViewTextBoxColumn();
			SpacerColumn = new DataGridViewTextBoxColumn();
			ReplaceColumn = new DataGridViewTextBoxColumn();
			RuleToolTip = new ToolTip(components);
			TestLabel = new Label();
			InputTextBox = new CustomControls.RoundedTextBox();
			OutputTextBox = new CustomControls.RoundedTextBox();
			ArrowLabel = new Label();
			((System.ComponentModel.ISupportInitialize)RuleDataGridView).BeginInit();
			SuspendLayout();
			// 
			// RuleDataGridView
			// 
			RuleDataGridView.AllowDrop = true;
			RuleDataGridView.AllowUserToResizeColumns = false;
			RuleDataGridView.AllowUserToResizeRows = false;
			RuleDataGridView.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			RuleDataGridView.BackgroundColor = Color.WhiteSmoke;
			RuleDataGridView.BorderStyle = BorderStyle.None;
			RuleDataGridView.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
			RuleDataGridView.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			RuleDataGridView.Columns.AddRange(new DataGridViewColumn[] { IndexColumn, StatusColumn, SearchColumn, SpacerColumn, ReplaceColumn });
			RuleDataGridView.GridColor = SystemColors.ControlLight;
			RuleDataGridView.Location = new Point(12, 12);
			RuleDataGridView.MultiSelect = false;
			RuleDataGridView.Name = "RuleDataGridView";
			RuleDataGridView.RowHeadersVisible = false;
			RuleDataGridView.RowTemplate.Height = 25;
			RuleDataGridView.ScrollBars = ScrollBars.None;
			RuleDataGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
			RuleDataGridView.ShowCellToolTips = false;
			RuleDataGridView.Size = new Size(503, 279);
			RuleDataGridView.TabIndex = 1;
			RuleDataGridView.CellBeginEdit += RuleDataGridView_CellBeginEdit;
			RuleDataGridView.CellContentClick += RuleDataGridView_CellContentClick;
			RuleDataGridView.CellEndEdit += RuleDataGridView_CellEndEdit;
			RuleDataGridView.RowsAdded += RuleDataGridView_RowsAdded;
			RuleDataGridView.RowsRemoved += RuleDataGridView_RowsRemoved;
			RuleDataGridView.DragDrop += RuleDataGridView_DragDrop;
			RuleDataGridView.DragOver += RuleDataGridView_DragOver;
			RuleDataGridView.KeyDown += RuleDataGridView_KeyDown;
			RuleDataGridView.MouseDown += RuleDataGridView_MouseDown;
			RuleDataGridView.MouseMove += RuleDataGridView_MouseMove;
			// 
			// IndexColumn
			// 
			IndexColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
			IndexColumn.HeaderText = "#";
			IndexColumn.Name = "IndexColumn";
			IndexColumn.Width = 32;
			// 
			// StatusColumn
			// 
			StatusColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
			StatusColumn.HeaderText = "";
			StatusColumn.Name = "StatusColumn";
			StatusColumn.Resizable = DataGridViewTriState.False;
			StatusColumn.Width = 20;
			// 
			// SearchColumn
			// 
			SearchColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
			SearchColumn.HeaderText = "Search";
			SearchColumn.Name = "SearchColumn";
			SearchColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
			// 
			// SpacerColumn
			// 
			SpacerColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
			SpacerColumn.HeaderText = "";
			SpacerColumn.Name = "SpacerColumn";
			SpacerColumn.Resizable = DataGridViewTriState.False;
			SpacerColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
			SpacerColumn.Width = 24;
			// 
			// ReplaceColumn
			// 
			ReplaceColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
			ReplaceColumn.HeaderText = "Replace";
			ReplaceColumn.Name = "ReplaceColumn";
			ReplaceColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
			// 
			// RuleToolTip
			// 
			RuleToolTip.AutoPopDelay = 10000;
			RuleToolTip.InitialDelay = 500;
			RuleToolTip.ReshowDelay = 100;
			// 
			// TestLabel
			// 
			TestLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
			TestLabel.AutoSize = true;
			TestLabel.Location = new Point(12, 294);
			TestLabel.Name = "TestLabel";
			TestLabel.Size = new Size(457, 15);
			TestLabel.TabIndex = 2;
			TestLabel.Text = "Test the rules,    Ctrl+C/V to copy-paste ruleset, Ctrl+Shift+C/V for tabbed copy/paste";
			// 
			// InputTextBox
			// 
			InputTextBox.AllowDrop = true;
			InputTextBox.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
			InputTextBox.BorderColor = Color.DarkGray;
			InputTextBox.BorderMargin = 1F;
			InputTextBox.BorderRadius = 5F;
			InputTextBox.BorderShape = CustomControls.ShapeSR.ROUNDED;
			InputTextBox.BorderWidth = 1.5F;
			InputTextBox.Location = new Point(12, 312);
			InputTextBox.MinimumSize = new Size(4, 26);
			InputTextBox.Multiline = false;
			InputTextBox.Name = "InputTextBox";
			InputTextBox.ReadOnly = false;
			InputTextBox.SelectedText = "";
			InputTextBox.SelectionLength = 0;
			InputTextBox.SelectionStart = 0;
			InputTextBox.Size = new Size(230, 26);
			InputTextBox.TabIndex = 3;
			InputTextBox.TextChanged += InputTextBox_TextChanged;
			InputTextBox.DragDrop += InputTextBox_DragDrop;
			InputTextBox.DragOver += InputTextBox_DragOver;
			// 
			// OutputTextBox
			// 
			OutputTextBox.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
			OutputTextBox.BorderColor = Color.DarkGray;
			OutputTextBox.BorderMargin = 1F;
			OutputTextBox.BorderRadius = 5F;
			OutputTextBox.BorderShape = CustomControls.ShapeSR.ROUNDED;
			OutputTextBox.BorderWidth = 1.5F;
			OutputTextBox.Location = new Point(285, 312);
			OutputTextBox.MinimumSize = new Size(4, 26);
			OutputTextBox.Multiline = false;
			OutputTextBox.Name = "OutputTextBox";
			OutputTextBox.ReadOnly = true;
			OutputTextBox.SelectedText = "";
			OutputTextBox.SelectionLength = 0;
			OutputTextBox.SelectionStart = 0;
			OutputTextBox.Size = new Size(230, 26);
			OutputTextBox.TabIndex = 4;
			// 
			// ArrowLabel
			// 
			ArrowLabel.Anchor = AnchorStyles.Bottom;
			ArrowLabel.AutoSize = true;
			ArrowLabel.Location = new Point(248, 315);
			ArrowLabel.Name = "ArrowLabel";
			ArrowLabel.Size = new Size(30, 15);
			ArrowLabel.TabIndex = 5;
			ArrowLabel.Text = "--->";
			// 
			// RuleEditForm
			// 
			AutoScaleDimensions = new SizeF(7F, 15F);
			AutoScaleMode = AutoScaleMode.Font;
			BackColor = SystemColors.ControlLightLight;
			ClientSize = new Size(527, 348);
			Controls.Add(RuleDataGridView);
			Controls.Add(ArrowLabel);
			Controls.Add(OutputTextBox);
			Controls.Add(InputTextBox);
			Controls.Add(TestLabel);
			Name = "RuleEditForm";
			Text = "Ruleset Editor";
			Load += RuleEditForm_Load;
			Resize += RuleEditForm_Resize;
			((System.ComponentModel.ISupportInitialize)RuleDataGridView).EndInit();
			ResumeLayout(false);
			PerformLayout();
		}

		#endregion

		private CustomControls.RoundedDataGridView RuleDataGridView;
		private ToolTip RuleToolTip;
		private Label TestLabel;
		private CustomControls.RoundedTextBox InputTextBox;
		private CustomControls.RoundedTextBox OutputTextBox;
		private Label ArrowLabel;
		private DataGridViewTextBoxColumn IndexColumn;
		private DataGridViewCheckBoxColumn StatusColumn;
		private DataGridViewTextBoxColumn SearchColumn;
		private DataGridViewTextBoxColumn SpacerColumn;
		private DataGridViewTextBoxColumn ReplaceColumn;
	}
}