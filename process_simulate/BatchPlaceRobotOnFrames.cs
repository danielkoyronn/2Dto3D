using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Tecnomatix.Engineering;

// Process Simulate 2512 .NET Script
//
// Purpose:
//   Reuse one approved articulated COJT robot package and place one instance on
//   every generated robot Frame (for example BSR_ROBOT_001, EC_ROBOT_014,
//   MID_MAIN_TRACK_ROBOT_003). The imported robot's real Baseframe is aligned
//   exactly to the target TxFrame.
//
// Important:
//   - This does NOT guess which robot model belongs to a CAD symbol.
//   - All matching Frames currently receive the same user-approved robot package.
//   - TRACK_ROBOT Frames can be included, but this script only places the robot
//     arm; it does not create the linear rail / 7th axis.
//   - Safe fence insertion will be added after the user supplies that resource.

public class MainScript
{
    public static void Main()
    {
        StringWriter output = new StringWriter();
        BatchRobotPlacement.Run(ref output);
        TxMessageBox.Show(
            output.ToString(),
            "Batch Robot Placement",
            System.Windows.Forms.MessageBoxButtons.OK,
            System.Windows.Forms.MessageBoxIcon.Information);
    }
}

internal static class BatchRobotPlacement
{
    // Approved complete articulated COJT package.
    // Change only when you intentionally want to use another robot package.
    private const string RobotPackagePath =
        @"D:\WEDING\MOVING\work261007\Library\BSR_OP10_R01_KR210_R2700.cojt";

    private const string RobotModelTag = "KR210_R2700";

    // true: *_TRACK_ROBOT_* Frames also get the robot arm.
    // false: only normal *_ROBOT_* Frames are populated.
    private const bool IncludeTrackRobotFrames = true;

    // Use true for a no-change inspection run.
    private const bool DryRun = false;

    // Protect against corrupted frame data such as an accidental multi-kilometer Z.
    private const double MaxAbsTargetZMm = 10000.0;

    public static void Run(ref StringWriter output)
    {
        StringBuilder report = new StringBuilder();

        try
        {
            Execute(report);
        }
        catch (Exception ex)
        {
            Exception detail = Unwrap(ex);
            report.AppendLine("BATCH ROBOT ERROR: " + detail.GetType().Name);
            report.AppendLine(detail.Message);
        }

        output.Write(report.ToString());
    }

    private static void Execute(StringBuilder report)
    {
        TxDocument document = TxApplication.ActiveDocument;
        if (document == null)
            throw new InvalidOperationException("No active study.");

        TxPhysicalRoot physicalRoot = document.PhysicalRoot;
        if (physicalRoot == null)
            throw new InvalidOperationException("Active study has no PhysicalRoot.");

        ValidateRobotPackage();

        List<TxFrame> targets = CollectRobotFrames(physicalRoot);

        report.AppendLine("Robot package : " + RobotPackagePath);
        report.AppendLine("Robot Frames  : " + targets.Count);
        report.AppendLine("Track Frames  : " +
            (IncludeTrackRobotFrames ? "included" : "excluded"));
        report.AppendLine("Dry run       : " + DryRun);
        report.AppendLine("");

        int success = 0;
        int skipped = 0;
        int failed = 0;

        for (int i = 0; i < targets.Count; i++)
        {
            TxFrame targetFrame = targets[i];
            string instanceName = BuildInstanceName(targetFrame.Name);

            double targetZ = targetFrame.AbsoluteLocation.Translation.Z;
            if (Math.Abs(targetZ) > MaxAbsTargetZMm)
            {
                report.AppendLine(
                    "FAIL " + targetFrame.Name +
                    ": suspicious target Z=" + targetZ.ToString("F3") + " mm");
                failed++;
                continue;
            }

            if (FindObjectByName(physicalRoot, instanceName) != null)
            {
                report.AppendLine(
                    "SKIP " + targetFrame.Name +
                    ": instance already exists -> " + instanceName);
                skipped++;
                continue;
            }

            if (DryRun)
            {
                report.AppendLine(
                    "DRY  " + targetFrame.Name + " -> " + instanceName);
                skipped++;
                continue;
            }

            ITxComponent inserted = null;
            try
            {
                inserted = InsertOneRobot(
                    physicalRoot, targetFrame, instanceName, report);
                success++;
            }
            catch (Exception ex)
            {
                Exception detail = Unwrap(ex);
                report.AppendLine(
                    "FAIL " + targetFrame.Name + ": " +
                    detail.GetType().Name + " - " + detail.Message);
                RemoveIncompleteInsert(inserted, report);
                failed++;
            }
        }

        report.AppendLine("");
        report.AppendLine("Batch robot placement finished.");
        report.AppendLine("SUCCESS : " + success);
        report.AppendLine("SKIP    : " + skipped);
        report.AppendLine("FAIL    : " + failed);
        report.AppendLine("TOTAL   : " + targets.Count);
        report.AppendLine("");
        report.AppendLine(
            "NOTE: all targets currently receive the same approved COJT package.");
        report.AppendLine(
            "No automatic CAD-to-robot-model mapping is performed.");

        if (IncludeTrackRobotFrames)
        {
            report.AppendLine(
                "TRACK NOTE: track targets receive the arm only; " +
                "the rail/7th-axis resource is not created by this script.");
        }
    }

