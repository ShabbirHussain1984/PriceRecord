namespace PriceRecord.UI
{
    partial class MainForm
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
            if (disposing)
            {
                DetachLogSink();
                _cts?.Dispose();
                components?.Dispose();
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
            components = new System.ComponentModel.Container();
            grpDatabase = new GroupBox();
            lblConnection = new Label();
            txtConnectionString = new TextBox();
            btnTestConnection = new Button();
            splitMain = new SplitContainer();
            grpCategories = new GroupBox();
            lstCategories = new CheckedListBox();
            pnlCategoryButtons = new FlowLayoutPanel();
            chkSelectAll = new CheckBox();
            btnLoadCategories = new Button();
            splitRight = new SplitContainer();
            grdProducts = new DataGridView();
            pnlFilter = new Panel();
            txtFilter = new TextBox();
            lblFilter = new Label();
            lblRowCount = new Label();
            pnlStatus = new Panel();
            lblStatus = new Label();
            progressBar = new ProgressBar();
            pnlOptions = new FlowLayoutPanel();
            lblDelay = new Label();
            numDelay = new NumericUpDown();
            lblMaxPages = new Label();
            numMaxPages = new NumericUpDown();
            chkFetchDetails = new CheckBox();
            chkResume = new CheckBox();
            btnStart = new Button();
            btnStop = new Button();
            btnExport = new Button();
            rtbLog = new RichTextBox();
            filterTimer = new System.Windows.Forms.Timer(components);
            toolTip = new ToolTip(components);
            grpDatabase.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitMain).BeginInit();
            splitMain.Panel1.SuspendLayout();
            splitMain.Panel2.SuspendLayout();
            splitMain.SuspendLayout();
            grpCategories.SuspendLayout();
            pnlCategoryButtons.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitRight).BeginInit();
            splitRight.Panel1.SuspendLayout();
            splitRight.Panel2.SuspendLayout();
            splitRight.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)grdProducts).BeginInit();
            pnlFilter.SuspendLayout();
            pnlStatus.SuspendLayout();
            pnlOptions.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)numDelay).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numMaxPages).BeginInit();
            SuspendLayout();
            //
            // grpDatabase
            //
            grpDatabase.Controls.Add(btnTestConnection);
            grpDatabase.Controls.Add(txtConnectionString);
            grpDatabase.Controls.Add(lblConnection);
            grpDatabase.Dock = DockStyle.Top;
            grpDatabase.Location = new Point(8, 8);
            grpDatabase.Name = "grpDatabase";
            grpDatabase.Padding = new Padding(8);
            grpDatabase.Size = new Size(1168, 58);
            grpDatabase.TabIndex = 0;
            grpDatabase.TabStop = false;
            grpDatabase.Text = "SQL Server";
            //
            // lblConnection
            //
            lblConnection.AutoSize = true;
            lblConnection.Location = new Point(11, 26);
            lblConnection.Name = "lblConnection";
            lblConnection.Size = new Size(106, 15);
            lblConnection.TabIndex = 0;
            lblConnection.Text = "Connection string:";
            //
            // txtConnectionString
            //
            txtConnectionString.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtConnectionString.Location = new Point(123, 22);
            txtConnectionString.Name = "txtConnectionString";
            txtConnectionString.Size = new Size(904, 23);
            txtConnectionString.TabIndex = 1;
            //
            // btnTestConnection
            //
            btnTestConnection.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnTestConnection.Location = new Point(1035, 21);
            btnTestConnection.Name = "btnTestConnection";
            btnTestConnection.Size = new Size(122, 25);
            btnTestConnection.TabIndex = 2;
            btnTestConnection.Text = "Test Connection";
            btnTestConnection.UseVisualStyleBackColor = true;
            btnTestConnection.Click += btnTestConnection_Click;
            //
            // splitMain
            //
            splitMain.Dock = DockStyle.Fill;
            splitMain.FixedPanel = FixedPanel.Panel1;
            splitMain.Location = new Point(8, 106);
            splitMain.Name = "splitMain";
            splitMain.Panel1.Controls.Add(grpCategories);
            splitMain.Panel2.Controls.Add(splitRight);
            splitMain.Size = new Size(1168, 591);
            splitMain.SplitterDistance = 250;
            splitMain.TabIndex = 3;
            //
            // grpCategories
            //
            grpCategories.Controls.Add(lstCategories);
            grpCategories.Controls.Add(pnlCategoryButtons);
            grpCategories.Dock = DockStyle.Fill;
            grpCategories.Name = "grpCategories";
            grpCategories.Padding = new Padding(6);
            grpCategories.TabIndex = 0;
            grpCategories.TabStop = false;
            grpCategories.Text = "Categories";
            //
            // pnlCategoryButtons
            //
            pnlCategoryButtons.AutoSize = true;
            pnlCategoryButtons.Controls.Add(chkSelectAll);
            pnlCategoryButtons.Controls.Add(btnLoadCategories);
            pnlCategoryButtons.Dock = DockStyle.Top;
            pnlCategoryButtons.Name = "pnlCategoryButtons";
            pnlCategoryButtons.TabIndex = 0;
            pnlCategoryButtons.WrapContents = false;
            //
            // chkSelectAll
            //
            chkSelectAll.AutoSize = true;
            chkSelectAll.Margin = new Padding(3, 7, 12, 3);
            chkSelectAll.Name = "chkSelectAll";
            chkSelectAll.TabIndex = 0;
            chkSelectAll.Text = "Select All";
            chkSelectAll.UseVisualStyleBackColor = true;
            chkSelectAll.CheckedChanged += chkSelectAll_CheckedChanged;
            //
            // btnLoadCategories
            //
            btnLoadCategories.AutoSize = true;
            btnLoadCategories.Name = "btnLoadCategories";
            btnLoadCategories.TabIndex = 1;
            btnLoadCategories.Text = "Reload";
            btnLoadCategories.UseVisualStyleBackColor = true;
            btnLoadCategories.Click += btnLoadCategories_Click;
            //
            // lstCategories
            //
            lstCategories.CheckOnClick = true;
            lstCategories.Dock = DockStyle.Fill;
            lstCategories.IntegralHeight = false;
            lstCategories.Name = "lstCategories";
            lstCategories.TabIndex = 1;
            //
            // splitRight
            //
            splitRight.Dock = DockStyle.Fill;
            splitRight.Name = "splitRight";
            splitRight.Orientation = Orientation.Horizontal;
            splitRight.Panel1.Controls.Add(grdProducts);
            splitRight.Panel1.Controls.Add(pnlFilter);
            splitRight.Panel2.Controls.Add(rtbLog);
            splitRight.Size = new Size(914, 591);
            splitRight.SplitterDistance = 400;
            splitRight.TabIndex = 0;
            //
            // pnlFilter
            //
            pnlFilter.Controls.Add(lblRowCount);
            pnlFilter.Controls.Add(txtFilter);
            pnlFilter.Controls.Add(lblFilter);
            pnlFilter.Dock = DockStyle.Top;
            pnlFilter.Name = "pnlFilter";
            pnlFilter.Size = new Size(914, 32);
            pnlFilter.TabIndex = 0;
            //
            // lblFilter
            //
            lblFilter.AutoSize = true;
            lblFilter.Location = new Point(0, 8);
            lblFilter.Name = "lblFilter";
            lblFilter.TabIndex = 0;
            lblFilter.Text = "Filter by name:";
            //
            // txtFilter
            //
            txtFilter.Location = new Point(92, 4);
            txtFilter.Name = "txtFilter";
            txtFilter.PlaceholderText = "type to filter…";
            txtFilter.Size = new Size(280, 23);
            txtFilter.TabIndex = 1;
            txtFilter.TextChanged += txtFilter_TextChanged;
            //
            // lblRowCount
            //
            lblRowCount.AutoSize = true;
            lblRowCount.Location = new Point(384, 8);
            lblRowCount.Name = "lblRowCount";
            lblRowCount.TabIndex = 2;
            lblRowCount.Text = "0 rows";
            //
            // grdProducts
            //
            grdProducts.AllowUserToAddRows = false;
            grdProducts.AllowUserToDeleteRows = false;
            grdProducts.AllowUserToOrderColumns = true;
            grdProducts.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            grdProducts.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            grdProducts.Dock = DockStyle.Fill;
            grdProducts.Name = "grdProducts";
            grdProducts.ReadOnly = true;
            grdProducts.RowHeadersVisible = false;
            grdProducts.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grdProducts.TabIndex = 1;
            //
            // rtbLog
            //
            rtbLog.BackColor = Color.FromArgb(30, 30, 30);
            rtbLog.Dock = DockStyle.Fill;
            rtbLog.Font = new Font("Consolas", 9F);
            rtbLog.ForeColor = Color.Gainsboro;
            rtbLog.Name = "rtbLog";
            rtbLog.ReadOnly = true;
            rtbLog.TabIndex = 0;
            rtbLog.Text = "";
            rtbLog.WordWrap = false;
            //
            // pnlOptions
            //
            pnlOptions.AutoSize = true;
            pnlOptions.Controls.Add(lblDelay);
            pnlOptions.Controls.Add(numDelay);
            pnlOptions.Controls.Add(lblMaxPages);
            pnlOptions.Controls.Add(numMaxPages);
            pnlOptions.Controls.Add(chkFetchDetails);
            pnlOptions.Controls.Add(chkResume);
            pnlOptions.Controls.Add(btnStart);
            pnlOptions.Controls.Add(btnStop);
            pnlOptions.Controls.Add(btnExport);
            pnlOptions.Dock = DockStyle.Top;
            pnlOptions.Name = "pnlOptions";
            pnlOptions.Padding = new Padding(0, 4, 0, 4);
            pnlOptions.TabIndex = 1;
            //
            // lblDelay
            //
            lblDelay.AutoSize = true;
            lblDelay.Margin = new Padding(3, 8, 3, 0);
            lblDelay.Name = "lblDelay";
            lblDelay.Text = "Delay (ms):";
            //
            // numDelay
            //
            numDelay.Increment = new decimal(new int[] { 250, 0, 0, 0 });
            numDelay.Maximum = new decimal(new int[] { 60000, 0, 0, 0 });
            numDelay.Name = "numDelay";
            numDelay.Size = new Size(80, 23);
            numDelay.TabIndex = 0;
            numDelay.ThousandsSeparator = true;
            numDelay.Value = new decimal(new int[] { 1500, 0, 0, 0 });
            numDelay.ValueChanged += numDelay_ValueChanged;
            //
            // lblMaxPages
            //
            lblMaxPages.AutoSize = true;
            lblMaxPages.Margin = new Padding(15, 8, 3, 0);
            lblMaxPages.Name = "lblMaxPages";
            lblMaxPages.Text = "Max pages / category (0 = all):";
            //
            // numMaxPages
            //
            numMaxPages.Maximum = new decimal(new int[] { 1000, 0, 0, 0 });
            numMaxPages.Name = "numMaxPages";
            numMaxPages.Size = new Size(60, 23);
            numMaxPages.TabIndex = 1;
            //
            // chkFetchDetails
            //
            chkFetchDetails.AutoSize = true;
            chkFetchDetails.Margin = new Padding(15, 6, 3, 3);
            chkFetchDetails.Name = "chkFetchDetails";
            chkFetchDetails.TabIndex = 2;
            chkFetchDetails.Text = "Visit product pages";
            chkFetchDetails.UseVisualStyleBackColor = true;
            //
            // chkResume
            //
            chkResume.AutoSize = true;
            chkResume.Margin = new Padding(8, 6, 3, 3);
            chkResume.Name = "chkResume";
            chkResume.TabIndex = 3;
            chkResume.Text = "Resume previous run";
            chkResume.UseVisualStyleBackColor = true;
            //
            // btnStart
            //
            btnStart.Margin = new Padding(15, 3, 3, 3);
            btnStart.Name = "btnStart";
            btnStart.Size = new Size(80, 27);
            btnStart.TabIndex = 4;
            btnStart.Text = "Start";
            btnStart.UseVisualStyleBackColor = true;
            btnStart.Click += btnStart_Click;
            //
            // btnStop
            //
            btnStop.Enabled = false;
            btnStop.Name = "btnStop";
            btnStop.Size = new Size(80, 27);
            btnStop.TabIndex = 5;
            btnStop.Text = "Stop";
            btnStop.UseVisualStyleBackColor = true;
            btnStop.Click += btnStop_Click;
            //
            // btnExport
            //
            btnExport.Name = "btnExport";
            btnExport.Size = new Size(120, 27);
            btnExport.TabIndex = 6;
            btnExport.Text = "Export CSV/Excel…";
            btnExport.UseVisualStyleBackColor = true;
            btnExport.Click += btnExport_Click;
            //
            // pnlStatus
            //
            pnlStatus.Controls.Add(lblStatus);
            pnlStatus.Controls.Add(progressBar);
            pnlStatus.Dock = DockStyle.Bottom;
            pnlStatus.Name = "pnlStatus";
            pnlStatus.Padding = new Padding(0, 4, 0, 0);
            pnlStatus.Size = new Size(1168, 30);
            pnlStatus.TabIndex = 4;
            //
            // progressBar
            //
            progressBar.Dock = DockStyle.Left;
            progressBar.Name = "progressBar";
            progressBar.Size = new Size(300, 26);
            progressBar.TabIndex = 0;
            //
            // lblStatus
            //
            lblStatus.Dock = DockStyle.Fill;
            lblStatus.Name = "lblStatus";
            lblStatus.Padding = new Padding(8, 0, 0, 0);
            lblStatus.TabIndex = 1;
            lblStatus.Text = "Ready";
            lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            //
            // filterTimer
            //
            filterTimer.Interval = 300;
            filterTimer.Tick += filterTimer_Tick;
            //
            // MainForm
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1184, 761);
            Controls.Add(splitMain);
            Controls.Add(pnlStatus);
            Controls.Add(pnlOptions);
            Controls.Add(grpDatabase);
            MinimumSize = new Size(900, 600);
            Name = "MainForm";
            Padding = new Padding(8);
            StartPosition = FormStartPosition.CenterScreen;
            Text = "PriceRecord — priceoye.pk price collector";
            FormClosing += MainForm_FormClosing;
            Load += MainForm_Load;
            grpDatabase.ResumeLayout(false);
            grpDatabase.PerformLayout();
            splitMain.Panel1.ResumeLayout(false);
            splitMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitMain).EndInit();
            splitMain.ResumeLayout(false);
            grpCategories.ResumeLayout(false);
            grpCategories.PerformLayout();
            pnlCategoryButtons.ResumeLayout(false);
            pnlCategoryButtons.PerformLayout();
            splitRight.Panel1.ResumeLayout(false);
            splitRight.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitRight).EndInit();
            splitRight.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)grdProducts).EndInit();
            pnlFilter.ResumeLayout(false);
            pnlFilter.PerformLayout();
            pnlStatus.ResumeLayout(false);
            pnlOptions.ResumeLayout(false);
            pnlOptions.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)numDelay).EndInit();
            ((System.ComponentModel.ISupportInitialize)numMaxPages).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private GroupBox grpDatabase;
        private Label lblConnection;
        private TextBox txtConnectionString;
        private Button btnTestConnection;
        private SplitContainer splitMain;
        private GroupBox grpCategories;
        private CheckedListBox lstCategories;
        private FlowLayoutPanel pnlCategoryButtons;
        private CheckBox chkSelectAll;
        private Button btnLoadCategories;
        private SplitContainer splitRight;
        private DataGridView grdProducts;
        private Panel pnlFilter;
        private TextBox txtFilter;
        private Label lblFilter;
        private Label lblRowCount;
        private Panel pnlStatus;
        private Label lblStatus;
        private ProgressBar progressBar;
        private FlowLayoutPanel pnlOptions;
        private Label lblDelay;
        private NumericUpDown numDelay;
        private Label lblMaxPages;
        private NumericUpDown numMaxPages;
        private CheckBox chkFetchDetails;
        private CheckBox chkResume;
        private Button btnStart;
        private Button btnStop;
        private Button btnExport;
        private RichTextBox rtbLog;
        private System.Windows.Forms.Timer filterTimer;
        private ToolTip toolTip;
    }
}
