// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using AccessibilityInsights.Extensions.Interfaces.IssueReporting;
using AccessibilityInsights.SharedUx.FileBug;
using AccessibilityInsights.SharedUx.Settings;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using AccessibilityInsights.SharedUx.Properties;


namespace AccessibilityInsights.SharedUx.Controls.SettingsTabs
{
    /// <summary>
    /// Interaction logic for ConnectionControl.xaml
    /// </summary>
    public partial class ConnectionControl : UserControl
    {
        public ConnectionControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Represents different states for whether user has connected to server yet
        /// </summary>
        private enum States
        {
            NoServer,       // First screen with "next"
            EditingServer,  // Second screen with treeview
            HasServer       // Third screen with selected team
        };

        /// <summary>
        /// Delegates
        /// </summary>
        public Action UpdateSaveButton { get; set; }
        public Action<Uri, bool, Action> HandleLoginRequest { get; set; }
        public Action<Action> HandleLogoutRequest { get; set; }
        public Action<bool> ShowSaveButton { get; set; }

        IssueConfigurationControl issueConfigurationControl = null;
        IIssueReporting selectedIssueReporter = null;

        #region configuration updating code
        /// <summary>
        /// Initializes the view.
        /// </summary>
        /// <param name="configuration"></param>
        public void UpdateFromConfig()
        {
                InitializeView();
        }

        private RadioButton CreateRadioButton(IIssueReporting reporter)
        {
            RadioButton issueReportingOption = new RadioButton();
            issueReportingOption.Content = reporter.ServiceName;
            issueReportingOption.Tag = reporter.StableIdentifier;
            issueReportingOption.Margin = new Thickness(2, 2, 2, 2);
            issueReportingOption.Checked += IssueReporterOnChecked;
            issueReportingOption.FontSize = 14;
            return issueReportingOption;
        }

        private void IssueReporterOnChecked(object sender, RoutedEventArgs e)
        {
            if (issueConfigurationControl != null)
            {
                issueFilingGrid.Children.Remove(issueConfigurationControl);
                issueConfigurationControl = null;
            }

            Guid clickedOptionTag = (Guid)((RadioButton)sender).Tag;
            if (clickedOptionTag != Guid.Empty)
            {
                IssueReporterManager.GetInstance().GetIssueFilingOptionsDict().TryGetValue(clickedOptionTag, out selectedIssueReporter);
                issueConfigurationControl = selectedIssueReporter?.RetrieveConfigurationControl(this.UpdateSaveButton);
                Grid.SetRow(issueConfigurationControl, 3);
                issueFilingGrid.Children.Add(issueConfigurationControl);
            }
            UpdateSaveButton();
        }

        /// <summary>
        /// Adds the currently selected connection to the configuration so it is persisted
        /// </summary>
        /// <param name="configuration"></param>
        public bool UpdateConfigFromSelections(ConfigurationModel configuration)
        {
            if (issueConfigurationControl != null && (issueConfigurationControl.CanSave || configuration.SelectedIssueReporter != selectedIssueReporter?.StableIdentifier))
            {
                configuration.SelectedIssueReporter = selectedIssueReporter.StableIdentifier;
                string serializedConfigs = configuration.IssueReporterSerializedConfigs;
                Dictionary<Guid, string> configs = string.IsNullOrEmpty(serializedConfigs) ? new Dictionary<Guid, string>() 
                    : JsonConvert.DeserializeObject<Dictionary<Guid, string>>(serializedConfigs);

                configs = configs ?? new Dictionary<Guid, string>();

                if (serializedConfigs != null)
                {
                    configs = JsonConvert.DeserializeObject<Dictionary<Guid, string>>(serializedConfigs);
                }

                string newConfigs = issueConfigurationControl.OnSave();
                configs[selectedIssueReporter.StableIdentifier] = newConfigs;
                configuration.IssueReporterSerializedConfigs = JsonConvert.SerializeObject(configs);
                IssueReporterManager.GetInstance().SetIssueReporter(selectedIssueReporter.StableIdentifier);
                issueConfigurationControl.OnDismiss();
                return true;
            }
            return false;
        }

        /// <summary>
        /// For this control we want SaveAndClose to be enabled if the extension control indicates that something can be saved.
        /// </summary>
        public bool IsConfigurationChanged(ConfigurationModel configuration)
        {
            var changed = configuration.SelectedIssueReporter != selectedIssueReporter?.StableIdentifier;

            return changed || (issueConfigurationControl != null ? issueConfigurationControl.CanSave : false);
        }
        #endregion

        /// <summary>
        /// Inititates the view. Fetches a list of all the available issue reporters and creates a list.
        /// </summary>
        public void InitializeView()
        {
            IReadOnlyDictionary<Guid, IIssueReporting> options = IssueReporterManager.GetInstance().GetIssueFilingOptionsDict();
            availableIssueReporters.Children.Clear();
            Guid selectedGUID = BugReporter.IssueReporting != null ? BugReporter.IssueReporting.StableIdentifier : default(Guid);
            foreach (var reporter in options)
            {
                if (reporter.Key == null || reporter.Value == null) {
                    continue;
                }

                RadioButton rb = CreateRadioButton(reporter.Value);
                if (selectedGUID.Equals(reporter.Key))
                {
                    rb.IsChecked = true;
                    issueConfigurationControl = reporter.Value.RetrieveConfigurationControl(this.UpdateSaveButton);
                    Grid.SetRow(issueConfigurationControl, 3);
                    if (!issueFilingGrid.Children.Contains(issueConfigurationControl))
                    {
                        issueFilingGrid.Children.Add(issueConfigurationControl);
                    }
                }
                availableIssueReporters.Children.Add(rb);
            }

            this.issueFilingGrid.Visibility = Visibility.Visible;
        }
    }
}