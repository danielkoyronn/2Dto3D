using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Tecnomatix.Engineering;

// ============================================================================
// Process Simulate 2512 .NET Script - ALL IN ONE
// ============================================================================
// Final workflow:
//   1) Read factory DXF directly (no Python / no CSV required).
//   2) Detect Robot / Track-Robot / Safe-Fence placement Frames.
//   3) Create all TxFrame objects in Process Simulate.
//   4) Insert one Robot template at world origin (0,0,0), then instantiate the
//      same approved COJT package on every Robot Frame and align Robot.Baseframe.
//   5) Insert one Safe-Fence JT template at world origin (0,0,0), then instantiate
//      the same JT on every Fence Frame.
//
// Naming standard:
//   Normal robot frame : <AREA>_ROBOT_<NNN>
//   Track robot frame  : <AREA>_TRACK_ROBOT_<NNN>
//   MID_MAIN corrected : MID_MAIN_<BM/FM station>_ROBOT_R<NN>
//                        or MID_MAIN_<BM/FM station>_ROBOT_<NNN> fallback
//   Fence frame        : <AREA>_FENCE_<NNN>
//
// Resource instance naming:
//   Robot template     : TEMPLATE_ROBOT_KR210_R2700
//   Fence template     : TEMPLATE_FENCE_BSL_SF_0044
//   Robot instance     : AUTO_<FRAME_NAME>_KR210_R2700
//   Fence instance     : AUTO_<FRAME_NAME>_BSL_SF_0044
//
// IMPORTANT:
// - This script parses ASCII DXF directly. Binary DXF is not supported.
// - It reads ModelSpace-like entities from the ENTITIES section and ignores
//   entities explicitly marked as PaperSpace (group code 67 = 1).
// - Fence LINE/LWPOLYLINE bulges are treated as straight chords.
// - Track-Robot Frames currently receive the robot arm only; no rail/7th axis is
//   created automatically.
// - Resource "copy" is implemented by repeated InsertComponent from the same
//   approved COJT/JT source. This is more deterministic than clipboard copy/paste.
// ============================================================================

public class MainScript
{
    // ========================================================================
    // USER CONFIGURATION - edit these three paths first
    // ========================================================================

    private const string DxfPath =
        @"D:\WEDING\MODEL\1013.dxf";

    private const string RobotPackagePath =
        @"D:\WEDING\MOVING\work261007\Library\BSR_OP10_R01_KR210_R2700.cojt";

    private const string FenceJtPath =
        @"D:\WEDING\MOVING\work261007\Library\BSL_SF-0044.jt";

    // ========================================================================
    // RESOURCE SETTINGS
    // ========================================================================

    private const string RobotModelTag = "KR210_R2700";
    private const string FenceModelTag = "BSL_SF_0044";

    private const string RobotTemplateName = "TEMPLATE_ROBOT_KR210_R2700";
    private const string FenceTemplateName = "TEMPLATE_FENCE_BSL_SF_0044";

    private const double FenceOffsetX = 0.0;
    private const double FenceOffsetY = 0.0;
    private const double FenceOffsetZ = 0.0;
    private const double FenceOffsetRzDeg = 0.0;

    private const bool IncludeTrackRobotFrames = true;
    private const bool KeepTemplatesAtOrigin = true;
    private const double MaxAbsTargetZMm = 10000.0;
    private const double Default2DRobotZMm = 0.0;
    private const double RobotLabelMaxDistanceMm = 3000.0;
    private const double MidMainYSplit = -1975000.0;

    private sealed class AreaDef
    {
        public string Name;
        public double XMin;
        public double XMax;
        public double YMin;
        public double YMax;
        public AreaDef(string name, double xmin, double xmax, double ymin, double ymax)
        { Name = name; XMin = xmin; XMax = xmax; YMin = ymin; YMax = ymax; }
        public bool Contains(double x, double y)
        { return x >= XMin && x <= XMax && y >= YMin && y <= YMax; }
    }

    private static readonly AreaDef[] AreaBounds = new AreaDef[]
    {
        new AreaDef("BSL_AUX", 941617.149078, 959617.149078, -1952582.839094, -1942582.839094),
        new AreaDef("BSR_AUX", 963617.149078, 983617.149078, -1952582.839094, -1942582.839094),
        new AreaDef("MID_AUX", 1012617.149078, 1034617.149078, -1988582.839094, -1977582.787282),
        new AreaDef("BOTTOM", 941617.149078, 1034617.149078, -2001582.839094, -1994582.839094),
        new AreaDef("BSL", 939617.149078, 959617.149078, -1939582.839094, -1896582.839094),
        new AreaDef("BSR", 963617.149078, 983617.149078, -1939582.839094, -1896582.839094),
        new AreaDef("EC", 988617.149078, 1009017.149078, -1952582.839094, -1896582.839094),
        new AreaDef("RR", 1014017.149050, 1030617.149050, -1952582.839094, -1896582.839094),
        new AreaDef("MID_MAIN", 941617.149078, 1034617.149078, -1988582.839094, -1957582.839094)
    };

    private static readonly Dictionary<string, double> UpperStationX = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
    {
        { "BM02", 957496.0 }, { "BM01", 972315.0 }, { "FM07", 982662.0 },
        { "FM05", 995953.0 }, { "FM04", 1001809.0 }, { "FM03", 1008036.0 },
        { "FM02", 1015611.0 }, { "FM01", 1025683.0 }
    };

    private static readonly Dictionary<string, double> LowerStationX = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
    {
        { "BM05", 950331.0 }, { "BM06", 958850.0 }, { "BM07", 965237.0 },
        { "BM08", 971295.0 }, { "BM09", 977535.0 }, { "BM10", 984581.0 },
        { "BM12", 996952.0 }
    };

    private sealed class DxfPair
    {
        public int Code; public string Value;
        public DxfPair(int code, string value) { Code = code; Value = value == null ? String.Empty : value; }
    }

