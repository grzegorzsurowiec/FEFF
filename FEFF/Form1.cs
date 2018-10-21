using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Security.AccessControl;
using System.Windows.Forms;
using System.Linq;

namespace FEFF
{
    public partial class Form1 : Form
    {
        Size _size;
        ulong _files, _directories;
        ListViewColumnSorter lvwColumnSorter = new ListViewColumnSorter();
        Dictionary<string, DirectoryHelper> _dir = new Dictionary<string, DirectoryHelper>();
        bool p_dirsMenuVisible = false;

        public Form1()
        {
            Icon = Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath);
            
            InitializeComponent();

            this.listView1.ListViewItemSorter = lvwColumnSorter;
            this._size = this.Size;

            this.label4.Parent =this.progressBar1;
            this.label4.BackColor = Color.Transparent;

            this.Init();
        }

        private void Init()
        {
            this.lFF.Text = "0/0";
            this._files = 0;
            this._directories = 0;
            this.lDir.Text = "";
            this.splitContainer1.Hide();
            this.treeView1.Nodes.Clear();
            this.listView1.Items.Clear();
            this._dir.Clear();
            this.Size = new Size(this.panel1.Size.Width, this._size.Height- this.splitContainer1.Size.Height);
            this.progressBar1.Visible = true;
            this.SetInfo("Stanby");
        }

        private void SplitContainer1_SizeChanged(object sender, EventArgs e)
        {
            panel2.Size = new Size(this.treeView1.Size.Width, this.panel2.Size.Height);
        }

        private void SetInfo(string info)
        {
            this.label4.Text = info;
            this.label4.Location = new Point((this.progressBar1.Size.Width-this.label4.Size.Width)/2,this.label4.Size.Height/2);
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            this.SetInfo("Initializing system");
            this.Init();

            TreeNode lastNode, checkNode;
            char[] pathseparators = new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };

            if (this.folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                this.lDir.Text = folderBrowserDialog1.SelectedPath;
            }
            else return;

            this.progressBar1.Style = ProgressBarStyle.Marquee;
            this.progressBar1.Step = 5;
            this.progressBar1.MarqueeAnimationSpeed = 30;
            this.timer1.Enabled = true;

            #region build _FolderSize & empty files objects

            string[] paths = this.lDir.Text.Split(pathseparators, StringSplitOptions.RemoveEmptyEntries);

            this.SetInfo("Build files/folders list");

            this.treeView1.Nodes.Clear();

            if (paths.Length > 1)
            {
                lastNode = new TreeNode(paths[0]);

                this.treeView1.Nodes.Add(lastNode);

                if (paths.Length > 2)
                {
                    for (int i = 1; i < paths.Length - 1; i++)
                    {
                        checkNode = new TreeNode(paths[i]);
                        lastNode.Nodes.Add(checkNode);
                        lastNode = checkNode;
                    }
                }

                lastNode.Nodes.Add(this.Enumerate(new DirectoryInfo(this.lDir.Text)));
            }

            else
            {
                this.treeView1.Nodes.Add(this.Enumerate(new DirectoryInfo(this.lDir.Text)));
            }

            #endregion

            this.SetInfo("Get empty folders candidates");

            Dictionary<string, DirectoryHelper> verifyDir = new Dictionary<string, DirectoryHelper>();

            foreach (KeyValuePair<string,DirectoryHelper> dh in this._dir)
            {
                if (!dh.Value.files || dh.Value.size.Equals(0)) verifyDir.Add(dh.Key, dh.Value);
            }

            this.SetInfo("Check folders child nodes");
            foreach (KeyValuePair<string, DirectoryHelper> dh in verifyDir)
            {
                foreach (KeyValuePair<string, DirectoryHelper> allDirs in this._dir)
                {
                    if (allDirs.Key.StartsWith(dh.Key))
                    {
                        dh.Value.size += allDirs.Value.size;
                        if (allDirs.Value.files) dh.Value.files = true;
                        dh.Value.dirs += allDirs.Value.dirs;
                    }
                }
            }
            verifyDir.Clear();

            
            this.treeView1.Nodes.Clear();

            List<string> Dirs = new List<string>();
            this.SetInfo("Clean results");

            foreach (KeyValuePair<string, DirectoryHelper> dh in this._dir)
            {
                if (!dh.Value.files || dh.Value.size.Equals(0))
                {
                    string[] path = dh.Key.Split(pathseparators, StringSplitOptions.RemoveEmptyEntries);
                    Dirs.Add(dh.Key);
                }
            }

            this.SetInfo("Build empty folders tree");
            PopulateTreeView(this.treeView1, Dirs, pathseparators);
            this._dir.Clear();
            Dirs.Clear();

            Application.DoEvents();

            this.timer1.Enabled = false;
            this.progressBar1.Visible = false;
            this.splitContainer1.Show();
            this.progressBar1.Style = ProgressBarStyle.Continuous;
            this.progressBar1.MarqueeAnimationSpeed = 0;
            this.Size = this._size;

            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        private static void PopulateTreeView(TreeView treeView, IEnumerable<string> paths, char[] pathSeparator)
        {
            TreeNode lastNode = null;
            string subPathAgg;
            foreach (string path in paths)
            {
                Application.DoEvents();
                subPathAgg = string.Empty;
                foreach (string subPath in path.Split(pathSeparator))
                {
                    subPathAgg += subPath + pathSeparator;
                    TreeNode[] nodes = treeView.Nodes.Find(subPathAgg, true);
                    if (nodes.Length == 0)
                        if (lastNode == null)
                            lastNode = treeView.Nodes.Add(subPathAgg, subPath);
                        else
                            lastNode = lastNode.Nodes.Add(subPathAgg, subPath);
                    else
                        lastNode = nodes[0];
                }
            }
        }

