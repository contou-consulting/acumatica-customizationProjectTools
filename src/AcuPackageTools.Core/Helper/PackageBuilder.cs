using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;

namespace AcuPackageTools.Helper
{
    /// <summary>
    /// Builds a customization package from an Acumatica source-control folder,
    /// replicating PX.CommandLine.exe /method BuildProject byte-for-byte (modulo
    /// zip entry timestamps). The algorithm mirrors PXSourceControl.LoadProject +
    /// CstDocument.SavePackage/ExportCustomizationFile from PX.Web.Customization.dll.
    /// </summary>
    public class PackageBuilder
    {
        private sealed class PackageItem
        {
            public string Key;
            public XmlElement Element;
            public string AppRelativePath; // File/PerTenantFile only
            public string SourceFile;      // File/PerTenantFile only
            public bool IsPerTenantFile;
        }

        public static void BuildCustomizationPackage(string customizationPath,
                                                     string packageFilename,
                                                     string description,
                                                     int? level,
                                                     string productVersion,
                                                     Action<string> writeVerbose = null)
        {
            customizationPath = Path.GetFullPath(customizationPath).TrimEnd('\\');
            string projectXmlPath = Path.Combine(customizationPath, "project.xml");
            string projectDir = Path.Combine(customizationPath, "_project");

            var fragments = new List<XmlElement>();
            int? metaLevel = null;
            string metaDescription = "";

            var doc = new XmlDocument();
            if (Directory.Exists(projectDir))
            {
                var sb = new StringBuilder();
                foreach (string file in Directory.GetFiles(projectDir))
                {
                    // Same skip rule as LoadProject: substring test on the full path.
                    if (file.Contains("ProjectMetadata.xml")) continue;
                    writeVerbose?.Invoke($"Appending {Path.GetFileName(file)} to customization project.xml...");
                    sb.Append(File.ReadAllText(file));
                }
                doc.LoadXml("<c>" + sb + "</c>");
                fragments.AddRange(doc.DocumentElement.ChildNodes.OfType<XmlElement>());
            }
            else if (File.Exists(projectXmlPath))
            {
                doc.LoadXml(File.ReadAllText(projectXmlPath));
                XmlElement root = doc.DocumentElement;
                string levelAttr = root.GetAttribute("level");
                if (int.TryParse(levelAttr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                {
                    metaLevel = parsed;
                }
                metaDescription = root.GetAttribute("description");
                fragments.AddRange(root.ChildNodes.OfType<XmlElement>());
            }

            // Items keyed like CstDocument.Items: duplicate keys collapse, last wins.
            var items = new Dictionary<string, PackageItem>(StringComparer.Ordinal);

            // <File> fragments become a lookaside reattached only when the file exists
            // on disk; <PerTenantFile> fragments are dropped (their AppRelativePath is
            // development-relative and never matches a folder-relative disk path — the
            // engine has the same dead branch).
            var fileLookaside = new Dictionary<string, XmlElement>(StringComparer.OrdinalIgnoreCase);
            foreach (XmlElement fragment in fragments)
            {
                if (fragment.Name == "File")
                {
                    fileLookaside[fragment.GetAttribute("AppRelativePath")] = fragment;
                }
                else if (fragment.Name != "PerTenantFile")
                {
                    var item = new PackageItem { Key = CstItemKey.GetKey(fragment), Element = fragment };
                    items[item.Key] = item;
                }
            }

            const string devPrefix = "FrontendSources\\screen\\src\\development\\";
            foreach (string fullPath in Directory.GetFiles(customizationPath, "*", SearchOption.AllDirectories))
            {
                if (string.Equals(fullPath, projectXmlPath, StringComparison.OrdinalIgnoreCase)) continue;
                if (fullPath.StartsWith(projectDir, StringComparison.InvariantCultureIgnoreCase)) continue;

                string rel = fullPath.Substring(customizationPath.Length + 1);
                writeVerbose?.Invoke($"Adding {rel} to customization project...");

                PackageItem item;
                if (fileLookaside.TryGetValue(rel, out XmlElement declared))
                {
                    // Reuse the declared element (keeps Description/SystemFile/etc.)
                    // and its declared path casing, exactly like LoadProject.
                    item = new PackageItem
                    {
                        Element = declared,
                        AppRelativePath = declared.GetAttribute("AppRelativePath"),
                        SourceFile = fullPath,
                    };
                    item.Key = "File#" + item.AppRelativePath;
                }
                else if (rel.StartsWith(devPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    string appRel = rel.Substring(devPrefix.Length);
                    string screenId = CstItemKey.TryParseScreenId(appRel);
                    XmlElement e = doc.CreateElement("PerTenantFile");
                    e.SetAttribute("AppRelativePath", appRel);
                    if (!string.IsNullOrEmpty(screenId))
                    {
                        e.SetAttribute("ScreenId", screenId);
                    }
                    item = new PackageItem
                    {
                        Key = "PerTenantFile#" + appRel,
                        Element = e,
                        AppRelativePath = appRel,
                        SourceFile = fullPath,
                        IsPerTenantFile = true,
                    };
                }
                else
                {
                    XmlElement e = doc.CreateElement("File");
                    e.SetAttribute("AppRelativePath", rel);
                    item = new PackageItem
                    {
                        Key = "File#" + rel,
                        Element = e,
                        AppRelativePath = rel,
                        SourceFile = fullPath,
                    };
                }
                items[item.Key] = item;
            }

            if (File.Exists(Path.Combine(projectDir, "ProjectMetadata.xml")))
            {
                var metadata = new XmlDocument();
                metadata.Load(Path.Combine(projectDir, "ProjectMetadata.xml"));
                string levelAttr = metadata.DocumentElement.GetAttribute("level");
                if (int.TryParse(levelAttr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                {
                    metaLevel = parsed;
                }
                metaDescription = metadata.DocumentElement.GetAttribute("description");
            }

            int? effectiveLevel = level ?? metaLevel;
            string effectiveDescription = description ?? metaDescription ?? "";

            // Single global sort by key, linguistic comparison — same as the engine's
            // OrderBy(_ => _.Key) with Comparer<string>.Default.
            List<PackageItem> sorted = items.Values.OrderBy(i => i.Key, StringComparer.CurrentCulture).ToList();

            string projectXml = SerializeProjectXml(sorted, effectiveLevel, effectiveDescription, productVersion);

            using (var ms = new MemoryStream())
            {
                using (var archive = new ZipArchive(ms, ZipArchiveMode.Update, leaveOpen: true))
                {
                    WriteEntry(archive, "project.xml", Encoding.UTF8.GetBytes(projectXml));
                    foreach (PackageItem file in sorted.Where(i => i.SourceFile != null && !i.IsPerTenantFile))
                    {
                        WriteEntry(archive, NormalizeEntryName(file.AppRelativePath), File.ReadAllBytes(file.SourceFile));
                    }
                    foreach (PackageItem file in sorted.Where(i => i.IsPerTenantFile))
                    {
                        WriteEntry(archive, NormalizeEntryName(file.AppRelativePath), File.ReadAllBytes(file.SourceFile));
                    }
                }
                ms.Flush();
                File.WriteAllBytes(packageFilename, ms.ToArray());
            }
        }

        private static string SerializeProjectXml(IEnumerable<PackageItem> sortedItems,
                                                  int? level, string description, string productVersion)
        {
            var outDoc = new XmlDocument();
            XmlElement root = outDoc.CreateElement("Customization");
            outDoc.AppendChild(root);
            root.SetAttribute("level", level.HasValue ? level.Value.ToString(CultureInfo.InvariantCulture) : "");
            root.SetAttribute("description", description);
            root.SetAttribute("product-version", productVersion);
            foreach (PackageItem item in sortedItems)
            {
                root.AppendChild(outDoc.ImportNode(item.Element, true));
            }

            var sb = new StringBuilder();
            using (var writer = new XmlTextWriter(new StringWriter(sb)))
            {
                writer.Formatting = Formatting.Indented;
                writer.Indentation = 4;
                root.WriteTo(writer);
            }
            return sb.ToString();
        }

        private static string NormalizeEntryName(string path)
        {
            return path.Replace('\\', '/').Trim('/');
        }

        private static void WriteEntry(ZipArchive archive, string name, byte[] content)
        {
            ZipArchiveEntry entry = archive.CreateEntry(name);
            using (Stream stream = entry.Open())
            {
                stream.Write(content, 0, content.Length);
            }
        }
    }
}
