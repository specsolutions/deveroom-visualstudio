﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media;
using Deveroom.VisualStudio.Interop;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using IServiceProvider = System.IServiceProvider;
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

namespace Deveroom.VisualStudio
{
    public static class VsUtils
    {
        public static ProjectItem GetProjectItemFromTextBuffer(ITextBuffer textBuffer)
        {
            try
            {
                if (!textBuffer.Properties.TryGetProperty(typeof(IVsTextBuffer), out IVsTextBuffer bufferAdapter) ||
                    bufferAdapter == null)
                    return null;

                var extensibleObject = bufferAdapter as IExtensibleObject;
                if (extensibleObject != null)
                {
                    extensibleObject.GetAutomationObject("Document", null, out object documentObj);
                    var document = (Document) documentObj;
                    if (document != null)
                    {
                        return document.ProjectItem;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex, $"{nameof(VsUtils)}.{nameof(GetProjectItemFromTextBuffer)}");
                return null;
            }
        }

        public static IWpfTextView GetWpfTextViewFromFilePath(string filePath, IServiceProvider serviceProvider, IVsEditorAdaptersFactoryService editorAdaptersFactoryService)
        {
            if (VsShellUtilities.IsDocumentOpen(serviceProvider, filePath, Guid.Empty,
                out var _, out var _, out var windowFrame))
            {
                // Get the IVsTextView from the windowFrame.
                IVsTextView textView = VsShellUtilities.GetTextView(windowFrame);
                if (!IsInitialized(textView))
                    return null;

                return editorAdaptersFactoryService.GetWpfTextView(textView);
            }

            return null;
        }

        /// <summary>
        /// IVsEditorAdaptersFactoryService.GetWpfTextView brings the text view into an inconsistent state when it is not fully initialized (open project with files opened but not activated yet)
        /// </summary>
        private static bool IsInitialized(IVsTextView textView)
        {
            if (textView == null)
                return false;
            var propertyInfo = textView.GetType().GetProperty("CurrentInitializationState", BindingFlags.Instance | BindingFlags.Public);
            if (propertyInfo == null)
                return true; // actually we don't know
            var value = propertyInfo.GetValue(textView).ToString();
            return value == "TextViewAvailable";
        }

        public static Project GetProject(ProjectItem projectItem)
        {
            try
            {
                return projectItem?.ContainingProject;
            }
            catch(Exception ex)
            {
                Debug.WriteLine(ex, $"{nameof(VsUtils)}.{nameof(GetProject)}");
                return null;
            }
        }

        public static bool IsSolutionProject(Project project)
        {
            try
            {
                return !string.IsNullOrWhiteSpace(project.FullName) &&
                    Path.GetDirectoryName(project.FullName) != null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex, $"{nameof(VsUtils)}.{nameof(IsSolutionProject)}");
                return false;
            }
        }

        public static string GetProjectFolder(Project project)
        {
            try
            {
                return project.Properties.Item("FullPath").Value.ToString();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex, $"{nameof(VsUtils)}.{nameof(GetProjectFolder)}");
                return string.IsNullOrEmpty(project.FullName) ? null :
                    Path.GetDirectoryName(project.FullName);
            }
        }

