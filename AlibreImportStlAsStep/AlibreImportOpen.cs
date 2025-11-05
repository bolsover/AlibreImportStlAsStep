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
            // Build a combined form that lets the user pick input/output, tolerance, and options,
            // with a progress bar and cancel while converting.
            var form = new Form
            {
                Text = "STL → STEP Converter",
                StartPosition = FormStartPosition.Manual,
                Location = new System.Drawing.Point(300, 300),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MinimizeBox = false,
                MaximizeBox = false,
                ShowInTaskbar = false,
                Width = 600,
                Height = 260
            };

            // Layout: simple table
            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 6,
                Padding = new Padding(10),
                AutoSize = true
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));

            // Prepare output textbox first so input browse handler can reference it
            var txtOut = new TextBox { ReadOnly = true, Anchor = AnchorStyles.Left | AnchorStyles.Right };

            // Input STL
            var lblIn = new Label { Text = "Input STL:", AutoSize = true, Anchor = AnchorStyles.Left };
            var txtIn = new TextBox { ReadOnly = true, Anchor = AnchorStyles.Left | AnchorStyles.Right };
            var btnIn = new Button { Text = "Browse...", Width = 80, Anchor = AnchorStyles.Right };
            btnIn.Click += (_, __) =>
            {
                var dlg = new OpenFileDialog { Filter = "Standard Tessellation Language|*.stl", Title = "Select an STL file" };
                if (dlg.ShowDialog(new WindowWrapper(_parentWinHandle)) == DialogResult.OK)
                {
                    txtIn.Text = dlg.FileName;
                    if (string.IsNullOrWhiteSpace(txtOut.Text))
                        txtOut.Text = Path.ChangeExtension(dlg.FileName, ".stp");
                }
            };

            // Output STEP
            var lblOut = new Label { Text = "Output STEP:", AutoSize = true, Anchor = AnchorStyles.Left };
            var btnOut = new Button { Text = "Browse...", Width = 80, Anchor = AnchorStyles.Right };
            btnOut.Click += (_, __) =>
            {
                var dlg = new SaveFileDialog { Filter = "STEP file|*.stp;*.step", Title = "Save STEP file" };
                if (!string.IsNullOrWhiteSpace(txtIn.Text))
                    dlg.FileName = Path.ChangeExtension(txtIn.Text, ".stp");
                if (dlg.ShowDialog(new WindowWrapper(_parentWinHandle)) == DialogResult.OK)
                    txtOut.Text = dlg.FileName;
            };

            // Tolerance spinner
            var lblTol = new Label { Text = "Tolerance:", AutoSize = true, Anchor = AnchorStyles.Left };
            var nudTol = new NumericUpDown
            {
                DecimalPlaces = 7,
                Minimum = 0.0000001M,
                Maximum = 0.1M,
                Increment = 0.0001M,
                Value = 0.0001M,
                Anchor = AnchorStyles.Left
            };
            var lblTolHint = new Label { Text = "(0.1 … 0.0000001)", AutoSize = true, Anchor = AnchorStyles.Left };

            // Checkbox
            var chkOpen = new CheckBox { Text = "Open converted file in Alibre", Checked = true, AutoSize = true, Anchor = AnchorStyles.Left };

            // Progress bar
            var pb = new ProgressBar { Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 30, Dock = DockStyle.Fill, Visible = false };

            // Buttons
            var flowButtons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, AutoSize = true };
            var btnConvert = new Button { Text = "Convert", Width = 100 };
            var btnCancel = new Button { Text = "Cancel", Width = 100 };
            flowButtons.Controls.Add(btnConvert);
            flowButtons.Controls.Add(btnCancel);

            // Add to table
            table.Controls.Add(lblIn, 0, 0);
            table.Controls.Add(txtIn, 1, 0);
            table.Controls.Add(btnIn, 2, 0);

            table.Controls.Add(lblOut, 0, 1);
            table.Controls.Add(txtOut, 1, 1);
            table.Controls.Add(btnOut, 2, 1);

            table.Controls.Add(lblTol, 0, 2);
            table.Controls.Add(nudTol, 1, 2);
            table.Controls.Add(lblTolHint, 2, 2);

            table.Controls.Add(chkOpen, 1, 3);

            table.Controls.Add(pb, 0, 4);
            table.SetColumnSpan(pb, 3);

            table.Controls.Add(flowButtons, 0, 5);
            table.SetColumnSpan(flowButtons, 3);

            form.Controls.Add(table);

            string stlFile = string.Empty;
            string stepFile = string.Empty;
            bool openInAlibre = true;

            var cts = new CancellationTokenSource();
            Process? proc = null;

            // Wire cancel to close or to cancel running process
            AttachCancellationHandler(btnCancel, form, cts, () => proc);

            btnConvert.Click += async (_, __) =>
            {
                stlFile = txtIn.Text.Trim();
                stepFile = txtOut.Text.Trim();
                openInAlibre = chkOpen.Checked;
                if (string.IsNullOrWhiteSpace(stlFile) || !File.Exists(stlFile))
                {
                    MessageBox.Show(form, "Please select a valid input STL file.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (string.IsNullOrWhiteSpace(stepFile))
                {
                    MessageBox.Show(form, "Please select an output STEP file.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var exePath = FindStlToStepExecutable();
                if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
                {
                    MessageBox.Show(form, "stltostp.exe not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Disable inputs during conversion
                foreach (Control c in table.Controls) c.Enabled = false;
                pb.Visible = true;

                try
                {
                    var tol = (double)nudTol.Value;
                    var psi = BuildStlToStepProcessStartInfo(exePath!, stlFile, stepFile, tol);
                    await ExecuteConversionWithProgressAsync(form, psi, stepFile, cts, p => proc = p);
                }
                catch (Exception ex)
                {
                    form.Tag = ex;
                    form.DialogResult = DialogResult.Abort;
                    form.Close();
                }
            };

            var result = form.ShowDialog(new WindowWrapper(_parentWinHandle));
            if (!HandleWaitFormResult(result, form))
                return null!;

            // Success
            if (openInAlibre)
                _alibreRoot.ImportSTEPFileEx(stepFile, true, true);
            else
                MessageBox.Show(new WindowWrapper(_parentWinHandle), "conversion complete", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);

            return null!;
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
            string outputStepPath, double tolerance)
        {
            var tol = tolerance;
            if (tol < 0.0000001) tol = 0.0000001;
            if (tol > 0.1) tol = 0.1;
            var tolStr = tol.ToString(System.Globalization.CultureInfo.InvariantCulture);

            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = $"\"{inputStlPath}\" \"{outputStepPath}\" tol {tolStr}",
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