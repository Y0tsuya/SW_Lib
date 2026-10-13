namespace CustomForms {
	partial class ColorPickerForm {
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
			ColorTabs = new TabControl();
			SystemTabPage = new TabPage();
			SystemListBox = new ListBox();
			WebTabPage = new TabPage();
			WebListBox = new ListBox();
			ColorTabs.SuspendLayout();
			SystemTabPage.SuspendLayout();
			WebTabPage.SuspendLayout();
			SuspendLayout();
			// 
			// ColorTabs
			// 
			ColorTabs.Controls.Add(SystemTabPage);
			ColorTabs.Controls.Add(WebTabPage);
			ColorTabs.Location = new Point(0, 0);
			ColorTabs.Name = "ColorTabs";
			ColorTabs.SelectedIndex = 0;
			ColorTabs.Size = new Size(256, 256);
			ColorTabs.TabIndex = 0;
			// 
			// SystemTabPage
			// 
			SystemTabPage.Controls.Add(SystemListBox);
			SystemTabPage.Location = new Point(4, 24);
			SystemTabPage.Name = "SystemTabPage";
			SystemTabPage.Padding = new Padding(3);
			SystemTabPage.Size = new Size(248, 228);
			SystemTabPage.TabIndex = 0;
			SystemTabPage.Text = "System";
			SystemTabPage.UseVisualStyleBackColor = true;
			// 
			// SystemListBox
			// 
			SystemListBox.DrawMode = DrawMode.OwnerDrawFixed;
			SystemListBox.FormattingEnabled = true;
			SystemListBox.ItemHeight = 15;
			SystemListBox.Location = new Point(8, 6);
			SystemListBox.Name = "SystemListBox";
			SystemListBox.Size = new Size(232, 214);
			SystemListBox.TabIndex = 0;
			SystemListBox.DrawItem += SystemListBox_DrawItem;
			SystemListBox.SelectedIndexChanged += SystemListBox_SelectedIndexChanged;
			// 
			// WebTabPage
			// 
			WebTabPage.Controls.Add(WebListBox);
			WebTabPage.Location = new Point(4, 24);
			WebTabPage.Name = "WebTabPage";
			WebTabPage.Padding = new Padding(3);
			WebTabPage.Size = new Size(248, 228);
			WebTabPage.TabIndex = 1;
			WebTabPage.Text = "Web";
			WebTabPage.UseVisualStyleBackColor = true;
			// 
			// WebListBox
			// 
			WebListBox.DrawMode = DrawMode.OwnerDrawFixed;
			WebListBox.FormattingEnabled = true;
			WebListBox.ItemHeight = 15;
			WebListBox.Location = new Point(8, 6);
			WebListBox.Name = "WebListBox";
			WebListBox.Size = new Size(232, 214);
			WebListBox.TabIndex = 0;
			WebListBox.DrawItem += WebListBox_DrawItem;
			WebListBox.SelectedIndexChanged += WebListBox_SelectedIndexChanged;
			// 
			// ColorPickerForm
			// 
			AutoScaleDimensions = new SizeF(7F, 15F);
			AutoScaleMode = AutoScaleMode.Font;
			ClientSize = new Size(256, 256);
			Controls.Add(ColorTabs);
			FormBorderStyle = FormBorderStyle.None;
			Name = "ColorPickerForm";
			Text = "Color Selector";
			FormClosing += ColorPickerForm_FormClosing;
			Load += ColorPickerForm_Load;
			ColorTabs.ResumeLayout(false);
			SystemTabPage.ResumeLayout(false);
			WebTabPage.ResumeLayout(false);
			ResumeLayout(false);
		}

		#endregion

		private TabControl ColorTabs;
		private TabPage SystemTabPage;
		private ListBox SystemListBox;
		private TabPage WebTabPage;
		private ListBox WebListBox;
	}
}