﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using AccessibilityInsights.Extensions.Interfaces.IssueReporting;
using Newtonsoft.Json;

namespace AccessibilityInsights.Extensions.GitHub
{
    /// <summary>
    /// Interaction logic for ConfigurationModel.xaml
    /// </summary>
    public partial class ConfigurationModel : IssueConfigurationControl
    {
        public ConnectionConfiguration Config { get; set;}
        public ConfigurationModel()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Called when save button clicked.
        /// </summary>
        /// <returns>The extension’s new configuration, serialized</returns>
        public override string OnSave()
        {
            this.Config.RepoLink = this.tbURL.Text;
            //canSave = false;
            UpdateSaveButton();
            return JsonConvert.SerializeObject(this.Config);
        }

        /// <summary>
        /// Called when settings page dismissed
        /// </summary>
        public override void OnDismiss()
        {
            this.tbURL.Text = this.Config.RepoLink;
            //canSave = false;
            UpdateSaveButton();
        }

        public void TextChangeUpdateSaveButton(object sender, EventArgs e)
        {
            string curURL = this.tbURL.Text;
            if (!string.IsNullOrEmpty(curURL) && ((this.Config !=null && string.IsNullOrEmpty(this.Config.RepoLink)) || !this.Config.Equals(curURL)))
            {
                //canSave = true;
            }
            else
            {
                //canSave = false;
            }
            UpdateSaveButton();
        }

        //private bool canSave = false;
        /// <summary>
        /// Can the save button be clicked
        /// </summary>
        public override bool CanSave => tbURL.Text != Config.RepoLink;


        private Action updateSaveButton;
        public override Action UpdateSaveButton {
            get
            {
                return updateSaveButton;
            }
            set
            {
                updateSaveButton = value;
                this.tbURL.TextChanged += TextChangeUpdateSaveButton;
            }
        }

        #pragma warning disable CA1801 // unused parameter
        private void IssueConfigurationControl_IsVisibleChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        #pragma warning restore CA1801 // unused parameter
        {
            if ((bool)e.NewValue)
            {
                tbURL.Text = Config.RepoLink;
            }
        }
    }
}
