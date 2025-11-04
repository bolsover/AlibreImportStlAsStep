using System.Diagnostics;
using System.Windows.Forms;
using AlibreAddOn;
using AlibreExportOpen;
using AlibreX;

namespace AlibreImportStlAsStep
{
    public class AlibreImportOpen : IAlibreAddOn
    {
        private const int MenuIdRoot = 501;
        private const int MenuIdSample = 502;

        private readonly int[] _menuIdsBase =
        {
            MenuIdSample
        };

        private IADRoot _alibreRoot;
        private IntPtr _parentWinHandle;
        private readonly bool _useSvgIcons;

        public AlibreImportOpen(IADRoot alibreRoot, IntPtr parentWinHandle)
        {
            _alibreRoot = alibreRoot;
            _parentWinHandle = parentWinHandle;
            string version = _alibreRoot.Version.Replace("PRODUCTVERSION ", "");
            string[] versionarr = version.Split(',');
            int majorVersion = int.Parse(versionarr[0]);
            _useSvgIcons = majorVersion > 25;
        }

        #region Menus

        /// <summary>
        /// Returns the menu ID of the add-on's root menu item
        /// </summary>
        public int RootMenuItem => MenuIdRoot;


        /// <summary>
        /// Description("Returns Whether the given Menu ID has any sub menus")
        /// </summary>
        /// <param name="menuId"></param>
        /// <returns></returns>
        public bool HasSubMenus(int menuId)
        {
            //   return false;
            return menuId == MenuIdRoot;
        }

        /// <summary>
        /// Returns the ID's of sub menu items under a popup menu item; the menu ID of a 'leaf' menu becomes its command ID
        /// </summary>
        /// <param name="menuId"></param>
        /// <returns></returns>
        public Array SubMenuItems(int menuId)
        {
            return _menuIdsBase;
        }

        /// <summary>
        /// Returns the display name of a menu item; a menu item with text of a single dash (“-“) is a separator
        /// </summary>
        /// <param name="menuId"></param>
        /// <returns></returns>
        public string MenuItemText(int menuId)
        {
            return "Import,Open .stl as .stp";
        }

        /// <summary>
        /// Returns True if input menu item has sub menus // seems odd given name of method
        /// </summary>
        /// <param name="menuId"></param>
        /// <returns></returns>
        public bool PopupMenu(int menuId)
        {
            return true;
        }

        /// <summary>
        /// Returns property bits providing information about the state of a menu item
        /// ADDON_MENU_ENABLED = 1,
        /// ADDON_MENU_GRAYED = 2,
        /// ADDON_MENU_CHECKED = 3,
        /// ADDON_MENU_UNCHECKED = 4,
        /// </summary>
        /// <param name="menuId"></param>
        /// <param name="sessionIdentifier"></param>
        /// <returns></returns>
        public ADDONMenuStates MenuItemState(int menuId, string sessionIdentifier)
        {
            var session = _alibreRoot.Sessions.Item(sessionIdentifier);

            switch (session)
            {
                case IADAssemblySession: return ADDONMenuStates.ADDON_MENU_ENABLED;
                case IADPartSession: return ADDONMenuStates.ADDON_MENU_ENABLED;
            }


            return ADDONMenuStates.ADDON_MENU_GRAYED;
        }

        /// <summary>
        /// Returns a tool tip string if input menu ID is that of a 'leaf' menu item
        /// </summary>
        /// <param name="menuId"></param>
        /// <returns></returns>
        public string MenuItemToolTip(int menuId)
        {
            return "Import,Open .stl as .stp";
        }

        /// <summary>
        /// Returns the icon name (with extension) for a menu item; the icon will be searched under the folder where the add-on's .adc file is present
        /// </summary>
        /// <param name="menuId"></param>
        /// <returns></returns>
        public string MenuIcon(int menuId)
        {
            return _useSvgIcons ? "3DPrint.svg" : "3DPrint.ico";
        }

        /// <summary>
        /// Returns True if AddOn has updated Persistent Data
        /// </summary>
        /// <param name="sessionIdentifier"></param>
        /// <returns></returns>
        public bool HasPersistentDataToSave(string sessionIdentifier)
        {
            return false;
        }

