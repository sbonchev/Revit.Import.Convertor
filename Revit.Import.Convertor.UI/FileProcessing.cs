using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit.Async;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using RVDB = Autodesk.Revit.DB;

namespace Revit.Import.Convertor.UI
{
    [Transaction(TransactionMode.Manual)]
    public class FileProcessing : IExternalEventHandler
    {
        public FileType FileTypeProc { get; set; }

        public string[]? Paths { get; set; }

        public BackgroundWorker? Worker { get; set; }

        public string GetFileImpInfo => _fileImpInfo;

        private UIDocument? _uidoc;

        private string _fileImpInfo = "";

        private void ImportDwg(string[] dwgPaths)
        {
            if (_uidoc == null)
                return;

            var dwgOptions = new DWGImportOptions
            {
                Unit = ImportUnit.Meter,
                ColorMode = ImportColorMode.Preserved,
                Placement = ImportPlacement.Origin,
                ThisViewOnly = false,
            };
            var metric = UnitSystem.Metric;
            var saveAsOptions = new SaveAsOptions { OverwriteExistingFile = true };
            var doc = _uidoc.Document;
            ElementId elementId;
            Transaction? trans = null;
            int inc = 1;
            try
            {
                var path = $"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}\\TestDwg\\";
                foreach (string dwgPath in dwgPaths)
                {
                    if (Worker != null && Worker.CancellationPending)
                    {
                        _fileImpInfo = $"Imported {inc} {FileType.Dwg} file(s) to {FileType.Rvt}!";
                        return;
                    }
                    //Worker?.ReportProgress((inc / dwgPaths.Length) * 100);
                    string fileName = $"{Path.GetFileNameWithoutExtension(dwgPath)}{DateTime.UtcNow.ToString("yyyyMMddHHmmssfff")}";
                    path += $"{fileName}.rvt";
                    Document newDoc = doc.Application.NewProjectDocument(metric);
                    using (trans = new Transaction(newDoc, $"ImportDwgFile{inc}"))
                    {
                        trans.Start();
                        var currView = newDoc.ActiveView;
                        newDoc.Import(dwgPath, dwgOptions, currView, out elementId);
                        trans.Commit();
                        newDoc.SaveAs(path, saveAsOptions);
                        newDoc.Close();
                    }
                    inc++;
                    //trans.Commit(); ! REMEMBER: CANNOT PROVIDE NORMAL SAVE !
                }
                _fileImpInfo = $"Successfully imported {inc} {FileType.Dwg} file(s) to {FileType.Rvt}!";
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                _fileImpInfo = $"Import all {FileType.Dwg} files failed, successed {inc}!";
                trans?.RollBack();
            }
            finally
            {
                //Worker?.ReportProgress(0); 
            }
        }

        private bool ExportingPdf(string[] rvtPaths)
        {
            var doc = _uidoc?.Document;
            if (doc == null)
                return false;

            Transaction? trans = null;
            int inc = 1;
            var path = $"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}\\TestDpf\\";
            var options = new PDFExportOptions { Combine = true };
            try
            {
                foreach (string rvtPath in rvtPaths)
                {
                    Document opRvtDoc = doc!.Application.OpenDocumentFile(rvtPath);
                    using (trans = new Transaction(doc, $"ToPdf{inc}"))
                    {
                        trans.Start();
                        List<RVDB.View> views = new FilteredElementCollector(opRvtDoc)
                            .OfClass(typeof(RVDB.View))
                            .Cast<RVDB.View>()
                            //.Where(vw => vw.ViewType == ViewType.DrawingSheet && !vw.IsTemplate)
                            .ToList();

                        var viewIds = new List<ElementId>();
                        foreach (RVDB.View view in views)
                        {
                            //var viewSheet = view as ViewSheet;
                            //if (viewSheet != null)
                                viewIds.Add(view.Id);
                        }
                        //if (views.Count > 0)
                        //{
                        //var vw = new View();
                            options.FileName = $"{Path.GetFileNameWithoutExtension(rvtPath)}" +
                                               $"{DateTime.UtcNow.ToString("yyyyMMddHHmmssfff")}";
                            opRvtDoc?.Export(path, viewIds, options);
                        //}
                        trans.Commit();
                        opRvtDoc?.Close();
                        inc++;
                    }
                    _fileImpInfo = $"Successfully converted {inc} {FileType.Rvt} file(s) to {FileType.Pdf}!";
                }
            }
            catch (Exception ex)
            {
                trans?.RollBack();
                Debug.WriteLine(ex.Message);
               _fileImpInfo = $"Import all {FileType.Rvt} files failed, successed {inc}!";
            }

            return true;
        }

        public void Execute(UIApplication app)
        {
            if (Paths == null || Paths.Length < 1)
                return;

            // Get active UI doc:
            _uidoc = app.ActiveUIDocument;
            switch (FileTypeProc) 
            {
                case FileType.Dwg:
                    //RevitTask.Initialize(app);
                    //await Task.Run(async () => await RevitTask.RunAsync(() => ImportDwg(Paths!))).ConfigureAwait(false);
                    ImportDwg(Paths!);
                    break;
                case FileType.Rvt:
                    ExportingPdf(Paths!);
                    break;
            }
        }

        public string GetName()
        {
            return $"{Paths} FileProcessing";
        }
    }
}
