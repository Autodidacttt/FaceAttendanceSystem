using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FaceAttendanceSystem.Infrastructure
{
    /// <summary>Minimal RFC 4180 CSV builder with protection against spreadsheet formula injection.</summary>
    public class Csv
    {
        private readonly StringBuilder _sb = new StringBuilder();

        public Csv Row(params object[] values)
        {
            _sb.Append(string.Join(",", values.Select(v => Escape(v == null ? "" : System.Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture)))));
            _sb.Append("\r\n");
            return this;
        }

        public Csv Rows<T>(IEnumerable<T> items, System.Func<T, object[]> selector)
        {
            foreach (var item in items) Row(selector(item));
            return this;
        }

        /// <summary>UTF-8 with BOM so Excel opens non-ASCII names correctly.</summary>
        public byte[] ToBytes()
        {
            var preamble = Encoding.UTF8.GetPreamble();
            var body = Encoding.UTF8.GetBytes(_sb.ToString());
            var result = new byte[preamble.Length + body.Length];
            preamble.CopyTo(result, 0);
            body.CopyTo(result, preamble.Length);
            return result;
        }

        public override string ToString() => _sb.ToString();

        public static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";

            // Cells starting with these characters are treated as formulas by Excel/Sheets.
            var first = value[0];
            if (first == '=' || first == '+' || first == '-' || first == '@' || first == '\t' || first == '\r')
            {
                // Plain negative numbers are fine and common (none here), anything else gets neutralised.
                double ignored;
                if (!(first == '-' && double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out ignored)))
                    value = "'" + value;
            }

            var needsQuotes = value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 || value != value.Trim();
            if (value.Contains("\"")) value = value.Replace("\"", "\"\"");
            return needsQuotes ? "\"" + value + "\"" : value;
        }
    }
}
