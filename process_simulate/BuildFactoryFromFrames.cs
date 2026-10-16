using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Tecnomatix.Engineering;

// Process Simulate 2512 .NET Script
//
// Minimal workflow:
// 1. Read FactoryPlacementFrames.csv generated from DXF.
// 2. Create all robot/fence Frames.
// 3. Import one approved Robot template at world origin (0,0,0), then create
//    one package instance per ROBOT/TRACK_ROBOT Frame and align Robot.Baseframe.
// 4. Import the supplied Safe Fence JT at world origin, then create one JT
//    instance per FENCE Frame and move it to that Frame.
//
// Naming standard:
//   Robot frame      : <AREA>_ROBOT_<NNN>
//   Track robot frame: <AREA>_TRACK_ROBOT_<NNN>
//   Fence frame      : <AREA>_FENCE_<NNN>
//
// Template instances:
//   TEMPLATE_ROBOT_KR210_R2700
//   TEMPLATE_FENCE_BSL_SF_0044
//
// Placement instances:
//   AUTO_<FRAME_NAME>_KR210_R2700
//   AUTO_<FRAME_NAME>_BSL_SF_0044
//
// NOTE ABOUT "COPY":
// Process Simulate API copy/paste behavior varies by resource type/version.
// For reliability this script uses repeated InsertComponent calls with the same
// approved source package/JT. Functionally each placement is a copy, while the
// original template remains at the world origin for inspection.

public class MainScript
{
    // ----- Local configuration -----
    private const string FrameCsvPath =
        @"D:\WEDING\MOVING\work261007\Library\FactoryPlacementFrames.csv";

    private const string RobotPackagePath =
        @"D:\WEDING\MOVING\work261007\Library\BSR_OP10_R01_KR210_R2700.cojt";

    private const string FenceJtPath =
        @"D:\WEDING\MOVING\work261007\Library\BSL_SF-0044.jt";

    private const string RobotModelTag = "KR210_R2700";
    private const string FenceModelTag = "BSL_SF_0044";

    private const string RobotTemplateName = "TEMPLATE_ROBOT_KR210_R2700";
    private const string FenceTemplateName = "TEMPLATE_FENCE_BSL_SF_0044";

    // The supplied JT is assumed to use the desired fence placement origin.
    // If its local origin does not match the CAD fence Frame, calibrate here.
    private const double FenceOffsetX = 0.0;
    private const double FenceOffsetY = 0.0;
    private const double FenceOffsetZ = 0.0;
    private const double FenceOffsetRzDeg = 0.0;

    private const bool IncludeTrackRobotFrames = true;
    private const bool KeepTemplatesAtOrigin = true;
    private const double MaxAbsTargetZMm = 10000.0;

