using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace AcuPackageTools.Helper
{
    /// <summary>
    /// Computes the sort key for a customization project item exactly as the
    /// Acumatica customization engine does (each Cst* class's Key property in
    /// PX.Web.Customization.dll). The engine sorts ALL items of a project by this
    /// key with a linguistic (culture-sensitive) string comparison before writing
    /// project.xml and the zip entries.
    /// </summary>
    public static class CstItemKey
    {
        private static readonly char[] PathSeparators = { '\\', '/' };

        // CstEntityBase subclasses: tag -> KeyColumns. The key value(s) are read
        // from the first data row of the entity's main table inside the fragment.
        private static readonly Dictionary<string, string[]> EntityKeyColumns =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["SiteMapNode"] = new[] { "NodeID" },
                ["ScreenWithRights"] = new[] { "ScreenID" },
                ["GenericInquiryScreen"] = new[] { "Name" },
                ["BpEvent"] = new[] { "EventID" },
                ["Dashboard"] = new[] { "Name" },
                ["DashboardV2"] = new[] { "Name" },
                ["SharedFilter"] = new[] { "FilterName", "ScreenID" },
                ["Locale"] = new[] { "LocaleName" },
                ["MobileSiteMapWorkspaces"] = new[] { "Name" },
                ["OAuthClient"] = new[] { "ClientID" },
                ["PushNotification"] = new[] { "HookId" },
                ["ReportDefinition"] = new[] { "ReportUID" },
                ["Webhook"] = new[] { "Name" },
                ["WikiArticle"] = new[] { "PageID" },
                ["XportScenario"] = new[] { "Name" },
                ["DeletedRecordsTrackingTable"] = new[] { "TableName" },
                ["CSAttribute"] = new[] { "AttributeID" },
            };

        public static string GetKey(XmlElement e)
        {
            switch (e.Name)
            {
                case "File": return "File#" + e.GetAttribute("AppRelativePath");
                case "PerTenantFile": return "PerTenantFile#" + e.GetAttribute("AppRelativePath");
                case "Sql": return "Sql#" + e.GetAttribute("TableName");
                case "Table": return e.GetAttribute("TableName");
                case "Page": return e.GetAttribute("path").ToLowerInvariant();
                case "Graph": return "Code#" + e.GetAttribute("ClassName");
                case "DAC": return e.GetAttribute("type");
                case "Data": return "DATA#" + e.GetAttribute("tableName").ToUpperInvariant();
                case "Export": return "EXPORT#" + e.GetAttribute("Name").ToUpperInvariant();
                case "DataFields": return "DataFields#" + e.GetAttribute("Table").ToUpperInvariant();
                case "Report": return "REPORT#" + e.GetAttribute("Name").ToUpperInvariant();
                case "Screen": return "SCREEN#" + e.GetAttribute("ID").ToUpperInvariant();
                case "MobileSiteMap": return "MobileSiteMap#" + e.GetAttribute("ScreenID").ToUpperInvariant();
                case "ScreenConfiguration": return "ScreenConfiguration#" + e.GetAttribute("ScreenId");
                case "EditorUpdatedFields": return e.GetAttribute("ScreenId");
                case "Info": return "Info";
                case "GenericInquiry": return "GenericInquiry";
                case "CodeGenSettings": return "CodeGenSettings";
                case "WorkflowContainer": return "WorkflowContainer#" + e.GetAttribute("ScreenID");
                case "Forms": return "Forms#" + e.GetAttribute("ScreenID");
                case "Actions": return "Actions#" + e.GetAttribute("ScreenID");
                case "Fields": return "Fields#" + e.GetAttribute("ScreenID");
                case "EventHandlers": return "EventHandlers#" + e.GetAttribute("ScreenID");
                case "EntityEndpoint": return "EntityEndpoint#" + e.GetAttribute("Version") + "§" + e.GetAttribute("Name");
                case "AUScreenAction":
                    return "AUScreenAction#" + e.GetAttribute("ScreenID").ToUpperInvariant() + "#" + e.GetAttribute("ActionName").ToUpperInvariant();
                case "AUScreenNavigationAction":
                    return "AUScreenNavigationAction#" + e.GetAttribute("ScreenID").ToUpperInvariant() + "#" + e.GetAttribute("ActionName").ToUpperInvariant();
                case "AutomationScreenCondition":
                    return "AutomationScreenCondition#" + e.GetAttribute("ScreenID") + "#" + e.GetAttribute("ConditionID") + "#" + e.GetAttribute("ConditionName");
                case "AutomationScreenField":
                    return "AutomationScreenField#" + e.GetAttribute("ScreenID") + "#" + e.GetAttribute("TableName") + "#" + e.GetAttribute("FieldName");
                default:
                    if (EntityKeyColumns.TryGetValue(e.Name, out string[] keyColumns))
                    {
                        string[] keys = ExtractEntityKeys(e, keyColumns);
                        return e.Name + "#" + string.Join("§", keys.Where(k => !string.IsNullOrEmpty(k)));
                    }
                    throw new NotSupportedException($"Unknown tag {e.Name}");
            }
        }

        // Port of CstEntityBase.extractKeysFromXml: main table name comes from
        // <relations main-table="...">, the key values from the first element
        // named after the main table (its first child when the element itself
        // carries no attributes, which is the normal <Table><row .../></Table> shape).
        private static string[] ExtractEntityKeys(XmlElement xe, string[] keyColumns)
        {
            string mainTable = xe.GetElementsByTagName("relations")[0].Attributes["main-table"].Value;
            XmlNodeList rows = xe.GetElementsByTagName(mainTable);
            if (rows.Count == 0)
            {
                rows = xe.GetElementsByTagName(mainTable.ToLowerInvariant());
            }
            XmlNode node = rows[0];
            if (node == null) return new string[0];
            if (node.Attributes.Count == 0)
            {
                node = node.FirstChild;
            }
            var result = new string[keyColumns.Length];
            for (int i = 0; i < keyColumns.Length; i++)
            {
                string column = keyColumns[i];
                result[i] = node.Attributes.OfType<XmlAttribute>()
                    .Single(a => a.Name.Equals(column, StringComparison.OrdinalIgnoreCase)).Value;
            }
            return result;
        }

        // Port of ProjectNewUiFrontendFileMaintenance.TryParseScreenId. Input is the
        // development-relative path (e.g. screens\LS\LSPT1010\LSPT1010.ts).
        public static string TryParseScreenId(string path)
        {
            if (path == null) return null;
            if (Path.GetFileName(path).Length >= 9)
            {
                string prefix = path.Substring(0, 8);
                char c = path[8];
                if (prefix.All(char.IsLetterOrDigit) && (c == '.' || c == '_'))
                {
                    return prefix.ToUpperInvariant();
                }
            }
            return Path.GetDirectoryName(path)
                ?.Split(PathSeparators, StringSplitOptions.RemoveEmptyEntries)
                .LastOrDefault(p => p.Length == 8 && p.All(char.IsLetterOrDigit))
                ?.ToUpperInvariant();
        }
    }
}
