using System;

namespace WindowsFormsApp1.AI
{
    public static class TitleDepartmentParser
    {
        public static (string Title, string Department) ParseTitleDepartmentLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return ("", "");

            string raw = line.Trim();

            // Normalize common separators: "Unvan / Birim" (with or without spaces)
            int idx = raw.IndexOf('/');
            if (idx < 0)
            {
                // fallback: if only one token present, treat as title
                return (raw, "");
            }

            string left = raw.Substring(0, idx).Trim();
            string right = raw.Substring(idx + 1).Trim();

            // Safety: avoid returning nulls
            return (left ?? "", right ?? "");
        }

        public static string FormatTitleDepartment(string title, string department)
        {
            title = (title ?? "").Trim();
            department = (department ?? "").Trim();

            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(department)) return "";
            if (string.IsNullOrWhiteSpace(department)) return title;
            if (string.IsNullOrWhiteSpace(title)) return department;
            return $"{title} / {department}";
        }
    }
}

