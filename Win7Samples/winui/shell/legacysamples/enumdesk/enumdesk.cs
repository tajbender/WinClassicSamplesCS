using System.Runtime.Versioning;
using Vanara.Extensions;
using Vanara.PInvoke;
using Vanara.Windows.Shell;
using static Vanara.PInvoke.ComCtl32;
using static Vanara.PInvoke.Shell32;

namespace enumdesk;

[SupportedOSPlatform("windows")]
public partial class EnumDesk : Form
{
	private ImageList himlLarge, himlSmall;
	private ShellFolder? pParentFolder;
	private static readonly object dummy = new();

	public EnumDesk()
	{
		InitializeComponent();
		Shell_GetImageLists(out var hLg, out var hSm);
		himlLarge = hLg.ToImageList();
		himlSmall = hSm.ToImageList();
	}

	private static TreeNode MakeItem(ShellItem item)
	{
		int iImageIndex = 0, iSelImageIndex = 0;
		SHFILEINFO sfi = new();
		if (SHGetFileInfo(item.PIDL, 0, ref sfi, Marshal.SizeOf<SHFILEINFO>(), SHGFI.SHGFI_PIDL | SHGFI.SHGFI_SYSICONINDEX | SHGFI.SHGFI_SMALLICON | SHGFI.SHGFI_LINKOVERLAY) != 0)
			iImageIndex = sfi.iIcon;
		sfi = new();
		if (SHGetFileInfo(item.PIDL, 0, ref sfi, Marshal.SizeOf<SHFILEINFO>(), SHGFI.SHGFI_PIDL | SHGFI.SHGFI_SYSICONINDEX | SHGFI.SHGFI_SMALLICON | SHGFI.SHGFI_OPENICON) != 0)
			iSelImageIndex = sfi.iIcon;
		//bool hasChildren = attr.IsFlagSet(ShellItemAttribute.HasSubfolder);
		bool isShared = item.IShellItem.GetAttributes(SFGAO.SFGAO_SHARE) != 0;
		bool hasChildren = item.IShellItem.GetAttributes(SFGAO.SFGAO_HASSUBFOLDER) != 0;
		var ret = new TreeNode() { Tag = item.PIDL, Text = item.Name, ImageIndex = iImageIndex, SelectedImageIndex = iSelImageIndex, StateImageIndex = isShared ? INDEXTOOVERLAYMASK(1) : 0 };
		if (hasChildren)
			ret.Nodes.Add(new TreeNode() { Text = "Loading...", Tag = dummy });
		return ret;
	}

	private static ListViewItem MakeListItem(PIDL pIDL, int arg2)
	{
		ShellItem item = new(pIDL);

		int iImageIndex = 0;
		SHFILEINFO sfi = new();
		if (SHGetFileInfo(item.PIDL, 0, ref sfi, Marshal.SizeOf<SHFILEINFO>(), SHGFI.SHGFI_PIDL | SHGFI.SHGFI_SYSICONINDEX | SHGFI.SHGFI_SMALLICON | SHGFI.SHGFI_LINKOVERLAY) != 0)
			iImageIndex = sfi.iIcon;
		//bool hasChildren = attr.IsFlagSet(ShellItemAttribute.HasSubfolder);
		bool isShared = item.IShellItem.GetAttributes(SFGAO.SFGAO_SHARE) != 0;
		//bool isGhost = attr.IsFlagSet(ShellItemAttribute.Ghosted);
		return new ListViewItem(item.Name, iImageIndex) { Tag = item.PIDL, StateImageIndex = isShared ? INDEXTOOVERLAYMASK(1) : 0 };
	}

	private void EnumDesk_Load(object sender, EventArgs e)
	{
		// init tree
		hwndTreeView.ImageList = himlSmall;
		hwndTreeView.AfterExpand += (s, e) =>
		{
			if ((e.Node?.Nodes.Count) != 0)
				Tree_GetChildItems(e.Node!);
		};
		//hwndTreeView.AfterCollapse += (s, e) => e.Node?.Nodes.Clear();
		hwndTreeView.AfterSelect += (s, e) => List_DisplayFolder((PIDL?)e.Node?.Tag);
		hwndTreeView.NodeMouseClick += (s, e) => { if (e.Button == MouseButtons.Right) Tree_DoItemMenu(e.Node, hwndTreeView.PointToScreen(e.Location)); };

		// init list
		hwndListView.LargeImageList = himlLarge;
		hwndListView.SmallImageList = himlSmall;
		hwndListView.DoubleClick += (s, e) => List_DoDefault(hwndListView.SelectedItems.Cast<ListViewItem>().FirstOrDefault());
		hwndListView.MouseDown += (s, e) =>
		{
			if (e.Button == MouseButtons.Right)
			{
				var item = hwndListView.HitTest(e.X, e.Y).Item;
				if (item != null)
				{
					item.Selected = true;
					List_DoItemMenu(item, hwndListView.PointToScreen(e.Location));
				}
			}
		};

		// load root node for tree
		hwndTreeView.BeginUpdate();
		hwndTreeView.Nodes.Clear();
		var tvItem = MakeItem(ShellFolder.Desktop);
		hwndTreeView.Nodes.Add(tvItem);
		hwndTreeView.EndUpdate();
		tvItem.Expand();

		TreeView_SetScrollTime(hwndTreeView.Handle, 100);
		hwndTreeView.Focus();
	}

