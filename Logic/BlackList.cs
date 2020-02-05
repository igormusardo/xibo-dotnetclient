/**
 * Copyright (C) 2020 Xibo Signage Ltd
 *
 * Xibo - Digital Signage - http://www.xibo.org.uk
 *
 * This file is part of Xibo.
 *
 * Xibo is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * any later version.
 *
 * Xibo is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with Xibo.  If not, see <http://www.gnu.org/licenses/>.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Windows.Forms;
using System.Diagnostics;

namespace XiboClient.Logic
{
    class BlackList
    {
        private static readonly Lazy<BlackList>
            lazy =
            new Lazy<BlackList>
            (() => new BlackList());

        public static BlackList Instance { get { return lazy.Value; } }

        private xmds.xmds xmds1;
        private HardwareKey hardwareKey;

        private string blackListFile;

        public BlackList()
        {
            // Check that the black list file is available
            blackListFile = ApplicationSettings.Default.LibraryPath + @"\" + ApplicationSettings.Default.BlackListLocation;

            // Get the key for this display
            hardwareKey = new HardwareKey();
        }

        /// <summary>
        /// Adds a media item to the Black list. Adds Locally and to the WebService
        /// </summary>
        /// <param name="id">The Media ID</param>
        /// <param name="type">The BlackListType, either All (to blacklist on all displays) or Single (to blacklist only on this display)</param>
        /// <param name="reason">The reason for the blacklist</param>
        public void Add(string id, BlackListType type, string reason)
        {
            // Do some validation
            if (reason == "") reason = "No reason provided";
            
            int mediaId;
            if (!int.TryParse(id, out mediaId))
            {
                System.Diagnostics.Trace.WriteLine(String.Format("Currently can only append Integer media types. Id {0}", id), "BlackList - Add");
            }

            // Send to the webservice
            xmds1 = new XiboClient.xmds.xmds();
            xmds1.BlackListCompleted += new XiboClient.xmds.BlackListCompletedEventHandler(xmds1_BlackListCompleted);

            xmds1.BlackListAsync(ApplicationSettings.Default.ServerKey, hardwareKey.Key, mediaId, type.ToString(), reason, ApplicationSettings.Default.Version);

            // Add to the local list
            AddLocal(id);
        }

        private void xmds1_BlackListCompleted(object sender, XiboClient.xmds.BlackListCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                System.Diagnostics.Trace.WriteLine("Error sending blacklist", "BlackList - BlackListCompleted");
            }
            else
            {
                System.Diagnostics.Trace.WriteLine("Blacklist sending complete", "BlackList - BlackListCompleted");
            }

            return;
        }

        /// <summary>
        /// Adds the Media Items in the XMLNodeList to the Blacklist (will only add these locally)
        /// </summary>
        /// <param name="items">The XMLNodeList containing the blacklist items. Each node must have an "id".</param>
        public void Add(XmlNodeList items)
        {
            Trace.WriteLine(new LogMessage("Blacklist - Add", "Adding XMLNodeList to Blacklist"), LogType.Info.ToString());

            foreach (XmlNode node in items)
            {
                XmlAttributeCollection attributes = node.Attributes;

                if (attributes["id"].Value != null)
                {
                    AddLocal(attributes["id"].Value);
                }
            }
        }

        /// <summary>
        /// Adds the Media ID to the local blacklist
        /// </summary>
        /// <param name="id">The ID to be blacklisted.</param>
        private void AddLocal(string id)
        {
            try
            {
                using (FileStream fileStream = File.Open(blackListFile, FileMode.Append, FileAccess.Write, FileShare.Read))
                {
                    using (StreamWriter tw = new StreamWriter(fileStream, Encoding.UTF8))
                    {
                        tw.Write(String.Format("[{0}],", id));
                        tw.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message, "Blacklist - Add");
                System.Diagnostics.Trace.WriteLine(String.Format("Cant add {0} to the blacklist", id));
            }

            return;
        }

        /// <summary>
        /// Truncates the local Blacklist
        /// </summary>
        public void Truncate()
        {
            try
            {
                File.Delete(ApplicationSettings.Default.LibraryPath + @"\" + ApplicationSettings.Default.BlackListLocation);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("Cannot truncate the BlackList", "Blacklist - Truncate");
                System.Diagnostics.Trace.WriteLine(ex.Message);
            }
        }

        /// <summary>
        /// Checks whether or not a media item is in the blacklist
        /// </summary>
        /// <param name="fileId"></param>
        /// <returns></returns>
        public Boolean BlackListed(string fileId)
        {
            // Store as an XML Fragment
            if (!File.Exists(blackListFile))
            {
                return false;
            }

            try
            {
                // Use an XML Text Reader to grab the shiv from the black list location.
                using (FileStream fileStream = File.Open(blackListFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (StreamReader sr = new StreamReader(fileStream))
                    {
                        string listed = sr.ReadToEnd();
                        
                        return listed.Contains(String.Format("[{0}]", fileId));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message, "BlackList - BlackListed");
            }

            return false;
        }
    }

    public enum BlackListType { Single, All }
}
