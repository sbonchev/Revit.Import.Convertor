using Microsoft.Win32;
using System.ComponentModel;
using IO = System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Autodesk.Revit.UI;
using System.Windows.Media;


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
        #pragma warning restore CS8618

        public FormatConvertorWindow(FileProcessing fileProcessing) 
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

            _handlerExternalEvent = ExternalEvent.Create(fileProcessing);
            _fileProc = fileProcessing;

            //ShowDialog(); // --- not a good choice for such issue!
            Show();

            lblInfoAll.Foreground = lblInfo.Foreground;
            lblInfoAll.Text = "Select proper processing .dwg or .rvt file(s)!";
        }

        private readonly ExternalEvent _handlerExternalEvent;
        private readonly FileProcessing _fileProc;

        //public FormatConvertorWindow(UIDocument uidoc)
        //{
        //    InitializeComponent();

        //    _worker = new BackgroundWorker { WorkerSupportsCancellation = true, WorkerReportsProgress = true };
        //    _worker.DoWork += DoWork;
        //    _worker.ProgressChanged += ProgressChanged;
        //    _worker.RunWorkerCompleted += WorkerCompleted;
        //    btnToRvt.IsEnabled = false;
        //    btnToPdf.IsEnabled = false;

        //    _selectedDwgPaths = new ();
        //    _selectedRvtPaths = new ();

        //    _fileProc = new FileProcessing(uidoc);

        //    ShowDialog();
        //}

        private readonly BackgroundWorker _worker;

        private readonly List<string> _selectedDwgPaths;

        private readonly List<string> _selectedRvtPaths;

        private void ToRvtClick(object sender, RoutedEventArgs e)
        {
            _fileProc.FileTypeProc = FileType.Dwg;
            _fileProc.Paths = _selectedDwgPaths.ToArray();
            _worker.RunWorkerAsync();
        }

        private void ToPdfClick(object sender, RoutedEventArgs e)
        {
            _fileProc.FileTypeProc = FileType.Pdf;
            _handlerExternalEvent.Raise();
            //_fileProc.Paths = _selected.ToArray();
            _worker.RunWorkerAsync();
        }

        private void DoWork(object? sender, DoWorkEventArgs e)
        {
            _handlerExternalEvent.Raise();
        }

        private void Cancel(object? sender)
        {
            if (_worker.IsBusy)
                _worker.CancelAsync();
        }

        private void ProgressChanged(object? sender, ProgressChangedEventArgs e)
        {
            //GetInfo.ProgressInfo = e.ProgressPercentage;
            _worker.ReportProgress(50);
        }

        private void WorkerCompleted(object? sender, RunWorkerCompletedEventArgs e)
        {
            var infoText = _fileProc.GetFileImpInfo;
            if (e.Cancelled) {
                lblInfoAll.Foreground = new SolidColorBrush(Colors.DarkRed);
                lblInfoAll.Text = $"Processing Canceled: {infoText}";
            }
            else if (e.Error!= null)
            {
                lblInfoAll.Foreground = new SolidColorBrush(Colors.DarkRed);
                lblInfoAll.Text = $"Processing Error: {infoText}";
            }
            else {
                lblInfoAll.Foreground = new SolidColorBrush(Colors.DarkGreen);
                lblInfoAll.Text = infoText;
            }
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
            if(_selectedDwgPaths.Count > 0) _selectedDwgPaths.Clear();
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

        

    }
}