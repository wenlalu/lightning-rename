using System;
using System.IO;

namespace LightningRename
{
    public class Item
    {
        public string Path;
        public bool IsDir;
        public int InsOrder;
        public long Size;
        public DateTime MTime;

        public string NewName = "";
        public string NewPath = "";
        public string Error;

        public string Dir
        {
            get { return System.IO.Path.GetDirectoryName(Path); }
        }

        public string OldName
        {
            get { return System.IO.Path.GetFileName(Path); }
        }

        public bool Changed
        {
            get { return !string.Equals(NewPath, Path, StringComparison.Ordinal); }
        }

        /// <summary>
        /// 生成一份仅含"标识字段"的轻量副本，供后台线程安全地推算预览，
        /// 计算结果再由 UI 线程写回原对象，避免跨线程直接改状态。
        /// </summary>
        public Item DuplicateSnapshotForPreview()
        {
            return new Item
            {
                Path = this.Path,
                IsDir = this.IsDir,
                InsOrder = this.InsOrder,
                Size = this.Size,
                MTime = this.MTime
            };
        }
    }
}
