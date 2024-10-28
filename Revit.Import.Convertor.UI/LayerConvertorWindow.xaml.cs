using Microsoft.Win32;
using System.ComponentModel;
using IO = System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Autodesk.Revit.UI;
using System.Windows.Media;
using Revit.Import.Convertor.UI.Enums;
using Revit.Import.Convertor.UI.BL;


namespace Revit.Import.Convertor.UI
{
    /// <summary>
    /// File processing logic for MainWindow.xaml
    /// </summary>
    public partial class FormatConvertorWindow : Window
    {
#pragma warning disable CS8618
        /// <summary>
        /// This ctor needs only for xaml view rendering!
        /// </summary>
        public FormatConvertorWindow()
        { }

        public FormatConvertorWindow(FileProcessing fileProcessing, ExternalEvent extEvent)
        {
            if (MyApp.AppIsRun)
                return;

            Init(fileProcessing, extEvent);
        }

        public FormatConvertorWindow(FileProcessing fileProcessing)
        {
            if (MyApp.AppIsRun)
                return;

            Init(fileProcessing, ExternalEvent.Create(fileProcessing));
        }

#pragma warning restore CS8618

        private ExternalEvent _handlerExternalEvent;

        private FileProcessing _fileProc;

        private BackgroundWorker _worker;

        private List<string> _selectedDwgPaths;

        private List<string> _selectedRvtPaths;

        private void Init(FileProcessing fileProcessing, ExternalEvent extEvent)
        {
            InitializeComponent();

            _worker = new BackgroundWorker { WorkerSupportsCancellation = true, WorkerReportsProgress = true };
            _worker.DoWork += DoWork;
            _worker.ProgressChanged += ProgressChanged;
            _worker.RunWorkerCompleted += WorkerCompleted;
            btnToRvt.IsEnabled = false;
            btnToPdf.IsEnabled = false;

            _selectedDwgPaths = new();
            _selectedRvtPaths = new();

            Show(); // --- ShowDialog() not a good choice for such issue but a form with similar behavior!

            lblInfoAll.Foreground = lblInfo.Foreground;
            lblInfoAll.Text = "Select proper processing .dwg or .rvt file(s)!";
            //btnCncel.IsEnabled = false;

            MyApp.AppIsRun = true;
            MyApp.AppWindow = this;
            Closed += (o,e) => MyApp.AppIsRun = false; // --- CANNOT RUN MORE THAN ONE APP INSTANCE!

            _handlerExternalEvent = extEvent;
            _fileProc = fileProcessing;
            _fileProc.OnProcessCompleteEvent += (o, e) =>  OnProcessCompleteEvent(o!,e); //new EventHandler<ProcessInfo>(OnProcessCompleteEvent!);
            _fileProc.OnProcessProgressEvent += (o, e) => OnProcessProgressEvent(o!, e);
        }

        private void ToRvtClick(object sender, RoutedEventArgs e)
        {
            SetButtonsState(false);
            _fileProc.FileTypeProc = FileType.Dwg;
            _fileProc.Paths = _selectedDwgPaths.ToArray();

            //_worker.RunWorkerAsync();
            _handlerExternalEvent.Raise();
            //while (_fileProc.ProcessInfoResult == null) { }; // --- Complete Process Waiting
        }

        private void ToPdfClick(object sender, RoutedEventArgs e)
        {
             SetButtonsState(false);
            _fileProc.FileTypeProc = FileType.Rvt;
            _fileProc.Paths = _selectedRvtPaths.ToArray();

            //_worker.RunWorkerAsync();
            _handlerExternalEvent.Raise();
            //while (_fileProc.ProcessInfoResult == null) { }; // --- Complete Process Waiting
        }

        void OnProcessCompleteEvent(object sender, ProcessInfo info)
        {
            SetInfo(info);
        }

        void OnProcessProgressEvent(object sender, int prog)
        {
            ProcessDispatcher.Execute(() => { prgBar.Value = prog; });
            Application.Current.Dispatcher.Invoke(() => prgBar.Value = prog);
            //prgBar.Value = prog;
        }

        private void DoWork(object? sender, DoWorkEventArgs e)
        {
            _fileProc.FileProcess(_worker);
            ProcessDispatcher.Execute(() => e.Result = _fileProc.ProcessInfoResult);
            var info = e.Result as ProcessInfo;
            while (info?.Result != ProcessResult.None) // ON TRANS START: 'The managed object is not valid'
            { }
        }

        private void OnCncelClick(object sender, RoutedEventArgs e)
        {
            if (_worker.IsBusy)
                _worker.CancelAsync();

            _fileProc.IsAbort = true;
        }

