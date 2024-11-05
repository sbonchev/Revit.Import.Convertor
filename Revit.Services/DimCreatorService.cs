using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Revit.Services.Enums;
using System.Diagnostics;

namespace Revit.Services
{
    public class DimCreatorService 
    {
        public Result Execute(UIApplication uiapp)
        {
            var doc = uiapp.ActiveUIDocument.Document;
            try
            {
                Reference pickRef = uiapp.ActiveUIDocument.Selection.PickObject(ObjectType.Element, "Select_Object!");
                Element selectedElem = doc.GetElement(pickRef);
                Wall? selectedWall = selectedElem as Wall;
                if (selectedWall == null)
                    return Result.Failed;

                var refArray = new ReferenceArray();
                Reference? r1, r2;
                Face? wallFace = GetFace(selectedWall, selectedWall.Orientation);
                if (wallFace != null && !wallFace.EdgeLoops.IsEmpty)
                {
                    var edgeArrays = wallFace.EdgeLoops;
                    EdgeArray edges = edgeArrays.get_Item(0);
                    var edgeList = new List<Edge>();
                    foreach (Edge edge in edges)
                    {
                        Line? line = edge.AsCurve() as Line;
                        if (line != null && IsLineVertical(line))
                            edgeList.Add(edge);
                    }
                    var sortedEdges = edgeList.OrderByDescending(e => e.AsCurve().Length).ToList();
                    r1 = sortedEdges[0].Reference;
                    r2 = sortedEdges[1].Reference;
                    refArray.Append(r1);
                    List<BuiltInCategory> catList = new() { BuiltInCategory.OST_Windows, BuiltInCategory.OST_Doors };
                    var wallFilter = new ElementMulticategoryFilter(catList);
                    // --- Get windows & doors by the wall and create ref.:
                    IList<ElementId> wallElemIds = selectedWall.GetDependentElements(wallFilter);
                    foreach (ElementId elemId in wallElemIds)
                    {
                        var curFI = doc.GetElement(elemId) as FamilyInstance;
                        Reference? curRef = GetSpecialFamilyRef(curFI, SpeciacRefDirType.CenterLR);
                        if (curRef != null) 
                            refArray.Append(curRef);
                    }
                    refArray.Append(r2);
                    // Create dimension line:
                    LocationCurve? wallLoc = selectedWall.Location as LocationCurve;
                    Line? wallLine = wallLoc?.Curve as Line;
                    XYZ offset1 = GetOffSetByWallOrientation(wallLine?.GetEndPoint(0), selectedWall.Orientation, 5);
                    XYZ offset2 = GetOffSetByWallOrientation(wallLine?.GetEndPoint(1), selectedWall.Orientation, 5);
                    Line dimLine = Line.CreateBound(offset1, offset2);
                    using (Transaction trans = new(doc))
                    {
                        trans.Start("CreateNewdim");
                        var newDim = doc.Create.NewDimension(doc.ActiveView, wallLine, refArray);
                        trans.Commit();
                    }
                    return Result.Succeeded;
                }
                return Result.Failed;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return Result.Failed;
            }
        }

        private XYZ GetOffSetByWallOrientation(XYZ point, XYZ orientation, int value)
        {
            XYZ newVector = orientation.Multiply(value);
            return point.Add(newVector);
        }

        private Reference? GetSpecialFamilyRef(FamilyInstance? inst, SpeciacRefDirType refType)
        {
            Reference? indexRef = null;
            int idx = (int)refType;
            if (inst == null)
                return indexRef;

            var dbDoc = inst.Document;
            var geomOptions = new Options
            {
                ComputeReferences = true,
                DetailLevel = ViewDetailLevel.Undefined,
                IncludeNonVisibleObjects = true
            };
            GeometryElement gElem = inst.get_Geometry(geomOptions);
            var gInst = gElem.First() as GeometryInstance;
            var gSymbol = gInst?.GetSymbolGeometry();
            //Autodesk.Revit.DB.Point point = gInst as Autodesk.Revit.DB.Point;
            if (gSymbol != null)
            {
                string? stabRef = "";
                foreach (GeometryObject gObj in gSymbol)
                {
                    var solid = gObj as Solid;
                    var curve = gObj as Curve;
                    if (solid != null && solid.Faces.Size > 0)
                    {
                        stabRef = solid.Faces.get_Item(0)
                                       .Reference.ConvertToStableRepresentation(dbDoc);
                        break;
                    }
                    else if (curve != null && curve.Reference != null)
                    {
                        stabRef = curve.Reference.ConvertToStableRepresentation(dbDoc);
                        break;
                    }
                    //else if (point != null)
                    //{
                    //    stabRef = point.Reference.ConvertToStableRepresentation(dbDoc);
                    //    break;
                    //}
                }
                var refTokens = stabRef.Split([':']);
                var custStabRef = $"{refTokens[0]}:{refTokens[1]}:{refTokens[2]}:{refTokens[3]}:{idx}";
                indexRef = Reference.ParseFromStableRepresentation(dbDoc, custStabRef);
                var geomObj = inst.GetGeometryObjectFromReference(indexRef);
                if (geomObj != null)
                {
                    var token = "";
                    if (geomObj is Edge)
                        token = ":LINEAR";
                    if (geomObj is Face)
                        token = ":SURFACE";

                    custStabRef += token;
                    indexRef = Reference.ParseFromStableRepresentation(dbDoc, custStabRef);
                }
            }
            else
                throw new Exception("No Symbol Geometry Found!");

            return indexRef;
        }

        private bool IsLineVertical(Line line) => line.Direction.IsAlmostEqualTo(XYZ.BasisZ)
                                                  || line.Direction.IsAlmostEqualTo(-XYZ.BasisZ);

        private Face? GetFace(Element? selectedElem, XYZ orientation)
        {
            var solids = GetSolids(selectedElem);
            PlanarFace? pf = null;
            foreach (var solid in solids)
            {
                foreach (Face face in solid.Faces)
                {
                    pf = face as PlanarFace;
                    if (pf != null && pf.FaceNormal.IsAlmostEqualTo(orientation))
                        break;
                }
            }
            return pf;
        }

        private List<Solid> GetSolids(Element? selectedElem)
        {
            var resultSolids = new List<Solid>();
            var options = new Options
            {
                ComputeReferences = true,
                DetailLevel = ViewDetailLevel.Fine
            };
            GeometryElement geomElm = selectedElem?.get_Geometry(options)!;
            foreach (GeometryObject obj in geomElm)
            {
                var solid = obj as Solid;
                if (solid != null && solid.Faces.Size > 0 && solid.Volume > 0.0)
                    resultSolids.Add(solid);
            }
            return resultSolids;
        }
    }
}