    private static List<TxFrame> CollectRobotFrames(TxPhysicalRoot physicalRoot)
    {
        List<TxFrame> result = new List<TxFrame>();
        TxObjectList frames =
            physicalRoot.GetAllDescendants(new TxTypeFilter(typeof(TxFrame)));

        foreach (ITxObject item in frames)
        {
            TxFrame frame = item as TxFrame;
            if (frame == null || String.IsNullOrEmpty(frame.Name))
                continue;

            if (IsRobotTargetFrame(frame.Name))
                result.Add(frame);
        }

        result.Sort(new FrameNameComparer());
        return result;
    }

    private static bool IsRobotTargetFrame(string name)
    {
        if (String.IsNullOrEmpty(name))
            return false;

        // Generated names use patterns such as:
        // BSR_ROBOT_001
        // EC_ROBOT_014
        // MID_MAIN_TRACK_ROBOT_003
        if (name.IndexOf("_ROBOT_", StringComparison.OrdinalIgnoreCase) < 0)
            return false;

        bool isTrack =
            name.IndexOf("_TRACK_ROBOT_", StringComparison.OrdinalIgnoreCase) >= 0;

        if (isTrack && !IncludeTrackRobotFrames)
            return false;

        return true;
    }

    private static string BuildInstanceName(string frameName)
    {
        return "AUTO_" + frameName + "_" + RobotModelTag;
    }

    private static ITxComponent InsertOneRobot(
        TxPhysicalRoot physicalRoot,
        TxFrame targetFrame,
        string instanceName,
        StringBuilder report)
    {
        report.AppendLine("LOAD " + targetFrame.Name + " -> " + instanceName);

        ITxComponent inserted;
        using (TxInsertComponentCreationData data =
            new TxInsertComponentCreationData(instanceName, RobotPackagePath))
        {
            // Put the package close to the target first. The exact Baseframe
            // alignment is corrected immediately afterwards.
            data.AbsoluteLocation =
                new TxTransformation(targetFrame.AbsoluteLocation);
            inserted = physicalRoot.InsertComponent(data);
        }

        if (inserted == null)
            throw new InvalidOperationException("InsertComponent returned null.");

        ITxLocatableObject insertedRoot = inserted as ITxLocatableObject;
        if (insertedRoot == null)
            throw new InvalidOperationException(
                "Inserted component is not locatable.");

        if (!insertedRoot.CanLocate())
            throw new InvalidOperationException(
                "Inserted component cannot be relocated.");

        TxRobot robot = FindRobot(inserted);
        if (robot == null)
            throw new InvalidOperationException(
                "COJT was not recognized as a Robot. " +
                "Import the complete COJT package, not only the inner JT file.");

        AlignRobotBase(insertedRoot, robot, targetFrame, report);

        int jointCount = robot.Joints == null ? 0 : robot.Joints.Count;
        int drivingJointCount =
            robot.DrivingJoints == null ? 0 : robot.DrivingJoints.Count;
        int linkCount = robot.Links == null ? 0 : robot.Links.Count;

        if (jointCount < 6 || drivingJointCount < 6)
            throw new InvalidOperationException(
                "Fewer than six Robot joints were detected.");

        report.AppendLine(
            "OK   " + targetFrame.Name +
            " | links=" + linkCount +
            " joints=" + jointCount +
            " driving=" + drivingJointCount);

        return inserted;
    }

