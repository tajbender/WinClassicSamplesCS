namespace enumdesk;

partial class EnumDesk
{
	/// <summary>
	///  Required designer variable.
	/// </summary>
	private System.ComponentModel.IContainer components = null;

	/// <summary>
	///  Clean up any resources being used.
	/// </summary>
	/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
	protected override void Dispose(bool disposing)
	{
		if (disposing && (components != null))
		{
			components.Dispose();
		}
		base.Dispose(disposing);
	}

	#region Windows Form Designer generated code

	/// <summary>
	///  Required method for Designer support - do not modify
	///  the contents of this method with the code editor.
	/// </summary>
	private void InitializeComponent()
	{
		hwndTreeView = new TreeView();
		hwndListView = new ListView();
		splitContainer1 = new SplitContainer();
		menuStrip1 = new MenuStrip();
		fileToolStripMenuItem = new ToolStripMenuItem();
		toolStripSeparator = new ToolStripSeparator();
		exitToolStripMenuItem = new ToolStripMenuItem();
		viewToolStripMenuItem = new ToolStripMenuItem();
		IDM_LARGE_ICONS = new ToolStripMenuItem();
		IDM_SMALL_ICONS = new ToolStripMenuItem();
		IDM_LIST = new ToolStripMenuItem();
		toolStripSeparator5 = new ToolStripSeparator();
		IDM_REFRESH = new ToolStripMenuItem();
		((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();
		splitContainer1.Panel1.SuspendLayout();
		splitContainer1.Panel2.SuspendLayout();
		splitContainer1.SuspendLayout();
		menuStrip1.SuspendLayout();
		SuspendLayout();
		// 
		// hwndTreeView
		// 
		hwndTreeView.Dock = DockStyle.Fill;
		hwndTreeView.Location = new Point(0, 0);
		hwndTreeView.Name = "hwndTreeView";
		hwndTreeView.Size = new Size(266, 536);
		hwndTreeView.TabIndex = 0;
		// 
		// hwndListView
		// 
		hwndListView.Dock = DockStyle.Fill;
		hwndListView.Location = new Point(0, 0);
		hwndListView.Name = "hwndListView";
		hwndListView.Size = new Size(530, 536);
		hwndListView.TabIndex = 1;
		hwndListView.UseCompatibleStateImageBehavior = false;
		// 
		// splitContainer1
		// 
		splitContainer1.Dock = DockStyle.Fill;
		splitContainer1.Location = new Point(0, 24);
		splitContainer1.Name = "splitContainer1";
		// 
		// splitContainer1.Panel1
		// 
		splitContainer1.Panel1.Controls.Add(hwndTreeView);
		// 
		// splitContainer1.Panel2
		// 
		splitContainer1.Panel2.Controls.Add(hwndListView);
		splitContainer1.Size = new Size(800, 536);
		splitContainer1.SplitterDistance = 266;
		splitContainer1.TabIndex = 2;
		// 
		// menuStrip1
		// 
		menuStrip1.Items.AddRange(new ToolStripItem[] { fileToolStripMenuItem, viewToolStripMenuItem });
		menuStrip1.Location = new Point(0, 0);
		menuStrip1.Name = "menuStrip1";
		menuStrip1.Size = new Size(800, 24);
		menuStrip1.TabIndex = 3;
		menuStrip1.Text = "menuStrip1";
		// 
		// fileToolStripMenuItem
		// 
		fileToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { toolStripSeparator, exitToolStripMenuItem });
		fileToolStripMenuItem.Name = "fileToolStripMenuItem";
		fileToolStripMenuItem.Size = new Size(37, 20);
		fileToolStripMenuItem.Text = "&File";
		// 
		// toolStripSeparator
		// 
		toolStripSeparator.Name = "toolStripSeparator";
		toolStripSeparator.Size = new Size(89, 6);
		// 
		// exitToolStripMenuItem
		// 
		exitToolStripMenuItem.Name = "exitToolStripMenuItem";
		exitToolStripMenuItem.Size = new Size(92, 22);
		exitToolStripMenuItem.Text = "E&xit";
		exitToolStripMenuItem.Click += exitToolStripMenuItem_Click;
		// 
		// viewToolStripMenuItem
		// 
		viewToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { IDM_LARGE_ICONS, IDM_SMALL_ICONS, IDM_LIST, toolStripSeparator5, IDM_REFRESH });
		viewToolStripMenuItem.Name = "viewToolStripMenuItem";
		viewToolStripMenuItem.Size = new Size(44, 20);
		viewToolStripMenuItem.Text = "&View";
		// 
		// IDM_LARGE_ICONS
		// 
		IDM_LARGE_ICONS.Name = "IDM_LARGE_ICONS";
		IDM_LARGE_ICONS.Size = new Size(134, 22);
		IDM_LARGE_ICONS.Text = "&Large Icons";
		IDM_LARGE_ICONS.Click += IDM_LARGE_ICONS_Click;
		// 
		// IDM_SMALL_ICONS
		// 
		IDM_SMALL_ICONS.Name = "IDM_SMALL_ICONS";
		IDM_SMALL_ICONS.Size = new Size(134, 22);
		IDM_SMALL_ICONS.Text = "&Small Icons";
		IDM_SMALL_ICONS.Click += IDM_SMALL_ICONS_Click;
		// 
		// IDM_LIST
		// 
		IDM_LIST.Name = "IDM_LIST";
		IDM_LIST.Size = new Size(134, 22);
		IDM_LIST.Text = "&List";
		IDM_LIST.Click += IDM_LIST_Click;
		// 
		// toolStripSeparator5
		// 
		toolStripSeparator5.Name = "toolStripSeparator5";
		toolStripSeparator5.Size = new Size(131, 6);
		// 
		// IDM_REFRESH
		// 
		IDM_REFRESH.Name = "IDM_REFRESH";
		IDM_REFRESH.Size = new Size(134, 22);
		IDM_REFRESH.Text = "&Refresh";
		IDM_REFRESH.Click += IDM_REFRESH_Click;
		// 
		// EnumDesk
		// 
		AutoScaleDimensions = new SizeF(7F, 15F);
		AutoScaleMode = AutoScaleMode.Font;
		ClientSize = new Size(800, 560);
		Controls.Add(splitContainer1);
		Controls.Add(menuStrip1);
		MainMenuStrip = menuStrip1;
		Name = "EnumDesk";
		Text = "New EnumDesk Sample";
		Load += EnumDesk_Load;
		splitContainer1.Panel1.ResumeLayout(false);
		splitContainer1.Panel2.ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)splitContainer1).EndInit();
		splitContainer1.ResumeLayout(false);
		menuStrip1.ResumeLayout(false);
		menuStrip1.PerformLayout();
		ResumeLayout(false);
		PerformLayout();
	}

	#endregion

	private TreeView hwndTreeView;
	private ListView hwndListView;
	private SplitContainer splitContainer1;
	private MenuStrip menuStrip1;
	private ToolStripMenuItem fileToolStripMenuItem;
	private ToolStripSeparator toolStripSeparator;
	private ToolStripMenuItem exitToolStripMenuItem;
	private ToolStripMenuItem viewToolStripMenuItem;
	private ToolStripMenuItem IDM_LARGE_ICONS;
	private ToolStripMenuItem IDM_SMALL_ICONS;
	private ToolStripMenuItem IDM_LIST;
	private ToolStripSeparator toolStripSeparator5;
	private ToolStripMenuItem IDM_REFRESH;
}
