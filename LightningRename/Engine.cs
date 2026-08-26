using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace LightningRename
{
    public static class Engine
    {
        // Win32 API，用于支持长路径（>260字符）
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool MoveFileW(string lpExistingFileName, string lpNewFileName);

        private const int MaxLongPath = 32767;

        /// <summary>
        /// 为路径添加 \\?\ 前缀以支持长路径（>260字符）
        /// </summary>
        private static string AddLongPathPrefix(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            if (path.StartsWith(@"\\?\", StringComparison.Ordinal)) return path;
            // UNC 路径 \\server\share -> \\?\UNC\server\share
            if (path.StartsWith(@"\\"))
                return @"\\?\UNC\" + path.Substring(2);
            return @"\\?\" + path;
        }

        /// <summary>
        /// 移动文件/文件夹，支持长路径
        /// </summary>
        private static bool LongPathMove(string from, string to)
        {
            return MoveFileW(AddLongPathPrefix(from), AddLongPathPrefix(to));
        }

        private class Plan
        {
            public string Old;
            public string Temp;
            public string New;
            public bool IsDir;
            public int Depth;
        }

        // 查找替换的正则缓存
        private static Regex cachedRegex;
        private static string cachedFind = null;
        private static bool cachedIgnoreCase;

        public static string BaseOf(string name, bool isDir)
        {
            if (isDir) return name;
            int dot = name.LastIndexOf('.');
            if (dot > 0) return name.Substring(0, dot);
            return name;
        }

        public static string ExtOf(string name, bool isDir)
        {
            if (isDir) return "";
            int dot = name.LastIndexOf('.');
            if (dot > 0 && dot < name.Length - 1)
                return name.Substring(dot + 1);
            return "";
        }

        public static string CleanExt(string s)
        {
            s = s.Trim();
            while (s.StartsWith("."))
                s = s.Substring(1);
            foreach (char c in s)
            {
                if (Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0)
                    return null;
            }
            return s;
        }

        private static string ReplaceAll(string s, string find, string rep, bool caseSensitive)
        {
            if (find.Length == 0) return s;
            if (caseSensitive) return s.Replace(find, rep);
            if (cachedRegex == null || cachedFind != find || cachedIgnoreCase != !caseSensitive)
            {
                cachedRegex = new Regex(Regex.Escape(find), RegexOptions.IgnoreCase);
                cachedFind = find;
                cachedIgnoreCase = true;
            }
            return cachedRegex.Replace(s, rep.Replace("$", "$$"));
        }

        public static string TitleCase(string s)
        {
            StringBuilder sb = new StringBuilder(s.Length);
            bool newWord = true;
            foreach (char c in s)
            {
                char ch = c;
                if (!newWord || !char.IsLetter(c))
                {
                    newWord = (c == ' ' || c == '-' || c == '_' || c == '.');
                }
                else
                {
                    ch = char.ToUpperInvariant(c);
                    newWord = false;
                }
                sb.Append(ch);
            }
            return sb.ToString();
        }

        /// <summary>
        /// 按规则顺序对文件名应用所有规则：
        /// ①替换 → ②增删/插入 → ③编号 → ④日期 → ⑤大小写 → ⑥扩展名
        /// </summary>
        public static string ApplyRules(string baseName, string ext, bool isDir, Rules r, int index, DateTime now)
        {
            string text = baseName;
            string text2 = ext;

            // ① 查找替换
            if (r.ReplaceEnabled && r.FindText.Length > 0)
            {
                if (r.ReplaceScope == 0)
                {
                    text = ReplaceAll(text, r.FindText, r.ReplaceText, r.CaseSensitive);
                }
                else if (r.ReplaceScope == 1)
                {
                    text2 = ReplaceAll(text2, r.FindText, r.ReplaceText, r.CaseSensitive);
                }
                else
                {
                    string s = text + ((text2.Length > 0) ? ("." + text2) : "");
                    s = ReplaceAll(s, r.FindText, r.ReplaceText, r.CaseSensitive);
                    if (isDir)
                    {
                        text = s;
                        text2 = "";
                    }
                    else
                    {
                        int dot = s.LastIndexOf('.');
                        if (dot > 0)
                        {
                            text = s.Substring(0, dot);
                            text2 = s.Substring(dot + 1);
                        }
                        else
                        {
                            text = s;
                            text2 = "";
                        }
                    }
                }
            }

            // ② 删除字符（作用于主名）
            if (r.RemoveEnabled)
            {
                int skipFront = Math.Min(r.RemoveFirst, text.Length);
                text = text.Substring(skipFront);
                int skipBack = Math.Min(r.RemoveLast, text.Length);
                text = text.Substring(0, text.Length - skipBack);
            }

            // ② 添加前缀/后缀
            if (r.AddEnabled)
            {
                text = r.Prefix + text + r.Suffix;
            }

            // ② 指定位置插入文本
            if (r.InsertEnabled && r.InsertText != null && r.InsertText.Length > 0)
            {
                string target;
                bool splitExt = false;
                if (r.InsertIncludeExt && !isDir && text2.Length > 0)
                {
                    target = text + "." + text2;
                    splitExt = true;
                }
                else
                {
                    target = text;
                }

                int pos;
                if (r.InsertFromEnd)
                    pos = Math.Max(0, target.Length - Math.Max(0, r.InsertPos));
                else
                    pos = Math.Min(Math.Max(0, r.InsertPos), target.Length);

                string inserted = target.Substring(0, pos) + r.InsertText + target.Substring(pos);

                if (splitExt)
                {
                    int dot = inserted.LastIndexOf('.');
                    if (dot > 0)
                    {
                        text = inserted.Substring(0, dot);
                        text2 = inserted.Substring(dot + 1);
                    }
                    else
                    {
                        text = inserted;
                        text2 = "";
                    }
                }
                else
                {
                    text = inserted;
                }
            }

            // ③ 编号（用 64 位运算防：极大增量×海量文件时 int 乘法溢出）
            if (r.NumberEnabled)
            {
                string numStr = ((long)r.NumberStart + (long)r.NumberStep * index)
                    .ToString().PadLeft(Math.Max(1, r.NumberDigits), '0');
                text = (r.NumberPos == 0)
                    ? (numStr + r.NumberSep + text)
                    : (text + r.NumberSep + numStr);
            }

            // ④ 日期
            if (r.DateEnabled && !string.IsNullOrEmpty(r.DateFormat))
            {
                string dateStr;
                try { dateStr = now.ToString(r.DateFormat); }
                catch { dateStr = now.ToString("yyyy-MM-dd"); }

                foreach (char c in Path.GetInvalidFileNameChars())
                    dateStr = dateStr.Replace(c.ToString(), "");

                text = (r.DatePos == 0)
                    ? (dateStr + r.DateSep + text)
                    : (text + r.DateSep + dateStr);
            }

            // ⑤ 大小写转换
            if (r.CaseMode == 1)
                text = text.ToUpperInvariant();
            else if (r.CaseMode == 2)
                text = text.ToLowerInvariant();
            else if (r.CaseMode == 3)
                text = TitleCase(text);

            // ⑤ 去除首尾空格
            if (r.TrimEnabled)
                text = text.Trim();

            // ⑥ 扩展名（仅文件）
            if (!isDir)
            {
                if (r.ExtMode == 1)
                {
                    string cleaned = CleanExt(r.ExtNew);
                    if (cleaned != null) text2 = cleaned;
                }
                else if (r.ExtMode == 2)
                {
                    text2 = "";
                }
            }

            if (text.Length == 0 && text2.Length == 0) return "";
            return text + ((text2.Length > 0) ? ("." + text2) : "");
        }

        public static string ValidateName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "新名称为空";
            if (name == "." || name == "..") return "名称不合法";
            // Windows 不允许名称以空格或点结尾，提前拦截避免执行时才失败回滚
            char last = name[name.Length - 1];
            if (last == ' ' || last == '.') return "名称不能以空格或点结尾";
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                if (name.IndexOf(c) >= 0)
                    return "包含非法字符: " + c;
            }
            return null;
        }

        private static int Depth(string p)
        {
            int d = 0;
            foreach (char c in p)
                if (c == '\\') d++;
            return d;
        }

        public static Dictionary<string, string> BuildFolderMap(List<Item> items)
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            List<Item> dirs = new List<Item>();
            foreach (Item item in items)
                if (item.IsDir) dirs.Add(item);
            dirs.Sort((Item a, Item b) => Depth(a.Path).CompareTo(Depth(b.Path)));
            foreach (Item d in dirs)
            {
                string mappedDir = MapDir(d.Dir, map);
                string newPath = Path.Combine(mappedDir, d.NewName);
                map[d.Path] = newPath;
            }
            return map;
        }

        public static string MapDir(string dir, Dictionary<string, string> map)
        {
            string matched = null;
            int bestLen = -1;
            foreach (KeyValuePair<string, string> kv in map)
            {
                string key = kv.Key;
                if ((string.Equals(dir, key, StringComparison.OrdinalIgnoreCase)
                    || dir.StartsWith(key + "\\", StringComparison.OrdinalIgnoreCase))
                    && key.Length > bestLen)
                {
                    bestLen = key.Length;
                    matched = key;
                }
            }
            if (matched == null) return dir;
            string suffix = (dir.Length == matched.Length) ? "" : dir.Substring(matched.Length);
            return map[matched] + suffix;
        }

        public static void ComputeAll(List<Item> items, Rules r, bool checkDisk)
        {
            DateTime now = DateTime.Now;
            for (int i = 0; i < items.Count; i++)
            {
                Item item = items[i];
                item.Error = ValidateName(
                    item.NewName = ApplyRules(
                        BaseOf(item.OldName, item.IsDir),
                        ExtOf(item.OldName, item.IsDir),
                        item.IsDir, r, i, now));
            }

            Dictionary<string, string> folderMap = BuildFolderMap(items);
            HashSet<string> oldPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Item it in items)
                oldPaths.Add(it.Path);

            Dictionary<string, int> newPathCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int j = 0; j < items.Count; j++)
            {
                Item item = items[j];
                if (item.Error == null)
                    item.NewPath = Path.Combine(MapDir(item.Dir, folderMap), item.NewName);
                else
                    item.NewPath = item.Path;

                if (item.Error == null && item.NewPath.Length > MaxLongPath)
                    item.Error = "路径过长(>32767)";

                if (item.Error == null)
                {
                    if (newPathCount.ContainsKey(item.NewPath))
                        item.Error = "列表内新名称重复";
                    else
                        newPathCount[item.NewPath] = 1;
                }

                if (item.Error == null && item.Changed && checkDisk
                    && (item.IsDir ? Directory.Exists(item.NewPath) : File.Exists(item.NewPath))
                    && !oldPaths.Contains(item.NewPath))
                {
                    item.Error = "磁盘上已存在同名项";
                }
            }
        }

        public static void SafeMove(string from, string to, bool isDir)
        {
            if (!LongPathMove(from, to))
            {
                int err = Marshal.GetLastWin32Error();
                try
                {
                    if (isDir)
                        Directory.Move(from, to);
                    else
                        File.Move(from, to);
                }
                catch (Exception inner)
                {
                    throw new IOException("移动文件失败(错误码:" + err + "): " + inner.Message, inner);
                }
            }
        }

        private static string RunTwoPhase(List<Plan> plan, out List<Plan> shallowFirst, Func<bool> cancelled)
        {
            shallowFirst = new List<Plan>(plan);
            shallowFirst.Sort((Plan a, Plan b) => a.Depth.CompareTo(b.Depth));
            List<Plan> deepFirst = new List<Plan>(plan);
            deepFirst.Sort((Plan a, Plan b) => b.Depth.CompareTo(a.Depth));

            List<Plan> renamedToTemp = new List<Plan>();
            List<Plan> renamedToNew = new List<Plan>();

            try
            {
                // 第一阶段：全部改名为临时名（深层先改）
                foreach (Plan p in deepFirst)
                {
                    if (cancelled != null && cancelled())
                        throw new OperationCanceledException();
                    p.Temp = Path.Combine(Path.GetDirectoryName(p.Old),
                        "~rntmp_" + Guid.NewGuid().ToString("N").Substring(0, 10));
                    SafeMove(p.Old, p.Temp, p.IsDir);
                    renamedToTemp.Add(p);
                }
                // 第二阶段：临时名改为新名（浅层先改）
                foreach (Plan p in shallowFirst)
                {
                    if (cancelled != null && cancelled())
                        throw new OperationCanceledException();
                    SafeMove(p.Temp, p.New, p.IsDir);
                    renamedToNew.Add(p);
                    if (p.IsDir)
                        RemapTemps(plan, p, p.Old, p.New);
                }
            }
            catch (Exception ex)
            {
                // 回滚：新名→临时名→原名
                for (int i = renamedToNew.Count - 1; i >= 0; i--)
                {
                    Plan p = renamedToNew[i];
                    try { SafeMove(p.New, p.Temp, p.IsDir); }
                    catch { }
                    if (p.IsDir)
                        RemapTemps(plan, p, p.New, p.Old);
                }
                for (int i = renamedToTemp.Count - 1; i >= 0; i--)
                {
                    try { SafeMove(renamedToTemp[i].Temp, renamedToTemp[i].Old, renamedToTemp[i].IsDir); }
                    catch { }
                }
                if (ex is OperationCanceledException)
                    return "已取消，所有改动已自动回滚。";
                return "操作失败并已自动回滚: " + ex.Message;
            }
            return null;
        }

        private static void RemapTemps(List<Plan> plan, Plan folder, string fromPrefix, string toPrefix)
        {
            foreach (Plan p in plan)
            {
                if (p != folder && p.Depth > folder.Depth
                    && p.Temp.StartsWith(fromPrefix + "\\", StringComparison.OrdinalIgnoreCase))
                {
                    p.Temp = toPrefix + p.Temp.Substring(fromPrefix.Length);
                }
            }
        }

        public static string Execute(List<Item> items, out List<string[]> donePairs, Func<bool> cancelled)
        {
            donePairs = new List<string[]>();
            List<Plan> plan = new List<Plan>();
            foreach (Item item in items)
            {
                if (item.Error != null)
                    return "存在冲突项: " + item.OldName + " (" + item.Error + ")";
                if (item.Changed)
                {
                    plan.Add(new Plan
                    {
                        Old = item.Path,
                        New = item.NewPath,
                        IsDir = item.IsDir,
                        Depth = Depth(item.Path)
                    });
                }
            }
            if (plan.Count == 0) return null;

            List<Plan> shallowFirst;
            string err = RunTwoPhase(plan, out shallowFirst, cancelled);
            if (err != null) return err;

            foreach (Plan p in shallowFirst)
            {
                donePairs.Add(new string[3]
                {
                    p.Old, p.New, p.IsDir ? "1" : "0"
                });
            }
            return null;
        }

        public static string ExecutePairs(List<string[]> pairs, Func<bool> cancelled)
        {
            List<Plan> plan = new List<Plan>();
            foreach (string[] pair in pairs)
            {
                plan.Add(new Plan
                {
                    Old = pair[0],
                    New = pair[1],
                    IsDir = (pair[2] == "1"),
                    Depth = Depth(pair[0])
                });
            }
            List<Plan> shallowFirst;
            return RunTwoPhase(plan, out shallowFirst, cancelled);
        }
    }
}
