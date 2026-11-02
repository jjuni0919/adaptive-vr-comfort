using System;
using System.Globalization;
using System.Text;

namespace VRComfortStudy
{
    public static class CsvFormat
    {
        public static string Row(params object[] fields)
        {
            var row = new StringBuilder();
            for (int i = 0; i < fields.Length; i++)
            {
                if (i > 0) row.Append(',');
                object value = fields[i];
                string text = value == null ? "" : value is bool b ? (b ? "true" : "false") :
                    value is IFormattable f ? f.ToString(null, CultureInfo.InvariantCulture) : value.ToString();
                // A missing/non-finite number is never silently converted to zero.
                if ((value is double d && !StudyClock.Finite(d)) ||
                    (value is float n && (float.IsNaN(n) || float.IsInfinity(n)))) text = "";
                if (text.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0)
                    row.Append('"').Append(text.Replace("\"", "\"\"")).Append('"');
                else row.Append(text);
            }
            return row.ToString();
        }

        public static string SafeSegment(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "anonymous";
            var output = new StringBuilder();
            foreach (char c in text)
                output.Append((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                    (c >= '0' && c <= '9') || c == '_' || c == '-' ? c : '_');
            return output.ToString(0, Math.Min(output.Length, 64));
        }
    }
}