    private sealed class DxfEntity
    {
        public string Type;
        public List<DxfPair> Pairs = new List<DxfPair>();
        public DxfEntity(string type) { Type = type == null ? String.Empty : type.Trim().ToUpperInvariant(); }
        public string First(int code, string defaultValue)
        {
            for (int i = 0; i < Pairs.Count; i++) if (Pairs[i].Code == code) return Pairs[i].Value;
            return defaultValue;
        }
        public int IntValue(int code, int defaultValue)
        {
            string raw = First(code, null); int value;
            if (raw != null && Int32.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) return value;
            return defaultValue;
        }
        public double DoubleValue(int code, double defaultValue)
        {
            string raw = First(code, null); double value;
            if (raw != null && Double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return value;
            return defaultValue;
        }
        public string AllText()
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < Pairs.Count; i++) if (Pairs[i].Code == 3 || Pairs[i].Code == 1) sb.Append(Pairs[i].Value);
            return sb.ToString();
        }
    }

    private sealed class Point2 { public double X; public double Y; public Point2(double x, double y) { X = x; Y = y; } }
    private sealed class RobotCandidate
    {
        public string Area, Type, Layer, Block, InitialName, FinalName, Station;
        public double X, Y, Z, Rz;
    }
    private sealed class FenceCandidate
    {
        public string Area, Layer, Block, SourceEntity;
        public double X, Y, Z, Rz;
    }
    private sealed class TextCandidate { public string Text; public double X, Y; }
    private sealed class DxfScanResult
    {
        public List<RobotCandidate> Robots = new List<RobotCandidate>();
        public List<FenceCandidate> FenceInserts = new List<FenceCandidate>();
        public List<FenceCandidate> FenceSegments = new List<FenceCandidate>();
        public List<TextCandidate> Texts = new List<TextCandidate>();
        public long EntityCount;
    }
    private sealed class FrameSpec
    {
        public string Name, Type, Area, SourceLayer, SourceBlock, SourceEntity;
        public double X, Y, Z, Rx, Ry, Rz;
    }
    private sealed class RobotLabel { public int Id, RobotNo; public string Station; public double X, Y; }
    private sealed class MatchPair { public double Distance; public int RobotIndex, LabelId, RobotNo; }

    public static void Main()
    {
        StringWriter output = new StringWriter();
        try { Run(ref output); }
        catch (Exception ex)
        {
            Exception detail = Unwrap(ex);
            output.WriteLine(""); output.WriteLine("FATAL: " + detail.GetType().Name + " - " + detail.Message);
        }
        TxMessageBox.Show(output.ToString(), "ALL-IN-ONE DXF -> Frames -> Robot + Fence", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static void Run(ref StringWriter output)
    {
        TxDocument document = TxApplication.ActiveDocument;
        if (document == null) throw new InvalidOperationException("No active Process Simulate study.");
        TxPhysicalRoot physicalRoot = document.PhysicalRoot;
        if (physicalRoot == null) throw new InvalidOperationException("Active study has no PhysicalRoot.");
        ValidateInputs();

        output.WriteLine("DXF    : " + DxfPath);
        output.WriteLine("Robot  : " + RobotPackagePath);
        output.WriteLine("Fence  : " + FenceJtPath);
        output.WriteLine(""); output.WriteLine("STEP 1/4 - Reading DXF ...");

        DxfScanResult scan = ReadDxf(DxfPath);
        output.WriteLine("DXF entities scanned : " + scan.EntityCount);
        output.WriteLine("Robot INSERTs        : " + scan.Robots.Count);
        output.WriteLine("Fence INSERTs        : " + scan.FenceInserts.Count);
        output.WriteLine("Fence segments       : " + scan.FenceSegments.Count);
        output.WriteLine("Text/MTEXT records   : " + scan.Texts.Count);

        List<FrameSpec> specs = BuildFrameSpecs(scan, output);
        ValidateFrameNames(specs);
        int robotFrameCount = 0, fenceFrameCount = 0;
        for (int i = 0; i < specs.Count; i++)
        {
            if (IsRobotType(specs[i].Type)) robotFrameCount++;
            if (String.Equals(specs[i].Type, "FENCE", StringComparison.OrdinalIgnoreCase)) fenceFrameCount++;
        }
        output.WriteLine("Detected Frames       : " + specs.Count);
        output.WriteLine("Robot Frames          : " + robotFrameCount);
        output.WriteLine("Fence Frames          : " + fenceFrameCount);
        output.WriteLine(""); output.WriteLine("STEP 2/4 - Creating Process Simulate Frames ...");

        Dictionary<string, TxFrame> frameMap = CollectFrameMap(physicalRoot);
        HashSet<string> objectNames = CollectObjectNames(physicalRoot);
        int frameOk = 0, frameSkip = 0, frameFail = 0;
        for (int i = 0; i < specs.Count; i++)
        {
            FrameSpec spec = specs[i];
            if (frameMap.ContainsKey(spec.Name)) { frameSkip++; continue; }
            try
            {
                TxFrame frame = CreateFrame(physicalRoot, spec);
                frameMap[spec.Name] = frame; objectNames.Add(spec.Name); frameOk++;
            }
            catch (Exception ex)
            { output.WriteLine("FAIL FRAME " + spec.Name + ": " + Unwrap(ex).Message); frameFail++; }
        }
        output.WriteLine("Frame result: OK=" + frameOk + " SKIP=" + frameSkip + " FAIL=" + frameFail);
        output.WriteLine(""); output.WriteLine("STEP 3/4 - Creating templates at world origin ...");

        ITxComponent robotTemplate = EnsureTemplate(physicalRoot, RobotTemplateName, RobotPackagePath, objectNames, output);
        ITxComponent fenceTemplate = EnsureTemplate(physicalRoot, FenceTemplateName, FenceJtPath, objectNames, output);
        TxRobot templateRobot = FindRobot(robotTemplate);
        if (templateRobot == null) throw new InvalidOperationException("Robot template was inserted but no TxRobot was found. Use the complete COJT package.");
        int joints = templateRobot.Joints == null ? 0 : templateRobot.Joints.Count;
        int driving = templateRobot.DrivingJoints == null ? 0 : templateRobot.DrivingJoints.Count;
        output.WriteLine("Robot template: joints=" + joints + " driving=" + driving);
        if (joints < 6 || driving < 6) throw new InvalidOperationException("Robot template has fewer than six joints/driving joints.");
        output.WriteLine("Fence template: " + FenceTemplateName + " @ (0,0,0)");
        output.WriteLine(""); output.WriteLine("STEP 4/4 - Placing Robot and Fence instances ...");

        int robotOk = 0, robotSkip = 0, robotFail = 0, fenceOk = 0, fenceSkip = 0, fenceFail = 0;
        for (int i = 0; i < specs.Count; i++)
        {
            FrameSpec spec = specs[i]; TxFrame targetFrame;
            if (!frameMap.TryGetValue(spec.Name, out targetFrame) || targetFrame == null)
            { output.WriteLine("FAIL TARGET " + spec.Name + ": Frame not available."); continue; }
            if (Math.Abs(targetFrame.AbsoluteLocation.Translation.Z) > MaxAbsTargetZMm)
            { output.WriteLine("FAIL TARGET " + spec.Name + ": suspicious Z=" + targetFrame.AbsoluteLocation.Translation.Z.ToString("F3", CultureInfo.InvariantCulture)); continue; }

            if (IsRobotType(spec.Type))
            {
                if (String.Equals(spec.Type, "TRACK_ROBOT", StringComparison.OrdinalIgnoreCase) && !IncludeTrackRobotFrames) continue;
                string instanceName = "AUTO_" + spec.Name + "_" + RobotModelTag;
                if (objectNames.Contains(instanceName)) { robotSkip++; continue; }
                ITxComponent inserted = null;
                try
                {
                    inserted = InsertComponent(physicalRoot, instanceName, RobotPackagePath, targetFrame.AbsoluteLocation);
                    ITxLocatableObject root = inserted as ITxLocatableObject; TxRobot robot = FindRobot(inserted);
                    if (root == null || !root.CanLocate()) throw new InvalidOperationException("Inserted Robot root is not locatable.");
                    if (robot == null) throw new InvalidOperationException("Inserted COJT contains no TxRobot.");
                    AlignRobotBase(root, robot, targetFrame); objectNames.Add(instanceName); robotOk++;
                }
                catch (Exception ex)
                { RemoveIncompleteInsert(inserted, output); output.WriteLine("FAIL ROBOT " + spec.Name + ": " + Unwrap(ex).Message); robotFail++; }
            }
            else if (String.Equals(spec.Type, "FENCE", StringComparison.OrdinalIgnoreCase))
            {
                string instanceName = "AUTO_" + spec.Name + "_" + FenceModelTag;
                if (objectNames.Contains(instanceName)) { fenceSkip++; continue; }
                ITxComponent inserted = null;
                try
                {
                    TxTransformation target = ApplyFenceOffset(targetFrame.AbsoluteLocation);
                    inserted = InsertComponent(physicalRoot, instanceName, FenceJtPath, target);
                    ITxLocatableObject root = inserted as ITxLocatableObject;
                    if (root == null || !root.CanLocate()) throw new InvalidOperationException("Inserted Fence JT is not locatable.");
                    root.AbsoluteLocation = target; objectNames.Add(instanceName); fenceOk++;
                }
                catch (Exception ex)
                { RemoveIncompleteInsert(inserted, output); output.WriteLine("FAIL FENCE " + spec.Name + ": " + Unwrap(ex).Message); fenceFail++; }
            }
        }

        if (!KeepTemplatesAtOrigin) { TryDelete(robotTemplate, output); TryDelete(fenceTemplate, output); }
        output.WriteLine(""); output.WriteLine("==================== SUMMARY ====================");
        output.WriteLine("Frames : OK=" + frameOk + " SKIP=" + frameSkip + " FAIL=" + frameFail);
        output.WriteLine("Robot  : OK=" + robotOk + " SKIP=" + robotSkip + " FAIL=" + robotFail);
        output.WriteLine("Fence  : OK=" + fenceOk + " SKIP=" + fenceSkip + " FAIL=" + fenceFail);
        output.WriteLine("=================================================");
    }

    private static DxfScanResult ReadDxf(string path)
    {
        DxfScanResult result = new DxfScanResult();
        using (FileStream probe = File.OpenRead(path))
        {
            byte[] header = new byte[22]; int read = probe.Read(header, 0, header.Length);
            string text = Encoding.ASCII.GetString(header, 0, read);
            if (text.StartsWith("AutoCAD Binary DXF", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException("Binary DXF is not supported. Save/export as ASCII DXF first.");
        }
        using (StreamReader reader = new StreamReader(path, Encoding.Default, true, 65536))
        {
            string section = String.Empty; bool waitingSectionName = false; DxfEntity current = null;
            while (true)
            {
                int code; string value;
                if (!ReadDxfPair(reader, out code, out value)) break;
                if (code == 0 && String.Equals(value.Trim(), "SECTION", StringComparison.OrdinalIgnoreCase))
                { FinalizeEntity(current, result); current = null; waitingSectionName = true; continue; }
                if (waitingSectionName)
                {
                    if (code == 2) { section = value.Trim().ToUpperInvariant(); waitingSectionName = false; }
                    continue;
                }
                if (code == 0 && String.Equals(value.Trim(), "ENDSEC", StringComparison.OrdinalIgnoreCase))
                { FinalizeEntity(current, result); current = null; section = String.Empty; continue; }
                if (!String.Equals(section, "ENTITIES", StringComparison.OrdinalIgnoreCase)) continue;
                if (code == 0)
                { FinalizeEntity(current, result); current = new DxfEntity(value); continue; }
                if (current != null) current.Pairs.Add(new DxfPair(code, value));
            }
            FinalizeEntity(current, result);
        }
        return result;
    }

    private static bool ReadDxfPair(StreamReader reader, out int code, out string value)
    {
        code = -999; value = null;
        string codeLine = reader.ReadLine(); if (codeLine == null) return false;
        string valueLine = reader.ReadLine(); if (valueLine == null) return false;
        int parsed;
        if (Int32.TryParse(codeLine.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)) code = parsed;
        value = valueLine; return true;
    }

    private static void FinalizeEntity(DxfEntity entity, DxfScanResult result)
    {
        if (entity == null) return;
        result.EntityCount++;
        if (entity.IntValue(67, 0) == 1) return;
        string type = entity.Type; string layer = entity.First(8, String.Empty).Trim();
        if (String.Equals(type, "INSERT", StringComparison.OrdinalIgnoreCase))
        {
            string block = entity.First(2, String.Empty).Trim();
            double x = entity.DoubleValue(10, 0.0), y = entity.DoubleValue(20, 0.0), z = entity.DoubleValue(30, 0.0);
            double rz = NormalizeDegrees(entity.DoubleValue(50, 0.0));
            if (IsRobotInsert(layer, block))
            {
                RobotCandidate r = new RobotCandidate(); r.X = x; r.Y = y; r.Z = Repair2DZ(z); r.Rz = rz;
                r.Layer = layer; r.Block = block; r.Area = AreaFor(x, y); r.Type = IsTrack(layer, block) ? "TRACK_ROBOT" : "ROBOT";
                result.Robots.Add(r);
            }
            if (IsFence(layer, block))
            {
                FenceCandidate f = new FenceCandidate(); f.X = x; f.Y = y; f.Z = z; f.Rz = rz;
                f.Layer = layer; f.Block = block; f.Area = AreaFor(x, y); f.SourceEntity = "INSERT"; result.FenceInserts.Add(f);
            }
        }
        else if (String.Equals(type, "LINE", StringComparison.OrdinalIgnoreCase))
        {
            if (IsFence(layer, String.Empty))
                AddFenceSegment(result.FenceSegments, layer, "LINE", entity.DoubleValue(10, 0.0), entity.DoubleValue(20, 0.0), entity.DoubleValue(11, 0.0), entity.DoubleValue(21, 0.0));
        }
        else if (String.Equals(type, "LWPOLYLINE", StringComparison.OrdinalIgnoreCase))
        {
            if (!IsFence(layer, String.Empty)) return;
            List<Point2> points = ExtractLwPolylinePoints(entity);
            for (int i = 0; i + 1 < points.Count; i++)
                AddFenceSegment(result.FenceSegments, layer, "LWPOLYLINE", points[i].X, points[i].Y, points[i + 1].X, points[i + 1].Y);
            bool closed = (entity.IntValue(70, 0) & 1) != 0;
            if (closed && points.Count > 2)
                AddFenceSegment(result.FenceSegments, layer, "LWPOLYLINE", points[points.Count - 1].X, points[points.Count - 1].Y, points[0].X, points[0].Y);
        }
        else if (String.Equals(type, "TEXT", StringComparison.OrdinalIgnoreCase) || String.Equals(type, "MTEXT", StringComparison.OrdinalIgnoreCase))
        {
            TextCandidate t = new TextCandidate(); t.X = entity.DoubleValue(10, 0.0); t.Y = entity.DoubleValue(20, 0.0);
            t.Text = NormalizeCadText(String.Equals(type, "MTEXT", StringComparison.OrdinalIgnoreCase) ? entity.AllText() : entity.First(1, String.Empty));
            result.Texts.Add(t);
        }
    }

    private static List<Point2> ExtractLwPolylinePoints(DxfEntity entity)
    {
        List<Point2> points = new List<Point2>(); bool hasX = false; double currentX = 0.0;
        for (int i = 0; i < entity.Pairs.Count; i++)
        {
            DxfPair p = entity.Pairs[i];
            if (p.Code == 10)
            {
                double x; if (Double.TryParse(p.Value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out x)) { currentX = x; hasX = true; }
            }
            else if (p.Code == 20 && hasX)
            {
                double y; if (Double.TryParse(p.Value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out y)) { points.Add(new Point2(currentX, y)); hasX = false; }
            }
        }
        return points;
    }

    private static void AddFenceSegment(List<FenceCandidate> output, string layer, string entityType, double x1, double y1, double x2, double y2)
    {
        double dx = x2 - x1, dy = y2 - y1, length = Math.Sqrt(dx * dx + dy * dy); if (length < 1.0) return;
        double x = (x1 + x2) / 2.0, y = (y1 + y2) / 2.0;
        FenceCandidate f = new FenceCandidate(); f.X = x; f.Y = y; f.Z = 0.0;
        f.Rz = NormalizeDegrees(Math.Atan2(dy, dx) * 180.0 / Math.PI); f.Layer = layer; f.Block = String.Empty;
        f.Area = AreaFor(x, y); f.SourceEntity = entityType; output.Add(f);
    }

    private static bool IsRobotInsert(string layer, string block)
    {
        string l = (layer ?? String.Empty).ToUpperInvariant(), b = (block ?? String.Empty).ToUpperInvariant();
        if (l.Contains("010_ROBOT") || l.Contains("ROBOT") || l.Contains("機器人") || l.Contains("机器人")) return true;
        if (b.Contains("R-2000") || b.Contains("ROBOT") || b.Contains("DTL0033")) return true;
        if (Regex.IsMatch(b, @"(^|[^A-Z])KR\s*\d+", RegexOptions.IgnoreCase)) return true;
        return false;
    }

    private static bool IsTrack(string layer, string block)
    {
        string value = ((layer ?? String.Empty) + " " + (block ?? String.Empty)).ToUpperInvariant();
        return value.Contains("TRACK") || value.Contains("TRACKER") || value.Contains("RAIL") || value.Contains("7TH") ||
               value.Contains("7TH_AXIS") || value.Contains("軌道") || value.Contains("轨道") || value.Contains("履帶") || value.Contains("履带");
    }

    private static bool IsFence(string layer, string block)
    {
        string l = (layer ?? String.Empty).ToUpperInvariant(), b = (block ?? String.Empty).ToUpperInvariant(), value = l + " " + b;
        if (value.Contains("FENCE") || value.Contains("SAFE_FENCE") || value.Contains("SAFE-FENCE") ||
            value.Contains("SAFETY_FENCE") || value.Contains("SAFETY-FENCE") || value.Contains("安全圍籬") ||
            value.Contains("安全围栏") || value.Contains("安全柵欄") || value.Contains("安全栅栏") || value.Contains("安全門") || value.Contains("安全门")) return true;
        if (Regex.IsMatch(b, @"(^|[_\- ])SF[_\- ]?\d*($|[_\- ])", RegexOptions.IgnoreCase)) return true;
        if (Regex.IsMatch(l, @"(^|[_\- ])SF[_\- ]?\d*($|[_\- ])", RegexOptions.IgnoreCase)) return true;
        return false;
    }

    private static List<FrameSpec> BuildFrameSpecs(DxfScanResult scan, StringWriter output)
    {
        List<FrameSpec> result = new List<FrameSpec>();
        result.AddRange(BuildRobotFrames(scan.Robots, scan.Texts, output));
        result.AddRange(BuildFenceFrames(scan.FenceInserts, scan.FenceSegments, output));
        result.Sort(delegate(FrameSpec a, FrameSpec b)
        {
            int c = String.Compare(a.Type, b.Type, StringComparison.OrdinalIgnoreCase); if (c != 0) return c;
            c = String.Compare(a.Area, b.Area, StringComparison.OrdinalIgnoreCase); if (c != 0) return c;
            return String.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });
        return result;
    }

    private static List<FrameSpec> BuildRobotFrames(List<RobotCandidate> robots, List<TextCandidate> texts, StringWriter output)
    {
        Dictionary<string, List<RobotCandidate>> groups = new Dictionary<string, List<RobotCandidate>>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < robots.Count; i++)
        {
            RobotCandidate r = robots[i]; string key = r.Area + "|" + r.Type; List<RobotCandidate> list;
            if (!groups.TryGetValue(key, out list)) { list = new List<RobotCandidate>(); groups[key] = list; }
            list.Add(r);
        }
        foreach (KeyValuePair<string, List<RobotCandidate>> kv in groups)
        {
            List<RobotCandidate> list = kv.Value;
            list.Sort(delegate(RobotCandidate a, RobotCandidate b)
            { int c = a.Y.CompareTo(b.Y); if (c != 0) return c; c = a.X.CompareTo(b.X); if (c != 0) return c; return String.Compare(a.Block, b.Block, StringComparison.OrdinalIgnoreCase); });
            for (int i = 0; i < list.Count; i++)
            {
                RobotCandidate r = list[i]; string token = String.Equals(r.Type, "TRACK_ROBOT", StringComparison.OrdinalIgnoreCase) ? "TRACK_ROBOT" : "ROBOT";
                r.InitialName = r.Area + "_" + token + "_" + (i + 1).ToString("000"); r.FinalName = r.InitialName;
            }
        }
        ApplyMidMainRobotCorrections(robots, texts, output);
        List<FrameSpec> result = new List<FrameSpec>();
        for (int i = 0; i < robots.Count; i++)
        {
            RobotCandidate r = robots[i]; FrameSpec s = new FrameSpec();
            s.Name = r.FinalName; s.Type = r.Type; s.Area = r.Area; s.X = r.X; s.Y = r.Y; s.Z = Repair2DZ(r.Z);
            s.Rx = 0.0; s.Ry = 0.0; s.Rz = NormalizeDegrees(r.Rz); s.SourceLayer = r.Layer; s.SourceBlock = r.Block; s.SourceEntity = "INSERT";
            result.Add(s);
        }
        return result;
    }

    private static void ApplyMidMainRobotCorrections(List<RobotCandidate> robots, List<TextCandidate> texts, StringWriter output)
    {
        List<RobotLabel> labels = new List<RobotLabel>(); int labelId = 0;
        for (int i = 0; i < texts.Count; i++)
        {
            TextCandidate t = texts[i];
            if (t.X < 935000.0 || t.X > 1040000.0 || t.Y < -1992000.0 || t.Y > -1953000.0) continue;
            int robotNo; if (!TryParseBareRobotLabel(t.Text, out robotNo)) continue;
            RobotLabel label = new RobotLabel(); label.Id = labelId++; label.RobotNo = robotNo; label.X = t.X; label.Y = t.Y; label.Station = StationFor(t.X, t.Y); labels.Add(label);
        }
        Dictionary<string, List<int>> stationRobotIndexes = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < robots.Count; i++)
        {
            RobotCandidate r = robots[i];
            if (!String.Equals(r.Area, "MID_MAIN", StringComparison.OrdinalIgnoreCase) || String.Equals(r.Type, "TRACK_ROBOT", StringComparison.OrdinalIgnoreCase)) continue;
            r.Station = StationFor(r.X, r.Y); List<int> list;
            if (!stationRobotIndexes.TryGetValue(r.Station, out list)) { list = new List<int>(); stationRobotIndexes[r.Station] = list; }
            list.Add(i);
        }
        int matchedTotal = 0;
        foreach (KeyValuePair<string, List<int>> kv in stationRobotIndexes)
        {
            string station = kv.Key; List<int> robotIndexes = kv.Value; List<RobotLabel> stationLabels = new List<RobotLabel>();
            for (int i = 0; i < labels.Count; i++) if (String.Equals(labels[i].Station, station, StringComparison.OrdinalIgnoreCase)) stationLabels.Add(labels[i]);
            List<MatchPair> pairs = new List<MatchPair>();
            for (int ri = 0; ri < robotIndexes.Count; ri++)
            {
                int robotIndex = robotIndexes[ri]; RobotCandidate r = robots[robotIndex];
                for (int li = 0; li < stationLabels.Count; li++)
                {
                    RobotLabel label = stationLabels[li]; double dx = r.X - label.X, dy = r.Y - label.Y, d = Math.Sqrt(dx * dx + dy * dy);
                    if (d <= RobotLabelMaxDistanceMm)
                    { MatchPair pair = new MatchPair(); pair.Distance = d; pair.RobotIndex = robotIndex; pair.LabelId = label.Id; pair.RobotNo = label.RobotNo; pairs.Add(pair); }
                }
            }
            pairs.Sort(delegate(MatchPair a, MatchPair b)
            { int c = a.Distance.CompareTo(b.Distance); if (c != 0) return c; c = a.RobotIndex.CompareTo(b.RobotIndex); if (c != 0) return c; return a.LabelId.CompareTo(b.LabelId); });
            HashSet<int> usedRobots = new HashSet<int>(), usedLabels = new HashSet<int>(), usedRobotNumbers = new HashSet<int>();
            for (int p = 0; p < pairs.Count; p++)
            {
                MatchPair pair = pairs[p];
                if (usedRobots.Contains(pair.RobotIndex) || usedLabels.Contains(pair.LabelId) || usedRobotNumbers.Contains(pair.RobotNo)) continue;
                RobotCandidate r = robots[pair.RobotIndex]; r.FinalName = "MID_MAIN_" + station + "_ROBOT_R" + pair.RobotNo.ToString("00");
                usedRobots.Add(pair.RobotIndex); usedLabels.Add(pair.LabelId); usedRobotNumbers.Add(pair.RobotNo); matchedTotal++;
            }
            for (int ri = 0; ri < robotIndexes.Count; ri++)
            {
                int robotIndex = robotIndexes[ri]; if (usedRobots.Contains(robotIndex)) continue;
                RobotCandidate r = robots[robotIndex]; r.FinalName = "MID_MAIN_" + station + "_ROBOT_" + NumericSuffix(r.InitialName);
            }
        }
        output.WriteLine("MID_MAIN bare R labels : " + labels.Count); output.WriteLine("MID_MAIN label matches : " + matchedTotal);
    }

    private static List<FrameSpec> BuildFenceFrames(List<FenceCandidate> insertCandidates, List<FenceCandidate> segmentCandidates, StringWriter output)
    {
        Dictionary<string, List<FenceCandidate>> insertByArea = GroupFenceByArea(insertCandidates), segmentByArea = GroupFenceByArea(segmentCandidates);
        HashSet<string> areas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string key in insertByArea.Keys) areas.Add(key); foreach (string key in segmentByArea.Keys) areas.Add(key);
        List<string> areaList = new List<string>(areas); areaList.Sort(StringComparer.OrdinalIgnoreCase); List<FrameSpec> result = new List<FrameSpec>();
        for (int a = 0; a < areaList.Count; a++)
        {
            string area = areaList[a]; List<FenceCandidate> source;
            if (insertByArea.ContainsKey(area) && insertByArea[area].Count > 0) source = insertByArea[area];
            else if (segmentByArea.ContainsKey(area)) source = segmentByArea[area]; else continue;
            List<FenceCandidate> deduped = DeduplicateFenceCandidates(source);
            deduped.Sort(delegate(FenceCandidate x, FenceCandidate y)
            { int c = x.Y.CompareTo(y.Y); if (c != 0) return c; c = x.X.CompareTo(y.X); if (c != 0) return c; return x.Rz.CompareTo(y.Rz); });
            for (int i = 0; i < deduped.Count; i++)
            {
                FenceCandidate f = deduped[i]; FrameSpec s = new FrameSpec(); s.Name = area + "_FENCE_" + (i + 1).ToString("000");
                s.Type = "FENCE"; s.Area = area; s.X = f.X; s.Y = f.Y; s.Z = f.Z; s.Rx = 0.0; s.Ry = 0.0; s.Rz = NormalizeDegrees(f.Rz);
                s.SourceLayer = f.Layer; s.SourceBlock = f.Block; s.SourceEntity = f.SourceEntity; result.Add(s);
            }
        }
        output.WriteLine("Fence Frames after area preference/dedup: " + result.Count); return result;
    }

    private static Dictionary<string, List<FenceCandidate>> GroupFenceByArea(List<FenceCandidate> input)
    {
        Dictionary<string, List<FenceCandidate>> result = new Dictionary<string, List<FenceCandidate>>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < input.Count; i++)
        {
            FenceCandidate f = input[i]; List<FenceCandidate> list;
            if (!result.TryGetValue(f.Area, out list)) { list = new List<FenceCandidate>(); result[f.Area] = list; }
            list.Add(f);
        }
        return result;
    }

    private static List<FenceCandidate> DeduplicateFenceCandidates(List<FenceCandidate> input)
    {
        List<FenceCandidate> result = new List<FenceCandidate>(); HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < input.Count; i++)
        {
            FenceCandidate f = input[i]; string key = f.Area + "|" + Math.Round(f.X).ToString(CultureInfo.InvariantCulture) + "|" + Math.Round(f.Y).ToString(CultureInfo.InvariantCulture) + "|" + Math.Round(NormalizeDegrees(f.Rz)).ToString(CultureInfo.InvariantCulture);
            if (seen.Add(key)) result.Add(f);
        }
        return result;
    }

    private static void ValidateFrameNames(List<FrameSpec> specs)
    {
        HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < specs.Count; i++)
        {
            if (String.IsNullOrWhiteSpace(specs[i].Name)) throw new InvalidDataException("Detected a Frame with an empty name.");
            if (!names.Add(specs[i].Name)) throw new InvalidDataException("Duplicate Frame name: " + specs[i].Name);
        }
    }

    private static string AreaFor(double x, double y)
    { for (int i = 0; i < AreaBounds.Length; i++) if (AreaBounds[i].Contains(x, y)) return AreaBounds[i].Name; return "FACTORY"; }

    private static string StationFor(double x, double y)
    {
        Dictionary<string, double> map = y > MidMainYSplit ? UpperStationX : LowerStationX; string bestName = String.Empty; double bestDistance = Double.MaxValue;
        foreach (KeyValuePair<string, double> kv in map)
        { double d = Math.Abs(x - kv.Value); if (d < bestDistance) { bestDistance = d; bestName = kv.Key; } }
        return bestName;
    }

    private static bool TryParseBareRobotLabel(string value, out int robotNo)
    {
        robotNo = 0; if (String.IsNullOrWhiteSpace(value)) return false;
        Match m = Regex.Match(value.Trim(), @"^R\s*0*(\d{1,2})$", RegexOptions.IgnoreCase); if (!m.Success) return false;
        return Int32.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out robotNo);
    }

    private static string NumericSuffix(string name)
    {
        Match m = Regex.Match(name ?? String.Empty, @"(\d+)$"); if (!m.Success) return "000"; int value;
        if (!Int32.TryParse(m.Groups[1].Value, out value)) return "000"; return value.ToString("000");
    }

    private static string NormalizeCadText(string text)
    {
        if (text == null) return String.Empty; string value = text.Replace("\\P", " ").Trim();
        value = Regex.Replace(value, @"\\[A-Za-z][^;]*;", String.Empty); value = value.Replace("{", String.Empty).Replace("}", String.Empty).Trim(); return value;
    }

    private static double NormalizeDegrees(double value)
    { value = value % 360.0; if (value < 0.0) value += 360.0; if (Math.Abs(value - 360.0) < 1e-9) value = 0.0; return value; }
    private static double Repair2DZ(double z)
    { if (Double.IsNaN(z) || Double.IsInfinity(z) || Math.Abs(z) > MaxAbsTargetZMm) return Default2DRobotZMm; return z; }
    private static bool IsRobotType(string type)
    { return String.Equals(type, "ROBOT", StringComparison.OrdinalIgnoreCase) || String.Equals(type, "TRACK_ROBOT", StringComparison.OrdinalIgnoreCase); }

    private static Dictionary<string, TxFrame> CollectFrameMap(TxPhysicalRoot physicalRoot)
    {
        Dictionary<string, TxFrame> result = new Dictionary<string, TxFrame>(StringComparer.OrdinalIgnoreCase);
        TxObjectList frames = physicalRoot.GetAllDescendants(new TxTypeFilter(typeof(TxFrame)));
        foreach (ITxObject item in frames)
        { TxFrame frame = item as TxFrame; if (frame != null && !String.IsNullOrEmpty(frame.Name) && !result.ContainsKey(frame.Name)) result[frame.Name] = frame; }
        return result;
    }

    private static TxFrame CreateFrame(TxPhysicalRoot physicalRoot, FrameSpec s)
    {
        Type dataType = FindType("TxFrameCreationData"); if (dataType == null) throw new InvalidOperationException("TxFrameCreationData type not found.");
        double d2r = Math.PI / 180.0;
        TxTransformation tr = new TxTransformation(new TxVector(s.X, s.Y, s.Z), new TxVector(s.Rx * d2r, s.Ry * d2r, s.Rz * d2r), TxTransformation.TxRotationType.RPY_XYZ);
        object data = MakeFrameCreationData(dataType, s.Name, tr); if (data == null) throw new InvalidOperationException("Could not construct TxFrameCreationData.");
        MethodInfo create = FindCreateFrameMethod(physicalRoot.GetType(), dataType); if (create == null) throw new InvalidOperationException("No compatible CreateFrame method.");
        object created = create.Invoke(physicalRoot, new object[] { data }); if (created == null) throw new InvalidOperationException("CreateFrame returned null.");
        SetProperty(created, "AbsoluteLocation", tr); SetProperty(created, "Location", tr);
        TxFrame frame = created as TxFrame; if (frame == null) throw new InvalidOperationException("Created object is not a TxFrame."); return frame;
    }

    private static ITxComponent EnsureTemplate(TxPhysicalRoot physicalRoot, string name, string path, HashSet<string> objectNames, StringWriter output)
    {
        if (objectNames.Contains(name))
        {
            ITxComponent existingComponent = FindObjectByName(physicalRoot, name) as ITxComponent;
            if (existingComponent != null) { output.WriteLine("SKIP TEMPLATE " + name + ": already exists."); return existingComponent; }
        }
        TxTransformation origin = WorldOrigin(); ITxComponent inserted = InsertComponent(physicalRoot, name, path, origin);
        ITxLocatableObject loc = inserted as ITxLocatableObject; if (loc != null && loc.CanLocate()) loc.AbsoluteLocation = origin;
        objectNames.Add(name); output.WriteLine("OK TEMPLATE " + name + " @ (0,0,0)"); return inserted;
    }

    private static ITxComponent InsertComponent(TxPhysicalRoot physicalRoot, string instanceName, string path, TxTransformation location)
    {
        ITxComponent inserted;
        using (TxInsertComponentCreationData data = new TxInsertComponentCreationData(instanceName, path))
        { data.AbsoluteLocation = new TxTransformation(location); inserted = physicalRoot.InsertComponent(data); }
        if (inserted == null) throw new InvalidOperationException("InsertComponent returned null: " + instanceName); return inserted;
    }

    private static TxTransformation WorldOrigin()
    { return new TxTransformation(new TxVector(0.0, 0.0, 0.0), new TxVector(0.0, 0.0, 0.0), TxTransformation.TxRotationType.RPY_XYZ); }

    private static void AlignRobotBase(ITxLocatableObject insertedRoot, TxRobot robot, TxFrame targetFrame)
    {
        TxFrame robotBase = robot.Baseframe; if (robotBase == null) throw new InvalidOperationException("Robot Baseframe is missing.");
        TxTransformation baseRelativeToRoot = robotBase.GetLocationRelativeToObject(insertedRoot);
        insertedRoot.AbsoluteLocation = targetFrame.AbsoluteLocation * baseRelativeToRoot.Inverse;
        TxTransformation residual = robotBase.GetLocationRelativeToObject(targetFrame); TxVector delta = residual.Translation; TxVector rotation = residual.RotationRPY_XYZ;
        double positionError = Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y + delta.Z * delta.Z);
        double angleErrorRad = Math.Max(Math.Abs(NormalizeAngle(rotation.X)), Math.Max(Math.Abs(NormalizeAngle(rotation.Y)), Math.Abs(NormalizeAngle(rotation.Z))));
        double angleErrorDeg = angleErrorRad * 180.0 / Math.PI;
        if (positionError > 0.1 || angleErrorDeg > 0.01)
            throw new InvalidOperationException("Robot Baseframe alignment outside tolerance: " + positionError.ToString("F6", CultureInfo.InvariantCulture) + " mm, " + angleErrorDeg.ToString("F6", CultureInfo.InvariantCulture) + " deg");
    }

    private static TxTransformation ApplyFenceOffset(TxTransformation frameLocation)
    {
        double d2r = Math.PI / 180.0;
        TxTransformation offset = new TxTransformation(new TxVector(FenceOffsetX, FenceOffsetY, FenceOffsetZ), new TxVector(0.0, 0.0, FenceOffsetRzDeg * d2r), TxTransformation.TxRotationType.RPY_XYZ);
        return frameLocation * offset;
    }

    private static void ValidateInputs()
    {
        if (!File.Exists(DxfPath)) throw new FileNotFoundException("DXF was not found. Edit DxfPath at the top of the script.", DxfPath);
        if (!Directory.Exists(RobotPackagePath)) throw new DirectoryNotFoundException("Robot COJT package folder was not found: " + RobotPackagePath);
        string[] robotRequired = new string[] { "BSR_OP10_R01_KR210_R2700.jt", "kin_graph.xml", "motionparameters.e", "TuneData.xml" };
        for (int i = 0; i < robotRequired.Length; i++) { string path = Path.Combine(RobotPackagePath, robotRequired[i]); if (!File.Exists(path)) throw new FileNotFoundException("Robot package is incomplete.", path); }
        if (!File.Exists(FenceJtPath)) throw new FileNotFoundException("Safe Fence JT was not found. Copy BSL_SF-0044.jt there or edit FenceJtPath.", FenceJtPath);
    }

    private static HashSet<string> CollectObjectNames(TxPhysicalRoot physicalRoot)
    {
        HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase); TxObjectList objects = physicalRoot.GetAllDescendants(new TxNoTypeFilter());
        foreach (ITxObject item in objects) if (item != null && !String.IsNullOrEmpty(item.Name)) result.Add(item.Name); return result;
    }
    private static TxRobot FindRobot(ITxComponent inserted)
    {
        TxRobot robot = inserted as TxRobot; if (robot != null) return robot; ITxObjectCollection collection = inserted as ITxObjectCollection; if (collection == null) return null;
        TxObjectList robots = collection.GetAllDescendants(new TxTypeFilter(typeof(TxRobot)));
        foreach (ITxObject item in robots) { robot = item as TxRobot; if (robot != null) return robot; } return null;
    }
    private static ITxObject FindObjectByName(TxPhysicalRoot physicalRoot, string name)
    {
        TxObjectList objects = physicalRoot.GetAllDescendants(new TxNoTypeFilter());
        foreach (ITxObject item in objects) if (String.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)) return item; return null;
    }
    private static void RemoveIncompleteInsert(ITxComponent inserted, StringWriter output)
    {
        try { if (inserted != null && inserted.CanBeDeleted) { inserted.Delete(); output.WriteLine("     incomplete instance removed."); } }
        catch (Exception ex) { output.WriteLine("     cleanup warning: " + ex.Message); }
    }
    private static void TryDelete(ITxComponent component, StringWriter output)
    {
        try { if (component != null && component.CanBeDeleted) component.Delete(); }
        catch (Exception ex) { output.WriteLine("Template cleanup warning: " + ex.Message); }
    }
    private static double NormalizeAngle(double radians)
    { while (radians > Math.PI) radians -= 2.0 * Math.PI; while (radians < -Math.PI) radians += 2.0 * Math.PI; return radians; }
    private static Exception Unwrap(Exception ex)
    { Exception detail = ex; while (detail.InnerException != null) detail = detail.InnerException; return detail; }
    private static Type FindType(string shortName)
    {
        Type[] types = typeof(TxApplication).Assembly.GetTypes(); for (int i = 0; i < types.Length; i++) if (types[i].Name == shortName) return types[i]; return null;
    }
    private static object MakeFrameCreationData(Type type, string name, TxTransformation tr)
    {
        ConstructorInfo[] constructors = type.GetConstructors();
        for (int c = 0; c < constructors.Length; c++)
        {
            ConstructorInfo ctor = constructors[c]; ParameterInfo[] ps = ctor.GetParameters(); object[] args = new object[ps.Length];
            try
            {
                for (int i = 0; i < ps.Length; i++)
                {
                    Type pt = ps[i].ParameterType;
                    if (pt == typeof(string)) args[i] = name; else if (pt == typeof(TxTransformation)) args[i] = tr;
                    else if (ps[i].HasDefaultValue) args[i] = ps[i].DefaultValue; else if (pt.IsValueType) args[i] = Activator.CreateInstance(pt); else args[i] = null;
                }
                object data = ctor.Invoke(args); SetProperty(data, "Name", name); SetProperty(data, "AbsoluteLocation", tr); SetProperty(data, "Location", tr); SetProperty(data, "Transformation", tr); return data;
            }
            catch { }
        }
        return null;
    }
    private static MethodInfo FindCreateFrameMethod(Type rootType, Type dataType)
    {
        MethodInfo[] methods = rootType.GetMethods(BindingFlags.Instance | BindingFlags.Public);
        for (int i = 0; i < methods.Length; i++)
        {
            MethodInfo m = methods[i]; if (!String.Equals(m.Name, "CreateFrame", StringComparison.Ordinal)) continue;
            ParameterInfo[] ps = m.GetParameters(); if (ps.Length == 1 && ps[0].ParameterType.IsAssignableFrom(dataType)) return m;
        }
        return null;
    }
    private static void SetProperty(object obj, string name, object value)
    {
        if (obj == null || value == null) return; PropertyInfo p = obj.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
        if (p == null || !p.CanWrite || !p.PropertyType.IsAssignableFrom(value.GetType())) return;
        try { p.SetValue(obj, value, null); } catch { }
    }
}
