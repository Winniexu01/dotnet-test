// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NuGet.Common;

namespace NuGet.Test.Utility
{
    /// <summary>
    /// Solution
    /// </summary>
    public class SimpleTestSolutionContext
    {
        public SimpleTestSolutionContext(string solutionRoot, params SimpleTestProjectContext[] projects) : this(solutionRoot, false, projects)
        {
        }

        public SimpleTestSolutionContext(string solutionRoot, bool useSlnx, params SimpleTestProjectContext[] projects)
        {
            SolutionPath = Path.Combine(solutionRoot, useSlnx ? "solution.slnx" : "solution.sln");

            Projects.AddRange(projects);
        }

        /// <summary>
        /// Full path
        /// </summary>
        public string SolutionPath { get; set; }

        /// <summary>
        /// Projects
        /// </summary>
        public List<SimpleTestProjectContext> Projects { get; set; } = new List<SimpleTestProjectContext>();

        /// <summary>
        /// Guid
        /// </summary>
        public Guid SolutionGuid { get; set; } = Guid.NewGuid();

        public void Save()
        {
            Save(SolutionPath);
        }

        public void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            File.WriteAllText(path, GetContent().ToString());
        }

        public StringBuilder GetContent()
        {
            if (Path.GetExtension(SolutionPath) == ".sln")
            {
                return GetContentForSln();
            }
            else if (Path.GetExtension(SolutionPath) == ".slnx")
            {
                return GetContentForSlnx();
            }
            else
            {
                throw new InvalidOperationException("Unknown solution file type");
            }

            StringBuilder GetContentForSln()
            {
                StringBuilder sb = new StringBuilder();

                sb.AppendLine("Microsoft Visual Studio Solution File, Format Version 12.00");
                sb.AppendLine("# Visual Studio 2012");

                foreach (var project in Projects)
                {
                    sb.AppendLine("Project(\"{" + "FAE04EC0-301F-11D3-BF4B-00C04F79EFBC" + "}"
                        + $"\") = \"{project.ProjectName}\", " + "\"" + PathUtility.GetRelativePath(SolutionPath, project.ProjectPath) + "\", \"{" + project.ProjectGuid.ToString().ToUpperInvariant() + "}\"");
                    sb.AppendLine("EndProject");
                }

                sb.AppendLine("Global");
                sb.AppendLine("  GlobalSection(SolutionConfigurationPlatforms) = preSolution");
                sb.AppendLine("    Debug|Any CPU = Debug|Any CPU");
                sb.AppendLine("    Release|Any CPU = Release|Any CPU");
                sb.AppendLine("  EndGlobalSection");
                sb.AppendLine("  GlobalSection(ProjectConfigurationPlatforms) = postSolution");
                foreach (var project in Projects)
                {
                    // this should probably be uppercase?
                    sb.AppendLine("    {" + project.ProjectGuid.ToString().ToUpperInvariant() + "}.Debug|Any CPU.ActiveCfg = Debug|Any CPU");
                    sb.AppendLine("    {" + project.ProjectGuid.ToString().ToUpperInvariant() + "}.Debug|Any CPU.Build.0 = Debug|Any CPU");
                    sb.AppendLine("    {" + project.ProjectGuid.ToString().ToUpperInvariant() + "}.Release|Any CPU.ActiveCfg = Release|Any CPU");
                    sb.AppendLine("    {" + project.ProjectGuid.ToString().ToUpperInvariant() + "}.Release|Any CPU.Build.0 = Release|Any CPU");
                }
                sb.AppendLine("  EndGlobalSection");
                sb.AppendLine("  GlobalSection(SolutionProperties) = preSolution");
                sb.AppendLine("    HideSolutionNode = FALSE");
                sb.AppendLine("  EndGlobalSection");
                sb.AppendLine("  GlobalSection(ExtensibilityGlobals) = postSolution");
                sb.AppendLine("    SolutionGuid = {" + SolutionGuid.ToString() + "}");
                sb.AppendLine("  EndGlobalSection");
                sb.AppendLine("EndGlobal");

                return sb;
            }

            StringBuilder GetContentForSlnx()
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("<Solution>");
                foreach (var project in Projects)
                {
                    sb.AppendLine($"<Project Path=\"{PathUtility.GetRelativePath(SolutionPath, project.ProjectPath)}\" />");
                }
                sb.AppendLine("</Solution>");

                return sb;
            }
        }

        /// <summary>
        /// Create an entire solution and projects, this will adjust the paths as needed
        /// </summary>
        public void Create(string solutionFolder = null)
        {
            Save();

            foreach (var project in GetAllProjects())
            {
                // only save after updating everything
                project.Save();
            }

            if (CentralPackageVersionsManagementFile != null)
            {
                CentralPackageVersionsManagementFile.Save();
            }
        }

        /// <summary>
        /// All projects used in the solution
        /// </summary>
        public HashSet<SimpleTestProjectContext> GetAllProjects()
        {
            var projects = new HashSet<SimpleTestProjectContext>();
            var toWalk = new Stack<SimpleTestProjectContext>(Projects);

            while (toWalk.Count > 0)
            {
                var project = toWalk.Pop();

                if (projects.Add(project))
                {
                    foreach (var dep in project.AllProjectReferences)
                    {
                        toWalk.Push(dep);
                    }
                }
            }

            return projects;
        }

        public CentralPackageVersionsManagementFile CentralPackageVersionsManagementFile { get; set; }
    }
}