        /// <summary>
        /// Invokes the add-on command identified by menu ID; returning the add-on command interface is optional
        /// </summary>
        /// <param name="menuId"></param>
        /// <param name="sessionIdentifier"></param>
        /// <returns></returns>
        public IAlibreAddOnCommand InvokeCommand(int menuId, string sessionIdentifier)
        {
            var session = _alibreRoot.Sessions.Item(sessionIdentifier);
            return ConvertStlToStepAndImport(session);
        }

        #endregion

        private IAlibreAddOnCommand ConvertStlToStepAndImport(IADSession currentSession)
        {
            var stlFile = ShowOpenStlDialog();
            if (string.IsNullOrWhiteSpace(stlFile))
                return null!;

            var stepFile = ShowSaveStepDialog(stlFile);
            if (string.IsNullOrWhiteSpace(stepFile))
                return null!;

            var exePath = FindStlToStepExecutable();

            try
            {
                var psi = BuildStlToStepProcessStartInfo(exePath, stlFile, stepFile);
                var waitForm = CreateWaitForm(out var cancelButton, "Converting STL to STEP, please wait...");
                var cts = new CancellationTokenSource();
                Process? proc = null;

                AttachCancellationHandler(cancelButton, waitForm, cts, () => proc);

                waitForm.Shown += async (_, __) =>
                {
                    await ExecuteConversionWithProgressAsync(waitForm, psi, stepFile, cts, p => proc = p);
                };

                var result = waitForm.ShowDialog(new WindowWrapper(_parentWinHandle));
                if (HandleWaitFormResult(result, waitForm))
                    _alibreRoot.ImportSTEPFileEx(stepFile, true, true);
                else
                    return null!;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error running stltostp.exe: {ex.Message}", "Error", MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }

            return null!;
        }

        private static string ShowOpenStlDialog()
        {
            var openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Standard Tessellation Language|*.stl";
            openFileDialog.Title = "Select an STL File to convert";
            openFileDialog.Multiselect = false;
            return openFileDialog.ShowDialog() == DialogResult.OK ? openFileDialog.FileName : string.Empty;
        }

        private static string ShowSaveStepDialog(string sourceStlPath)
        {
            var defaultStepPath = Path.ChangeExtension(sourceStlPath, ".stp");
            var saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Step File|*.stp";
            saveFileDialog.Title = "Save a Step File";
            saveFileDialog.FileName = defaultStepPath;
            return saveFileDialog.ShowDialog() == DialogResult.OK ? saveFileDialog.FileName : string.Empty;
        }

     

        private static void AttachCancellationHandler(Button cancelButton, Form waitForm, CancellationTokenSource cts,
            Func<Process?> getProcess)
        {
            cancelButton.Click += (_, __) =>
            {
                try
                {
                    cts.Cancel();
                    var proc = getProcess();
                    if (proc == null || proc.HasExited) return;
                    try
                    {
                        proc.CloseMainWindow();
                    }
                    catch
                    {
                        /* ignore */
                    }

                    try
                    {
                        if (!proc.WaitForExit(1000))
                            proc.Kill();
                    }
                    catch
                    {
                        /* ignore */
                    }
                }
                catch
                {
                    /* ignore */
                }
                finally
                {
                    waitForm.DialogResult = DialogResult.Cancel;
                    waitForm.Close();
                }
            };
        }

        private async Task ExecuteConversionWithProgressAsync(
            Form waitForm,
            ProcessStartInfo psi,
            string stepFilePath,
            CancellationTokenSource cts,
            Action<Process> onProcessStarted)
        {
            try
            {
                var proc = Process.Start(psi);
                if (proc == null)
                    throw new InvalidOperationException("Failed to start stltostp.exe.");
                onProcessStarted(proc);

                var readStdout = Task.Run(() => proc.StandardOutput.ReadToEnd());
                var readStderr = Task.Run(() => proc.StandardError.ReadToEnd());

                while (!proc.HasExited && !cts.IsCancellationRequested)
                    await Task.Delay(100);

                if (cts.IsCancellationRequested)
                {
                    try
                    {
                        if (!proc.HasExited) proc.Kill();
                    }
                    catch
                    {
                        /* ignore */
                    }

                    waitForm.DialogResult = DialogResult.Cancel;
                    waitForm.Close();
                    return;
                }

                var stdout = await readStdout;
                var stderr = await readStderr;

                if (proc.ExitCode != 0)
                    throw new ApplicationException(
                        $"stltostp.exe failed ({proc.ExitCode}).{Environment.NewLine}{stderr}");

                
                waitForm.DialogResult = DialogResult.OK;
              //  _alibreRoot.ImportSTEPFileEx(stepFilePath, true, true);
            }
            catch (Exception ex)
            {
                waitForm.Tag = ex;
                waitForm.DialogResult = DialogResult.Abort;
            }
            finally
            {
                waitForm.Close();
            }
        }

        private bool HandleWaitFormResult(DialogResult result, Form waitForm)
        {
            if (result != DialogResult.Abort || waitForm.Tag is not Exception ex)
            {
                return result != DialogResult.Cancel;
            }

            MessageBox.Show($"Error running stltostp.exe: {ex.Message}", "Error", MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return false;
        }

        private static ProcessStartInfo BuildStlToStepProcessStartInfo(string exePath, string inputStlPath,
            string outputStepPath)
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = $"\"{inputStlPath}\" \"{outputStepPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            return psi;
        }

        private string? FindStlToStepExecutable()
        {
            var baseDir = Globals.InstallPath;

            // Candidate relative locations under the codebase/build output
            string[] candidates =
            {
                Path.Combine(baseDir, "stltostp.exe"),
                Path.Combine(baseDir, "StlToStep", "stltostp.exe"),
                Path.Combine(baseDir, "tools", "stltostp.exe"),
                Path.Combine(baseDir, "..", "StlToStep", "bin", "Release", "stltostp.exe"),
                Path.Combine(baseDir, "..", "StlToStep", "bin", "Debug", "stltostp.exe")
            };

            return candidates.Select(path => Path.GetFullPath(path)).FirstOrDefault(full => File.Exists(full));
        }

        private static Form CreateWaitForm(out Button cancelButton, string message = "Converting, please wait...")
        {
            var f = new Form
            {
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                ControlBox = false,
                MinimizeBox = false,
                MaximizeBox = false,
                ShowInTaskbar = false,
                Width = 380,
                Height = 150,
                Text = "Converting, please wait"
            };

            var lbl = new Label
            {
                Text = message,
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 60,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter
            };

            var pb = new ProgressBar
            {
                Style = ProgressBarStyle.Marquee,
                Dock = DockStyle.Top,
                Height = 22,
                MarqueeAnimationSpeed = 30
            };

            cancelButton = new Button
            {
                Text = "Cancel",
                Dock = DockStyle.Bottom,
                Height = 30
            };

            f.Controls.Add(cancelButton);
            f.Controls.Add(pb);
            f.Controls.Add(lbl);
            return f;
        }


        /// <summary>
        /// Loads Data from AddOn
        /// </summary>
        /// <param name="pCustomData"></param>
        /// <param name="sessionIdentifier"></param>
        public void LoadData(IStream pCustomData, string sessionIdentifier)
        {
        }

        /// <summary>
        /// Saves Data to AddOn
        /// </summary>
        /// <param name="pCustomData"></param>
        /// <param name="sessionIdentifier"></param>
        public void SaveData(IStream pCustomData, string sessionIdentifier)
        {
        }

        /// <summary>
        /// Sets the IsLicensed bit for the tightly coupled Add-on
        /// </summary>
        /// <param name="isLicensed"></param>
        public void setIsAddOnLicensed(bool isLicensed)
        {
        }

        /// <summary>
        /// Returns True if the AddOn needs to use a Dedicated Ribbon Tab
        /// </summary>
        /// <returns></returns>
        public bool UseDedicatedRibbonTab()
        {
            return true;
        }

        private sealed class WindowWrapper : IWin32Window
        {
            private readonly IntPtr _hwnd;

            public WindowWrapper(IntPtr handle)
            {
                _hwnd = handle;
            }

            public IntPtr Handle => _hwnd;
        }
    }
}