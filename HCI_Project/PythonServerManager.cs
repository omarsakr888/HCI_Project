using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace HCI_Project
{
    public static class PythonServerManager
    {
        private static Process _serverProcess;
        private static readonly object _lock = new object();
        private static bool _exitHandlerRegistered;

        public static void StartIfNeeded()
        {
            lock (_lock)
            {
                if (_serverProcess != null && !_serverProcess.HasExited)
                    return;

                if (!_exitHandlerRegistered)
                {
                    Application.ApplicationExit += OnApplicationExit;
                    _exitHandlerRegistered = true;
                }

                try
                {
                    string startupPath = Application.StartupPath;
                    string script = ResolveFaceServerScriptPath(startupPath);
                    if (script == null)
                    {
                        Debug.WriteLine("[PythonServerManager] face_server.py not found.");
                        return;
                    }

                    string scriptDir = Path.GetDirectoryName(script);
                    string venvPython = Path.Combine(scriptDir, ".venv", "Scripts", "python.exe");
                    string pythonExe = File.Exists(venvPython) ? venvPython : "python";

                    var psi = new ProcessStartInfo
                    {
                        FileName = pythonExe,
                        Arguments = "\"" + script + "\" 5000",
                        WorkingDirectory = scriptDir,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    _serverProcess = new Process { StartInfo = psi };
                    _serverProcess.OutputDataReceived += (s, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                            Debug.WriteLine("[face_server] " + e.Data);
                    };
                    _serverProcess.ErrorDataReceived += (s, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                            Debug.WriteLine("[face_server ERR] " + e.Data);
                    };

                    _serverProcess.Start();
                    _serverProcess.BeginOutputReadLine();
                    _serverProcess.BeginErrorReadLine();

                    Debug.WriteLine("[PythonServerManager] Server started. PID: " + _serverProcess.Id);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[PythonServerManager] Launch error: " + ex.Message);
                }
            }
        }

        private static void OnApplicationExit(object sender, EventArgs e)
        {
            lock (_lock)
            {
                try
                {
                    if (_serverProcess != null && !_serverProcess.HasExited)
                    {
                        _serverProcess.Kill();
                        _serverProcess.WaitForExit(2000);
                        Debug.WriteLine("[PythonServerManager] Server killed.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[PythonServerManager] Kill error: " + ex.Message);
                }
            }
        }

        private static string ResolveFaceServerScriptPath(string startupPath)
        {
            string current = startupPath;
            for (int i = 0; i < 10 && !string.IsNullOrEmpty(current); i++)
            {
                string candidate = Path.Combine(current, "face_recognition", "face_server.py");
                Debug.WriteLine("[PythonServerManager] probing: " + candidate);
                if (File.Exists(candidate))
                    return candidate;

                var parent = Directory.GetParent(current);
                current = parent?.FullName;
            }
            return null;
        }
    }
}