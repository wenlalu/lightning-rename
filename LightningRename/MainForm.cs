using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace LightningRename
{
    public class MainForm : Form
    {
        private const int DiskCheckLimit = 2000;

        private List<Item> master = new List<Item>();
        private HashSet<string> pathSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private Rules rules = new Rules();

        private bool refreshing;
        private bool ready;
        private bool running;
        private volatile bool cancelRequested;
        private string[] pendingArgs;

        private ToolStrip ts;
        private ToolStripComboBox tsSort;
        private ToolStripComboBox tsOrder;
        private ToolStripProgressBar tsProgress;

        private DataGridView dgv;
        private TabControl tabs;
        private Panel bottomBar;
        private Font boldFont;
        private System.Windows.Forms.Timer previewTimer;

        // 后台预览线程（解决大数据量/低配机输入卡顿）
        private readonly object previewLock = new object();
        private int previewSeq;      // 每次请求+1，作为最新请求标记
        private int listGen;         // 列表结构变更(增删/排序)时+1，使过期结果作废
        private Thread previewThread;
        private Item[] workItems;    // 后台计算用的轻量副本快照
        private Rules workRules;     // 后台计算用的规则快照
        private int workGen;         // 本次快照对应的 listGen

        private class PreviewState
        {
            public Item[] Items;
            public int WillRename;
            public int Conflicts;
            public int FileCount;
            public int DirCount;
        }

        // ① 查找替换
        private CheckBox chkReplace;
        private CheckBox chkCase;
        private TextBox txtFind;
        private TextBox txtReplace;
        private ComboBox cmbScope;

        // ② 增删字符
        private CheckBox chkAdd;
        private CheckBox chkRemove;
        private TextBox txtPrefix;
        private TextBox txtSuffix;
        private NumericUpDown numRF;
        private NumericUpDown numRL;
        private CheckBox chkInsert;
        private NumericUpDown numInsertPos;
        private TextBox txtInsertText;
        private ComboBox cmbInsertDir;
        private CheckBox chkInsertExt;

        // ③ 编号
        private CheckBox chkNum;
        private NumericUpDown numStart;
        private NumericUpDown numStep;
        private NumericUpDown numDigits;
        private ComboBox cmbNumPos;
        private TextBox txtNumSep;
        private Label lblNumTip;

        // ④ 日期
        private CheckBox chkDate;
        private ComboBox cmbDateFormat;
        private ComboBox cmbDatePos;
        private TextBox txtDateSep;

        // ⑤ 大小写/空格
        private ComboBox cmbCase;
        private CheckBox chkTrim;

        // ⑥ 扩展名
        private ComboBox cmbExt;
        private TextBox txtExt;

        // 底部
        private Label lblStatus;
        private Button btnRun;

        public MainForm()
        {
            Text = "闪电重命名 v1.0.0 - 批量重命名文件/文件夹";
            Size = new Size(980, 700);
            MinimumSize = new Size(860, 620);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Microsoft YaHei", 9f);
            BackColor = Color.White;
            boldFont = new Font(Font, FontStyle.Bold);
            AllowDrop = true;
            DragEnter += Main_DragEnter;
            DragDrop += Main_DragDrop;
            KeyPreview = true;
            KeyDown += MainForm_KeyDown;
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
            }
            previewTimer = new System.Windows.Forms.Timer();
            previewTimer.Interval = 180;
            previewTimer.Tick += OnPreviewTick;
            Shown += OnShown;
        }

        private void OnShown(object s, EventArgs e)
        {
            BuildToolbar();
            BuildGrid();
            BuildBottom();
            BuildTabs();
            bottomBar.SendToBack();
            dgv.BringToFront();
            ready = true;
            if (pendingArgs != null)
            {
                LoadArgs(pendingArgs);
                pendingArgs = null;
            }
            RefreshPreview();
        }

        // ==================== 界面构建 ====================

        private void BuildToolbar()
        {
            ts = new ToolStrip();
            ts.GripStyle = ToolStripGripStyle.Hidden;
            ts.RenderMode = ToolStripRenderMode.System;
            ts.BackColor = Color.White;
            ts.ForeColor = Color.FromArgb(30, 40, 54);
            ts.ImageScalingSize = new Size(16, 16);
            ts.Items.Add(MakeBtn("添加文件(F)", "选择要重命名的文件", OnAddFiles));
            ts.Items.Add(MakeBtn("添加文件夹(D)", "选择要重命名的文件夹（文件夹本身被重命名）", OnAddDirs));
            ts.Items.Add(MakeBtn("添加文件夹内容", "递归添加所选文件夹里的全部文件（适合上万档案）", OnAddDirContents));
            ts.Items.Add(MakeBtn("移除选中", "从列表移除选中行", OnRemoveSel));
            ts.Items.Add(MakeBtn("清空", "清空列表", OnClear));
            ts.Items.Add(new ToolStripSeparator());
            ts.Items.Add(MakeBtn("后悔药(撤销上次重命名)", "撤销上一次重命名操作 (Ctrl+Z)", OnUndo));
            ts.Items.Add(new ToolStripSeparator());
            ts.Items.Add(MakeBtn("保存规则", "保存当前规则配置到文件 (Ctrl+S)", OnSaveRules));
            ts.Items.Add(MakeBtn("加载规则", "从文件加载规则配置 (Ctrl+O)", OnLoadRules));
            ts.Items.Add(new ToolStripSeparator());
            ts.Items.Add(new ToolStripLabel("排序:"));
            tsSort = new ToolStripComboBox();
            tsSort.DropDownStyle = ComboBoxStyle.DropDownList;
            tsSort.Items.AddRange(new object[] { "不排序", "名称", "扩展名", "大小", "修改时间" });
            tsSort.SelectedIndex = 0;
            tsSort.SelectedIndexChanged += OnSortChanged;
            ts.Items.Add(tsSort);
            tsOrder = new ToolStripComboBox();
            tsOrder.DropDownStyle = ComboBoxStyle.DropDownList;
            tsOrder.Items.AddRange(new object[] { "升序", "降序" });
            tsOrder.SelectedIndex = 0;
            tsOrder.SelectedIndexChanged += OnSortChanged;
            ts.Items.Add(tsOrder);
            tsProgress = new ToolStripProgressBar();
            tsProgress.Style = ProgressBarStyle.Marquee;
            tsProgress.Visible = false;
            ts.Items.Add(tsProgress);
            ts.Dock = DockStyle.Top;
            Controls.Add(ts);
        }

        private ToolStripButton MakeBtn(string text, string tip, EventHandler click)
        {
            ToolStripButton btn = new ToolStripButton(text);
            btn.ToolTipText = tip;
            btn.Click += click;
            return btn;
        }

        private void BuildGrid()
        {
            dgv = new DataGridView();
            dgv.Dock = DockStyle.Fill;
            dgv.AllowUserToAddRows = false;
            dgv.AllowUserToDeleteRows = false;
            dgv.AllowUserToResizeRows = false;
            dgv.RowHeadersWidth = 30;
            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgv.MultiSelect = true;
            dgv.ReadOnly = true;
            dgv.VirtualMode = true;
            dgv.RowCount = 0;
            dgv.CellValueNeeded += OnCellValueNeeded;
            dgv.CellPainting += OnCellPainting;
            // 白色家族：白底、浅表头、柔和分隔线
            dgv.EnableHeadersVisualStyles = false;
            dgv.BackgroundColor = Color.White;
            dgv.GridColor = Color.FromArgb(221, 226, 232);
            dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(241, 243, 246);
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(30, 40, 54);
            dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(241, 243, 246);
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft YaHei", 9f, FontStyle.Bold);
            dgv.DefaultCellStyle.BackColor = Color.White;
            dgv.DefaultCellStyle.ForeColor = Color.FromArgb(40, 46, 56);
            dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(198, 220, 244);
            dgv.DefaultCellStyle.SelectionForeColor = Color.FromArgb(20, 32, 48);
            // 可读性：更高的行、统一正文字体
            dgv.RowTemplate.Height = 28;
            dgv.DefaultCellStyle.Font = new Font("Microsoft YaHei", 9f);
            dgv.RowsDefaultCellStyle.Padding = new Padding(3, 0, 3, 0);
            dgv.Columns.Add("cOld", "原名称");
            dgv.Columns.Add("cNew", "新名称(预览)");
            dgv.Columns.Add("cType", "类型");
            dgv.Columns.Add("cDir", "所在目录");
            dgv.Columns[0].Width = 240;
            dgv.Columns[1].Width = 260;
            dgv.Columns[2].Width = 60;
            dgv.Columns[3].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            // 预览列：浅色高亮底，配合加粗深色文字（白色家族 + 高对比）
            dgv.Columns[1].DefaultCellStyle.BackColor = Color.FromArgb(244, 249, 255);
            dgv.AllowDrop = true;
            dgv.DragEnter += Main_DragEnter;
            dgv.DragDrop += Main_DragDrop;

            ContextMenuStrip ctx = new ContextMenuStrip();
            ctx.Items.Add("移除选中", null, OnRemoveSel);
            ctx.Items.Add("打开所在目录", null, OnOpenDir);
            ctx.Items.Add("清空列表", null, OnClear);
            dgv.ContextMenuStrip = ctx;

            // 启用双缓冲减少闪烁
            typeof(DataGridView).InvokeMember("DoubleBuffered",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.SetProperty,
                null, dgv, new object[] { true });
            Controls.Add(dgv);
        }

        private void OnCellValueNeeded(object s, DataGridViewCellValueEventArgs e)
        {
            if (e.RowIndex >= 0 && e.RowIndex < master.Count)
            {
                Item item = master[e.RowIndex];
                switch (e.ColumnIndex)
                {
                    case 0:
                        e.Value = item.OldName;
                        break;
                    case 1:
                        e.Value = (item.Error != null)
                            ? (item.NewName + "  [" + item.Error + "]")
                            : item.NewName;
                        break;
                    case 2:
                        e.Value = item.IsDir ? "文件夹" : "文件";
                        break;
                    default:
                        e.Value = item.Dir;
                        break;
                }
            }
        }

        private void OnCellPainting(object s, DataGridViewCellPaintingEventArgs e)
        {
            // 预览列(cNew)自绘：白色家族主题 —— 预览文本一律【加粗 + 深色高对比】，
            // 不再区分改/未改（都醒目加粗）；报错行加粗暗红提示。
            if (e.RowIndex >= 0 && e.RowIndex < master.Count && e.ColumnIndex == 1)
            {
                Item item = master[e.RowIndex];
                e.PaintBackground(e.ClipBounds, true);
                // 无论选中/未选中，预览文字始终为深色"黑色家族"，保持高对比可读
                Color fore = (item.Error != null)
                    ? Color.FromArgb(166, 30, 30)
                    : Color.FromArgb(12, 26, 52);
                const TextFormatFlags flags = TextFormatFlags.EndEllipsis
                    | TextFormatFlags.VerticalCenter | TextFormatFlags.PreserveGraphicsClipping;
                string text = (item.Error != null)
                    ? (item.NewName + "  [" + item.Error + "]")
                    : item.NewName;
                TextRenderer.DrawText(e.Graphics, text, boldFont, e.CellBounds, fore, flags);
                e.Handled = true;
            }
        }

        private void BuildTabs()
        {
            tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;

            // ① 查找替换
            TabPage tp1 = new TabPage("① 查找替换");
            chkReplace = Chk(tp1, "启用查找替换", 12, 12);
            Lbl(tp1, "查找:", 16, 46);
            txtFind = Txt(tp1, 70, 43, 200);
            Lbl(tp1, "替换为:", 290, 46);
            txtReplace = Txt(tp1, 352, 43, 200);
            Lbl(tp1, "范围:", 16, 80);
            cmbScope = Cmb(tp1, 70, 77, 120, new[] { "主名(不含扩展名)", "扩展名", "完整名" });
            chkCase = Chk(tp1, "区分大小写", 220, 80);
            Label tip1 = Lbl(tp1, "说明: 查找内容会全部替换；范围为\u201C主名\u201D时不影响扩展名。", 16, 112);
            tip1.ForeColor = Color.Gray;
            tabs.TabPages.Add(tp1);

            // ② 增删字符
            TabPage tp2 = new TabPage("② 增删字符");
            chkAdd = Chk(tp2, "启用添加前缀/后缀", 12, 8);
            Lbl(tp2, "前缀:", 20, 38);
            txtPrefix = Txt(tp2, 70, 35, 180);
            Lbl(tp2, "后缀:", 270, 38);
            txtSuffix = Txt(tp2, 320, 35, 180);
            chkRemove = Chk(tp2, "启用删除字符(作用于主名)", 12, 64);
            Lbl(tp2, "删除前", 24, 94);
            numRF = Num(tp2, 80, 91, 60, 0, 200);
            Lbl(tp2, "个字符", 148, 94);
            Lbl(tp2, "删除后", 240, 94);
            numRL = Num(tp2, 296, 91, 60, 0, 200);
            Lbl(tp2, "个字符", 364, 94);
            chkInsert = Chk(tp2, "启用指定位置插入文本", 12, 120);
            cmbInsertDir = Cmb(tp2, 20, 148, 110, new[] { "从前往后数", "从后往前数" });
            numInsertPos = Num(tp2, 140, 148, 50, 0, 200);
            Lbl(tp2, "个字符后插入", 200, 151);
            txtInsertText = Txt(tp2, 300, 148, 120);
            chkInsertExt = Chk(tp2, "含扩展名", 430, 151);
            tabs.TabPages.Add(tp2);

            // ③ 编号
            TabPage tp3 = new TabPage("③ 编号");
            chkNum = Chk(tp3, "启用编号(按列表顺序)", 12, 12);
            Lbl(tp3, "起始值:", 20, 46);
            numStart = Num(tp3, 82, 43, 70, -99999, 999999);
            Lbl(tp3, "增量:", 170, 46);
            numStep = Num(tp3, 220, 43, 70, -9999, 9999);
            Lbl(tp3, "位数:", 310, 46);
            numDigits = Num(tp3, 360, 43, 60, 1, 10);
            Lbl(tp3, "位置:", 20, 80);
            cmbNumPos = Cmb(tp3, 70, 77, 90, new[] { "前缀", "后缀" });
            Lbl(tp3, "分隔文本:", 190, 80);
            txtNumSep = Txt(tp3, 262, 77, 80);
            lblNumTip = Lbl(tp3, "示例: 01 02 03 …", 370, 80);
            lblNumTip.ForeColor = Color.Gray;
            tabs.TabPages.Add(tp3);

            // ④ 日期
            TabPage tp4 = new TabPage("④ 日期");
            chkDate = Chk(tp4, "启用添加日期", 12, 12);
            Lbl(tp4, "格式:", 20, 46);
            cmbDateFormat = new ComboBox();
            cmbDateFormat.Location = new Point(70, 43);
            cmbDateFormat.Width = 180;
            cmbDateFormat.Items.AddRange(new object[]
            {
                "yyyy-MM-dd", "yyyyMMdd", "yyyy-MM-dd_HHmmss",
                "yyyy年MM月dd日", "HHmmss"
            });
            cmbDateFormat.Text = "yyyy-MM-dd";
            Hook(cmbDateFormat);
            tp4.Controls.Add(cmbDateFormat);
            Lbl(tp4, "位置:", 280, 46);
            cmbDatePos = Cmb(tp4, 330, 43, 90, new[] { "前缀", "后缀" });
            Lbl(tp4, "分隔文本:", 20, 80);
            txtDateSep = Txt(tp4, 92, 77, 80);
            Label tip4 = Lbl(tp4, "支持 .NET 日期格式，如 yyyy-MM-dd、yyyyMMdd、MM_dd 等。", 200, 80);
            tip4.ForeColor = Color.Gray;
            tabs.TabPages.Add(tp4);

            // ⑤ 大小写/空格
            TabPage tp5 = new TabPage("⑤ 大小写/空格");
            Lbl(tp5, "大小写转换:", 16, 24);
            cmbCase = Cmb(tp5, 110, 21, 140, new[] { "保持原样", "全部大写", "全部小写", "首字母大写" });
            chkTrim = Chk(tp5, "去除名称首尾空格", 16, 60);
            tabs.TabPages.Add(tp5);

            // ⑥ 扩展名
            TabPage tp6 = new TabPage("⑥ 扩展名");
            Lbl(tp6, "扩展名(仅文件):", 16, 24);
            cmbExt = Cmb(tp6, 120, 21, 120, new[] { "保持原样", "改为指定", "删除扩展名" });
            Lbl(tp6, "新扩展名:", 270, 24);
            txtExt = Txt(tp6, 345, 21, 100);
            Label tip6 = Lbl(tp6, "例如输入 jpg / txt / pdf；对文件夹无效。", 16, 60);
            tip6.ForeColor = Color.Gray;
            tabs.TabPages.Add(tp6);

            // 分隔条
            Splitter splitter = new Splitter();
            splitter.Dock = DockStyle.Bottom;
            splitter.Height = 6;

            txtFind.AccessibleName = "查找输入";
            txtReplace.AccessibleName = "替换输入";
            txtPrefix.AccessibleName = "前缀输入";
            txtSuffix.AccessibleName = "后缀输入";
            txtExt.AccessibleName = "新扩展名输入";

            Panel panel = new Panel();
            panel.Dock = DockStyle.Bottom;
            panel.Height = 240;
            tabs.Dock = DockStyle.Fill;
            panel.Controls.Add(tabs);
            Controls.Add(panel);
            Controls.Add(splitter);
        }

        private void BuildBottom()
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Bottom;
            panel.Height = 52;
            bottomBar = panel;

            lblStatus = new Label();
            lblStatus.Dock = DockStyle.Fill;
            lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            lblStatus.AutoSize = false;
            lblStatus.Text = "正在加载规则面板…";

            btnRun = new Button();
            btnRun.Text = "执行重命名";
            btnRun.Size = new Size(140, 38);
            btnRun.Dock = DockStyle.Right;
            btnRun.Font = new Font("Microsoft YaHei", 10.5f, FontStyle.Bold);
            btnRun.BackColor = Color.FromArgb(46, 160, 67);
            btnRun.ForeColor = Color.White;
            btnRun.FlatStyle = FlatStyle.Flat;
            btnRun.Enabled = false;
            btnRun.Click += OnRun;

            panel.Controls.Add(lblStatus);
            panel.Controls.Add(btnRun);
            Controls.Add(panel);
        }

        // ==================== 控件工厂方法 ====================

        private Label Lbl(Control parent, string text, int x, int y)
        {
            Label lbl = new Label();
            lbl.Text = text;
            lbl.Location = new Point(x, y);
            lbl.AutoSize = true;
            parent.Controls.Add(lbl);
            return lbl;
        }

        private TextBox Txt(Control parent, int x, int y, int w)
        {
            TextBox tb = new TextBox();
            tb.Location = new Point(x, y);
            tb.Width = w;
            Hook(tb);
            parent.Controls.Add(tb);
            return tb;
        }

        private CheckBox Chk(Control parent, string text, int x, int y)
        {
            CheckBox cb = new CheckBox();
            cb.Text = text;
            cb.Location = new Point(x, y);
            cb.AutoSize = true;
            Hook(cb);
            parent.Controls.Add(cb);
            return cb;
        }

        private ComboBox Cmb(Control parent, int x, int y, int w, string[] items)
        {
            ComboBox cb = new ComboBox();
            cb.DropDownStyle = ComboBoxStyle.DropDownList;
            cb.Location = new Point(x, y);
            cb.Width = w;
            cb.Items.AddRange(items);
            cb.SelectedIndex = 0;
            Hook(cb);
            parent.Controls.Add(cb);
            return cb;
        }

        private NumericUpDown Num(Control parent, int x, int y, int w, int min, int max)
        {
            NumericUpDown num = new NumericUpDown();
            num.Location = new Point(x, y);
            num.Width = w;
            num.Minimum = min;
            num.Maximum = max;
            Hook(num);
            parent.Controls.Add(num);
            return num;
        }

        private void Hook(Control c)
        {
            c.TextChanged += OnRuleChanged;
            CheckBox cb = c as CheckBox;
            if (cb != null) cb.CheckedChanged += OnRuleChanged;
            ComboBox cmb = c as ComboBox;
            if (cmb != null) cmb.SelectedIndexChanged += OnRuleChanged;
            NumericUpDown num = c as NumericUpDown;
            if (num != null) num.ValueChanged += OnRuleChanged;
        }

        // ==================== 预览刷新 ====================

        private void OnRuleChanged(object s, EventArgs e)
        {
            if (ready && !running && !refreshing)
            {
                previewTimer.Stop();
                previewTimer.Start();
            }
        }

        private void OnPreviewTick(object s, EventArgs e)
        {
            previewTimer.Stop();
            RefreshPreview();
        }

        private void OnSortChanged(object s, EventArgs e)
        {
            if (ready && !running)
            {
                ApplySort();
                listGen++;
                RefreshPreview();
            }
        }

        private void RefreshPreview()
        {
            if (!ready || running) return;
            RequestPreview();
        }

        // 性能优化核心：预览计算放到后台线程，UI 不再被整表重算+全量刷新卡住。
        // 输入/改规则只需提交"最新快照"，后台算完再回 UI 刷新可见区域。
        private void RequestPreview()
        {
            int gen = listGen;
            Rules r;
            try { r = ReadRules(); }
            catch { return; }

            int n = master.Count;
            Item[] snap = new Item[n];
            for (int i = 0; i < n; i++)
                snap[i] = master[i].DuplicateSnapshotForPreview();

            lock (previewLock)
            {
                workGen = gen;
                workRules = r;
                workItems = snap;
                previewSeq++;
                if (previewThread == null || !previewThread.IsAlive)
                {
                    previewThread = new Thread(PreviewWorker);
                    previewThread.IsBackground = true;
                    previewThread.Priority = ThreadPriority.BelowNormal;
                    previewThread.Start();
                }
            }
        }

        private void PreviewWorker()
        {
            while (true)
            {
                Item[] items; Rules rr; int gen; int reqSeq;
                lock (previewLock)
                {
                    items = workItems; rr = workRules; gen = workGen; reqSeq = previewSeq;
                }
                ComputeAndPost(items, rr, gen, reqSeq);
                lock (previewLock)
                {
                    if (previewSeq == reqSeq)   // 没有更新的请求，工作线程退出
                    {
                        previewThread = null;
                        break;
                    }
                }
            }
        }

        private void ComputeAndPost(Item[] items, Rules rr, int gen, int reqSeq)
        {
            if (items == null) return;
            PreviewState state;
            try
            {
                Engine.ComputeAll(new List<Item>(items), rr, items.Length <= DiskCheckLimit);
                int will = 0, con = 0, fc = 0, dc = 0;
                for (int i = 0; i < items.Length; i++)
                {
                    Item it = items[i];
                    if (it.IsDir) dc++; else fc++;
                    if (it.Error != null) con++;
                    else if (it.Changed) will++;
                }
                state = new PreviewState
                {
                    Items = items, WillRename = will, Conflicts = con, FileCount = fc, DirCount = dc
                };
            }
            catch
            {
                return;
            }
            try
            {
                BeginInvoke((Action)delegate { ApplyResults(state, gen, reqSeq); });
            }
            catch
            {
            }
        }

        private void ApplyResults(PreviewState st, int gen, int reqSeq)
        {
            if (running) return;
            if (listGen != gen)            // 列表结构已变，丢弃过期结果并用新列表重算
            {
                RequestPreview();
                return;
            }
            lock (previewLock)
            {
                if (previewSeq != reqSeq)   // 已有更新的请求，本结果作废
                    return;
            }

            int n = Math.Min(st.Items.Length, master.Count);
            for (int i = 0; i < n; i++)
            {
                master[i].NewName = st.Items[i].NewName;
                master[i].NewPath = st.Items[i].NewPath;
                master[i].Error = st.Items[i].Error;
            }
            if (master.Count != dgv.RowCount)
                dgv.RowCount = master.Count;
            dgv.Invalidate();

            string bigDataHint = (master.Count > DiskCheckLimit)
                ? " | 大数据量模式: 磁盘同名检查于执行时进行"
                : "";
            lblStatus.Text = string.Format(
                "共 {0} 项（{1} 文件 / {2} 文件夹）| 将重命名 {3} 项 | 冲突 {4} 项{5} | 规则顺序: ①替换→②增删/插入→③编号→④日期→⑤大小写→⑥扩展名",
                master.Count, st.FileCount, st.DirCount, st.WillRename, st.Conflicts, bigDataHint);
            btnRun.Enabled = !running && master.Count > 0 && st.WillRename > 0 && st.Conflicts == 0;

            if (lblNumTip != null)
            {
                int sv = (int)numStart.Value;
                int st2 = (int)numStep.Value;
                int dg = (int)numDigits.Value;
                lblNumTip.Text = string.Format("示例: {0} {1} {2} …",
                    sv.ToString().PadLeft(dg, '0'),
                    (sv + st2).ToString().PadLeft(dg, '0'),
                    (sv + 2 * st2).ToString().PadLeft(dg, '0'));
            }
        }

        // ==================== 规则读写 ====================

        private Rules ReadRules()
        {
            Rules r = new Rules();
            r.ReplaceEnabled = chkReplace.Checked;
            r.FindText = txtFind.Text;
            r.ReplaceText = txtReplace.Text;
            r.ReplaceScope = cmbScope.SelectedIndex;
            r.CaseSensitive = chkCase.Checked;
            r.AddEnabled = chkAdd.Checked;
            r.Prefix = txtPrefix.Text;
            r.Suffix = txtSuffix.Text;
            r.RemoveEnabled = chkRemove.Checked;
            r.RemoveFirst = (int)numRF.Value;
            r.RemoveLast = (int)numRL.Value;
            r.InsertEnabled = chkInsert.Checked;
            r.InsertPos = (int)numInsertPos.Value;
            r.InsertText = txtInsertText.Text;
            r.InsertFromEnd = (cmbInsertDir.SelectedIndex == 1);
            r.InsertIncludeExt = chkInsertExt.Checked;
            r.NumberEnabled = chkNum.Checked;
            r.NumberStart = (int)numStart.Value;
            r.NumberStep = (int)numStep.Value;
            r.NumberDigits = (int)numDigits.Value;
            r.NumberPos = cmbNumPos.SelectedIndex;
            r.NumberSep = txtNumSep.Text;
            r.DateEnabled = chkDate.Checked;
            r.DateFormat = cmbDateFormat.Text;
            r.DatePos = cmbDatePos.SelectedIndex;
            r.DateSep = txtDateSep.Text;
            r.CaseMode = cmbCase.SelectedIndex;
            r.TrimEnabled = chkTrim.Checked;
            r.ExtMode = cmbExt.SelectedIndex;
            r.ExtNew = txtExt.Text;
            return r;
        }

        private void ApplyRulesToUI(Rules r)
        {
            refreshing = true;
            try
            {
                chkReplace.Checked = r.ReplaceEnabled;
                txtFind.Text = r.FindText;
                txtReplace.Text = r.ReplaceText;
                cmbScope.SelectedIndex = r.ReplaceScope;
                chkCase.Checked = r.CaseSensitive;
                chkAdd.Checked = r.AddEnabled;
                txtPrefix.Text = r.Prefix;
                txtSuffix.Text = r.Suffix;
                chkRemove.Checked = r.RemoveEnabled;
                numRF.Value = Clamp(numRF, r.RemoveFirst);
                numRL.Value = Clamp(numRL, r.RemoveLast);
                chkInsert.Checked = r.InsertEnabled;
                numInsertPos.Value = Clamp(numInsertPos, r.InsertPos);
                txtInsertText.Text = r.InsertText;
                cmbInsertDir.SelectedIndex = r.InsertFromEnd ? 1 : 0;
                chkInsertExt.Checked = r.InsertIncludeExt;
                chkNum.Checked = r.NumberEnabled;
                numStart.Value = Clamp(numStart, r.NumberStart);
                numStep.Value = Clamp(numStep, r.NumberStep);
                numDigits.Value = Clamp(numDigits, r.NumberDigits);
                cmbNumPos.SelectedIndex = r.NumberPos;
                txtNumSep.Text = r.NumberSep;
                chkDate.Checked = r.DateEnabled;
                cmbDateFormat.Text = r.DateFormat;
                cmbDatePos.SelectedIndex = r.DatePos;
                txtDateSep.Text = r.DateSep;
                cmbCase.SelectedIndex = r.CaseMode;
                chkTrim.Checked = r.TrimEnabled;
                cmbExt.SelectedIndex = r.ExtMode;
                txtExt.Text = r.ExtNew;
            }
            finally
            {
                refreshing = false;
            }
        }

        private static decimal Clamp(NumericUpDown num, int value)
        {
            return Math.Max(num.Minimum, Math.Min(num.Maximum, value));
        }

        // ==================== 文件操作 ====================

        private void Main_DragEnter(object s, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        private void Main_DragDrop(object s, DragEventArgs e)
        {
            string[] paths = (string[])e.Data.GetData(DataFormats.FileDrop);
            AddPaths(paths);
        }

        private void OnAddFiles(object s, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Multiselect = true;
            ofd.Title = "选择要重命名的文件";
            if (ofd.ShowDialog() == DialogResult.OK)
                AddPaths(ofd.FileNames);
        }

        private void OnAddDirs(object s, EventArgs e)
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            fbd.Description = "选择要重命名的文件夹（文件夹本身将被重命名）";
            if (fbd.ShowDialog() == DialogResult.OK)
                AddPaths(new[] { fbd.SelectedPath });
        }

        private void OnAddDirContents(object s, EventArgs e)
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            fbd.Description = "选择文件夹：递归添加其中全部文件到列表";
            if (fbd.ShowDialog() == DialogResult.OK)
                LoadDirContentsAsync(fbd.SelectedPath);
        }

        private void OnRemoveSel(object s, EventArgs e)
        {
            if (running || dgv.SelectedRows.Count == 0) return;
            List<int> indices = new List<int>();
            foreach (DataGridViewRow row in dgv.SelectedRows)
                indices.Add(row.Index);
            indices.Sort();
            indices.Reverse();
            foreach (int idx in indices)
            {
                if (idx < master.Count)
                    pathSet.Remove(master[idx].Path);
                master.RemoveAt(idx);
            }
            dgv.ClearSelection();
            listGen++;
            dgv.RowCount = master.Count;
            RefreshPreview();
        }

        private void OnClear(object s, EventArgs e)
        {
            if (!running)
            {
                master.Clear();
                pathSet.Clear();
                listGen++;
                dgv.RowCount = 0;
                RefreshPreview();
            }
        }

        private void OnOpenDir(object s, EventArgs e)
        {
            if (dgv.CurrentRow == null || dgv.CurrentRow.Index >= master.Count) return;
            Item item = master[dgv.CurrentRow.Index];
            try
            {
                Process.Start("explorer.exe", "/select,\"" + item.Path + "\"");
            }
            catch
            {
            }
        }

        private void AddDirContents(string root)
        {
            AddPaths(CollectDirContents(root).ToArray());
        }

        // 后台扫描目录树，避免超大目录在 UI 线程长时间阻塞
        private void LoadDirContentsAsync(string root)
        {
            tsProgress.Visible = true;
            lblStatus.Text = "正在扫描文件夹…";
            Thread th = new Thread((ThreadStart)delegate
            {
                List<string> files = CollectDirContents(root);
                try
                {
                    BeginInvoke((Action)delegate
                    {
                        AddPaths(files.ToArray());
                        tsProgress.Visible = false;
                    });
                }
                catch { }
            });
            th.IsBackground = true;
            th.Start();
        }

        private List<string> CollectDirContents(string root)
        {
            Stack<string> stack = new Stack<string>();
            stack.Push(root);
            List<string> files = new List<string>();
            while (stack.Count > 0)
            {
                string dir = stack.Pop();
                try
                {
                    foreach (string f in Directory.EnumerateFiles(dir))
                        files.Add(f);
                    foreach (string d in Directory.EnumerateDirectories(dir))
                        stack.Push(d);
                }
                catch
                {
                }
            }
            files.Sort(StringComparer.OrdinalIgnoreCase);
            return files;
        }

        private void AddPaths(string[] paths)
        {
            if (paths == null) return;
            foreach (string path in paths)
            {
                string fullPath;
                try { fullPath = Path.GetFullPath(path); }
                catch { continue; }

                if (string.IsNullOrEmpty(fullPath) || pathSet.Contains(fullPath))
                    continue;

                bool isDir = Directory.Exists(fullPath);
                if (!isDir && !File.Exists(fullPath))
                    continue;

                long size = 0;
                DateTime mTime = DateTime.MinValue;
                try
                {
                    if (isDir)
                    {
                        DirectoryInfo di = new DirectoryInfo(fullPath);
                        mTime = di.LastWriteTime;
                    }
                    else
                    {
                        FileInfo fi = new FileInfo(fullPath);
                        size = fi.Length;
                        mTime = fi.LastWriteTime;
                    }
                }
                catch
                {
                }

                master.Add(new Item
                {
                    Path = fullPath,
                    IsDir = isDir,
                    InsOrder = master.Count,
                    Size = size,
                    MTime = mTime
                });
                pathSet.Add(fullPath);
            }
            if (master.Count != dgv.RowCount)
                dgv.RowCount = master.Count;
            listGen++;
            RefreshPreview();
        }

        public void AddInitialPaths(string[] paths)
        {
            pendingArgs = paths;
        }

        private void LoadArgs(string[] args)
        {
            List<string> files = new List<string>();
            foreach (string arg in args)
            {
                if (arg != null && arg.StartsWith("loadall:", StringComparison.OrdinalIgnoreCase))
                {
                    string dir = arg.Substring(8);
                    if (Directory.Exists(dir))
                        AddDirContents(dir);
                }
                else
                {
                    files.Add(arg);
                }
            }
            if (files.Count > 0)
                AddPaths(files.ToArray());
        }

        private void ApplySort()
        {
            int mode = tsSort.SelectedIndex;
            bool desc = tsOrder.SelectedIndex == 1;
            if (mode == 0)
            {
                master.Sort((a, b) => a.InsOrder.CompareTo(b.InsOrder));
                if (desc) master.Reverse();
                return;
            }
            master.Sort(delegate(Item a, Item b)
            {
                int cmp = 0;
                if (mode == 1)
                    cmp = string.Compare(a.OldName, b.OldName, StringComparison.OrdinalIgnoreCase);
                else if (mode == 2)
                    cmp = string.Compare(Engine.ExtOf(a.OldName, a.IsDir),
                                         Engine.ExtOf(b.OldName, b.IsDir),
                                         StringComparison.OrdinalIgnoreCase);
                else if (mode == 3)
                    cmp = a.Size.CompareTo(b.Size);
                else if (mode == 4)
                    cmp = a.MTime.CompareTo(b.MTime);
                if (cmp == 0)
                    cmp = string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase);
                return cmp;
            });
            if (desc) master.Reverse();
        }

        // ==================== 规则保存/加载 ====================

        private void OnSaveRules(object s, EventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "闪电重命名规则 (*.wrrules)|*.wrrules|所有文件 (*.*)|*.*";
            sfd.DefaultExt = "wrrules";
            sfd.FileName = "重命名规则.wrrules";
            sfd.Title = "保存规则配置";
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    Rules r = ReadRules();
                    r.Save(sfd.FileName);
                    MessageBox.Show(this, "规则已保存到:\n" + sfd.FileName,
                        "保存成功", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "保存失败: " + ex.Message,
                        "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void OnLoadRules(object s, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "闪电重命名规则 (*.wrrules)|*.wrrules|所有文件 (*.*)|*.*";
            ofd.DefaultExt = "wrrules";
            ofd.Title = "加载规则配置";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                Rules r = new Rules();
                if (r.Load(ofd.FileName))
                {
                    ApplyRulesToUI(r);
                    RefreshPreview();
                    MessageBox.Show(this, "规则已加载。",
                        "加载成功", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                }
                else
                {
                    MessageBox.Show(this, "规则文件格式不正确或已损坏。",
                        "加载失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // ==================== 快捷键 ====================

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (running)
            {
                if (e.KeyCode == Keys.Escape)
                {
                    cancelRequested = true;
                    e.Handled = true;
                }
                return;
            }
            if (e.Control)
            {
                if (e.KeyCode == Keys.S) { OnSaveRules(null, null); e.Handled = true; }
                else if (e.KeyCode == Keys.O) { OnLoadRules(null, null); e.Handled = true; }
                else if (e.KeyCode == Keys.Z) { OnUndo(null, null); e.Handled = true; }
                else if (e.KeyCode == Keys.Enter) { OnRun(null, null); e.Handled = true; }
                else if (e.KeyCode == Keys.A) { dgv.SelectAll(); e.Handled = true; }
            }
            else
            {
                if (e.KeyCode == Keys.F2) { OnAddFiles(null, null); e.Handled = true; }
                else if (e.KeyCode == Keys.F3) { OnAddDirs(null, null); e.Handled = true; }
                else if (e.KeyCode == Keys.F4) { OnAddDirContents(null, null); e.Handled = true; }
                else if (e.KeyCode == Keys.Delete) { OnRemoveSel(null, null); e.Handled = true; }
            }
        }

        // ==================== 执行重命名 ====================

        private void SetBusy(bool busy)
        {
            ts.Enabled = !busy;
            tabs.Enabled = !busy;
            dgv.Enabled = !busy;
            tsProgress.Visible = busy;
            btnRun.Text = busy ? "取消" : "执行重命名";
            btnRun.BackColor = busy ? Color.FromArgb(180, 60, 50) : Color.FromArgb(46, 160, 67);
            btnRun.Enabled = true;
        }

        private void OnRun(object s, EventArgs e)
        {
            if (running)
            {
                cancelRequested = true;
                return;
            }
            previewTimer.Stop();
            rules = ReadRules();
            Engine.ComputeAll(master, rules, checkDisk: true);
            dgv.RowCount = master.Count;
            dgv.Refresh();

            int willRename = 0, conflicts = 0;
            foreach (Item item in master)
            {
                if (item.Error != null) conflicts++;
                else if (item.Changed) willRename++;
            }

            if (conflicts > 0)
            {
                MessageBox.Show(this, "存在冲突项（标红行），请先处理。",
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                RefreshPreview();
                return;
            }
            if (willRename == 0) return;

            running = true;
            cancelRequested = false;
            SetBusy(true);
            lblStatus.Text = "正在重命名 " + willRename + " 项…（点击\u201C取消\u201D可中止并回滚）";

            Thread thread = new Thread((ThreadStart)delegate
            {
                List<string[]> done;
                string err = Engine.Execute(master, out done, () => cancelRequested);
                try
                {
                    BeginInvoke((Action)delegate { FinishRun(err, done); });
                }
                catch
                {
                }
            });
            thread.IsBackground = true;
            thread.Start();
        }

        private void FinishRun(string err, List<string[]> done)
        {
            running = false;
            SetBusy(false);
            if (err != null)
            {
                RefreshPreview();
                MessageBox.Show(this, err, "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            UndoLog.Append(done);
            pathSet.Clear();
            Dictionary<string, Item> byOldPath = new Dictionary<string, Item>(StringComparer.OrdinalIgnoreCase);
            foreach (Item item in master)
                byOldPath[item.Path] = item;
            foreach (string[] pair in done)
            {
                Item item;
                if (byOldPath.TryGetValue(pair[0], out item))
                    item.Path = pair[1];
            }
            foreach (Item item in master)
                pathSet.Add(item.Path);
            listGen++;
            RefreshPreview();
            MessageBox.Show(this,
                "成功重命名 " + done.Count + " 项。\n如需恢复，点击工具栏\u201C后悔药(撤销上次重命名)\u201D。",
                "完成", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
        }

        private void OnUndo(object s, EventArgs e)
        {
            if (running)
            {
                cancelRequested = true;
                return;
            }
            List<string[]> block = UndoLog.ReadLast();
            if (block == null)
            {
                MessageBox.Show(this, "没有可撤销的重命名记录。",
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }
            List<string[]> reverse = new List<string[]>();
            for (int i = block.Count - 1; i >= 0; i--)
            {
                reverse.Add(new[] { block[i][1], block[i][0], block[i][2] });
            }
            running = true;
            cancelRequested = false;
            SetBusy(true);
            lblStatus.Text = "正在撤销 " + block.Count + " 项…";
            Thread thread = new Thread((ThreadStart)delegate
            {
                string err = Engine.ExecutePairs(reverse, () => cancelRequested);
                try
                {
                    BeginInvoke((Action)delegate { FinishUndo(err, block); });
                }
                catch
                {
                }
            });
            thread.IsBackground = true;
            thread.Start();
        }

        private void FinishUndo(string err, List<string[]> block)
        {
            running = false;
            SetBusy(false);
            if (err != null)
            {
                RefreshPreview();
                MessageBox.Show(this, err, "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            UndoLog.RemoveLast();
            Dictionary<string, string> newToOld = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string[] pair in block)
                newToOld[pair[1]] = pair[0];
            foreach (Item item in master)
            {
                string oldPath;
                if (newToOld.TryGetValue(item.Path, out oldPath))
                    item.Path = oldPath;
            }
            pathSet.Clear();
            foreach (Item item in master)
                pathSet.Add(item.Path);
            listGen++;
            RefreshPreview();
            MessageBox.Show(this, "已撤销上一次重命名（" + block.Count + " 项）。",
                "完成", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
        }
    }
}