        public static string GetPlatformName(Project project)
        {
            try
            {
                return project.ConfigurationManager.ActiveConfiguration.PlatformName;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex, $"{nameof(VsUtils)}.{nameof(GetPlatformName)}");
                return null;
            }
        }

        public static string GetPlatformTargetName(Project project)
        {
            try
            {
                if (project.ConfigurationManager.ActiveConfiguration.Properties == null)
                    return null;
                return project.ConfigurationManager.ActiveConfiguration.Properties.Item("PlatformTarget").Value.ToString();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex, $"{nameof(VsUtils)}.{nameof(GetPlatformTargetName)}");
                return null;
            }
        }

        public static string GetOutputFileName(Project project)
        {
            try
            {
                var result = project.Properties.Item("OutputFileName").Value.ToString();
                if (string.IsNullOrWhiteSpace(result))
                {
                    result = project.Properties.Item("AssemblyName").Value + ".dll";
                }
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex, $"{nameof(VsUtils)}.{nameof(GetOutputFileName)}");
                return null;
            }
        }

        public static string GetOutputPath(Project project)
        {
            try
            {
                if (project.ConfigurationManager.ActiveConfiguration.Properties == null)
                    return null;
                return project.ConfigurationManager.ActiveConfiguration.Properties.Item("OutputPath").Value.ToString();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex, $"{nameof(VsUtils)}.{nameof(GetOutputPath)}");
                return null;
            }
        }

        public static string GetOutputAssemblyPath(Project project)
        {
            try
            {
                //DumpProperties(project.Properties, "project");
                //DumpProperties(project.ConfigurationManager.ActiveConfiguration.Properties, "ActiveConfiguration");
                var outputFileName = GetOutputFileName(project);
                if (outputFileName == null)
                    return null;
                var projectFolder = GetProjectFolder(project);
                if (projectFolder == null)
                    return null;
                var outputPath = GetOutputPath(project);
                if (outputPath == null)
                    return null;

                return Path.Combine(projectFolder, outputPath, outputFileName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex, $"{nameof(VsUtils)}.{nameof(GetOutputAssemblyPath)}");
                return null;
            }
        }

        public static string GetTargetFrameworkMoniker(Project project)
        {
            try
            {
                return project.Properties.Item("TargetFrameworkMoniker").Value.ToString();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex, $"{nameof(VsUtils)}.{nameof(GetTargetFrameworkMoniker)}");
                return null;
            }
        }

        private static void DumpProperties(Properties props, string category)
        {
            if (props == null)
                return;
            var result = new StringBuilder();
            result.AppendLine("START PROPS: " + category);
            foreach (Property prop in props)
            {
                result.Append($"{prop.Name} = ");
                try
                {
                    result.Append(prop.Value);
                }
                catch (Exception e)
                {
                    result.Append(e.Message);
                }
                result.AppendLine();
            }
            result.AppendLine("END PROPS: " + category);
            Debug.WriteLine(result);
        }

        public static T SafeResolveMefDependency<T>(DTE dte) where T : class
        {
            try
            {
                var oleServiceProvider = dte as IOleServiceProvider;
                if (oleServiceProvider == null)
                    return null;
                return ResolveMefDependency<T>(new ServiceProvider(oleServiceProvider));
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return null;
            }
        }

        public static T ResolveMefDependency<T>(IServiceProvider serviceProvider) where T : class
        {
            var componentModel = (IComponentModel)serviceProvider.GetService(typeof(SComponentModel));
            return componentModel?.GetService<T>();
        }

        public static string GetMefCatalogCacheFolder(IServiceProvider serviceProvider)
        {
            var componentModelHost = serviceProvider.GetService(typeof(SVsComponentModelHost)) as IVsComponentModelHost;
            componentModelHost.GetCatalogCacheFolder(out var folderPath);
            return folderPath;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        public static extern int ClientToScreen(IntPtr hWnd, [In, Out] User32Point pt);

        [StructLayout(LayoutKind.Sequential)]
        public class User32Point
        {
            public int x;
            public int y;
            public User32Point()
            {
                x = 0;
                y = 0;
            }

            public User32Point(int x, int y)
            {
                this.x = x;
                this.y = y;
            }
        }

        public static Point? GetCaretPosition(IServiceProvider serviceProvider)
        {
            try
            {
                var vsTextManager = (IVsTextManager) serviceProvider.GetService(typeof(SVsTextManager));

                ErrorHandler.ThrowOnFailure(vsTextManager.GetActiveView(Convert.ToInt32(true), null, out var activeView));
                if (activeView == null)
                    return null;

                ErrorHandler.ThrowOnFailure(activeView.GetCaretPos(out var caretLine, out var caretColumn));

                var interopPoint = new POINT[1];
                ErrorHandler.ThrowOnFailure(activeView.GetPointOfLineColumn(caretLine + 1, caretColumn + 1, interopPoint));

                var p = new User32Point(interopPoint[0].x, interopPoint[0].y);
                ErrorHandler.ThrowOnFailure(ClientToScreen(activeView.GetWindowHandle(), p));

                var wpfTextView = GetWpfTextView(serviceProvider, activeView) as Visual;
                if (wpfTextView != null)
                {
                    var target = PresentationSource.FromVisual(wpfTextView)?.CompositionTarget;
                    if (target != null)
                    {
                        var transformedPoint = target.TransformFromDevice.Transform(new Point(p.x, p.y));
                        return transformedPoint;
                    }
                }

                return new Point(p.x, p.y);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return null;
            }
        }

        private static IWpfTextView GetWpfTextView(IServiceProvider serviceProvider, IVsTextView activeView)
        {
            var editorAdaptersFactoryService = ResolveMefDependency<IVsEditorAdaptersFactoryService>(serviceProvider);
            return editorAdaptersFactoryService?.GetWpfTextView(activeView);
        }

        public static IVsHierarchy GetHierarchyFromProject(Project project)
        {
            try
            {
                var serviceProvider = new ServiceProvider(project.DTE as IOleServiceProvider);
                if (!(serviceProvider.GetService(typeof(SVsSolution)) is IVsSolution solution))
                    return null;
                if (!ErrorHandler.Succeeded(solution.GetProjectOfUniqueName(project.FullName, out var hierarchy)))
                    return null;
                return hierarchy;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return null;
            }
        }

        public static Project GetProjectFromHierarchy(IVsHierarchy hierarchy)
        {
            try
            {
                if (!ErrorHandler.Succeeded(hierarchy.GetProperty(
                    VSConstants.VSITEMID_ROOT,
                    (int) __VSHPROPID.VSHPROPID_ExtObject,
                    out var projectObj)))
                    return null;

                return projectObj as Project;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return null;
            }
        }

        public static string SafeGetProjectFilePath(IVsHierarchy vsHierarchy)
        {
            var project = GetProjectFromHierarchy(vsHierarchy);
            return project?.FullName;
        }

        public static string GetMsBuildPropertyValue(Project project, string propertyName)
        {
            try
            {
                var hierarchy = GetHierarchyFromProject(project);
                if (!(hierarchy is IVsBuildPropertyStorage propertyStorage))
                    return null;

                if (ErrorHandler.Succeeded(
                    propertyStorage.GetPropertyValue(propertyName, project.ConfigurationManager.ActiveConfiguration.ConfigurationName,
                    (uint)_PersistStorageType.PST_PROJECT_FILE, out var propValue)))
                    return propValue;

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return null;
            }
        }

        public static IEnumerable<ProjectItem> GetProjectItems(Project project)
        {
            return GetProjectItems(project.ProjectItems);
        }

        private static IEnumerable<ProjectItem> GetProjectItems(ProjectItems projectItems)
        {
            foreach (ProjectItem projectItem in projectItems)
            {
                yield return projectItem;
                if (projectItem.ProjectItems != null)
                    foreach (var subProjectItem in GetProjectItems(projectItem.ProjectItems))
                        yield return subProjectItem;
            }
        }

        public static IEnumerable<ProjectItem> GetPhysicalFileProjectItems(Project project)
        {
            return GetProjectItems(project).Where(IsPhysicalFile);
        }

        public static ProjectItem FindProjectItemByFilePath(Project project, string filePath)
        {
            return GetPhysicalFileProjectItems(project)
                .FirstOrDefault(pi => string.Equals(filePath, GetFilePath(pi), StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsPhysicalFile(ProjectItem projectItem)
        {
            return string.Equals(projectItem.Kind, VSConstants.GUID_ItemType_PhysicalFile.ToString("B"), StringComparison.InvariantCultureIgnoreCase);
        }

        public static string GetFilePath(ProjectItem projectItem)
        {
            if (!IsPhysicalFile(projectItem))
                return null;

            return projectItem.FileNames[1];
        }

        public static IEnumerable<Project> GetAllProjects(DTE dte)
        {
            var projects = dte.Solution.Projects.OfType<Project>().ToArray();
            return EnumerateProjectHierarchy(projects);
        }

        private static IEnumerable<Project> EnumerateProjectHierarchy(IEnumerable<Project> projects)
        {
            foreach (var project in projects)
            {
                yield return project;
                var subProjects = project.ProjectItems.OfType<ProjectItem>().Select(x => x.SubProject).OfType<Project>().ToArray();
                foreach (var subProject in EnumerateProjectHierarchy(subProjects))
                {
                    yield return subProject;
                }
            }
        }

        public static string GetVsMainVersion(IServiceProvider serviceProvider)
        {
            const string defaultMainVersion = "15.0";
            try
            {
                var dte = (DTE)serviceProvider.GetService(typeof(DTE));
                return dte?.Version ?? defaultMainVersion;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return defaultMainVersion;
            }
        }

        public static string GetVsSemanticVersion(IServiceProvider serviceProvider)
        {
            try
            {
                var vsAppId = serviceProvider.GetService<IVsAppId>(typeof(SVsAppId));

                vsAppId.GetProperty((int)VSAPropID.VSAPROPID_ProductSemanticVersion, out var semanticVersionObj);

                var semanticVersion = semanticVersionObj as string;
                if (semanticVersion != null)
                    return semanticVersion.Split('+', '-')[0];

                return GetVsMainVersion(serviceProvider);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return GetVsMainVersion(serviceProvider);
            }
        }
    }
}
