namespace CustomForms {
	partial class LogForm {
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
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(LogForm));
			ClearButton = new Button();
			SaveButton = new Button();
			saveLogDialog = new SaveFileDialog();
			CloseButton = new Button();
			CopyButton = new Button();
			LogListView = new CustomControls.HyperLogView();
			IconList = new ImageList(components);
			SuspendLayout();
			// 
			// ClearButton
			// 
			ClearButton.Anchor = AnchorStyles.None;
			ClearButton.Location = new Point(153, 192);
			ClearButton.Name = "ClearButton";
			ClearButton.Size = new Size(50, 23);
			ClearButton.TabIndex = 1;
			ClearButton.Text = "Clear";
			ClearButton.UseVisualStyleBackColor = true;
			ClearButton.Click += ClearButton_Click;
			// 
			// SaveButton
			// 
			SaveButton.Anchor = AnchorStyles.None;
			SaveButton.Location = new Point(266, 192);
			SaveButton.Name = "SaveButton";
			SaveButton.Size = new Size(50, 23);
			SaveButton.TabIndex = 2;
			SaveButton.Text = "Save";
			SaveButton.UseVisualStyleBackColor = true;
			SaveButton.Click += SaveButton_Click;
			// 
			// saveLogDialog
			// 
			saveLogDialog.FileName = "beowulf.log";
			saveLogDialog.Filter = "\"Log files|*.log|All Files|*.*\"";
			// 
			// CloseButton
			// 
			CloseButton.Anchor = AnchorStyles.None;
			CloseButton.Location = new Point(321, 192);
			CloseButton.Name = "CloseButton";
			CloseButton.Size = new Size(50, 23);
			CloseButton.TabIndex = 3;
			CloseButton.Text = "Close";
			CloseButton.UseVisualStyleBackColor = true;
			CloseButton.Click += CloseButton_Click;
			// 
			// CopyButton
			// 
			CopyButton.Anchor = AnchorStyles.None;
			CopyButton.Location = new Point(209, 192);
			CopyButton.Name = "CopyButton";
			CopyButton.Size = new Size(50, 23);
			CopyButton.TabIndex = 4;
			CopyButton.Text = "Copy";
			CopyButton.UseVisualStyleBackColor = true;
			CopyButton.Click += CopyButton_Click;
			// 
			// LogListView
			// 
			LogListView.BackColor = SystemColors.ControlLight;
			LogListView.ImageList = IconList;
			LogListView.Location = new Point(12, 12);
			LogListView.Name = "LogListView";
			LogListView.Size = new Size(360, 174);
			LogListView.TabIndex = 6;
			// 
			// IconList
			// 
			IconList.ColorDepth = ColorDepth.Depth8Bit;
			IconList.ImageStream = (ImageListStreamer)resources.GetObject("IconList.ImageStream");
			IconList.TransparentColor = Color.Transparent;
			IconList.Images.SetKeyName(0, "log00_blank.png");
			IconList.Images.SetKeyName(1, "log01_info.png");
			IconList.Images.SetKeyName(2, "log02_alert.png");
			IconList.Images.SetKeyName(3, "log03_warn.png");
			IconList.Images.SetKeyName(4, "log04_error.png");
			IconList.Images.SetKeyName(5, "log05_extend.png");
			IconList.Images.SetKeyName(6, "log06_info2.png");
			IconList.Images.SetKeyName(7, "log07_alert2.png");
			IconList.Images.SetKeyName(8, "log08_warn2.png");
			IconList.Images.SetKeyName(9, "log09_error2.png");
			IconList.Images.SetKeyName(10, "log10_extend2.png");
			// 
			// LogForm
			// 
			AutoScaleDimensions = new SizeF(6F, 13F);
			AutoScaleMode = AutoScaleMode.Font;
			ClientSize = new Size(384, 227);
			Controls.Add(LogListView);
			Controls.Add(CopyButton);
			Controls.Add(CloseButton);
			Controls.Add(SaveButton);
			Controls.Add(ClearButton);
			Font = new Font("Microsoft Sans Serif", 8.25F, FontStyle.Regular, GraphicsUnit.Point);
			FormBorderStyle = FormBorderStyle.SizableToolWindow;
			MinimumSize = new Size(300, 200);
			Name = "LogForm";
			Text = "Log";
			Load += LogForm_Load;
			Move += LogForm_Move;
			Resize += LogForm_Resize;
			ResumeLayout(false);
		}

		#endregion
		private Button ClearButton;
		private Button SaveButton;
		private SaveFileDialog saveLogDialog;
		private Button CloseButton;
		private Button CopyButton;
		private CustomControls.HyperLogView LogListView;
		private ImageList IconList;
	}
}