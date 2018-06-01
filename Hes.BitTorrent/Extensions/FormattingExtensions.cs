namespace Hes.BitTorrent.Extensions {

    using System;
    using System.Collections.Generic;
    using System.Linq;

    public static class FormattingExtensions {

        public static string GetFormattedString(this byte[] byteArray) {
            return string.Join("", byteArray.Select(x => x.ToString("x2"))) + " (" + System.Text.Encoding.UTF8.GetString(byteArray) + ")";
        }

        public static string GetFormattedString(this long obj){
            return obj.ToString();
        }

        public static string GetFormattedString(List<object> obj, int depth){
            var pad1 = new string(' ', depth * 2);
            var pad2 = new string(' ', (depth + 1) * 2);

            if (obj.Count < 1)
                return "[]";

            if (obj[0].GetType() == typeof(Dictionary<string, object>))
                return "\n" + pad1 + "[" + string.Join(",", obj.Select(x => pad2 + GetFormattedString(x, depth + 1))) + "\n" + pad1 + "]";

            return "[ " + string.Join(", ", obj.Select(x => GetFormattedString(x))) + " ]";
        }

        public static string GetFormattedString(Dictionary<string, object> obj, int depth){
            var pad1 = new string(' ', depth * 2);
            var pad2 = new string(' ', (depth + 1) * 2);
            return (depth > 0 ? "\n" : "") + pad1 + "{" + string.Join("", obj.Select(x => "\n" + pad2 + (x.Key + ":").PadRight(15, ' ') + GetFormattedString(x.Value, depth + 1))) + "\n" + pad1 + "}";
        }

        private static string GetFormattedString(this object obj, int depth = 0)
        {
            var output = "";

            switch(obj) {
                case byte[] bytes:
                    output += bytes.GetFormattedString();
                    break;

                case long l:
                    output += GetFormattedString(l);
                    break;

                default:
                    if (obj.GetType() == typeof(List<object>))
                        output += GetFormattedString((List<object>)obj, depth);
                    else if (obj.GetType() == typeof(Dictionary<string, object>))
                        output += GetFormattedString((Dictionary<string, object>)obj, depth);
                    else
                        throw new Exception("unable to encode type " + obj.GetType());

                    break;
            }

            return output;
        }
    }
}