namespace Sampler
{
	partial class Form1
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
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
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.components = new System.ComponentModel.Container();
			this.projectView = new System.Windows.Forms.TreeView();
			this.treeViewImageList = new System.Windows.Forms.ImageList(this.components);
			this.splitter1 = new System.Windows.Forms.Splitter();
			this.treeLoader = new System.ComponentModel.BackgroundWorker();
			this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
			this.explorerBrowser = new Vanara.Windows.Forms.ExplorerBrowser();
			this.titleLabel = new System.Windows.Forms.Label();
			this.runButton = new System.Windows.Forms.Button();
			this.tableLayoutPanel1.SuspendLayout();
			this.SuspendLayout();
			// 
			// projectView
			// 
			this.projectView.Dock = System.Windows.Forms.DockStyle.Left;
			this.projectView.ImageIndex = 0;
			this.projectView.ImageList = this.treeViewImageList;
			this.projectView.Location = new System.Drawing.Point(0, 0);
			this.projectView.Name = "projectView";
			this.projectView.SelectedImageIndex = 0;
			this.projectView.Size = new System.Drawing.Size(266, 592);
			this.projectView.TabIndex = 0;
			this.projectView.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.projectView_AfterSelect);
			// 
			// treeViewImageList
			// 
			this.treeViewImageList.ColorDepth = System.Windows.Forms.ColorDepth.Depth32Bit;
			this.treeViewImageList.ImageSize = new System.Drawing.Size(16, 16);
			this.treeViewImageList.TransparentColor = System.Drawing.Color.Transparent;
			// 
			// splitter1
			// 
			this.splitter1.Location = new System.Drawing.Point(266, 0);
			this.splitter1.Name = "splitter1";
			this.splitter1.Size = new System.Drawing.Size(3, 592);
			this.splitter1.TabIndex = 1;
			this.splitter1.TabStop = false;
			// 
			// treeLoader
			// 
			this.treeLoader.DoWork += new System.ComponentModel.DoWorkEventHandler(this.treeLoader_DoWork);
			this.treeLoader.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.treeLoader_RunWorkerCompleted);
			// 
			// tableLayoutPanel1
			// 
			this.tableLayoutPanel1.ColumnCount = 2;
			this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
			this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
			this.tableLayoutPanel1.Controls.Add(this.explorerBrowser, 0, 1);
			this.tableLayoutPanel1.Controls.Add(this.titleLabel, 0, 0);
			this.tableLayoutPanel1.Controls.Add(this.runButton, 1, 0);
			this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.tableLayoutPanel1.Location = new System.Drawing.Point(269, 0);
			this.tableLayoutPanel1.Name = "tableLayoutPanel1";
			this.tableLayoutPanel1.RowCount = 2;
			this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
			this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
			this.tableLayoutPanel1.Size = new System.Drawing.Size(622, 592);
			this.tableLayoutPanel1.TabIndex = 5;
			// 
			// explorerBrowser
			// 
			this.tableLayoutPanel1.SetColumnSpan(this.explorerBrowser, 2);
			this.explorerBrowser.ContentFlags = ((Vanara.Windows.Forms.ExplorerBrowserContentSectionOptions)((((Vanara.Windows.Forms.ExplorerBrowserContentSectionOptions.SingleSelection | Vanara.Windows.Forms.ExplorerBrowserContentSectionOptions.NoSubfolders) 
            | Vanara.Windows.Forms.ExplorerBrowserContentSectionOptions.NoWebView) 
            | Vanara.Windows.Forms.ExplorerBrowserContentSectionOptions.UseSearchFolder)));
			this.explorerBrowser.Dock = System.Windows.Forms.DockStyle.Fill;
			this.explorerBrowser.Location = new System.Drawing.Point(3, 32);
			this.explorerBrowser.Name = "explorerBrowser";
			this.explorerBrowser.NavigationFlags = Vanara.Windows.Forms.ExplorerBrowserNavigateOptions.ShowFrames;
			this.explorerBrowser.PaneVisibility.AdvancedQuery = Vanara.Windows.Forms.PaneVisibilityState.Hide;
			this.explorerBrowser.PaneVisibility.Commands = Vanara.Windows.Forms.PaneVisibilityState.Hide;
			this.explorerBrowser.PaneVisibility.CommandsOrganize = Vanara.Windows.Forms.PaneVisibilityState.Hide;
			this.explorerBrowser.PaneVisibility.CommandsView = Vanara.Windows.Forms.PaneVisibilityState.Hide;
			this.explorerBrowser.PaneVisibility.Navigation = Vanara.Windows.Forms.PaneVisibilityState.Hide;
			this.explorerBrowser.PaneVisibility.Query = Vanara.Windows.Forms.PaneVisibilityState.Hide;
			this.explorerBrowser.PaneVisibility.Ribbon = Vanara.Windows.Forms.PaneVisibilityState.Hide;
			this.explorerBrowser.PaneVisibility.StatusBar = Vanara.Windows.Forms.PaneVisibilityState.Hide;
			this.explorerBrowser.Size = new System.Drawing.Size(616, 557);
			this.explorerBrowser.TabIndex = 0;
			this.explorerBrowser.ViewMode = Vanara.Windows.Forms.ExplorerBrowserViewMode.SmallIcon;
			// 
			// titleLabel
			// 
			this.titleLabel.AutoSize = true;
			this.titleLabel.Dock = System.Windows.Forms.DockStyle.Left;
			this.titleLabel.Font = new System.Drawing.Font("Segoe UI", 14F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.titleLabel.Location = new System.Drawing.Point(3, 0);
			this.titleLabel.Name = "titleLabel";
			this.titleLabel.Padding = new System.Windows.Forms.Padding(3, 0, 7, 0);
			this.titleLabel.Size = new System.Drawing.Size(10, 29);
			this.titleLabel.TabIndex = 1;
			// 
			// runButton
			// 
			this.runButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.runButton.Location = new System.Drawing.Point(518, 3);
			this.runButton.Name = "runButton";
			this.runButton.Size = new System.Drawing.Size(101, 23);
			this.runButton.TabIndex = 2;
			this.runButton.Text = "Run in console";
			this.runButton.UseVisualStyleBackColor = true;
			// 
			// Form1
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(891, 592);
			this.Controls.Add(this.tableLayoutPanel1);
			this.Controls.Add(this.splitter1);
			this.Controls.Add(this.projectView);
			this.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.Name = "Form1";
			this.Text = "Windows Classic Samples Explorer";
			this.Load += new System.EventHandler(this.Form1_Load);
			this.tableLayoutPanel1.ResumeLayout(false);
			this.tableLayoutPanel1.PerformLayout();
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.TreeView projectView;
		private System.Windows.Forms.Splitter splitter1;
		private System.ComponentModel.BackgroundWorker treeLoader;
		private System.Windows.Forms.ImageList treeViewImageList;
		private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
		private Vanara.Windows.Forms.ExplorerBrowser explorerBrowser;
		private System.Windows.Forms.Label titleLabel;
		private System.Windows.Forms.Button runButton;
	}
}