    private static void AlignRobotBase(
        ITxLocatableObject insertedRoot,
        TxRobot robot,
        TxFrame targetFrame,
        StringBuilder report)
    {
        TxFrame robotBase = robot.Baseframe;
        if (robotBase == null)
            throw new InvalidOperationException("Robot Baseframe is missing.");

        // Preserve the robot package's own internal offset. Move only the COJT root.
        TxTransformation baseRelativeToRoot =
            robotBase.GetLocationRelativeToObject(insertedRoot);

        insertedRoot.AbsoluteLocation =
            targetFrame.AbsoluteLocation * baseRelativeToRoot.Inverse;

        // Validate the resulting base alignment.
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

        report.AppendLine(
            "     base error=" + positionError.ToString("F6") + " mm, " +
            angleErrorDeg.ToString("F6") + " deg");

        if (positionError > 0.1 || angleErrorDeg > 0.01)
            throw new InvalidOperationException(
                "Robot Baseframe alignment is outside tolerance.");
    }

    private static void ValidateRobotPackage()
    {
        if (!Directory.Exists(RobotPackagePath))
            throw new DirectoryNotFoundException(
                "COJT package folder was not found: " + RobotPackagePath);

        // These files were present in the user's verified KR210 package.
        string[] requiredFiles = new string[]
        {
            "BSR_OP10_R01_KR210_R2700.jt",
            "kin_graph.xml",
            "motionparameters.e",
            "TuneData.xml"
        };

        for (int i = 0; i < requiredFiles.Length; i++)
        {
            string path = Path.Combine(RobotPackagePath, requiredFiles[i]);
            if (!File.Exists(path))
                throw new FileNotFoundException(
                    "COJT package is incomplete.", path);
        }
    }

    private static ITxObject FindObjectByName(
        TxPhysicalRoot physicalRoot,
        string name)
    {
        TxObjectList objects =
            physicalRoot.GetAllDescendants(new TxNoTypeFilter());

        foreach (ITxObject item in objects)
        {
            if (String.Equals(
                item.Name, name, StringComparison.OrdinalIgnoreCase))
                return item;
        }

        return null;
    }

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

    private static void RemoveIncompleteInsert(
        ITxComponent inserted,
        StringBuilder report)
    {
        try
        {
            if (inserted != null && inserted.CanBeDeleted)
            {
                inserted.Delete();
                report.AppendLine("     incomplete Robot instance removed.");
            }
            else if (inserted != null)
            {
                report.AppendLine(
                    "     WARNING: delete the incomplete Robot instance manually.");
            }
        }
        catch (Exception cleanupError)
        {
            report.AppendLine(
                "     WARNING: Robot cleanup failed: " + cleanupError.Message);
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

    private sealed class FrameNameComparer : IComparer<TxFrame>
    {
        public int Compare(TxFrame a, TxFrame b)
        {
            string an = a == null ? String.Empty : a.Name;
            string bn = b == null ? String.Empty : b.Name;
            return String.Compare(an, bn, StringComparison.OrdinalIgnoreCase);
        }
    }
}
