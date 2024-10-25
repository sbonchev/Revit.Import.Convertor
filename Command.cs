using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit.Import.Convertor.UI;
using Revit.Import.Convertor.UI.BL;

namespace Revit.Import.Convertor.App
{

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            //new FormatConvertorWindow(uidoc); Calling UI Doc via ctor
            //RevitTask.Initialize(uidoc.Application);
            new FormatConvertorWindow(new FileProcessing());

            return Result.Succeeded;
        }   
    }


}