    public static void Main()
    {
        StringWriter output = new StringWriter();

        try
        {
            Run(ref output);
        }
        catch (Exception ex)
        {
            Exception detail = Unwrap(ex);
            output.WriteLine("");
            output.WriteLine("FATAL: " + detail.GetType().Name + " - " + detail.Message);
        }

        TxMessageBox.Show(
            output.ToString(),
            "DXF Frames + Robot + Safe Fence",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private sealed class FrameSpec
    {
        public string Name;
        public string Type;
        public string Area;
        public double X;
        public double Y;
        public double Z;
        public double Rx;
        public double Ry;
        public double Rz;
    }

    private static void Run(ref StringWriter output)
    {
        TxDocument document = TxApplication.ActiveDocument;
        if (document == null)
            throw new InvalidOperationException("No active Process Simulate study.");

        TxPhysicalRoot physicalRoot = document.PhysicalRoot;
        if (physicalRoot == null)
            throw new InvalidOperationException("Active study has no PhysicalRoot.");

        ValidateInputs();

        List<FrameSpec> specs = LoadFrameSpecs(FrameCsvPath);
        output.WriteLine("Frame CSV : " + FrameCsvPath);
        output.WriteLine("Rows      : " + specs.Count);
        output.WriteLine("");

        int frameOk = 0;
        int frameSkip = 0;
        int frameFail = 0;

        // ----------------------------------------------------
        // 1) Create Frames
        // ----------------------------------------------------
        foreach (FrameSpec spec in specs)
        {
            try
            {
                if (FrameExists(physicalRoot, spec.Name))
                {
                    output.WriteLine("SKIP FRAME " + spec.Name);
                    frameSkip++;
                    continue;
                }

                CreateFrame(physicalRoot, spec);
                output.WriteLine("OK   FRAME " + spec.Name);
                frameOk++;
            }
            catch (Exception ex)
            {
                output.WriteLine(
                    "FAIL FRAME " + spec.Name + ": " + Unwrap(ex).Message);
                frameFail++;
            }
        }

        output.WriteLine("");
        output.WriteLine(
            "Frame result: OK=" + frameOk +
            " SKIP=" + frameSkip +
            " FAIL=" + frameFail);
        output.WriteLine("");

        // ----------------------------------------------------
        // 2) Templates at world origin
        // ----------------------------------------------------
        ITxComponent robotTemplate = EnsureTemplate(
            physicalRoot,
            RobotTemplateName,
            RobotPackagePath,
            output);

        ITxComponent fenceTemplate = EnsureTemplate(
            physicalRoot,
            FenceTemplateName,
            FenceJtPath,
            output);

        // Verify the robot package really is articulated.
        TxRobot templateRobot = FindRobot(robotTemplate);
        if (templateRobot == null)
            throw new InvalidOperationException(
                "Robot template was inserted but no TxRobot was found. " +
                "Use the complete COJT package, not only its inner JT.");

        int joints = templateRobot.Joints == null ? 0 : templateRobot.Joints.Count;
        int driving = templateRobot.DrivingJoints == null ? 0 : templateRobot.DrivingJoints.Count;
        output.WriteLine(
            "Robot template at origin: joints=" + joints +
            " driving=" + driving);

        if (joints < 6 || driving < 6)
            throw new InvalidOperationException(
                "Robot template has fewer than six joints/driving joints.");

        output.WriteLine("Fence template at origin: " + FenceTemplateName);
        output.WriteLine("");

        // ----------------------------------------------------
        // 3) Place Robot/Fence instances on Frames
        // ----------------------------------------------------
        int robotOk = 0;
        int robotSkip = 0;
        int robotFail = 0;
        int fenceOk = 0;
        int fenceSkip = 0;
        int fenceFail = 0;

        foreach (FrameSpec spec in specs)
        {
            TxFrame frame = FindFrame(physicalRoot, spec.Name);
            if (frame == null)
            {
                output.WriteLine("FAIL TARGET " + spec.Name + ": Frame not found after creation.");
                continue;
            }

            if (Math.Abs(frame.AbsoluteLocation.Translation.Z) > MaxAbsTargetZMm)
            {
                output.WriteLine(
                    "FAIL TARGET " + spec.Name +
                    ": suspicious Z=" +
                    frame.AbsoluteLocation.Translation.Z.ToString("F3") + " mm");
                continue;
            }

            if (IsRobotType(spec.Type))
            {
                if (String.Equals(spec.Type, "TRACK_ROBOT", StringComparison.OrdinalIgnoreCase)
                    && !IncludeTrackRobotFrames)
                    continue;

                string instanceName =
                    "AUTO_" + spec.Name + "_" + RobotModelTag;

                if (FindObjectByName(physicalRoot, instanceName) != null)
                {
                    output.WriteLine("SKIP ROBOT " + spec.Name + " -> " + instanceName);
                    robotSkip++;
                    continue;
                }

                ITxComponent inserted = null;
                try
                {
                    inserted = InsertComponent(
                        physicalRoot,
                        instanceName,
                        RobotPackagePath,
                        frame.AbsoluteLocation);

                    ITxLocatableObject root = inserted as ITxLocatableObject;
                    TxRobot robot = FindRobot(inserted);

                    if (root == null || !root.CanLocate())
                        throw new InvalidOperationException("Inserted Robot root is not locatable.");

                    if (robot == null)
                        throw new InvalidOperationException("Inserted COJT contains no TxRobot.");

                    AlignRobotBase(root, robot, frame);
                    output.WriteLine("OK   ROBOT " + spec.Name + " -> " + instanceName);
                    robotOk++;
                }
                catch (Exception ex)
                {
                    RemoveIncompleteInsert(inserted, output);
                    output.WriteLine(
                        "FAIL ROBOT " + spec.Name + ": " + Unwrap(ex).Message);
                    robotFail++;
                }
            }
            else if (String.Equals(spec.Type, "FENCE", StringComparison.OrdinalIgnoreCase))
            {
                string instanceName =
                    "AUTO_" + spec.Name + "_" + FenceModelTag;

                if (FindObjectByName(physicalRoot, instanceName) != null)
                {
                    output.WriteLine("SKIP FENCE " + spec.Name + " -> " + instanceName);
                    fenceSkip++;
                    continue;
                }

                ITxComponent inserted = null;
                try
                {
                    TxTransformation target = ApplyFenceOffset(frame.AbsoluteLocation);
                    inserted = InsertComponent(
                        physicalRoot,
                        instanceName,
                        FenceJtPath,
                        target);

                    ITxLocatableObject root = inserted as ITxLocatableObject;
                    if (root == null || !root.CanLocate())
                        throw new InvalidOperationException("Inserted Fence JT is not locatable.");

                    root.AbsoluteLocation = target;
                    output.WriteLine("OK   FENCE " + spec.Name + " -> " + instanceName);
                    fenceOk++;
                }
                catch (Exception ex)
                {
                    RemoveIncompleteInsert(inserted, output);
                    output.WriteLine(
                        "FAIL FENCE " + spec.Name + ": " + Unwrap(ex).Message);
                    fenceFail++;
                }
            }
        }

        if (!KeepTemplatesAtOrigin)
        {
            TryDelete(robotTemplate, output);
            TryDelete(fenceTemplate, output);
        }

        output.WriteLine("");
        output.WriteLine("================ SUMMARY ================");
        output.WriteLine(
            "Frames: OK=" + frameOk + " SKIP=" + frameSkip + " FAIL=" + frameFail);
        output.WriteLine(
            "Robot : OK=" + robotOk + " SKIP=" + robotSkip + " FAIL=" + robotFail);
        output.WriteLine(
            "Fence : OK=" + fenceOk + " SKIP=" + fenceSkip + " FAIL=" + fenceFail);
        output.WriteLine("=========================================");
    }

    private static bool IsRobotType(string type)
    {
        return String.Equals(type, "ROBOT", StringComparison.OrdinalIgnoreCase)
            || String.Equals(type, "TRACK_ROBOT", StringComparison.OrdinalIgnoreCase);
    }

    // --------------------------------------------------------
    // CSV
    // --------------------------------------------------------
    private static List<FrameSpec> LoadFrameSpecs(string path)
    {
        List<FrameSpec> result = new List<FrameSpec>();
        string[] lines = File.ReadAllLines(path, Encoding.UTF8);
        if (lines.Length == 0)
            return result;

        string header = lines[0].TrimStart('\uFEFF');
        string[] columns = header.Split(';');
        Dictionary<string, int> index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < columns.Length; i++)
            index[columns[i].Trim()] = i;

        string[] required = new string[] { "name", "type", "area", "x", "y", "z", "rx", "ry", "rz" };
        foreach (string col in required)
            if (!index.ContainsKey(col))
                throw new InvalidDataException("Frame CSV missing column: " + col);

        for (int lineNo = 1; lineNo < lines.Length; lineNo++)
        {
            string line = lines[lineNo];
            if (String.IsNullOrWhiteSpace(line))
                continue;

            string[] p = line.Split(';');
            if (p.Length < columns.Length)
                throw new InvalidDataException("Malformed Frame CSV line " + (lineNo + 1));

            FrameSpec s = new FrameSpec();
            s.Name = p[index["name"]].Trim();
            s.Type = p[index["type"]].Trim();
            s.Area = p[index["area"]].Trim();
            s.X = ParseDouble(p[index["x"]]);
            s.Y = ParseDouble(p[index["y"]]);
            s.Z = ParseDouble(p[index["z"]]);
            s.Rx = ParseDouble(p[index["rx"]]);
            s.Ry = ParseDouble(p[index["ry"]]);
            s.Rz = ParseDouble(p[index["rz"]]);

            if (!String.IsNullOrEmpty(s.Name))
                result.Add(s);
        }

        return result;
    }

    private static double ParseDouble(string value)
    {
        return Double.Parse(
            value.Trim(),
            NumberStyles.Float | NumberStyles.AllowThousands,
            CultureInfo.InvariantCulture);
    }

    // --------------------------------------------------------
    // Frame creation
    // --------------------------------------------------------
    private static void CreateFrame(TxPhysicalRoot physicalRoot, FrameSpec s)
    {
        Type dataType = FindType("TxFrameCreationData");
        if (dataType == null)
            throw new InvalidOperationException("TxFrameCreationData type not found.");

        double d2r = Math.PI / 180.0;
        TxTransformation tr = new TxTransformation(
            new TxVector(s.X, s.Y, s.Z),
            new TxVector(s.Rx * d2r, s.Ry * d2r, s.Rz * d2r),
            TxTransformation.TxRotationType.RPY_XYZ);

        object data = MakeFrameCreationData(dataType, s.Name, tr);
        if (data == null)
            throw new InvalidOperationException("Could not construct TxFrameCreationData.");

        MethodInfo create = FindCreateFrameMethod(physicalRoot.GetType(), dataType);
        if (create == null)
            throw new InvalidOperationException("No compatible CreateFrame method.");

        object frame = create.Invoke(physicalRoot, new object[] { data });
        if (frame == null)
            throw new InvalidOperationException("CreateFrame returned null.");

        SetProperty(frame, "AbsoluteLocation", tr);
        SetProperty(frame, "Location", tr);
    }

    private static bool FrameExists(TxPhysicalRoot physicalRoot, string name)
    {
        return FindFrame(physicalRoot, name) != null;
    }

    private static TxFrame FindFrame(TxPhysicalRoot physicalRoot, string name)
    {
        TxObjectList frames =
            physicalRoot.GetAllDescendants(new TxTypeFilter(typeof(TxFrame)));

        foreach (ITxObject item in frames)
        {
            TxFrame frame = item as TxFrame;
            if (frame != null &&
                String.Equals(frame.Name, name, StringComparison.OrdinalIgnoreCase))
                return frame;
        }
        return null;
    }

    // --------------------------------------------------------
    // Templates / insertion
    // --------------------------------------------------------
    private static ITxComponent EnsureTemplate(
        TxPhysicalRoot physicalRoot,
        string name,
        string path,
        StringWriter output)
    {
        ITxObject existing = FindObjectByName(physicalRoot, name);
        ITxComponent existingComponent = existing as ITxComponent;
        if (existingComponent != null)
        {
            output.WriteLine("SKIP TEMPLATE " + name + ": already exists at study.");
            return existingComponent;
        }

        TxTransformation origin = new TxTransformation(
            new TxVector(0.0, 0.0, 0.0),
            new TxVector(0.0, 0.0, 0.0),
            TxTransformation.TxRotationType.RPY_XYZ);

        ITxComponent inserted = InsertComponent(
            physicalRoot,
            name,
            path,
            origin);

        ITxLocatableObject loc = inserted as ITxLocatableObject;
        if (loc != null && loc.CanLocate())
            loc.AbsoluteLocation = origin;

        output.WriteLine("OK   TEMPLATE " + name + " @ (0,0,0)");
        return inserted;
    }

    private static ITxComponent InsertComponent(
        TxPhysicalRoot physicalRoot,
        string instanceName,
        string path,
        TxTransformation location)
    {
        ITxComponent inserted;
        using (TxInsertComponentCreationData data =
            new TxInsertComponentCreationData(instanceName, path))
        {
            data.AbsoluteLocation = new TxTransformation(location);
            inserted = physicalRoot.InsertComponent(data);
        }

        if (inserted == null)
            throw new InvalidOperationException(
                "InsertComponent returned null: " + instanceName);

        return inserted;
    }

    // --------------------------------------------------------
    // Robot Base alignment
    // --------------------------------------------------------
    private static void AlignRobotBase(
        ITxLocatableObject insertedRoot,
        TxRobot robot,
        TxFrame targetFrame)
    {
        TxFrame robotBase = robot.Baseframe;
        if (robotBase == null)
            throw new InvalidOperationException("Robot Baseframe is missing.");

        TxTransformation baseRelativeToRoot =
            robotBase.GetLocationRelativeToObject(insertedRoot);

        insertedRoot.AbsoluteLocation =
            targetFrame.AbsoluteLocation * baseRelativeToRoot.Inverse;

        TxTransformation residual =
            robotBase.GetLocationRelativeToObject(targetFrame);

        TxVector delta = residual.Translation;
        TxVector rotation = residual.RotationRPY_XYZ;

        double positionError = Math.Sqrt(
            delta.X * delta.X + delta.Y * delta.Y + delta.Z * delta.Z);

        double angleErrorRad = Math.Max(
            Math.Abs(NormalizeAngle(rotation.X)),
            Math.Max(
                Math.Abs(NormalizeAngle(rotation.Y)),
                Math.Abs(NormalizeAngle(rotation.Z))));

        double angleErrorDeg = angleErrorRad * 180.0 / Math.PI;

        if (positionError > 0.1 || angleErrorDeg > 0.01)
            throw new InvalidOperationException(
                "Robot Baseframe alignment outside tolerance: " +
                positionError.ToString("F6") + " mm, " +
                angleErrorDeg.ToString("F6") + " deg");
    }

    // Fence JT has no TxRobot Baseframe. Target Frame drives its root location.
    private static TxTransformation ApplyFenceOffset(TxTransformation frameLocation)
    {
        double d2r = Math.PI / 180.0;
        TxTransformation offset = new TxTransformation(
            new TxVector(FenceOffsetX, FenceOffsetY, FenceOffsetZ),
            new TxVector(0.0, 0.0, FenceOffsetRzDeg * d2r),
            TxTransformation.TxRotationType.RPY_XYZ);
        return frameLocation * offset;
    }

    // --------------------------------------------------------
    // Resource validation
    // --------------------------------------------------------
    private static void ValidateInputs()
    {
        if (!File.Exists(FrameCsvPath))
            throw new FileNotFoundException("Frame CSV was not found.", FrameCsvPath);

        if (!Directory.Exists(RobotPackagePath))
            throw new DirectoryNotFoundException(
                "Robot COJT package folder was not found: " + RobotPackagePath);

        string[] robotRequired = new string[]
        {
            "BSR_OP10_R01_KR210_R2700.jt",
            "kin_graph.xml",
            "motionparameters.e",
            "TuneData.xml"
        };

        foreach (string file in robotRequired)
        {
            string path = Path.Combine(RobotPackagePath, file);
            if (!File.Exists(path))
                throw new FileNotFoundException("Robot package is incomplete.", path);
        }

        if (!File.Exists(FenceJtPath))
            throw new FileNotFoundException(
                "Safe Fence JT was not found. Copy the supplied BSL_SF-0044.jt to this path or edit FenceJtPath.",
                FenceJtPath);
    }

    // --------------------------------------------------------
    // Generic helpers
    // --------------------------------------------------------
    private static TxRobot FindRobot(ITxComponent inserted)
    {
        TxRobot robot = inserted as TxRobot;
        if (robot != null)
            return robot;

        ITxObjectCollection collection = inserted as ITxObjectCollection;
        if (collection == null)
            return null;

        TxObjectList robots =
            collection.GetAllDescendants(new TxTypeFilter(typeof(TxRobot)));

        foreach (ITxObject item in robots)
        {
            robot = item as TxRobot;
            if (robot != null)
                return robot;
        }
        return null;
    }

    private static ITxObject FindObjectByName(
        TxPhysicalRoot physicalRoot,
        string name)
    {
        TxObjectList objects =
            physicalRoot.GetAllDescendants(new TxNoTypeFilter());

        foreach (ITxObject item in objects)
            if (String.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))
                return item;

        return null;
    }

