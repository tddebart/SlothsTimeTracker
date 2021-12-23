using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace WindowsTimeTracker
{
    public partial class Statistics : Form
    {
        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        static extern int GetWindowText(IntPtr hwnd, StringBuilder ss, int count);
        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        private string processFolder; 
        
        private string processTitleLastFrame;

        private MyProcess[] splitProcessesLastFrame;

        private List<MyProcess> processes = new List<MyProcess>();

        public Statistics()
        {
            InitializeComponent();

            // Set update loop
            Timer tmr = new Timer();
            tmr.Interval = 1000;   // milliseconds
            tmr.Tick += Update;  // set handler
            tmr.Start();
            
            // Set autoSave loop
            Timer tmrAutoSave = new Timer();
            tmrAutoSave.Interval = 300000;   // milliseconds
            tmrAutoSave.Tick += AutoSave;  // set handler
            tmrAutoSave.Start();
            
            // Set process folder
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            processFolder = Path.Combine(localAppData, "SlothsTimeTracker");
            Directory.CreateDirectory(processFolder);
            if (!File.Exists(processFolder + "\\processes.json"))
            {
                using (StreamWriter w = File.AppendText(processFolder + "\\processes.json"))
                {
                    w.Write(JsonSerializer.Serialize(processes));
                }
                
            }
            
            // Load the processes
            using (StreamReader r = File.OpenText(processFolder + "\\processes.json"))
            {
                string json = r.ReadToEnd();
                if (json.Length > 0)
                {
                    processes = JsonSerializer.Deserialize<List<MyProcess>>(json);
                }
            }
            Console.WriteLine("test");

            processes.Sort((p1, p2) => p2.time.CompareTo(p1.time));
            PopulateTree(processes, null);
            
            foreach (var process in processes)
            {
                PopulateParents(process);
            }
        }
        
        private void PopulateTree(List<MyProcess> thisProcesses, TreeNode parentNode)
        {
            foreach (var process in thisProcesses)
            {
                TreeNode parent = null;
                if (parentNode != null)
                {
                    parent = parentNode.Nodes.Add(process.TimeSpent() + " - " + process.name);
                }
                else
                {
                    parent = treeView1.Nodes.Add(process.TimeSpent() + " - " + process.name);
                }
                PopulateTree(process.children, parent);
            }
        }

        public void PopulateParents(MyProcess process)
        {
            foreach (var child in process.children)
            {
                child.parent = process;
                PopulateParents(child);
            }
            process.children.Sort((p1, p2) => p2.time.CompareTo(p1.time));
        }


        private void Update(object sender, EventArgs e)
        {
            var windowTitle = ActiveWindowTitle();
            var processName = GetActiveProcessName();

            label1.Text = windowTitle;
            
            var splitProcesses= windowTitle.Split(new string[] {"–", " - "}, StringSplitOptions.RemoveEmptyEntries).Select(t=> t.Trim()).ToList();
            
            // Filter specific things out
            for (int i = 0; i < splitProcesses.Count; i++)
            {
                if (i != 0)
                {
                    if (splitProcesses[i].IndexOf("youtube", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        splitProcesses.RemoveAt(i);
                    }
                    if (splitProcesses[i].IndexOf(processName, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        splitProcesses.RemoveAt(i);
                    }
                }
            }


            if (processes.Any(p => p.name == processName))
            {
                AddTime(processes.First(p => p.name == processName), splitProcesses);
            }
            else
            {
                var parent = new MyProcess(processName, 0);
                AddToParent(parent, splitProcesses);
                
                processes.Add(parent);
            }

            label2.Text = processes.First(p => p.name == processName).TimeSpent();
            
            
            processTitleLastFrame = processName;
            splitProcessesLastFrame = splitProcesses.Select(p => new MyProcess(p, 0)).ToArray();
        }

        private void AddTime(MyProcess parent, List<string> splitProcesses)
        {
            parent.time++;
            if (splitProcesses.Count == 0)
            {
                return;
            }
            var child = parent.children.FirstOrDefault(p => p.name == splitProcesses[0]);
            if (child == null)
            {
                child = new MyProcess(splitProcesses[0], 0);
                child.parent = parent;
                parent.children.Add(child);
            }
            AddTime(child, splitProcesses.Skip(1).ToList());
            
        }

        private void AddToParent(MyProcess parent, List<string> splitProcesses)
        {
            while (true)
            {
                if (splitProcesses.Count > 0)
                {
                    var child = new MyProcess(splitProcesses[0], 0);
                    child.parent = parent;
                    parent.children.Add(child);
                    parent = child;
                    splitProcesses = splitProcesses.Skip(1).ToList();
                    continue;
                }

                break;
            }
        }

        private string ActiveWindowTitle()
        {
            //Create the variable
            const int nChar = 256;
            StringBuilder ss = new StringBuilder(nChar);
            
            //Run GetForeGroundWindows and get active window informations
            //assign them into handle pointer variable
            IntPtr handle = IntPtr.Zero;
            handle = GetForegroundWindow();

            if (GetWindowText(handle, ss, nChar) > 0) return ss.ToString();
            else return "";
        }
        private string GetActiveProcessName()
        {
            //Create the variable
            uint processID = 0;

            //Run GetForeGroundWindows and get active window informations
            //assign them into handle pointer variable
            IntPtr handle = IntPtr.Zero;
            handle = GetForegroundWindow();

            //Get the process ID of the active window
            GetWindowThreadProcessId(handle, out processID);
            return Process.GetProcessById((int)processID).ProcessName;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Save();
        }

        private void AutoSave(object sender, EventArgs e)
        {
            Save();
        }

        public void Save()
        {
            TextWriter txt = new StreamWriter(processFolder + "\\processes.json", false);  
            txt.Write(JsonSerializer.Serialize(processes));
            txt.Close();
            
            treeView1.Nodes.Clear();
            processes.Sort((p1, p2) => p2.time.CompareTo(p1.time));
            PopulateTree(processes, null);
        }
        
        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            Save();
        }
    }
    
    public class MyProcess
    {
        public string name { get; set; }
        public int time { get; set; }
        public List<MyProcess> children { get; set; }
        public DateTime startTime { get; set; }

        public MyProcess parent;
        
        public MyProcess(string name, int time)
        {
            this.name = name;
            this.time = time;
            children = new List<MyProcess>();
            startTime = DateTime.Now;
        }

        public string TimeSpent()
        {
            var t = TimeSpan.FromSeconds(time);
            return t.ToString(@"hh\:mm\:ss");
        }
        
        
    }
}