
namespace MECCG_Deck_Builder
{
    partial class Form1
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            this.ListBoxCardList = new System.Windows.Forms.ListBox();
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.ToolStripMenuItemCopyCard = new System.Windows.Forms.ToolStripMenuItem();
            this.ToolStripMenuItemPool = new System.Windows.Forms.ToolStripMenuItem();
            this.ToolStripMenuItemResource = new System.Windows.Forms.ToolStripMenuItem();
            this.ToolStripMenuItemHazard = new System.Windows.Forms.ToolStripMenuItem();
            this.ToolStripMenuItemSideboard = new System.Windows.Forms.ToolStripMenuItem();
            this.ToolStripMenuItemSite = new System.Windows.Forms.ToolStripMenuItem();
            this.PictureBoxCardImage = new System.Windows.Forms.PictureBox();
            this.ListBoxPool = new System.Windows.Forms.ListBox();
            this.ListBoxResources = new System.Windows.Forms.ListBox();
            this.ListBoxHazards = new System.Windows.Forms.ListBox();
            this.ListBoxSideboard = new System.Windows.Forms.ListBox();
            this.ListBoxSites = new System.Windows.Forms.ListBox();
            this.TabControlDeck = new System.Windows.Forms.TabControl();
            this.TabPagePool = new System.Windows.Forms.TabPage();
            this.TabPageResources = new System.Windows.Forms.TabPage();
            this.TabPageHazards = new System.Windows.Forms.TabPage();
            this.TabPageSideboard = new System.Windows.Forms.TabPage();
            this.TabPageSites = new System.Windows.Forms.TabPage();
            this.MenuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.newToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator = new System.Windows.Forms.ToolStripSeparator();
            this.SaveToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveAsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.printToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.printPreviewToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.editToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.undoToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.redoToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.cutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.copyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.pasteToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator4 = new System.Windows.Forms.ToolStripSeparator();
            this.selectAllToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.customizeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.optionsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ToolStripMenuItemSet = new System.Windows.Forms.ToolStripMenuItem();
            this.ToolStripMenuItemTW = new System.Windows.Forms.ToolStripMenuItem();
            this.ToolStripMenuItemTD = new System.Windows.Forms.ToolStripMenuItem();
            this.ToolStripMenuItemDM = new System.Windows.Forms.ToolStripMenuItem();
            this.ToolStripMenuItemLE = new System.Windows.Forms.ToolStripMenuItem();
            this.ToolStripMenuItemAS = new System.Windows.Forms.ToolStripMenuItem();
            this.ToolStripMenuItemWH = new System.Windows.Forms.ToolStripMenuItem();
            this.ToolStripMenuItemBA = new System.Windows.Forms.ToolStripMenuItem();
            this.helpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.contentsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.indexToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.searchToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator5 = new System.Windows.Forms.ToolStripSeparator();
            this.aboutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.contextMenuStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.PictureBoxCardImage)).BeginInit();
            this.TabControlDeck.SuspendLayout();
            this.TabPagePool.SuspendLayout();
            this.TabPageResources.SuspendLayout();
            this.TabPageHazards.SuspendLayout();
            this.TabPageSideboard.SuspendLayout();
            this.TabPageSites.SuspendLayout();
            this.MenuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // ListBoxCardList
            // 
            this.ListBoxCardList.ContextMenuStrip = this.contextMenuStrip1;
            this.ListBoxCardList.FormattingEnabled = true;
            this.ListBoxCardList.ItemHeight = 15;
            this.ListBoxCardList.Location = new System.Drawing.Point(12, 37);
            this.ListBoxCardList.Name = "ListBoxCardList";
            this.ListBoxCardList.Size = new System.Drawing.Size(190, 529);
            this.ListBoxCardList.TabIndex = 0;
            this.ListBoxCardList.SelectedIndexChanged += new System.EventHandler(this.ListBoxCardList_SelectedIndexChanged);
            this.ListBoxCardList.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.ListBoxCardList_MouseDoubleClick);
            this.ListBoxCardList.MouseDown += new System.Windows.Forms.MouseEventHandler(this.ListBoxCardList_MouseDown);
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.ToolStripMenuItemCopyCard});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(103, 26);
            // 
            // ToolStripMenuItemCopyCard
            // 
            this.ToolStripMenuItemCopyCard.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.ToolStripMenuItemPool,
            this.ToolStripMenuItemResource,
            this.ToolStripMenuItemHazard,
            this.ToolStripMenuItemSideboard,
            this.ToolStripMenuItemSite});
            this.ToolStripMenuItemCopyCard.Name = "ToolStripMenuItemCopyCard";
            this.ToolStripMenuItemCopyCard.Size = new System.Drawing.Size(102, 22);
            this.ToolStripMenuItemCopyCard.Text = "Copy";
            // 
            // ToolStripMenuItemPool
            // 
            this.ToolStripMenuItemPool.Name = "ToolStripMenuItemPool";
            this.ToolStripMenuItemPool.Size = new System.Drawing.Size(127, 22);
            this.ToolStripMenuItemPool.Text = "Pool";
            this.ToolStripMenuItemPool.Click += new System.EventHandler(this.ToolStripMenuItemPool_Click);
            // 
            // ToolStripMenuItemResource
            // 
            this.ToolStripMenuItemResource.Name = "ToolStripMenuItemResource";
            this.ToolStripMenuItemResource.Size = new System.Drawing.Size(127, 22);
            this.ToolStripMenuItemResource.Text = "Resources";
            this.ToolStripMenuItemResource.Click += new System.EventHandler(this.ToolStripMenuItemResource_Click);
            // 
            // ToolStripMenuItemHazard
            // 
            this.ToolStripMenuItemHazard.Name = "ToolStripMenuItemHazard";
            this.ToolStripMenuItemHazard.Size = new System.Drawing.Size(127, 22);
            this.ToolStripMenuItemHazard.Text = "Hazards";
            // 
            // ToolStripMenuItemSideboard
            // 
            this.ToolStripMenuItemSideboard.Name = "ToolStripMenuItemSideboard";
            this.ToolStripMenuItemSideboard.Size = new System.Drawing.Size(127, 22);
            this.ToolStripMenuItemSideboard.Text = "Sideboard";
            // 
            // ToolStripMenuItemSite
            // 
            this.ToolStripMenuItemSite.Name = "ToolStripMenuItemSite";
            this.ToolStripMenuItemSite.Size = new System.Drawing.Size(127, 22);
            this.ToolStripMenuItemSite.Text = "Sites";
            // 
            // PictureBoxCardImage
            // 
            this.PictureBoxCardImage.Location = new System.Drawing.Point(220, 37);
            this.PictureBoxCardImage.Name = "PictureBoxCardImage";
            this.PictureBoxCardImage.Size = new System.Drawing.Size(375, 525);
            this.PictureBoxCardImage.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.PictureBoxCardImage.TabIndex = 1;
            this.PictureBoxCardImage.TabStop = false;
            // 
            // ListBoxPool
            // 
            this.ListBoxPool.AllowDrop = true;
            this.ListBoxPool.FormattingEnabled = true;
            this.ListBoxPool.ItemHeight = 15;
            this.ListBoxPool.Location = new System.Drawing.Point(3, 10);
            this.ListBoxPool.Name = "ListBoxPool";
            this.ListBoxPool.Size = new System.Drawing.Size(198, 484);
            this.ListBoxPool.Sorted = true;
            this.ListBoxPool.TabIndex = 4;
            this.ListBoxPool.SelectedIndexChanged += new System.EventHandler(this.ListBoxPool_SelectedIndexChanged);
            this.ListBoxPool.DragDrop += new System.Windows.Forms.DragEventHandler(this.ListBoxPool_DragDrop);
            this.ListBoxPool.DragOver += new System.Windows.Forms.DragEventHandler(this.ListBoxPool_DragOver);
            this.ListBoxPool.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.ListBoxTab_MouseDoubleClick);
            this.ListBoxPool.MouseDown += new System.Windows.Forms.MouseEventHandler(this.ListBoxTab_MouseDown);
            // 
            // ListBoxResources
            // 
            this.ListBoxResources.AllowDrop = true;
            this.ListBoxResources.FormattingEnabled = true;
            this.ListBoxResources.ItemHeight = 15;
            this.ListBoxResources.Location = new System.Drawing.Point(3, 10);
            this.ListBoxResources.Name = "ListBoxResources";
            this.ListBoxResources.Size = new System.Drawing.Size(198, 484);
            this.ListBoxResources.Sorted = true;
            this.ListBoxResources.TabIndex = 5;
            this.ListBoxResources.SelectedIndexChanged += new System.EventHandler(this.ListBoxResources_SelectedIndexChanged);
            this.ListBoxResources.DragDrop += new System.Windows.Forms.DragEventHandler(this.ListBoxResources_DragDrop);
            this.ListBoxResources.DragOver += new System.Windows.Forms.DragEventHandler(this.ListBoxResources_DragOver);
            this.ListBoxResources.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.ListBoxTab_MouseDoubleClick);
            this.ListBoxResources.MouseDown += new System.Windows.Forms.MouseEventHandler(this.ListBoxTab_MouseDown);
            // 
            // ListBoxHazards
            // 
            this.ListBoxHazards.AllowDrop = true;
            this.ListBoxHazards.FormattingEnabled = true;
            this.ListBoxHazards.ItemHeight = 15;
            this.ListBoxHazards.Location = new System.Drawing.Point(3, 10);
            this.ListBoxHazards.Name = "ListBoxHazards";
            this.ListBoxHazards.Size = new System.Drawing.Size(198, 484);
            this.ListBoxHazards.Sorted = true;
            this.ListBoxHazards.TabIndex = 6;
            this.ListBoxHazards.SelectedIndexChanged += new System.EventHandler(this.ListBoxHazards_SelectedIndexChanged);
            this.ListBoxHazards.DragDrop += new System.Windows.Forms.DragEventHandler(this.ListBoxHazards_DragDrop);
            this.ListBoxHazards.DragOver += new System.Windows.Forms.DragEventHandler(this.ListBoxHazards_DragOver);
            this.ListBoxHazards.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.ListBoxTab_MouseDoubleClick);
            this.ListBoxHazards.MouseDown += new System.Windows.Forms.MouseEventHandler(this.ListBoxTab_MouseDown);
            // 
            // ListBoxSideboard
            // 
            this.ListBoxSideboard.AllowDrop = true;
            this.ListBoxSideboard.FormattingEnabled = true;
            this.ListBoxSideboard.ItemHeight = 15;
            this.ListBoxSideboard.Location = new System.Drawing.Point(3, 10);
            this.ListBoxSideboard.Name = "ListBoxSideboard";
            this.ListBoxSideboard.Size = new System.Drawing.Size(198, 484);
            this.ListBoxSideboard.Sorted = true;
            this.ListBoxSideboard.TabIndex = 7;
            this.ListBoxSideboard.SelectedIndexChanged += new System.EventHandler(this.ListBoxSideboard_SelectedIndexChanged);
            this.ListBoxSideboard.DragDrop += new System.Windows.Forms.DragEventHandler(this.ListBoxSideboard_DragDrop);
            this.ListBoxSideboard.DragOver += new System.Windows.Forms.DragEventHandler(this.ListBoxSideboard_DragOver);
            this.ListBoxSideboard.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.ListBoxTab_MouseDoubleClick);
            this.ListBoxSideboard.MouseDown += new System.Windows.Forms.MouseEventHandler(this.ListBoxTab_MouseDown);
            // 
            // ListBoxSites
            // 
            this.ListBoxSites.AllowDrop = true;
            this.ListBoxSites.FormattingEnabled = true;
            this.ListBoxSites.ItemHeight = 15;
            this.ListBoxSites.Location = new System.Drawing.Point(3, 10);
            this.ListBoxSites.Name = "ListBoxSites";
            this.ListBoxSites.Size = new System.Drawing.Size(198, 484);
            this.ListBoxSites.Sorted = true;
            this.ListBoxSites.TabIndex = 8;
            this.ListBoxSites.SelectedIndexChanged += new System.EventHandler(this.ListBoxSites_SelectedIndexChanged);
            this.ListBoxSites.DragDrop += new System.Windows.Forms.DragEventHandler(this.ListBoxSites_DragDrop);
            this.ListBoxSites.DragOver += new System.Windows.Forms.DragEventHandler(this.ListBoxSites_DragOver);
            this.ListBoxSites.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.ListBoxTab_MouseDoubleClick);
            this.ListBoxSites.MouseDown += new System.Windows.Forms.MouseEventHandler(this.ListBoxTab_MouseDown);
            // 
            // TabControlDeck
            // 
            this.TabControlDeck.Controls.Add(this.TabPagePool);
            this.TabControlDeck.Controls.Add(this.TabPageResources);
            this.TabControlDeck.Controls.Add(this.TabPageHazards);
            this.TabControlDeck.Controls.Add(this.TabPageSideboard);
            this.TabControlDeck.Controls.Add(this.TabPageSites);
            this.TabControlDeck.ItemSize = new System.Drawing.Size(41, 20);
            this.TabControlDeck.Location = new System.Drawing.Point(613, 37);
            this.TabControlDeck.Name = "TabControlDeck";
            this.TabControlDeck.SelectedIndex = 0;
            this.TabControlDeck.Size = new System.Drawing.Size(212, 529);
            this.TabControlDeck.SizeMode = System.Windows.Forms.TabSizeMode.Fixed;
            this.TabControlDeck.TabIndex = 14;
            // 
            // TabPagePool
            // 
            this.TabPagePool.Controls.Add(this.ListBoxPool);
            this.TabPagePool.Location = new System.Drawing.Point(4, 24);
            this.TabPagePool.Name = "TabPagePool";
            this.TabPagePool.Padding = new System.Windows.Forms.Padding(3);
            this.TabPagePool.Size = new System.Drawing.Size(204, 501);
            this.TabPagePool.TabIndex = 0;
            this.TabPagePool.Text = "Pool";
            this.TabPagePool.UseVisualStyleBackColor = true;
            // 
            // TabPageResources
            // 
            this.TabPageResources.Controls.Add(this.ListBoxResources);
            this.TabPageResources.Location = new System.Drawing.Point(4, 24);
            this.TabPageResources.Name = "TabPageResources";
            this.TabPageResources.Padding = new System.Windows.Forms.Padding(3);
            this.TabPageResources.Size = new System.Drawing.Size(204, 501);
            this.TabPageResources.TabIndex = 1;
            this.TabPageResources.Text = "Res";
            this.TabPageResources.UseVisualStyleBackColor = true;
            // 
            // TabPageHazards
            // 
            this.TabPageHazards.Controls.Add(this.ListBoxHazards);
            this.TabPageHazards.Location = new System.Drawing.Point(4, 24);
            this.TabPageHazards.Name = "TabPageHazards";
            this.TabPageHazards.Padding = new System.Windows.Forms.Padding(3);
            this.TabPageHazards.Size = new System.Drawing.Size(204, 501);
            this.TabPageHazards.TabIndex = 2;
            this.TabPageHazards.Text = "Haz";
            this.TabPageHazards.UseVisualStyleBackColor = true;
            // 
            // TabPageSideboard
            // 
            this.TabPageSideboard.Controls.Add(this.ListBoxSideboard);
            this.TabPageSideboard.Location = new System.Drawing.Point(4, 24);
            this.TabPageSideboard.Name = "TabPageSideboard";
            this.TabPageSideboard.Padding = new System.Windows.Forms.Padding(3);
            this.TabPageSideboard.Size = new System.Drawing.Size(204, 501);
            this.TabPageSideboard.TabIndex = 3;
            this.TabPageSideboard.Text = "Side";
            this.TabPageSideboard.UseVisualStyleBackColor = true;
            // 
            // TabPageSites
            // 
            this.TabPageSites.Controls.Add(this.ListBoxSites);
            this.TabPageSites.Location = new System.Drawing.Point(4, 24);
            this.TabPageSites.Name = "TabPageSites";
            this.TabPageSites.Padding = new System.Windows.Forms.Padding(3);
            this.TabPageSites.Size = new System.Drawing.Size(204, 501);
            this.TabPageSites.TabIndex = 4;
            this.TabPageSites.Text = "Site";
            this.TabPageSites.UseVisualStyleBackColor = true;
            // 
            // MenuStrip1
            // 
            this.MenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.editToolStripMenuItem,
            this.toolsToolStripMenuItem,
            this.ToolStripMenuItemSet,
            this.helpToolStripMenuItem});
            this.MenuStrip1.Location = new System.Drawing.Point(0, 0);
            this.MenuStrip1.Name = "MenuStrip1";
            this.MenuStrip1.Size = new System.Drawing.Size(838, 24);
            this.MenuStrip1.TabIndex = 15;
            this.MenuStrip1.Text = "File Menu";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.newToolStripMenuItem,
            this.openToolStripMenuItem,
            this.toolStripSeparator,
            this.SaveToolStripMenuItem,
            this.saveAsToolStripMenuItem,
            this.toolStripSeparator1,
            this.printToolStripMenuItem,
            this.printPreviewToolStripMenuItem,
            this.toolStripSeparator2,
            this.exitToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.fileToolStripMenuItem.Text = "&File";
            // 
            // newToolStripMenuItem
            // 
            this.newToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("newToolStripMenuItem.Image")));
            this.newToolStripMenuItem.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.newToolStripMenuItem.Name = "newToolStripMenuItem";
            this.newToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.N)));
            this.newToolStripMenuItem.Size = new System.Drawing.Size(146, 22);
            this.newToolStripMenuItem.Text = "&New";
            // 
            // openToolStripMenuItem
            // 
            this.openToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("openToolStripMenuItem.Image")));
            this.openToolStripMenuItem.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.openToolStripMenuItem.Name = "openToolStripMenuItem";
            this.openToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.O)));
            this.openToolStripMenuItem.Size = new System.Drawing.Size(146, 22);
            this.openToolStripMenuItem.Text = "&Open";
            this.openToolStripMenuItem.Click += new System.EventHandler(this.OpenToolStripMenuItem_Click);
            // 
            // toolStripSeparator
            // 
            this.toolStripSeparator.Name = "toolStripSeparator";
            this.toolStripSeparator.Size = new System.Drawing.Size(143, 6);
            // 
            // SaveToolStripMenuItem
            // 
            this.SaveToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("SaveToolStripMenuItem.Image")));
            this.SaveToolStripMenuItem.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.SaveToolStripMenuItem.Name = "SaveToolStripMenuItem";
            this.SaveToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.S)));
            this.SaveToolStripMenuItem.Size = new System.Drawing.Size(146, 22);
            this.SaveToolStripMenuItem.Text = "&Save";
            this.SaveToolStripMenuItem.Click += new System.EventHandler(this.SaveToolStripMenuItem_Click);
            // 
            // saveAsToolStripMenuItem
            // 
            this.saveAsToolStripMenuItem.Name = "saveAsToolStripMenuItem";
            this.saveAsToolStripMenuItem.Size = new System.Drawing.Size(146, 22);
            this.saveAsToolStripMenuItem.Text = "Save &As";
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(143, 6);
            // 
            // printToolStripMenuItem
            // 
            this.printToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("printToolStripMenuItem.Image")));
            this.printToolStripMenuItem.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.printToolStripMenuItem.Name = "printToolStripMenuItem";
            this.printToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.P)));
            this.printToolStripMenuItem.Size = new System.Drawing.Size(146, 22);
            this.printToolStripMenuItem.Text = "&Print";
            // 
            // printPreviewToolStripMenuItem
            // 
            this.printPreviewToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("printPreviewToolStripMenuItem.Image")));
            this.printPreviewToolStripMenuItem.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.printPreviewToolStripMenuItem.Name = "printPreviewToolStripMenuItem";
            this.printPreviewToolStripMenuItem.Size = new System.Drawing.Size(146, 22);
            this.printPreviewToolStripMenuItem.Text = "Print Pre&view";
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(143, 6);
            // 
            // exitToolStripMenuItem
            // 
            this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            this.exitToolStripMenuItem.Size = new System.Drawing.Size(146, 22);
            this.exitToolStripMenuItem.Text = "E&xit";
            // 
            // editToolStripMenuItem
            // 
            this.editToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.undoToolStripMenuItem,
            this.redoToolStripMenuItem,
            this.toolStripSeparator3,
            this.cutToolStripMenuItem,
            this.copyToolStripMenuItem,
            this.pasteToolStripMenuItem,
            this.toolStripSeparator4,
            this.selectAllToolStripMenuItem});
            this.editToolStripMenuItem.Name = "editToolStripMenuItem";
            this.editToolStripMenuItem.Size = new System.Drawing.Size(39, 20);
            this.editToolStripMenuItem.Text = "&Edit";
            // 
            // undoToolStripMenuItem
            // 
            this.undoToolStripMenuItem.Name = "undoToolStripMenuItem";
            this.undoToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Z)));
            this.undoToolStripMenuItem.Size = new System.Drawing.Size(144, 22);
            this.undoToolStripMenuItem.Text = "&Undo";
            // 
            // redoToolStripMenuItem
            // 
            this.redoToolStripMenuItem.Name = "redoToolStripMenuItem";
            this.redoToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Y)));
            this.redoToolStripMenuItem.Size = new System.Drawing.Size(144, 22);
            this.redoToolStripMenuItem.Text = "&Redo";
            // 
            // toolStripSeparator3
            // 
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            this.toolStripSeparator3.Size = new System.Drawing.Size(141, 6);
            // 
            // cutToolStripMenuItem
            // 
            this.cutToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("cutToolStripMenuItem.Image")));
            this.cutToolStripMenuItem.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.cutToolStripMenuItem.Name = "cutToolStripMenuItem";
            this.cutToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.X)));
            this.cutToolStripMenuItem.Size = new System.Drawing.Size(144, 22);
            this.cutToolStripMenuItem.Text = "Cu&t";
            // 
            // copyToolStripMenuItem
            // 
            this.copyToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("copyToolStripMenuItem.Image")));
            this.copyToolStripMenuItem.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.copyToolStripMenuItem.Name = "copyToolStripMenuItem";
            this.copyToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.C)));
            this.copyToolStripMenuItem.Size = new System.Drawing.Size(144, 22);
            this.copyToolStripMenuItem.Text = "&Copy";
            // 
            // pasteToolStripMenuItem
            // 
            this.pasteToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("pasteToolStripMenuItem.Image")));
            this.pasteToolStripMenuItem.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.pasteToolStripMenuItem.Name = "pasteToolStripMenuItem";
            this.pasteToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.V)));
            this.pasteToolStripMenuItem.Size = new System.Drawing.Size(144, 22);
            this.pasteToolStripMenuItem.Text = "&Paste";
            // 
            // toolStripSeparator4
            // 
            this.toolStripSeparator4.Name = "toolStripSeparator4";
            this.toolStripSeparator4.Size = new System.Drawing.Size(141, 6);
            // 
            // selectAllToolStripMenuItem
            // 
            this.selectAllToolStripMenuItem.Name = "selectAllToolStripMenuItem";
            this.selectAllToolStripMenuItem.Size = new System.Drawing.Size(144, 22);
            this.selectAllToolStripMenuItem.Text = "Select &All";
            // 
            // toolsToolStripMenuItem
            // 
            this.toolsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.customizeToolStripMenuItem,
            this.optionsToolStripMenuItem});
            this.toolsToolStripMenuItem.Name = "toolsToolStripMenuItem";
            this.toolsToolStripMenuItem.Size = new System.Drawing.Size(46, 20);
            this.toolsToolStripMenuItem.Text = "&Tools";
            // 
            // customizeToolStripMenuItem
            // 
            this.customizeToolStripMenuItem.Name = "customizeToolStripMenuItem";
            this.customizeToolStripMenuItem.Size = new System.Drawing.Size(130, 22);
            this.customizeToolStripMenuItem.Text = "&Customize";
            // 
            // optionsToolStripMenuItem
            // 
            this.optionsToolStripMenuItem.Name = "optionsToolStripMenuItem";
            this.optionsToolStripMenuItem.Size = new System.Drawing.Size(130, 22);
            this.optionsToolStripMenuItem.Text = "&Options";
            // 
            // ToolStripMenuItemSet
            // 
            this.ToolStripMenuItemSet.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.ToolStripMenuItemTW,
            this.ToolStripMenuItemTD,
            this.ToolStripMenuItemDM,
            this.ToolStripMenuItemLE,
            this.ToolStripMenuItemAS,
            this.ToolStripMenuItemWH,
            this.ToolStripMenuItemBA});
            this.ToolStripMenuItemSet.Name = "ToolStripMenuItemSet";
            this.ToolStripMenuItemSet.Size = new System.Drawing.Size(35, 20);
            this.ToolStripMenuItemSet.Text = "&Set";
            // 
            // ToolStripMenuItemTW
            // 
            this.ToolStripMenuItemTW.CheckOnClick = true;
            this.ToolStripMenuItemTW.Name = "ToolStripMenuItemTW";
            this.ToolStripMenuItemTW.Size = new System.Drawing.Size(181, 22);
            this.ToolStripMenuItemTW.Text = "The Wizards";
            this.ToolStripMenuItemTW.CheckedChanged += new System.EventHandler(this.ToolStripMenuItemTW_CheckedChanged);
            // 
            // ToolStripMenuItemTD
            // 
            this.ToolStripMenuItemTD.CheckOnClick = true;
            this.ToolStripMenuItemTD.Name = "ToolStripMenuItemTD";
            this.ToolStripMenuItemTD.Size = new System.Drawing.Size(181, 22);
            this.ToolStripMenuItemTD.Text = "The Dragons";
            this.ToolStripMenuItemTD.CheckedChanged += new System.EventHandler(this.ToolStripMenuItemTD_CheckedChanged);
            // 
            // ToolStripMenuItemDM
            // 
            this.ToolStripMenuItemDM.CheckOnClick = true;
            this.ToolStripMenuItemDM.Name = "ToolStripMenuItemDM";
            this.ToolStripMenuItemDM.Size = new System.Drawing.Size(181, 22);
            this.ToolStripMenuItemDM.Text = "Dark Dominions";
            this.ToolStripMenuItemDM.CheckedChanged += new System.EventHandler(this.ToolStripMenuItemDM_CheckedChanged);
            // 
            // ToolStripMenuItemLE
            // 
            this.ToolStripMenuItemLE.CheckOnClick = true;
            this.ToolStripMenuItemLE.Name = "ToolStripMenuItemLE";
            this.ToolStripMenuItemLE.Size = new System.Drawing.Size(181, 22);
            this.ToolStripMenuItemLE.Text = "The Lidless Eye";
            this.ToolStripMenuItemLE.CheckedChanged += new System.EventHandler(this.ToolStripMenuItemLE_CheckedChanged);
            // 
            // ToolStripMenuItemAS
            // 
            this.ToolStripMenuItemAS.CheckOnClick = true;
            this.ToolStripMenuItemAS.Name = "ToolStripMenuItemAS";
            this.ToolStripMenuItemAS.Size = new System.Drawing.Size(181, 22);
            this.ToolStripMenuItemAS.Text = "Against The Shadow";
            this.ToolStripMenuItemAS.CheckedChanged += new System.EventHandler(this.ToolStripMenuItemAS_CheckedChanged);
            // 
            // ToolStripMenuItemWH
            // 
            this.ToolStripMenuItemWH.CheckOnClick = true;
            this.ToolStripMenuItemWH.Name = "ToolStripMenuItemWH";
            this.ToolStripMenuItemWH.Size = new System.Drawing.Size(181, 22);
            this.ToolStripMenuItemWH.Text = "The White Hand";
            this.ToolStripMenuItemWH.CheckedChanged += new System.EventHandler(this.ToolStripMenuItemWH_CheckedChanged);
            // 
            // ToolStripMenuItemBA
            // 
            this.ToolStripMenuItemBA.CheckOnClick = true;
            this.ToolStripMenuItemBA.Name = "ToolStripMenuItemBA";
            this.ToolStripMenuItemBA.Size = new System.Drawing.Size(181, 22);
            this.ToolStripMenuItemBA.Text = "The Balrog";
            this.ToolStripMenuItemBA.CheckedChanged += new System.EventHandler(this.ToolStripMenuItemBA_CheckedChanged);
            // 
            // helpToolStripMenuItem
            // 
            this.helpToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.contentsToolStripMenuItem,
            this.indexToolStripMenuItem,
            this.searchToolStripMenuItem,
            this.toolStripSeparator5,
            this.aboutToolStripMenuItem});
            this.helpToolStripMenuItem.Name = "helpToolStripMenuItem";
            this.helpToolStripMenuItem.Size = new System.Drawing.Size(44, 20);
            this.helpToolStripMenuItem.Text = "&Help";
            // 
            // contentsToolStripMenuItem
            // 
            this.contentsToolStripMenuItem.Name = "contentsToolStripMenuItem";
            this.contentsToolStripMenuItem.Size = new System.Drawing.Size(122, 22);
            this.contentsToolStripMenuItem.Text = "&Contents";
            // 
            // indexToolStripMenuItem
            // 
            this.indexToolStripMenuItem.Name = "indexToolStripMenuItem";
            this.indexToolStripMenuItem.Size = new System.Drawing.Size(122, 22);
            this.indexToolStripMenuItem.Text = "&Index";
            // 
            // searchToolStripMenuItem
            // 
            this.searchToolStripMenuItem.Name = "searchToolStripMenuItem";
            this.searchToolStripMenuItem.Size = new System.Drawing.Size(122, 22);
            this.searchToolStripMenuItem.Text = "&Search";
            // 
            // toolStripSeparator5
            // 
            this.toolStripSeparator5.Name = "toolStripSeparator5";
            this.toolStripSeparator5.Size = new System.Drawing.Size(119, 6);
            // 
            // aboutToolStripMenuItem
            // 
            this.aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            this.aboutToolStripMenuItem.Size = new System.Drawing.Size(122, 22);
            this.aboutToolStripMenuItem.Text = "&About...";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(838, 579);
            this.Controls.Add(this.TabControlDeck);
            this.Controls.Add(this.PictureBoxCardImage);
            this.Controls.Add(this.ListBoxCardList);
            this.Controls.Add(this.MenuStrip1);
            this.MainMenuStrip = this.MenuStrip1;
            this.Name = "Form1";
            this.Text = "MECCG Deck Builder";
            this.contextMenuStrip1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.PictureBoxCardImage)).EndInit();
            this.TabControlDeck.ResumeLayout(false);
            this.TabPagePool.ResumeLayout(false);
            this.TabPageResources.ResumeLayout(false);
            this.TabPageHazards.ResumeLayout(false);
            this.TabPageSideboard.ResumeLayout(false);
            this.TabPageSites.ResumeLayout(false);
            this.MenuStrip1.ResumeLayout(false);
            this.MenuStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ListBox ListBoxCardList;
        private System.Windows.Forms.PictureBox PictureBoxCardImage;
        private System.Windows.Forms.ListBox ListBoxResources;
        private System.Windows.Forms.ListBox ListBoxHazards;
        private System.Windows.Forms.ListBox ListBoxSideboard;
        private System.Windows.Forms.ListBox ListBoxSites;
        private System.Windows.Forms.TabControl TabControlDeck;
        private System.Windows.Forms.TabPage TabPagePool;
        private System.Windows.Forms.TabPage TabPageResources;
        private System.Windows.Forms.TabPage TabPageHazards;
        private System.Windows.Forms.TabPage TabPageSideboard;
        private System.Windows.Forms.TabPage TabPageSites;
        private System.Windows.Forms.MenuStrip MenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem ToolStripMenuItemSet;
        private System.Windows.Forms.ToolStripMenuItem ToolStripMenuItemTW;
        private System.Windows.Forms.ToolStripMenuItem ToolStripMenuItemTD;
        private System.Windows.Forms.ToolStripMenuItem ToolStripMenuItemDM;
        private System.Windows.Forms.ToolStripMenuItem ToolStripMenuItemLE;
        private System.Windows.Forms.ToolStripMenuItem ToolStripMenuItemAS;
        private System.Windows.Forms.ToolStripMenuItem ToolStripMenuItemWH;
        private System.Windows.Forms.ToolStripMenuItem ToolStripMenuItemBA;
        private System.Windows.Forms.ListBox ListBoxPool;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem newToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator;
        private System.Windows.Forms.ToolStripMenuItem SaveToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem saveAsToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem printToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem printPreviewToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem editToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem undoToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem redoToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.ToolStripMenuItem cutToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem copyToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem pasteToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator4;
        private System.Windows.Forms.ToolStripMenuItem selectAllToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem toolsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem customizeToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem optionsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem helpToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem contentsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem indexToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem searchToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator5;
        private System.Windows.Forms.ToolStripMenuItem aboutToolStripMenuItem;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem ToolStripMenuItemCopyCard;
        private System.Windows.Forms.ToolStripMenuItem ToolStripMenuItemPool;
        private System.Windows.Forms.ToolStripMenuItem ToolStripMenuItemResource;
        private System.Windows.Forms.ToolStripMenuItem ToolStripMenuItemHazard;
        private System.Windows.Forms.ToolStripMenuItem ToolStripMenuItemSideboard;
        private System.Windows.Forms.ToolStripMenuItem ToolStripMenuItemSite;
    }
}

