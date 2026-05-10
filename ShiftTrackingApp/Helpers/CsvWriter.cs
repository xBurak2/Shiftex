using System.Globalization;
using System.Text;

namespace ShiftTrackingApp.Helpers
{
    /// <summary>
    /// RFC 4180 uyumlu CSV yazıcı. Dış kütüphane bağımlılığı yoktur.
    /// Excel'in Türkçe locale'iyle açabilmesi için UTF-8 BOM ekler ve
    /// virgül yerine noktalı virgül delimiter kullanır (configurable).
    /// </summary>
    public static class CsvWriter
    {
        public static byte[] Write(
            IEnumerable<string> headers,
            IEnumerable<IEnumerable<object?>> rows,
            char delimiter = ';')
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(delimiter, headers.Select(Escape)));
            foreach (var row in rows)
                sb.AppendLine(string.Join(delimiter, row.Select(c => Escape(c?.ToString() ?? ""))));

            // UTF-8 BOM — Excel için Türkçe karakterlerin doğru görünmesi şart
            var bom   = Encoding.UTF8.GetPreamble();
            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return bom.Concat(bytes).ToArray();

            static string Escape(string s)
            {
                if (s.Contains(';') || s.Contains('\n') || s.Contains('"'))
                    return $"\"{s.Replace("\"", "\"\"")}\"";
                return s;
            }
        }
    }
}
