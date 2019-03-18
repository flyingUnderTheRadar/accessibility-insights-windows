// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using AccessibilityInsights.Actions;
using AccessibilityInsights.Core.Enums;
using AccessibilityInsights.Desktop.Telemetry;
using AccessibilityInsights.Desktop.Utility;
using AccessibilityInsights.Extensions.Interfaces.BugReporting;
using AccessibilityInsights.Extensions.Interfaces.IssueReporting;
using mshtml;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;

using static System.FormattableString;

namespace AccessibilityInsights.SharedUx.FileBug
{
    /// <summary>
    /// Class with static functions used for filing bugs
    /// </summary>
    public static class FileBugAction
    {
        /// <summary>
        /// Opens bug filing window with prepopulated data
        /// </summary>
        /// <param name="issueInformation">Dictionary of bug info from with which to populate the bug</param>
        /// <param name="connection">connection info</param>
        /// <param name="onTop">Is window always on top</param>
        /// <param name="zoomLevel">Zoom level for bug file window</param>
        /// <param name="updateZoom">Callback to update configuration with zoom level</param>
        /// <returns></returns>
        public static IIssueResult FileIssueAsync(IssueInformation issueInformation)
        {
            if (!BugReporter.IsEnabled)
                return null;

            try
            {
                // Create a A11y-specific Guid for this bug if absent
                var a11yBugId = issueInformation.InternalGuid.HasValue
                    ? issueInformation.InternalGuid.Value.ToString()
                    : string.Empty;

                IIssueResult issueResult = BugReporter.FileIssueAsync(issueInformation);

                if (issueResult.IssueLink != null)
                {
                    if (issueInformation.RuleForTelemetry != null)
                    {
                        Logger.PublishTelemetryEvent(TelemetryAction.Issue_Save, new Dictionary<TelemetryProperty, string>
                        {
                            { TelemetryProperty.RuleId, issueInformation.RuleForTelemetry },
                            { TelemetryProperty.UIFramework, issueInformation.UIFramework ?? string.Empty },
                        });
                    }
                    else // if the bug is coming from the hierarchy tree, it will not have ruleID or UIFramework
                    {
                        Logger.PublishTelemetryEvent(TelemetryAction.Issue_Save);
                    }
                }
                else
                {
                    Logger.PublishTelemetryEvent(TelemetryAction.Issue_File_Attempt);
                }
                return issueResult;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Attaches screenshot and results file to existing issue
        /// Default case - resulting test file will open in A11yFileMode.Inspect mode, 
        ///     no additional loading parameters needed
        /// </summary>
        /// <param name="issueInformation"> Issue information object that needs to be populated with attachments</param>
        /// <param name="ecId">Element context id</param>
        /// <param name="rect">Bounding rect of element for screenshot</param>
        /// <param name="elId">Element unique id</param>         
        /// <returns>Success or failure</returns>
        public static void AttachIssueData(IssueInformation issueInformation, Guid ecId, Rectangle? rect, int? elId)
        {
            // Save snapshot locally in prep for uploading attachment
            var snapshotFileName = GetTempFileName(FileFilters.TestExtension);
            
            // when the file is open, it will be open in Inspect view, not Test view.
            SaveAction.SaveSnapshotZip(snapshotFileName, ecId, elId, Desktop.Settings.A11yFileMode.Inspect);
            issueInformation.Screenshot = GetScreenShotForIssueDescription(ecId, rect);
            issueInformation.TestFileName = snapshotFileName;
        }

        /// <summary>
        /// Returns the path to a newly created temporary directory
        /// </summary>
        /// <returns></returns>
        private static string GetTempDir()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);
            return tempDir;
        }

