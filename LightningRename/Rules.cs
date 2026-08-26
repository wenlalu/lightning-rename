using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LightningRename
{
    public class Rules
    {
        // ① 查找替换
        public bool ReplaceEnabled;
        public string FindText = "";
        public string ReplaceText = "";
        public int ReplaceScope;
        public bool CaseSensitive;

        // ② 增删字符
        public bool AddEnabled;
        public string Prefix = "";
        public string Suffix = "";
        public bool RemoveEnabled;
        public int RemoveFirst;
        public int RemoveLast;
        public bool InsertEnabled;
        public int InsertPos;
        public string InsertText = "";
        public bool InsertFromEnd;
        public bool InsertIncludeExt;

        // ③ 编号
        public bool NumberEnabled;
        public int NumberStart = 1;
        public int NumberStep = 1;
        public int NumberDigits = 2;
        public int NumberPos;
        public string NumberSep = "";

        // ④ 日期
        public bool DateEnabled;
        public string DateFormat = "yyyy-MM-dd";
        public int DatePos;
        public string DateSep = "";

        // ⑤ 大小写/空格
        public int CaseMode;
        public bool TrimEnabled;

        // ⑥ 扩展名
        public int ExtMode;
        public string ExtNew = "";

        /// <summary>
        /// 将规则保存到文件（简单键值对格式）
        /// </summary>
        public void Save(string path)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# 闪电重命名规则配置文件");
            sb.AppendLine("# 由程序自动生成，可手动编辑");
            sb.AppendLine("ReplaceEnabled=" + ReplaceEnabled);
            sb.AppendLine("FindText=" + FindText);
            sb.AppendLine("ReplaceText=" + ReplaceText);
            sb.AppendLine("ReplaceScope=" + ReplaceScope);
            sb.AppendLine("CaseSensitive=" + CaseSensitive);
            sb.AppendLine("AddEnabled=" + AddEnabled);
            sb.AppendLine("Prefix=" + Prefix);
            sb.AppendLine("Suffix=" + Suffix);
            sb.AppendLine("RemoveEnabled=" + RemoveEnabled);
            sb.AppendLine("RemoveFirst=" + RemoveFirst);
            sb.AppendLine("RemoveLast=" + RemoveLast);
            sb.AppendLine("InsertEnabled=" + InsertEnabled);
            sb.AppendLine("InsertPos=" + InsertPos);
            sb.AppendLine("InsertText=" + InsertText);
            sb.AppendLine("InsertFromEnd=" + InsertFromEnd);
            sb.AppendLine("InsertIncludeExt=" + InsertIncludeExt);
            sb.AppendLine("NumberEnabled=" + NumberEnabled);
            sb.AppendLine("NumberStart=" + NumberStart);
            sb.AppendLine("NumberStep=" + NumberStep);
            sb.AppendLine("NumberDigits=" + NumberDigits);
            sb.AppendLine("NumberPos=" + NumberPos);
            sb.AppendLine("NumberSep=" + NumberSep);
            sb.AppendLine("DateEnabled=" + DateEnabled);
            sb.AppendLine("DateFormat=" + DateFormat);
            sb.AppendLine("DatePos=" + DatePos);
            sb.AppendLine("DateSep=" + DateSep);
            sb.AppendLine("CaseMode=" + CaseMode);
            sb.AppendLine("TrimEnabled=" + TrimEnabled);
            sb.AppendLine("ExtMode=" + ExtMode);
            sb.AppendLine("ExtNew=" + ExtNew);
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// 从文件加载规则，返回是否成功
        /// </summary>
        public bool Load(string path)
        {
            if (!File.Exists(path)) return false;
            try
            {
                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                Dictionary<string, string> dict =
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string line in lines)
                {
                    string trim = line.Trim();
                    if (trim.Length == 0 || trim.StartsWith("#")) continue;
                    int eq = trim.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = trim.Substring(0, eq).Trim();
                    string val = trim.Substring(eq + 1);
                    dict[key] = val;
                }

                ReplaceEnabled = GetBool(dict, "ReplaceEnabled", false);
                FindText = GetString(dict, "FindText", "");
                ReplaceText = GetString(dict, "ReplaceText", "");
                ReplaceScope = GetInt(dict, "ReplaceScope", 0);
                CaseSensitive = GetBool(dict, "CaseSensitive", false);
                AddEnabled = GetBool(dict, "AddEnabled", false);
                Prefix = GetString(dict, "Prefix", "");
                Suffix = GetString(dict, "Suffix", "");
                RemoveEnabled = GetBool(dict, "RemoveEnabled", false);
                RemoveFirst = GetInt(dict, "RemoveFirst", 0);
                RemoveLast = GetInt(dict, "RemoveLast", 0);
                InsertEnabled = GetBool(dict, "InsertEnabled", false);
                InsertPos = GetInt(dict, "InsertPos", 0);
                InsertText = GetString(dict, "InsertText", "");
                InsertFromEnd = GetBool(dict, "InsertFromEnd", false);
                InsertIncludeExt = GetBool(dict, "InsertIncludeExt", false);
                NumberEnabled = GetBool(dict, "NumberEnabled", false);
                NumberStart = GetInt(dict, "NumberStart", 1);
                NumberStep = GetInt(dict, "NumberStep", 1);
                NumberDigits = GetInt(dict, "NumberDigits", 2);
                NumberPos = GetInt(dict, "NumberPos", 0);
                NumberSep = GetString(dict, "NumberSep", "");
                DateEnabled = GetBool(dict, "DateEnabled", false);
                DateFormat = GetString(dict, "DateFormat", "yyyy-MM-dd");
                DatePos = GetInt(dict, "DatePos", 0);
                DateSep = GetString(dict, "DateSep", "");
                CaseMode = GetInt(dict, "CaseMode", 0);
                TrimEnabled = GetBool(dict, "TrimEnabled", false);
                ExtMode = GetInt(dict, "ExtMode", 0);
                ExtNew = GetString(dict, "ExtNew", "");
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 深拷贝一份规则快照，供后台预览线程持有一致的规则配置。
        /// </summary>
        public Rules Clone()
        {
            Rules c = new Rules();
            c.ReplaceEnabled = ReplaceEnabled; c.FindText = FindText; c.ReplaceText = ReplaceText;
            c.ReplaceScope = ReplaceScope; c.CaseSensitive = CaseSensitive;
            c.AddEnabled = AddEnabled; c.Prefix = Prefix; c.Suffix = Suffix;
            c.RemoveEnabled = RemoveEnabled; c.RemoveFirst = RemoveFirst; c.RemoveLast = RemoveLast;
            c.InsertEnabled = InsertEnabled; c.InsertPos = InsertPos; c.InsertText = InsertText;
            c.InsertFromEnd = InsertFromEnd; c.InsertIncludeExt = InsertIncludeExt;
            c.NumberEnabled = NumberEnabled; c.NumberStart = NumberStart; c.NumberStep = NumberStep;
            c.NumberDigits = NumberDigits; c.NumberPos = NumberPos; c.NumberSep = NumberSep;
            c.DateEnabled = DateEnabled; c.DateFormat = DateFormat; c.DatePos = DatePos; c.DateSep = DateSep;
            c.CaseMode = CaseMode; c.TrimEnabled = TrimEnabled;
            c.ExtMode = ExtMode; c.ExtNew = ExtNew;
            return c;
        }

        private static string GetString(Dictionary<string, string> dict, string key, string def)
        {
            string v;
            return dict.TryGetValue(key, out v) ? v : def;
        }

        private static int GetInt(Dictionary<string, string> dict, string key, int def)
        {
            string v;
            int result;
            if (dict.TryGetValue(key, out v) && int.TryParse(v, out result))
                return result;
            return def;
        }

        private static bool GetBool(Dictionary<string, string> dict, string key, bool def)
        {
            string v;
            bool result;
            if (dict.TryGetValue(key, out v) && bool.TryParse(v, out result))
                return result;
            return def;
        }
    }
}