	private void exitToolStripMenuItem_Click(object sender, EventArgs e) => Close();

	private void IDM_LARGE_ICONS_Click(object sender, EventArgs e) => ListView_SetView(View.LargeIcon);

	private void IDM_LIST_Click(object sender, EventArgs e) => ListView_SetView(View.List);

	private void IDM_REFRESH_Click(object sender, EventArgs e) => List_DisplayFolder((PIDL?)hwndTreeView.SelectedNode?.Tag);

	private void IDM_SMALL_ICONS_Click(object sender, EventArgs e) => ListView_SetView(View.SmallIcon);

	private void List_DisplayFolder(PIDL? pidl)
	{
		if (pidl is null)
			return;
		hwndListView.Cursor = Cursors.WaitCursor;
		hwndListView.BeginUpdate();
		try
		{
			hwndListView.Items.Clear();
			hwndListView.Tag = pidl;
			pParentFolder = new ShellFolder(pidl);
			hwndListView.Items.AddRange([.. pParentFolder.EnumerateChildIds(FolderItemFilter.Folders | FolderItemFilter.NonFolders | FolderItemFilter.IncludeHidden,
				hwndListView.Handle).Order(Comparer<PIDL>.Create(comp)).Select(MakeListItem)]);

			int comp(PIDL x, PIDL y) { var hr = pParentFolder!.IShellFolder.CompareIDs(0, x, y); return hr.Succeeded ? (short)hr.Code : 0; }
		}
		finally
		{
			hwndListView.EndUpdate();
			hwndListView.Cursor = Cursors.Default;
		}
	}

	private void List_DoDefault(ListViewItem? listViewItem)
	{
		var pcm = pParentFolder![(PIDL?)listViewItem?.Tag];
		if (pcm is null)
			return;
		if (pcm.IsFolder)
			List_DisplayFolder(pcm.PIDL);
		else
			pcm.InvokeVerb("open");
	}

	private void List_DoItemMenu(ListViewItem node, in POINT pt)
	{
		var pItem = (PIDL?)node.Tag;
		if (pItem is null)
			return;

		var pSHItem = pParentFolder![(PIDL?)pItem];
		pSHItem?.ContextMenu.ShowContextMenu(pt, CMF.CMF_NORMAL | CMF.CMF_EXPLORE, null, hwndListView.Handle);
	}

	private void ListView_SetView(View view)
	{
		hwndListView.View = view;
		//hwndListView.Refresh();
		IDM_LARGE_ICONS.Checked = view == View.LargeIcon;
		IDM_SMALL_ICONS.Checked = view == View.SmallIcon;
		IDM_LIST.Checked = view == View.List;
	}

	private void Tree_DoItemMenu(TreeNode node, in POINT pt)
	{
		var pItem = (PIDL?)node.Tag;
		if (pItem is null)
			return;

		ShellFolder psfFolder = new(pItem);
		psfFolder.ContextMenu.ShowContextMenu(pt, CMF.CMF_NORMAL | CMF.CMF_EXPLORE, null, hwndTreeView.Handle);
	}

	private bool Tree_GetChildItems(TreeNode hParentItem)
	{
		//get the parent item's pidl
		var pItem = (PIDL?)hParentItem.Tag;
		if (pItem is null)
			return false;

		//change the cursor
		hwndTreeView.Cursor = Cursors.WaitCursor;

		//turn redawing off in the TreeView. This will speed things up as we add items
		hwndTreeView.BeginUpdate();
		hParentItem.Nodes.Clear();

		try
		{
			//otherwise we need to get the IShellFolder for this item
			ShellFolder pParentFolder = new(pItem);

			//enumerate and sort the item's PIDLs
			foreach (var pidl in pParentFolder.EnumerateChildIds(FolderItemFilter.Folders, hwndTreeView.Handle).Order(Comparer<PIDL>.Create(comp)))
			{
				try { hParentItem.Nodes.Add(MakeItem(new ShellFolder(pidl))); } catch { }
			}

			int comp(PIDL x, PIDL y) { var hr = pParentFolder.IShellFolder.CompareIDs(0, x, y); return hr.Succeeded ? (short)hr.Code : 0; }
		}
		finally
		{
			//turn redawing back on in the TreeView
			hwndTreeView.EndUpdate();

			hwndTreeView.Cursor = Cursors.Default;
		}

		return true;
	}
}