        private void ProgressChanged(object? sender, ProgressChangedEventArgs e)
        {
            prgBar.Value = e.ProgressPercentage;
        }

        private void WorkerCompleted(object? sender, RunWorkerCompletedEventArgs e)
        {
            var info = e.Result as ProcessInfo;
            if (e.Cancelled) {
                lblInfoAll.Foreground = new SolidColorBrush(Colors.DarkRed);
                lblInfoAll.Text = $"Processing Canceled: {info?.Info}";
            }
            else if (e.Error != null)
            {
                lblInfoAll.Foreground = new SolidColorBrush(Colors.DarkRed);
                lblInfoAll.Text = $"Processing Error: {info?.Info}";
            }
            else {
                lblInfoAll.Foreground = new SolidColorBrush(Colors.DarkGreen);
                lblInfoAll.Text = $"Successfully Processed: {info?.Info}";
            }
            SetButtonsState(true);
        }

        private void OpenFile(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "Drawing files (*.dwg)|*.dwg|(*.rvt)|*.rvt|All files (*.*)|*.*",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };
            try
            {
                if (openFileDialog.ShowDialog() == true)
                {
                    Mouse.OverrideCursor = Cursors.Wait;
                    var paths = openFileDialog.FileNames;
                    if (paths.Count() > 0)
                    {
                        if (lbFiles.Items.Count > 0)
                            lbFiles.Items.Clear();

                        foreach (var path in paths)
                            lbFiles.Items.Add(path);
                    }
                }
                var totalCount = lbFiles.Items.Count;
                lblInfo.Foreground = new SolidColorBrush(Colors.DarkGray);
                if (totalCount > 0)
                    lblInfo.Text = $" {totalCount} files";

                lblInfoAll.Text = "Select proper processing .dwg or .rvt file(s)!";
            }
            finally
            {
                Mouse.OverrideCursor = Cursors.Arrow;
            }
        }

        private void FilesSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedPaths = lbFiles.SelectedItems;
            if (_selectedDwgPaths.Count > 0) _selectedDwgPaths.Clear();
            if (_selectedRvtPaths.Count > 0) _selectedRvtPaths.Clear();
            foreach (var path in selectedPaths)
            {
                var strPath = path.ToString();
                string ext = IO.Path.GetExtension(strPath!);
                if (ext.ToUpper() == ".DWG")
                    _selectedDwgPaths.Add(strPath!);
                if (ext.ToUpper() == ".RVT")
                    _selectedRvtPaths.Add(strPath!);
            }
            btnToRvt.IsEnabled = _selectedDwgPaths.Count > 0;
            btnToPdf.IsEnabled = _selectedRvtPaths.Count > 0;

            lblInfoAll.Foreground = new SolidColorBrush(Colors.DarkBlue);
            lblInfoAll.Text = $"{_selectedDwgPaths.Count} {FileType.Dwg} and {_selectedRvtPaths.Count} {FileType.Rvt} file(s) has been selected!";
        }

        private void SetInfo(ProcessInfo info)
        {
            if (info == null)
                return;

            if (info.Result == ProcessResult.Cancel)
            {
                lblInfoAll.Foreground = new SolidColorBrush(Colors.DarkRed);
                lblInfoAll.Text = $"Canceled: {info?.Info}";
            }
            else if (info.Result == ProcessResult.Failed)
            {
                lblInfoAll.Foreground = new SolidColorBrush(Colors.DarkRed);
                lblInfoAll.Text = $"Error: {info?.Info}";
            }
            else if (info.Result == ProcessResult.None)
            {
                lblInfoAll.Foreground = new SolidColorBrush(Colors.DarkRed);
                lblInfoAll.Text = "Operation Failed";
            }
            else
            {
                lblInfoAll.Foreground = new SolidColorBrush(Colors.DarkGreen);
                lblInfoAll.Text = $"Processed: {info?.Info}";
            }

            SetButtonsState(true);
            ProcessDispatcher.Execute(() => { prgBar.Value = 0; });
            _fileProc.IsAbort = false;

        }

        private void SetButtonsState(bool isEnabled)
        {
            btnOpenFile.IsEnabled = isEnabled;
            btnToPdf.IsEnabled = isEnabled;
            btnToRvt.IsEnabled = isEnabled;
            //btnCncel.IsEnabled = !isEnabled;
            if(isEnabled)
            {
                btnToRvt.IsEnabled = _selectedDwgPaths.Count > 0;
                btnToPdf.IsEnabled = _selectedRvtPaths.Count > 0;
            }
        }

    }

    public static class MyApp
    {
        public static bool AppIsRun = false;
        public static FormatConvertorWindow? AppWindow { get; set; }
    }
}