        private TreeNode Enumerate(DirectoryInfo directoryInfo)
        {
            var directoryNode = new TreeNode(directoryInfo.Name);
            Application.DoEvents();

            if (directoryInfo.GetDirectories().Length > 0 || directoryInfo.GetFiles().Length > 0)
            {
                this._dir.Add(directoryInfo.FullName, new DirectoryHelper() { dirs = 0, files = false, path = directoryInfo.FullName, size = 0 });
            }

            try
            {
                foreach (var directory in directoryInfo.GetDirectories())
                {
                    if (directory.Attributes != FileAttributes.ReparsePoint && this.HasWriteAccessToFolder(directory.FullName))
                    {
                        this._dir[directoryInfo.FullName].dirs++;
                        directoryNode.Nodes.Add(Enumerate(directory));
                    }
                }
            }
            catch (Exception) { }

            try
            {
                foreach (var file in directoryInfo.GetFiles())
                {
                    if (file.Attributes == FileAttributes.ReparsePoint) continue;
                    Application.DoEvents();

                    this._dir[directoryInfo.FullName].files = true;
                    
                    if (file.Length.Equals(0))
                    {
                        this.listView1.Items.Add(new ListViewItem(file.FullName));
                        this._files++;
                     }

                    else
                    {
                        this._dir[directoryInfo.FullName].size += (ulong)file.Length;
                    }
                }
            }
            catch (Exception) { }

            return directoryNode;
        }

        private bool HasWriteAccessToFolder(string folderPath)
        {
            try
            {
                DirectorySecurity ds = Directory.GetAccessControl(folderPath);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private void Timer1_Tick(object sender, EventArgs e)
        {
            this.lFF.Text = string.Format("{0}/{1}", this._files, this._directories);
        }

        #region sort & resize

        private void ListView1_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (e.Column == lvwColumnSorter.SortColumn)
            {
                if (lvwColumnSorter.Order == SortOrder.Ascending)
                {
                    lvwColumnSorter.Order = SortOrder.Descending;
                }
                else
                {
                    lvwColumnSorter.Order = SortOrder.Ascending;
                }
            }
            else
            {
                lvwColumnSorter.SortColumn = e.Column;
                lvwColumnSorter.Order = SortOrder.Ascending;
            }
            this.listView1.Sort();
        }



        private void ListView1_SizeChanged(object sender, EventArgs e)
        {
            this.listView1.Columns[0].Width = this.listView1.Size.Width - 10;
        }
        
        #endregion 


        private void ToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(this.listView1.SelectedItems[0].Text);
        }

        private void ToolStripMenuItem2_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Do you really want to delete selected files?", "Confirm", MessageBoxButtons.YesNo) == System.Windows.Forms.DialogResult.Yes)
            {
                foreach (ListViewItem item in this.listView1.SelectedItems)
                {
                    try
                    {
                        Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(item.Text, Microsoft.VisualBasic.FileIO.UIOption.AllDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);

                        if (!File.Exists(item.Text)) item.Remove();
                    }
                    catch (Exception) { }

                }
            }
        }

        private void CmsDirs_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = !p_dirsMenuVisible;
        }

        private void CmsFiles_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = this.listView1.SelectedItems.Count.Equals(0);
        }

        private void TreeView1_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            this.treeView1.SelectedNode = e.Node;
            if (e.Button == System.Windows.Forms.MouseButtons.Right && e.Node.Nodes.Count.Equals(0))
            {
                
                p_dirsMenuVisible = true;
            }
        }

        private void CmsDirs_Closed(object sender, ToolStripDropDownClosedEventArgs e)
        {
            this.p_dirsMenuVisible = false;
        }

        private void ToolStripMenuItem3_Click(object sender, EventArgs e)
        {
            List<TreeNode> ancestorList = TreeHelpers.GetAncestors(this.treeView1.SelectedNode, x => x.Parent).Reverse().ToList();

            string path = string.Empty;

            foreach (TreeNode node in ancestorList)
            {
                path = string.Concat(path, Path.DirectorySeparatorChar, node.Text);
            }

            path = string.Concat(path.Substring(1), Path.DirectorySeparatorChar, this.treeView1.SelectedNode.Text);

            System.Diagnostics.Process.Start(path);
        }
     
    }

    public static class TreeHelpers
    {
        public static IEnumerable<TItem> GetAncestors<TItem>(TItem item, Func<TItem, TItem> getParentFunc)
        {
            if (getParentFunc == null)
            {
                throw new ArgumentNullException("getParentFunc");
            }
            if (ReferenceEquals(item, null)) yield break;
            for (TItem curItem = getParentFunc(item); !ReferenceEquals(curItem, null); curItem = getParentFunc(curItem))
            {
                yield return curItem;
            }
        }

        //TODO: Add other methods, for example for 'prefix' children recurence enumeration
    }

    public class DirectoryHelper
    {
        public ulong size;
        public string path;
        public bool files;
        public ushort dirs;
    }

}