    private static void RemoveIncompleteInsert(
        ITxComponent inserted,
        StringWriter output)
    {
        try
        {
            if (inserted != null && inserted.CanBeDeleted)
            {
                inserted.Delete();
                output.WriteLine("     incomplete instance removed.");
            }
        }
        catch (Exception ex)
        {
            output.WriteLine("     cleanup warning: " + ex.Message);
        }
    }

    private static void TryDelete(ITxComponent component, StringWriter output)
    {
        try
        {
            if (component != null && component.CanBeDeleted)
                component.Delete();
        }
        catch (Exception ex)
        {
            output.WriteLine("Template cleanup warning: " + ex.Message);
        }
    }

    private static double NormalizeAngle(double radians)
    {
        while (radians > Math.PI)
            radians -= 2.0 * Math.PI;
        while (radians < -Math.PI)
            radians += 2.0 * Math.PI;
        return radians;
    }

    private static Exception Unwrap(Exception ex)
    {
        Exception detail = ex;
        while (detail.InnerException != null)
            detail = detail.InnerException;
        return detail;
    }

    private static Type FindType(string shortName)
    {
        foreach (Type t in typeof(TxApplication).Assembly.GetTypes())
            if (t.Name == shortName)
                return t;
        return null;
    }

    private static object MakeFrameCreationData(
        Type type,
        string name,
        TxTransformation tr)
    {
        foreach (ConstructorInfo ctor in type.GetConstructors())
        {
            ParameterInfo[] ps = ctor.GetParameters();
            object[] args = new object[ps.Length];
            try
            {
                for (int i = 0; i < ps.Length; i++)
                {
                    Type pt = ps[i].ParameterType;
                    if (pt == typeof(string))
                        args[i] = name;
                    else if (pt == typeof(TxTransformation))
                        args[i] = tr;
                    else if (ps[i].HasDefaultValue)
                        args[i] = ps[i].DefaultValue;
                    else if (pt.IsValueType)
                        args[i] = Activator.CreateInstance(pt);
                    else
                        args[i] = null;
                }

                object data = ctor.Invoke(args);
                SetProperty(data, "Name", name);
                SetProperty(data, "AbsoluteLocation", tr);
                SetProperty(data, "Location", tr);
                SetProperty(data, "Transformation", tr);
                return data;
            }
            catch { }
        }
        return null;
    }

    private static MethodInfo FindCreateFrameMethod(
        Type rootType,
        Type dataType)
    {
        foreach (MethodInfo method in rootType.GetMethods(
            BindingFlags.Instance | BindingFlags.Public))
        {
            if (method.Name != "CreateFrame")
                continue;

            ParameterInfo[] ps = method.GetParameters();
            if (ps.Length == 1 && ps[0].ParameterType.IsAssignableFrom(dataType))
                return method;
        }
        return null;
    }

    private static void SetProperty(
        object obj,
        string name,
        object value)
    {
        if (obj == null || value == null)
            return;

        PropertyInfo p = obj.GetType().GetProperty(
            name,
            BindingFlags.Instance | BindingFlags.Public);

        if (p == null || !p.CanWrite ||
            !p.PropertyType.IsAssignableFrom(value.GetType()))
            return;

        try { p.SetValue(obj, value, null); }
        catch { }
    }
}
