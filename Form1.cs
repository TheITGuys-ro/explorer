using System;
using System.IO;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace FileExplorerPro;

public partial class Form1 : Form
{
    [DllImport("DwmApi")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, int[] attrValue, int attrSize);

    private SplitContainer mainSplitter;
    
    // Progress UI
    private Panel progressPanel;
    private ProgressBar actionProgressBar;
    private Label actionProgressLabel;
    
    // Left Pane Controls
    private TextBox leftPathBox;
    private ListView leftListView;
    
    // Right Pane Controls
    private TextBox rightPathBox;
    private ListView rightListView;

    private bool isLeftActive = true;
    private bool isProcessing = false;

    // Premium Color Palette (Dark Theme)
    private Color bgColor = Color.FromArgb(32, 32, 32);
    private Color panelColor = Color.FromArgb(45, 45, 48);
    private Color listColor = Color.FromArgb(30, 30, 30);
    private Color textColor = Color.FromArgb(240, 240, 240);
    private Color primaryColor = Color.FromArgb(0, 122, 204);
    private Color dangerColor = Color.FromArgb(190, 50, 50);
    private Font mainFont = new Font("Segoe UI", 10F, FontStyle.Regular);
    private Font headerFont = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);

    public Form1()
    {
        InitializeComponent();
        this.Text = "File Explorer Pro (Premium Edition)";
        this.Size = new Size(1400, 850);
        this.BackColor = bgColor;
        this.ForeColor = textColor;
        this.Font = mainFont;
        
        SetupUI();
        
        LoadDirectory(leftListView, leftPathBox, "C:\\");
        LoadDirectory(rightListView, rightPathBox, "D:\\");

        this.Load += (s, e) => { mainSplitter.SplitterDistance = mainSplitter.Width / 2; };
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        // Enable Dark Mode Title Bar & Scrollbars (Windows 10/11)
        if (DwmSetWindowAttribute(this.Handle, 19, new[] { 1 }, 4) != 0)
            DwmSetWindowAttribute(this.Handle, 20, new[] { 1 }, 4);
        base.OnHandleCreated(e);
    }

    private void SetupUI()
    {
        // Bottom Action Panel
        Panel bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 60, Padding = new Padding(15, 10, 15, 10), BackColor = panelColor };
        
        Button copyBtn = CreatePremiumButton("\U0001F4CB   Copy (F5)", primaryColor);
        copyBtn.Click += (s, e) => PerformAction(FileAction.Copy);
        
        Button moveBtn = CreatePremiumButton("\u2702   Move (F6)", primaryColor);
        moveBtn.Click += (s, e) => PerformAction(FileAction.Move);
        
        Button delBtn = CreatePremiumButton("\U0001F5D1   Delete (F8)", dangerColor);
        delBtn.Click += (s, e) => PerformAction(FileAction.Delete);
        
        Button refBtn = CreatePremiumButton("\U0001F504   Refresh", Color.FromArgb(70, 70, 70));
        refBtn.Dock = DockStyle.Right;
        refBtn.Click += (s, e) => { RefreshPane(true); RefreshPane(false); };

        bottomPanel.Controls.Add(delBtn);
        bottomPanel.Controls.Add(CreateSpacer(15));
        bottomPanel.Controls.Add(moveBtn);
        bottomPanel.Controls.Add(CreateSpacer(15));
        bottomPanel.Controls.Add(copyBtn);
        bottomPanel.Controls.Add(refBtn);

        mainSplitter = new SplitContainer 
        { 
            Dock = DockStyle.Fill, 
            BackColor = panelColor, 
            SplitterWidth = 3 
        };
        
        // Setup Left Pane
        leftPathBox = CreatePremiumTextBox();
        leftListView = CreateListView();
        leftListView.Enter += (s, e) => { isLeftActive = true; HighlightActivePane(); };
        leftListView.DoubleClick += (s, e) => HandleDoubleClick(leftListView, leftPathBox);
        mainSplitter.Panel1.Controls.Add(CreatePaneContainer(leftPathBox, leftListView));

        // Setup Right Pane
        rightPathBox = CreatePremiumTextBox();
        rightListView = CreateListView();
        rightListView.Enter += (s, e) => { isLeftActive = false; HighlightActivePane(); };
        rightListView.DoubleClick += (s, e) => HandleDoubleClick(rightListView, rightPathBox);
        mainSplitter.Panel2.Controls.Add(CreatePaneContainer(rightPathBox, rightListView));

        // Progress Panel
        progressPanel = new Panel { Dock = DockStyle.Bottom, Height = 30, Padding = new Padding(15, 0, 15, 10), BackColor = bgColor, Visible = false };
        actionProgressLabel = new Label { Dock = DockStyle.Right, Width = 350, ForeColor = textColor, TextAlign = ContentAlignment.MiddleRight, Font = mainFont, AutoEllipsis = true };
        actionProgressBar = new ProgressBar { Dock = DockStyle.Fill, Style = ProgressBarStyle.Continuous };
        progressPanel.Controls.Add(actionProgressBar);
        progressPanel.Controls.Add(actionProgressLabel);

        this.Controls.Add(mainSplitter);
        this.Controls.Add(progressPanel);
        this.Controls.Add(bottomPanel);
        mainSplitter.BringToFront(); // Ensures Fill takes remaining space and doesn't overlap Bottom docks
        this.KeyPreview = true;
        this.KeyDown += Form1_KeyDown;
        
        HighlightActivePane();
    }

    private Panel CreatePaneContainer(TextBox pathBox, ListView lv)
    {
        Panel container = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10), BackColor = bgColor };
        
        TableLayoutPanel navPanel = new TableLayoutPanel 
        { 
            Dock = DockStyle.Top, 
            Height = 35, 
            ColumnCount = 3,
            BackColor = bgColor
        };
        navPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50));
        navPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        navPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50));

        Button upBtn = CreateSmallButton("Up");
        upBtn.Dock = DockStyle.Fill;
        upBtn.Click += (s, e) => GoUp(pathBox, lv);
        
        Button goBtn = CreateSmallButton("Go");
        goBtn.Dock = DockStyle.Fill;
        goBtn.Click += (s, e) => LoadDirectory(lv, pathBox, pathBox.Text);

        pathBox.Dock = DockStyle.Fill;
        pathBox.Margin = new Padding(5, 4, 5, 4);
        pathBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; LoadDirectory(lv, pathBox, pathBox.Text); } };

        navPanel.Controls.Add(upBtn, 0, 0);
        navPanel.Controls.Add(pathBox, 1, 0);
        navPanel.Controls.Add(goBtn, 2, 0);

        container.Controls.Add(lv);
        container.Controls.Add(CreateSpacer(0, 10)); // Vertical Spacer
        container.Controls.Add(navPanel);
        return container;
    }

    private Button CreatePremiumButton(string text, Color backColor)
    {
        Button b = new Button
        {
            Text = text,
            Width = 140,
            Dock = DockStyle.Left,
            FlatStyle = FlatStyle.Flat,
            BackColor = backColor,
            ForeColor = Color.White,
            Font = headerFont,
            Cursor = Cursors.Hand
        };
        b.FlatAppearance.BorderSize = 0;
        b.MouseEnter += (s, e) => b.BackColor = ControlPaint.Light(backColor);
        b.MouseLeave += (s, e) => b.BackColor = backColor;
        return b;
    }

    private Button CreateSmallButton(string text)
    {
        Button b = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = panelColor,
            ForeColor = textColor,
            Font = mainFont,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 2, 0, 2)
        };
        b.FlatAppearance.BorderSize = 0;
        b.MouseEnter += (s, e) => b.BackColor = ControlPaint.Light(panelColor);
        b.MouseLeave += (s, e) => b.BackColor = panelColor;
        return b;
    }

    private TextBox CreatePremiumTextBox()
    {
        return new TextBox
        {
            BackColor = listColor,
            ForeColor = textColor,
            Font = new Font("Segoe UI", 11F),
            BorderStyle = BorderStyle.FixedSingle
        };
    }

    private Panel CreateSpacer(int width, int height = 0)
    {
        Panel p = new Panel { BackColor = Color.Transparent };
        if (width > 0) { p.Width = width; p.Dock = DockStyle.Left; }
        if (height > 0) { p.Height = height; p.Dock = DockStyle.Top; }
        return p;
    }

    private ListView CreateListView()
    {
        ListView lv = new ListView 
        { 
            Dock = DockStyle.Fill, 
            View = View.Details, 
            FullRowSelect = true, 
            HideSelection = false,
            BackColor = listColor,
            ForeColor = textColor,
            BorderStyle = BorderStyle.FixedSingle,
            Font = mainFont,
            OwnerDraw = true
        };
        lv.Columns.Add("Name", 300);
        lv.Columns.Add("Size", 100);
        lv.Columns.Add("Type", 120);
        lv.Columns.Add("Date Modified", 160);

        // Custom draw to fix ugly white headers
        lv.DrawColumnHeader += (s, e) =>
        {
            using (SolidBrush bgBrush = new SolidBrush(panelColor))
            {
                e.Graphics.FillRectangle(bgBrush, e.Bounds);
            }
            using (Pen borderPen = new Pen(Color.FromArgb(60, 60, 60)))
            {
                e.Graphics.DrawRectangle(borderPen, e.Bounds);
            }
            TextRenderer.DrawText(e.Graphics, e.Header.Text, headerFont, e.Bounds, textColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.LeftAndRightPadding);
        };
        
        lv.DrawItem += (s, e) => e.DrawDefault = true;
        lv.DrawSubItem += (s, e) => e.DrawDefault = true;

        return lv;
    }

    private void HighlightActivePane()
    {
        leftPathBox.BackColor = isLeftActive ? Color.FromArgb(40, 60, 80) : listColor;
        rightPathBox.BackColor = !isLeftActive ? Color.FromArgb(40, 60, 80) : listColor;
    }

    private void Form1_KeyDown(object? sender, KeyEventArgs e)
    {
        if (isProcessing) return;
        
        if (e.KeyCode == Keys.F5) { PerformAction(FileAction.Copy); e.Handled = true; }
        else if (e.KeyCode == Keys.F6) { PerformAction(FileAction.Move); e.Handled = true; }
        else if (e.KeyCode == Keys.F8 || e.KeyCode == Keys.Delete) { PerformAction(FileAction.Delete); e.Handled = true; }
    }

    private void RefreshPane(bool left)
    {
        if (left) LoadDirectory(leftListView, leftPathBox, leftPathBox.Text);
        else LoadDirectory(rightListView, rightPathBox, rightPathBox.Text);
    }

    private void LoadDirectory(ListView lv, TextBox pathBox, string path)
    {
        try
        {
            if (string.IsNullOrEmpty(path))
            {
                lv.Items.Clear();
                pathBox.Text = "";
                foreach (DriveInfo drive in DriveInfo.GetDrives())
                {
                    ListViewItem item = new ListViewItem(drive.Name);
                    item.SubItems.Add("");
                    item.SubItems.Add("Drive");
                    item.SubItems.Add("");
                    item.Tag = drive.Name;
                    lv.Items.Add(item);
                }
                return;
            }

            DirectoryInfo dir = new DirectoryInfo(path);
            if (!dir.Exists) return;

            pathBox.Text = dir.FullName;
            lv.Items.Clear();

            if (dir.Parent != null)
            {
                ListViewItem upItem = new ListViewItem("..");
                upItem.SubItems.Add("");
                upItem.SubItems.Add("Folder");
                upItem.SubItems.Add("");
                upItem.Tag = dir.Parent.FullName;
                lv.Items.Add(upItem);
            }
            else
            {
                ListViewItem rootItem = new ListViewItem("..");
                rootItem.SubItems.Add("");
                rootItem.SubItems.Add("Drives");
                rootItem.SubItems.Add("");
                rootItem.Tag = "";
                lv.Items.Add(rootItem);
            }

            foreach (DirectoryInfo subDir in dir.GetDirectories())
            {
                ListViewItem item = new ListViewItem(subDir.Name);
                item.SubItems.Add("");
                item.SubItems.Add("Folder");
                item.SubItems.Add(subDir.LastWriteTime.ToString("g"));
                item.Tag = subDir.FullName;
                lv.Items.Add(item);
            }

            foreach (FileInfo file in dir.GetFiles())
            {
                ListViewItem item = new ListViewItem(file.Name);
                item.SubItems.Add(FormatSize(file.Length));
                item.SubItems.Add(file.Extension);
                item.SubItems.Add(file.LastWriteTime.ToString("g"));
                item.Tag = file.FullName;
                lv.Items.Add(item);
            }
        }
        catch (Exception ex)
        {
            // Ignore access errors silently or log them
            Console.WriteLine(ex.Message);
        }
    }

    private void HandleDoubleClick(ListView lv, TextBox pathBox)
    {
        if (isProcessing) return;
        if (lv.SelectedItems.Count == 0) return;
        string path = (string)lv.SelectedItems[0].Tag;

        if (string.IsNullOrEmpty(path))
        {
            LoadDirectory(lv, pathBox, "");
        }
        else if (Directory.Exists(path))
        {
            LoadDirectory(lv, pathBox, path);
        }
        else if (File.Exists(path))
        {
            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error opening file", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void GoUp(TextBox pathBox, ListView lv)
    {
        string path = pathBox.Text;
        if (string.IsNullOrEmpty(path)) return;
        DirectoryInfo? parent = Directory.GetParent(path);
        if (parent != null)
        {
            LoadDirectory(lv, pathBox, parent.FullName);
        }
        else
        {
            LoadDirectory(lv, pathBox, "");
        }
    }

    private string FormatSize(long bytes)
    {
        string[] suf = { "B", "KB", "MB", "GB", "TB" };
        if (bytes == 0) return "0 B";
        long bytesAbs = Math.Abs(bytes);
        int place = Convert.ToInt32(Math.Floor(Math.Log(bytesAbs, 1024)));
        double num = Math.Round(bytesAbs / Math.Pow(1024, place), 1);
        return (Math.Sign(bytes) * num).ToString() + " " + suf[place];
    }

    private enum FileAction { Copy, Move, Delete }

    private class FileProgress
    {
        public long TotalBytes { get; set; }
        public long BytesTransferred { get; set; }
        public string CurrentFile { get; set; }
        public bool IsIndeterminate { get; set; }
    }

    private async void PerformAction(FileAction action)
    {
        if (isProcessing) return;
        
        ListView sourceLv = isLeftActive ? leftListView : rightListView;
        TextBox destPathBox = isLeftActive ? rightPathBox : leftPathBox;
        
        if (sourceLv.SelectedItems.Count == 0) return;

        List<string> selectedPaths = new List<string>();
        foreach (ListViewItem item in sourceLv.SelectedItems)
        {
            string path = (string)item.Tag;
            if (!string.IsNullOrEmpty(path) && item.Text != "..")
            {
                selectedPaths.Add(path);
            }
        }

        if (selectedPaths.Count == 0) return;

        string destDir = destPathBox.Text;
        
        if (action != FileAction.Delete && string.IsNullOrEmpty(destDir))
        {
            MessageBox.Show("Please select a valid destination directory in the opposite pane.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string msg = $"Are you sure you want to {action} {selectedPaths.Count} item(s)?";
        if (MessageBox.Show(msg, "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        isProcessing = true;

        // Disable UI
        progressPanel.Visible = true;
        actionProgressBar.Value = 0;
        actionProgressBar.Style = ProgressBarStyle.Marquee;
        actionProgressLabel.Text = "Calculating size...";

        var progress = new Progress<FileProgress>(p =>
        {
            if (p.IsIndeterminate)
            {
                actionProgressBar.Style = ProgressBarStyle.Marquee;
                actionProgressLabel.Text = $"Processing: {Path.GetFileName(p.CurrentFile)}";
            }
            else
            {
                actionProgressBar.Style = ProgressBarStyle.Continuous;
                if (p.TotalBytes > 0)
                {
                    int pct = (int)(p.BytesTransferred * 100 / p.TotalBytes);
                    actionProgressBar.Value = Math.Max(0, Math.Min(100, pct));
                }
                actionProgressLabel.Text = $"Remaining: {p.CurrentFile} | {FormatSize(p.BytesTransferred)} / {FormatSize(p.TotalBytes)}";
            }
        });

        Stopwatch sw = Stopwatch.StartNew();

        await Task.Run(() =>
        {
            // 1. Calculate Total Size (if not delete)
            long totalBytes = 0;
            if (action != FileAction.Delete)
            {
                foreach (string src in selectedPaths)
                {
                    totalBytes += GetTotalSize(src);
                }
            }

            long bytesTransferred = 0;

            foreach (string sourcePath in selectedPaths)
            {
                try
                {
                    bool isDir = Directory.Exists(sourcePath);
                    string itemName = Path.GetFileName(sourcePath);
                    if (string.IsNullOrEmpty(itemName) && isDir) itemName = new DirectoryInfo(sourcePath).Name;
                    
                    string destPath = Path.Combine(destDir, itemName);

                    if (action == FileAction.Copy)
                    {
                        if (isDir) CopyDirectoryWithProgress(sourcePath, destPath, totalBytes, ref bytesTransferred, progress, sw);
                        else CopyFileWithProgress(sourcePath, destPath, totalBytes, ref bytesTransferred, progress, sw);
                    }
                    else if (action == FileAction.Move)
                    {
                        // Check if same drive
                        bool sameDrive = Path.GetPathRoot(Path.GetFullPath(sourcePath)).Equals(Path.GetPathRoot(Path.GetFullPath(destPath)), StringComparison.OrdinalIgnoreCase);
                        if (sameDrive)
                        {
                            ((IProgress<FileProgress>)progress).Report(new FileProgress { IsIndeterminate = true, CurrentFile = itemName });
                            if (isDir) Directory.Move(sourcePath, destPath);
                            else File.Move(sourcePath, destPath);
                        }
                        else
                        {
                            if (isDir) CopyDirectoryWithProgress(sourcePath, destPath, totalBytes, ref bytesTransferred, progress, sw);
                            else CopyFileWithProgress(sourcePath, destPath, totalBytes, ref bytesTransferred, progress, sw);
                            if (isDir) Directory.Delete(sourcePath, true);
                            else File.Delete(sourcePath);
                        }
                    }
                    else if (action == FileAction.Delete)
                    {
                        ((IProgress<FileProgress>)progress).Report(new FileProgress { IsIndeterminate = true, CurrentFile = itemName });
                        if (isDir) Directory.Delete(sourcePath, true);
                        else File.Delete(sourcePath);
                    }
                }
                catch (Exception ex)
                {
                    this.Invoke(new Action(() => MessageBox.Show($"Error processing {sourcePath}:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)));
                }
            }
        });

        progressPanel.Visible = false;
        isProcessing = false;
        RefreshPane(true);
        RefreshPane(false);
    }

    private long GetTotalSize(string path)
    {
        if (File.Exists(path))
            return new FileInfo(path).Length;
        else if (Directory.Exists(path))
        {
            long size = 0;
            try
            {
                DirectoryInfo dir = new DirectoryInfo(path);
                foreach (FileInfo fi in dir.GetFiles("*", SearchOption.AllDirectories))
                    size += fi.Length;
            }
            catch { }
            return size;
        }
        return 0;
    }

    private void CopyFileWithProgress(string source, string dest, long totalBytes, ref long bytesTransferred, IProgress<FileProgress> progress, Stopwatch sw)
    {
        byte[] buffer = new byte[1024 * 1024]; // 1MB chunk
        long lastReportTime = 0;
        using (FileStream fsIn = new FileStream(source, FileMode.Open, FileAccess.Read))
        using (FileStream fsOut = new FileStream(dest, FileMode.Create, FileAccess.Write))
        {
            int bytesRead;
            while ((bytesRead = fsIn.Read(buffer, 0, buffer.Length)) > 0)
            {
                fsOut.Write(buffer, 0, bytesRead);
                bytesTransferred += bytesRead;

                long elapsedMs = sw.ElapsedMilliseconds;
                if (elapsedMs - lastReportTime > 100 || bytesTransferred == totalBytes)
                {
                    lastReportTime = elapsedMs;
                    string timeStr = "Calculating...";
                    if (elapsedMs > 500 && bytesTransferred > 0)
                    {
                        long bytesPerSec = (long)(bytesTransferred / (elapsedMs / 1000.0));
                        if (bytesPerSec > 0)
                        {
                            long remainingBytes = Math.Max(0, totalBytes - bytesTransferred);
                            long secondsLeft = remainingBytes / bytesPerSec;
                            TimeSpan ts = TimeSpan.FromSeconds(secondsLeft);
                            timeStr = ts.ToString(@"hh\:mm\:ss");
                        }
                    }

                    progress.Report(new FileProgress 
                    { 
                        TotalBytes = totalBytes, 
                        BytesTransferred = bytesTransferred, 
                        CurrentFile = timeStr 
                    });
                }
            }
        }
    }

    private void CopyDirectoryWithProgress(string sourceDir, string destDir, long totalBytes, ref long bytesTransferred, IProgress<FileProgress> progress, Stopwatch sw)
    {
        Directory.CreateDirectory(destDir);
        foreach (string file in Directory.GetFiles(sourceDir))
        {
            string destFile = Path.Combine(destDir, Path.GetFileName(file));
            CopyFileWithProgress(file, destFile, totalBytes, ref bytesTransferred, progress, sw);
        }
        foreach (string dir in Directory.GetDirectories(sourceDir))
        {
            string destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
            CopyDirectoryWithProgress(dir, destSubDir, totalBytes, ref bytesTransferred, progress, sw);
        }
    }
}
