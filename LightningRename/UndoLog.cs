using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace LightningRename
{
    public static class UndoLog
    {
        // 最多保留20次撤销记录，防止日志无限增长
        private const int MaxUndoBlocks = 20;

        private static string LogPath()
        {
            string dir = Application.StartupPath;
            try
            {
                // 测试程序目录是否可写
                string testFile = Path.Combine(dir, ".wr_test");
                using (File.Create(testFile)) { }
                File.Delete(testFile);
            }
            catch
            {
                // 程序目录不可写时，使用 LocalAppData
                dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "LightningRename");
                Directory.CreateDirectory(dir);
            }
            return Path.Combine(dir, "后悔药日志.txt");
        }

        public static int BlockCount()
        {
            try
            {
                string path = LogPath();
                if (!File.Exists(path)) return 0;
                int count = 0;
                foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
                    if (line.StartsWith("#BLOCK")) count++;
                return count;
            }
            catch
            {
                return 0;
            }
        }

        public static void Append(List<string[]> pairs)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("#BLOCK ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append("\r\n");
            foreach (string[] pair in pairs)
            {
                sb.Append(pair[0]).Append('\u0001')
                  .Append(pair[1]).Append('\u0001')
                  .Append(pair[2]).Append("\r\n");
            }
            sb.Append("#END\r\n");
            File.AppendAllText(LogPath(), sb.ToString(), Encoding.UTF8);
            TrimLog();
        }

        /// <summary>
        /// 只保留最近 MaxUndoBlocks 个撤销块，删除更旧的记录
        /// </summary>
        private static void TrimLog()
        {
            try
            {
                string path = LogPath();
                if (!File.Exists(path)) return;

                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                List<int> blockStarts = new List<int>();
                for (int i = 0; i < lines.Length; i++)
                    if (lines[i].StartsWith("#BLOCK"))
                        blockStarts.Add(i);

                if (blockStarts.Count <= MaxUndoBlocks) return;

                int keepFrom = blockStarts[blockStarts.Count - MaxUndoBlocks];
                List<string> kept = new List<string>();
                for (int i = keepFrom; i < lines.Length; i++)
                    kept.Add(lines[i]);
                File.WriteAllLines(path, kept.ToArray(), Encoding.UTF8);
            }
            catch
            {
            }
        }

        public static List<string[]> ReadLast()
        {
            try
            {
                string path = LogPath();
                if (!File.Exists(path)) return null;

                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                int blockStart = -1;
                int blockEnd = -1;

                // 从后往前找最近的一个完整块
                for (int i = lines.Length - 1; i >= 0; i--)
                {
                    if (blockEnd < 0 && lines[i].StartsWith("#END"))
                    {
                        blockEnd = i;
                    }
                    else if (blockEnd >= 0 && lines[i].StartsWith("#BLOCK"))
                    {
                        blockStart = i;
                        break;
                    }
                }

                if (blockStart < 0 || blockEnd < 0 || blockEnd <= blockStart)
                    return null;

                List<string[]> pairs = new List<string[]>();
                for (int i = blockStart + 1; i < blockEnd; i++)
                {
                    string[] parts = lines[i].Split('\u0001');
                    if (parts.Length == 3)
                        pairs.Add(parts);
                }
                return pairs.Count > 0 ? pairs : null;
            }
            catch
            {
                return null;
            }
        }

        public static void RemoveLast()
        {
            try
            {
                string path = LogPath();
                if (!File.Exists(path)) return;

                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                int lastBlock = -1;
                for (int i = lines.Length - 1; i >= 0; i--)
                {
                    if (lines[i].StartsWith("#BLOCK"))
                    {
                        lastBlock = i;
                        break;
                    }
                }

                if (lastBlock < 0)
                {
                    File.Delete(path);
                    return;
                }

                List<string> kept = new List<string>();
                for (int i = 0; i < lastBlock; i++)
                    kept.Add(lines[i]);
                File.WriteAllLines(path, kept.ToArray(), Encoding.UTF8);
            }
            catch
            {
            }
        }
    }
}
