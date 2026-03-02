using System;
using System.Collections.Immutable;
using System.IO;
using System.Xml.Linq;

namespace OmniSharp.MSBuild.SolutionParsing
{
    internal static class SlnxFile
    {
        public static SolutionFile ParseFile(string path)
        {
            var doc = XDocument.Load(path);
            var projects = ImmutableArray.CreateBuilder<ProjectBlock>();

            CollectProjects(doc.Root, projects);

            return SolutionFile.CreateFromSlnx(projects.ToImmutable());
        }

        private static void CollectProjects(XElement element, ImmutableArray<ProjectBlock>.Builder projects)
        {
            foreach (var child in element.Elements())
            {
                if (child.Name.LocalName == "Project")
                {
                    var relativePath = child.Attribute("Path")?.Value;
                    if (string.IsNullOrEmpty(relativePath))
                        continue;

                    var projectName = Path.GetFileNameWithoutExtension(relativePath);
                    var idAttr = child.Attribute("Id")?.Value;

                    string projectGuid;
                    if (!string.IsNullOrEmpty(idAttr) && Guid.TryParse(idAttr, out var parsed))
                    {
                        projectGuid = parsed.ToString("B").ToUpperInvariant();
                    }
                    else
                    {
                        // Generate a deterministic GUID from the relative path
                        projectGuid = GenerateDeterministicGuid(relativePath).ToString("B").ToUpperInvariant();
                    }

                    projects.Add(ProjectBlock.Create(projectName, relativePath, projectGuid));
                }
                else if (child.Name.LocalName == "Folder")
                {
                    // Recurse into solution folders
                    CollectProjects(child, projects);
                }
            }
        }

        private static Guid GenerateDeterministicGuid(string input)
        {
            // Simple deterministic GUID based on string hash
            var bytes = System.Text.Encoding.UTF8.GetBytes(input);
            using var sha = System.Security.Cryptography.SHA256.Create();
            var hash = sha.ComputeHash(bytes);
            var guidBytes = new byte[16];
            Array.Copy(hash, guidBytes, 16);
            return new Guid(guidBytes);
        }
    }
}
