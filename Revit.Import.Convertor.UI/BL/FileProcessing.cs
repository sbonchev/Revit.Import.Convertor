using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit.Import.Convertor.UI.Enums;
using System;

//using Revit.Async;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Revit.Import.Convertor.UI.BL
{
    [Transaction(TransactionMode.Manual)]
    public class FileProcessing : IExternalEventHandler
    {

        public EventHandler<ProcessInfo>? OnProcessCompleteEvent { get; set; }

        public EventHandler<int>? OnProcessProgressEvent { get; set; }

        public bool IsAbort { get; set; }

        public FileType FileTypeProc { get; set; }

        public string[]? Paths { get; set; }

        private ProcessInfo? _procInfo;

        public ProcessInfo? ProcessInfoResult => _procInfo;

        private UIDocument? _uidoc;

        private string GetDateToString => DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");

        private ProcessInfo ImportDwg(BackgroundWorker? worker=null)
        {
            string[] dwgPaths = Paths!;
            var fileImpInfo = new ProcessInfo { Result = ProcessResult.None, Info = "" };
            if (_uidoc == null)
            {
                fileImpInfo.Info = $"Import To {FileType.Dwg} Failed - No Active Doc!";
                fileImpInfo.Result = ProcessResult.Failed;
                return fileImpInfo;
            }
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
            int inc = 0;
            try
            {
                var path = $"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}\\TestDwg\\";
                foreach (string dwgPath in dwgPaths)
                {
                    if ((worker != null && worker.CancellationPending) || IsAbort)
                    {
                        fileImpInfo.Info = $"Imported {inc} {FileType.Dwg} file(s) to {FileType.Rvt}!";
                        fileImpInfo.Result = ProcessResult.Cancel;
                        return fileImpInfo;
                    }
                    string fileName = $"{Path.GetFileNameWithoutExtension(dwgPath)}{GetDateToString}";
                    path += $"{fileName}.rvt";
                    Document newDoc = doc.Application.NewProjectDocument(metric);
                    inc++;
                    using (trans = new Transaction(newDoc, $"ImportDwgFile{inc}"))
                    {
                        trans.Start();
                        var currView = newDoc.ActiveView;
                        newDoc.Import(dwgPath, dwgOptions, currView, out elementId);
                        trans.Commit();
                        newDoc.SaveAs(path, saveAsOptions);
                        newDoc.Close();
                    }
                    //Worker?.ReportProgress((inc / dwgPaths.Length) * 100);
                    OnProcessProgress((inc / dwgPaths.Length) * 100);
                    //trans.Commit(); ! REMEMBER: CANNOT PROVIDE NORMAL SAVE !
                }
                var cntInfo = inc > 1 ? "s" : "";
                fileImpInfo.Info = $"Successfully imported {inc} {FileType.Dwg} file{cntInfo} to {FileType.Rvt}!";
                fileImpInfo.Result= ProcessResult.Ok;

                return fileImpInfo;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                trans?.RollBack();
                fileImpInfo.Info = $"Import all {FileType.Dwg} files failed, successed {inc--}!";
                fileImpInfo.Result = ProcessResult.Failed;
                return fileImpInfo;
            }
            finally
            {
                //Worker?.ReportProgress(0); 
            }
        }

        private ProcessInfo ExportingPdf(BackgroundWorker? worker = null)
        {
            string[] rvtPaths = Paths!;
            var fileImpInfo = new ProcessInfo { Result = ProcessResult.None, Info = "" };
            var doc = _uidoc?.Document;
            if (doc == null)
            {
                fileImpInfo.Info = $"Import To {FileType.Dwg} Failed - No Active Doc!";
                fileImpInfo.Result = ProcessResult.Failed;
                return fileImpInfo;
            }
            Transaction? trans = null;
            int inc = 1;
            var path = $"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}\\TestDpf\\";
            var options = new PDFExportOptions { Combine = true };
            try
            {
                foreach (string rvtPath in rvtPaths)
                {
                    if (IsAbort)
                    {
                        fileImpInfo.Info = $"Imported {inc} {FileType.Rvt} file(s) to {FileType.Pdf}!";
                        fileImpInfo.Result = ProcessResult.Cancel;
                        return fileImpInfo;
                    }
                    Document opRvtDoc = doc!.Application.OpenDocumentFile(rvtPath);
                    using (trans = new Transaction(doc, $"ToPdf{inc}"))
                    {
                        trans.Start();
                        List<View> views = new FilteredElementCollector(opRvtDoc)
                            .OfClass(typeof(View))
                            .Cast<View>()
                            .Where(vw => vw.ViewType == ViewType.DrawingSheet && !vw.IsTemplate)
                            .ToList();

                        var viewIds = new List<ElementId>();
                        foreach (View view in views)
                        {
                            var viewSheet = view as ViewSheet;
                            if (viewSheet != null)
                                viewIds.Add(view.Id);
                        }
                        if (views.Count > 0)
                        {
                            options.FileName = $"{Path.GetFileNameWithoutExtension(rvtPath)}{GetDateToString}";
                            opRvtDoc?.Export(path, viewIds, options);
                        }
                        trans.Commit();
                        opRvtDoc?.Close();
                        inc++;
                    }
                }
                fileImpInfo.Info = $"Successfully converted {inc} {FileType.Rvt} file(s) to {FileType.Pdf}!";
                fileImpInfo.Result = ProcessResult.Ok;
                return fileImpInfo;
            }
            catch (Exception ex)
            {
                trans?.RollBack();
                Debug.WriteLine(ex.Message);
                fileImpInfo.Info = $"Import all {FileType.Rvt} files failed, successed {inc}!";
                fileImpInfo.Result = ProcessResult.Failed;
                return fileImpInfo;
            }
        }

        public void Execute(UIApplication app)
        {
            // Get active UI doc:
            _uidoc = app.ActiveUIDocument;
            FileProcess();
        }

        public void FileProcess(BackgroundWorker? worker=null)
        {
            _procInfo = null;
            switch (FileTypeProc)
            {
                case FileType.Dwg:
                    //await Task.Run(() => ImportDwg(Paths!)).ConfigureAwait(false);
                    _procInfo = ImportDwg(worker);
                    break;
                case FileType.Rvt:
                    _procInfo = ExportingPdf(worker);
                    break;
                default:
                    _procInfo = new() { Info = "", Result = ProcessResult.None }; 
                    break;
            }

            if(OnProcessCompleteEvent != null)
                OnProcessCompleteEvent(this, _procInfo);
        }

        private void OnProcessProgress(int progress)
        {
            if (OnProcessProgressEvent != null)
                OnProcessProgressEvent(this, progress);
        }

        //private void DoWork(object? sender, DoWorkEventArgs e)
        //{
        //    //await Task.Run(() => //ProcessDispatcher.Execute(() =>
        //    //{//_handlerExternalEvent.Raise()).ConfigureAwait(false);
        //        switch (FileTypeProc)
        //        {
        //            case FileType.Dwg:
        //                //await Task.Run(() => ImportDwg(Paths!)).ConfigureAwait(false);
        //                ImportDwg(Paths!);
        //                break;
        //            case FileType.Rvt:
        //                ExportingPdf(Paths!);
        //                break;
        //        }
        //    //});
        //}

        public string GetName()
        {
            return $"{Paths} FileProcessing";
        }
    }
}