        /// <summary>
        /// Highlights the given rectangle on a clone of the given data context's
        /// inner bitmap and returns it
        /// 
        /// If the given rectangle is null (might happen if bounding rectangle doesn't exist),
        ///     then the original bitmap is returned
        /// </summary>
        /// <param name="dc"></param>
        /// <param name="rect"></param>
        /// <returns></returns>
        private static Bitmap GetScreenShotForIssueDescription(Guid ecId, Rectangle? rect)
        {
            var dc = GetDataAction.GetElementDataContext(ecId);
            if (dc.Screenshot != null)
            {
                Bitmap newImg = new Bitmap(dc.Screenshot);

                if (rect.HasValue)
                {
                    Rectangle valueRect = rect.Value;
                    using (var graphics = Graphics.FromImage(newImg))
                    {
                        // Use element 
                        var el = GetDataAction.GetA11yElementInDataContext(ecId, dc.ScreenshotElementId);
                        var outerRect = el.BoundingRectangle;

                        valueRect.X = valueRect.X - outerRect.X;
                        valueRect.Y = valueRect.Y - outerRect.Y;
                        graphics.DrawRectangle(new Pen(Color.Red, 5), valueRect);
                    }
                }
                return newImg;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Remove internal text from repro step. 
        /// since the internal text is wrapped in a "div", find a div with matching key text and remove the child nodes of div. 
        /// generally, keyText is guid value. 
        /// </summary>
        /// <param name="inputHTML"></param>
        /// <param name="keyText"></param>
        /// <returns></returns>
        internal static string RemoveInternalHTML(string inputHTML, string keyText)
        {
            object[] htmlText = { inputHTML };
            HTMLDocument doc = new HTMLDocument();
            IHTMLDocument2 doc2 = doc as IHTMLDocument2;
            doc2.write(htmlText);
            IHTMLDOMNode node = null;

            // search div with matching bugguid. 
            // remove any of it if there is matched one. 
            var divnodes = doc.getElementsByTagName("div");

            if (divnodes != null)
            {
                foreach (IHTMLDOMNode divnode in divnodes)
                {
                    foreach (IHTMLDOMNode child in divnode.childNodes)
                    {
                        string nodevalue = child.nodeValue?.ToString();
                        if (nodevalue != null && nodevalue.Contains(keyText) == true)
                        {
                            node = divnode;
                            break;
                        }
                    }

                    if (node != null) break;
                }
            }

            if (node != null)
            {
                foreach (var n in node.childNodes)
                {
                    node.removeChild(n);
                }
            }

            return doc.body.outerHTML;
        }

        /// <summary>
        /// Returns true if the target guid is found in the given reprosteps string
        /// </summary>
        /// <param name="targetGuid"></param>
        /// <param name="reproSteps"></param>
        /// <returns></returns>
        private static bool GuidsMatchInReproSteps(string targetGuid, string reproSteps)
        {
            int filedBugIdIndex = reproSteps.IndexOf(targetGuid, StringComparison.Ordinal);
            return filedBugIdIndex >= 0;
        }

        /// <summary>
        /// Load the bug filing web browser in a blocking window and return the bug number (null or id)
        /// Change the configuration zoom level for the embedded browser
        /// </summary>
        /// <param name="url"></param>
        private static int? FileBugWindow(Uri url, bool onTop, int zoomLevel, Action<int> updateZoom)
        {
            System.Diagnostics.Trace.WriteLine(Invariant($"Url is {url.AbsoluteUri.Length} long: {url}"));
            // To force latest IE to show up
            var appName = System.Diagnostics.Process.GetCurrentProcess().ProcessName + ".exe";
            using (var Key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION", true))
                Key.SetValue(appName, 99999, Microsoft.Win32.RegistryValueKind.DWord);

            var dlg = new BugFileForm(url, onTop, zoomLevel, updateZoom);
            dlg.ScriptToRun = "window.onerror = function(msg,url,line) { window.external.Log(msg); return true; };";

            dlg.ShowDialog();

            return dlg.BugId;
        }

        /// <summary>
        /// Creates a temp file with the given extension and returns its path
        /// </summary>
        /// <param name="extension"></param>
        /// <returns></returns>
        private static string GetTempFileName(string extension)
        {
            return Path.Combine(GetTempDir(), Path.GetRandomFileName() + extension);
        }
    }
}
