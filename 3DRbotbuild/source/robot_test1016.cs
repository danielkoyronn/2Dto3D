using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Tecnomatix.Engineering;

// ============================================================================
// 3DRbotbuild - PHASE 2
// RED + PURPLE ROBOT BATCH PLACEMENT
//
// User mapping:
//   RED    -> KR150_R2700_2      -> welding-gun Robot
//   PURPLE -> KR150_R3100_PRIME  -> screw-tightening Robot
//
// IMPORTANT CLASSIFICATION METHOD:
//   This version does NOT rely on the strange Frame display colors.
//   It uses CAD BYBLOCK inheritance discovered from the original DXF:
//
//       DTL0033 with parent BXP210L@ACI 1 -> RED
//       DTL0033 with parent BXP210L@ACI 6 -> PURPLE
//
//   ACI 1 = red
//   ACI 6 = magenta/purple
//
// The target coordinates below were extracted from the original CAD and
// matched to the already-created Phase-1 Robot Frames by XYZ coordinate.
//
// Placement core is the verified method:
//   InsertComponent -> TxRobot -> Robot.Baseframe -> target TxFrame
// ============================================================================

public class MainScript
{
    private const double MaxFrameMatchDistanceMm = 20.0;

    private const string RedModelTag =
        "KR150_R2700_2";

    private const string PurpleModelTag =
        "KR150_R3100_PRIME";

    private static readonly string RedPackageRelativePath =
        @"Resources\Robot (Articulated)\KR150_R2700_2.cojt";

    private static readonly string PurplePackageRelativePath =
        @"Resources\Robot (Articulated)\kr150r3100prime.cojt";

    // ------------------------------------------------------------------------
    // CAD-derived RED Robot base coordinates
    // ------------------------------------------------------------------------
    private static readonly TargetPoint[] RedTargets =
        new TargetPoint[]
        {
            new TargetPoint("BSL_ROBOT_001", 942662.445796, -1903367.490271, 0.000000),
            new TargetPoint("BSL_ROBOT_003", 950171.369154, -1905158.231278, 0.000000),
            new TargetPoint("BSL_ROBOT_004", 947275.974878, -1905193.802291, 0.000000),
            new TargetPoint("BSL_ROBOT_006", 950179.090945, -1906846.578782, 0.000000),
            new TargetPoint("BSL_ROBOT_007", 948099.742514, -1911443.204778, 0.000000),
            new TargetPoint("BSL_ROBOT_008", 956185.117480, -1912432.884294, 0.000000),
            new TargetPoint("BSL_ROBOT_010", 953383.247350, -1917115.817350, 0.000000),
            new TargetPoint("BSL_ROBOT_011", 956386.166967, -1917116.975974, 0.000000),
            new TargetPoint("BSL_ROBOT_014", 951857.923254, -1921366.576599, 0.000000),
            new TargetPoint("BSL_ROBOT_015", 951857.170432, -1923595.557713, 0.000000),
            new TargetPoint("BSL_ROBOT_016", 949541.077879, -1923623.741412, 0.000000),
            new TargetPoint("BSL_ROBOT_017", 945982.386477, -1924178.256828, 0.000000),
            new TargetPoint("BSL_ROBOT_018", 950346.289889, -1931391.949873, 0.000000),
            new TargetPoint("BSR_ROBOT_001", 980588.167382, -1903367.490271, 0.000000),
            new TargetPoint("BSR_ROBOT_002", 978519.181158, -1904338.767439, 0.000000),
            new TargetPoint("BSR_ROBOT_003", 976282.036610, -1904882.125068, 0.000000),
            new TargetPoint("BSR_ROBOT_004", 973062.929002, -1905158.231278, 0.000000),
            new TargetPoint("BSR_ROBOT_006", 973055.207211, -1906846.578782, 0.000000),
            new TargetPoint("BSR_ROBOT_007", 975134.555643, -1911443.204778, 0.000000),
            new TargetPoint("BSR_ROBOT_008", 967049.180677, -1912432.884294, 0.000000),
            new TargetPoint("BSR_ROBOT_010", 969851.050806, -1917115.817350, 0.000000),
            new TargetPoint("BSR_ROBOT_011", 966848.131189, -1917116.975974, 0.000000),
            new TargetPoint("BSR_ROBOT_014", 971070.793542, -1921166.383311, 0.000000),
            new TargetPoint("BSR_ROBOT_015", 971071.546364, -1923395.364424, 0.000000),
            new TargetPoint("BSR_ROBOT_016", 977251.911680, -1924178.256828, 0.000000),
            new TargetPoint("BSR_ROBOT_017", 973494.086927, -1924183.597638, 0.000000),
            new TargetPoint("BSR_ROBOT_018", 972888.008268, -1931391.949873, 0.000000),
            new TargetPoint("EC_ROBOT_001", 997150.963846, -1900019.427608, 0.000000),
            new TargetPoint("EC_ROBOT_002", 997150.963846, -1904402.768770, 0.000000),
            new TargetPoint("EC_ROBOT_004", 997630.127838, -1907881.998754, 0.000000),
            new TargetPoint("EC_ROBOT_007", 997654.728636, -1912097.812518, 0.000000),
            new TargetPoint("EC_ROBOT_010", 1000305.167933, -1915712.601966, 0.000000),
            new TargetPoint("EC_ROBOT_011", 1000413.170078, -1919742.935956, 0.000000),
            new TargetPoint("EC_ROBOT_016", 1006620.947926, -1924751.190627, 0.000000),
            new TargetPoint("EC_ROBOT_022", 997322.024845, -1932355.405231, 0.000000),
            new TargetPoint("EC_ROBOT_023", 997412.813089, -1936437.380921, 0.000000),
            new TargetPoint("EC_ROBOT_026", 997418.422687, -1940177.401493, 0.000000),
            new TargetPoint("EC_ROBOT_027", 999373.921752, -1940246.257094, 0.000000),
            new TargetPoint("EC_ROBOT_028", 997509.210931, -1944259.377183, 0.000000),
            new TargetPoint("EC_ROBOT_029", 999464.709996, -1944328.232784, 0.000000),
            new TargetPoint("MID_AUX_ROBOT_001", 1018903.962286, -1980406.273880, 0.000000),
            new TargetPoint("MID_AUX_ROBOT_002", 1020713.926320, -1980407.121238, 0.000000),
            new TargetPoint("MID_AUX_ROBOT_003", 1029087.281751, -1981119.205316, 0.000000),
            new TargetPoint("MID_AUX_ROBOT_004", 1026584.536351, -1981895.792533, 0.000000),
            new TargetPoint("MID_AUX_ROBOT_007", 1018927.710943, -1986337.966630, 0.000000),
            new TargetPoint("MID_AUX_ROBOT_008", 1020727.710943, -1986337.966630, 0.000000),
            new TargetPoint("RR_ROBOT_008", 1019741.023306, -1918959.784100, 0.000000),
            new TargetPoint("RR_ROBOT_011", 1017157.425641, -1923296.362906, 0.000000),
            new TargetPoint("RR_ROBOT_022", 1022181.842875, -1938431.143339, 0.000000),
            new TargetPoint("RR_ROBOT_024", 1018983.264204, -1942585.756809, 0.000000),
        };

    // ------------------------------------------------------------------------
    // CAD-derived PURPLE Robot base coordinates
    // ------------------------------------------------------------------------
    private static readonly TargetPoint[] PurpleTargets =
        new TargetPoint[]
        {
            new TargetPoint("EC_ROBOT_005", 999651.370561, -1907883.734856, 0.000000),
            new TargetPoint("EC_ROBOT_006", 999651.370561, -1912077.221467, 0.000000),
            new TargetPoint("EC_ROBOT_009", 997298.779636, -1915689.077005, 0.000000),
            new TargetPoint("EC_ROBOT_012", 997298.779636, -1919882.563616, 0.000000),
            new TargetPoint("EC_ROBOT_015", 1000353.613275, -1923833.499186, 0.000000),
            new TargetPoint("EC_ROBOT_019", 999410.452504, -1927733.964976, 0.000000),
            new TargetPoint("MID_MAIN_ROBOT_012", 1000421.046333, -1963028.881210, -0.000002),
            new TargetPoint("RR_ROBOT_015", 1022124.420782, -1928066.062691, 0.000000),
            new TargetPoint("RR_ROBOT_017", 1022100.424070, -1932181.440939, 0.000000),
            new TargetPoint("RR_ROBOT_020", 1016996.620752, -1936838.776249, 0.000000),
            new TargetPoint("RR_ROBOT_021", 1016996.620752, -1938377.135565, 0.000000),
            new TargetPoint("RR_ROBOT_025", 1026219.423187, -1945536.597332, 0.000000),
            new TargetPoint("RR_ROBOT_028", 1026147.589769, -1949716.073885, 0.000000),
        };

    public static void Main()
    {
        StringWriter output =
            new StringWriter();

        try
        {
            Run(ref output);
        }
        catch (Exception ex)
        {
            Exception detail =
                Unwrap(ex);

            output.WriteLine("");
            output.WriteLine(
                "FATAL: " +
                detail.GetType().Name +
                " - " +
                detail.Message);
        }

        TxMessageBox.Show(
            output.ToString(),
            "3DRbotbuild - Red/Purple Batch Builder",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private static void Run(
        ref StringWriter output)
    {
        TxDocument document =
            TxApplication.ActiveDocument;

        if (document == null)
            throw new InvalidOperationException(
                "No active Process Simulate study.");

        TxPhysicalRoot physicalRoot =
            document.PhysicalRoot;

        if (physicalRoot == null)
            throw new InvalidOperationException(
                "Active study has no PhysicalRoot.");

        string systemRoot =
            TxApplication.SystemRootDirectory;

        if (String.IsNullOrEmpty(systemRoot))
            throw new InvalidOperationException(
                "Process Simulate System Root is empty.");

        string redPackage =
            Path.Combine(
                systemRoot,
                RedPackageRelativePath);

        string purplePackage =
            Path.Combine(
                systemRoot,
                PurplePackageRelativePath);

        output.WriteLine(
            "========== ROBOT LIBRARY ==========");

        output.WriteLine(
            "System Root : " +
            systemRoot);

        output.WriteLine(
            "RED package : " +
            redPackage);

        output.WriteLine(
            "PURPLE pkg  : " +
            purplePackage);

        ValidatePackage(
            redPackage,
            RedModelTag);

        ValidatePackage(
            purplePackage,
            PurpleModelTag);

        output.WriteLine(
            "Library validation: PASS");

        output.WriteLine(
            "===================================");

        List<TxFrame> allRobotFrames =
            FindRobotFrames(
                physicalRoot);

        output.WriteLine("");
        output.WriteLine(
            "Phase-1 Robot Frames found: " +
            allRobotFrames.Count);

        List<ResolvedTarget> redResolved =
            ResolveTargets(
                RedTargets,
                allRobotFrames,
                "RED",
                RedModelTag);

        List<ResolvedTarget> purpleResolved =
            ResolveTargets(
                PurpleTargets,
                allRobotFrames,
                "PURPLE",
                PurpleModelTag);

        WriteResolveSummary(
            redResolved,
            purpleResolved,
            ref output);

        int unresolved =
            CountUnresolved(redResolved) +
            CountUnresolved(purpleResolved);

        if (unresolved > 0)
        {
            output.WriteLine("");
            output.WriteLine(
                "WARNING: " +
                unresolved +
                " CAD target(s) could not be matched to a Phase-1 Frame.");
            output.WriteLine(
                "Unresolved targets will be skipped.");
        }

        int redReady =
            CountResolved(redResolved);

        int purpleReady =
            CountResolved(purpleResolved);

        StringBuilder confirm =
            new StringBuilder();

        confirm.AppendLine(
            "CAD classification method: BYBLOCK inheritance");

        confirm.AppendLine("");

        confirm.AppendLine(
            "RED / welding gun:");
        confirm.AppendLine(
            RedModelTag +
            " -> " +
            redReady +
            " Frames");

        confirm.AppendLine("");

        confirm.AppendLine(
            "PURPLE / screw tightening:");
        confirm.AppendLine(
            PurpleModelTag +
            " -> " +
            purpleReady +
            " Frames");

        confirm.AppendLine("");
        confirm.AppendLine(
            "Total planned placements: " +
            (redReady + purpleReady));

        confirm.AppendLine("");
        confirm.AppendLine(
            "Press OK to start batch placement.");

        DialogResult answer =
            MessageBox.Show(
                confirm.ToString(),
                "Confirm Red/Purple Robot Placement",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Question);

        if (answer != DialogResult.OK)
        {
            output.WriteLine("");
            output.WriteLine(
                "Cancelled before placement.");
            return;
        }

        int redPlaced = 0;
        int purplePlaced = 0;
        int skipped = 0;
        int failed = 0;

        output.WriteLine("");
        output.WriteLine(
            "========== RED ROBOTS ==========");

        PlaceResolvedTargets(
            physicalRoot,
            redResolved,
            redPackage,
            RedModelTag,
            ref redPlaced,
            ref skipped,
            ref failed,
            ref output);

        output.WriteLine("");
        output.WriteLine(
            "========== PURPLE ROBOTS ==========");

        PlaceResolvedTargets(
            physicalRoot,
            purpleResolved,
            purplePackage,
            PurpleModelTag,
            ref purplePlaced,
            ref skipped,
            ref failed,
            ref output);

        output.WriteLine("");
        output.WriteLine(
            "========== FINAL RESULT ==========");

        output.WriteLine(
            "RED placed    : " +
            redPlaced +
            " / " +
            redReady);

        output.WriteLine(
            "PURPLE placed : " +
            purplePlaced +
            " / " +
            purpleReady);

        output.WriteLine(
            "Skipped       : " +
            skipped);

        output.WriteLine(
            "Failed        : " +
            failed);

        output.WriteLine(
            "==================================");
    }

    // ========================================================================
    // TARGET RESOLUTION
    // ========================================================================

    private static List<ResolvedTarget> ResolveTargets(
        TargetPoint[] targets,
        List<TxFrame> frames,
        string cadClass,
        string modelTag)
    {
        List<ResolvedTarget> result =
            new List<ResolvedTarget>();

        for (
            int i = 0;
            i < targets.Length;
            i++)
        {
            TargetPoint target =
                targets[i];

            TxFrame bestFrame =
                null;

            double bestDistance =
                Double.MaxValue;

            for (
                int f = 0;
                f < frames.Count;
                f++)
            {
                TxFrame frame =
                    frames[f];

                TxVector p =
                    frame.AbsoluteLocation.Translation;

                double dx =
                    p.X -
                    target.X;

                double dy =
                    p.Y -
                    target.Y;

                double dz =
                    p.Z -
                    target.Z;

                double distance =
                    Math.Sqrt(
                        dx * dx +
                        dy * dy +
                        dz * dz);

                if (distance < bestDistance)
                {
                    bestDistance =
                        distance;

                    bestFrame =
                        frame;
                }
            }

            ResolvedTarget item =
                new ResolvedTarget();

            item.CadSourceName =
                target.SourceName;

            item.CadClass =
                cadClass;

            item.ModelTag =
                modelTag;

            item.TargetPoint =
                target;

            item.Frame =
                (
                    bestDistance <=
                    MaxFrameMatchDistanceMm
                ) ?
                bestFrame :
                null;

            item.MatchDistance =
                bestDistance;

            result.Add(
                item);
        }

        return result;
    }

    private static void WriteResolveSummary(
        List<ResolvedTarget> red,
        List<ResolvedTarget> purple,
        ref StringWriter output)
    {
        output.WriteLine("");
        output.WriteLine(
            "========== CAD -> FRAME MAP ==========");

        WriteResolvedGroup(
            red,
            ref output);

        WriteResolvedGroup(
            purple,
            ref output);

        output.WriteLine(
            "=====================================");
    }

    private static void WriteResolvedGroup(
        List<ResolvedTarget> items,
        ref StringWriter output)
    {
        for (
            int i = 0;
            i < items.Count;
            i++)
        {
            ResolvedTarget item =
                items[i];

            output.WriteLine(
                item.CadClass +
                " | CAD=" +
                item.CadSourceName +
                " | Frame=" +
                (
                    item.Frame == null ?
                    "NO_MATCH" :
                    item.Frame.Name
                ) +
                " | d=" +
                item.MatchDistance.ToString("F3") +
                " mm");
        }
    }

    // ========================================================================
    // BATCH PLACEMENT
    // ========================================================================

    private static void PlaceResolvedTargets(
        TxPhysicalRoot physicalRoot,
        List<ResolvedTarget> targets,
        string packagePath,
        string modelTag,
        ref int placed,
        ref int skipped,
        ref int failed,
        ref StringWriter output)
    {
        for (
            int i = 0;
            i < targets.Count;
            i++)
        {
            ResolvedTarget target =
                targets[i];

            if (target.Frame == null)
            {
                skipped++;
                continue;
            }

            string finalName =
                target.Frame.Name +
                "_" +
                modelTag;

            if (
                FindObjectByName(
                    physicalRoot,
                    finalName) != null)
            {
                output.WriteLine(
                    "SKIP existing: " +
                    finalName);

                skipped++;
                continue;
            }

            try
            {
                InsertAndAlign(
                    physicalRoot,
                    packagePath,
                    finalName,
                    target.Frame,
                    ref output);

                placed++;
            }
            catch (Exception ex)
            {
                failed++;

                Exception detail =
                    Unwrap(ex);

                output.WriteLine(
                    "FAIL " +
                    finalName +
                    " : " +
                    detail.GetType().Name +
                    " - " +
                    detail.Message);
            }
        }
    }

    private static void InsertAndAlign(
        TxPhysicalRoot physicalRoot,
        string packagePath,
        string instanceName,
        TxFrame targetFrame,
        ref StringWriter output)
    {
        ITxComponent inserted =
            null;

        try
        {
            using (
                TxInsertComponentCreationData data =
                    new TxInsertComponentCreationData(
                        instanceName,
                        packagePath))
            {
                data.AbsoluteLocation =
                    new TxTransformation(
                        targetFrame.AbsoluteLocation);

                inserted =
                    physicalRoot.InsertComponent(
                        data);
            }

            if (inserted == null)
                throw new InvalidOperationException(
                    "InsertComponent returned null.");

            ITxLocatableObject insertedRoot =
                inserted as ITxLocatableObject;

            if (insertedRoot == null)
                throw new InvalidOperationException(
                    "Inserted component is not locatable.");

            TxRobot robot =
                FindRobot(
                    inserted);

            if (robot == null)
                throw new InvalidOperationException(
                    "No TxRobot found in COJT package.");

            AlignRobotBase(
                insertedRoot,
                robot,
                targetFrame);

            output.WriteLine(
                "PASS " +
                instanceName);
        }
        catch
        {
            RemoveIncompleteInsert(
                inserted);

            throw;
        }
    }

    // ========================================================================
    // VERIFIED BASEFRAME ALIGNMENT
    // ========================================================================

    private static void AlignRobotBase(
        ITxLocatableObject insertedRoot,
        TxRobot robot,
        TxFrame targetFrame)
    {
        TxFrame robotBase =
            robot.Baseframe;

        if (robotBase == null)
            throw new InvalidOperationException(
                "Robot Baseframe is missing.");

        TxTransformation baseRelativeToRoot =
            robotBase.GetLocationRelativeToObject(
                insertedRoot);

        insertedRoot.AbsoluteLocation =
            targetFrame.AbsoluteLocation *
            baseRelativeToRoot.Inverse;

        TxTransformation residual =
            robotBase.GetLocationRelativeToObject(
                targetFrame);

        TxVector delta =
            residual.Translation;

        TxVector rotation =
            residual.RotationRPY_XYZ;

        double positionError =
            Math.Sqrt(
                delta.X * delta.X +
                delta.Y * delta.Y +
                delta.Z * delta.Z);

        double angleErrorRad =
            Math.Max(
                Math.Abs(
                    NormalizeAngle(
                        rotation.X)),
                Math.Max(
                    Math.Abs(
                        NormalizeAngle(
                            rotation.Y)),
                    Math.Abs(
                        NormalizeAngle(
                            rotation.Z))));

        double angleErrorDeg =
            angleErrorRad *
            180.0 /
            Math.PI;

        if (
            positionError > 0.1 ||
            angleErrorDeg > 0.01)
        {
            throw new InvalidOperationException(
                "Baseframe alignment outside tolerance: " +
                positionError.ToString("F6") +
                " mm / " +
                angleErrorDeg.ToString("F6") +
                " deg.");
        }
    }

    // ========================================================================
    // FRAME / OBJECT HELPERS
    // ========================================================================

    private static List<TxFrame> FindRobotFrames(
        TxPhysicalRoot physicalRoot)
    {
        List<TxFrame> result =
            new List<TxFrame>();

        TxObjectList frames =
            physicalRoot.GetAllDescendants(
                new TxTypeFilter(
                    typeof(TxFrame)));

        foreach (
            ITxObject item
            in frames)
        {
            TxFrame frame =
                item as TxFrame;

            if (frame == null)
                continue;

            string name =
                frame.Name;

            if (
                String.IsNullOrEmpty(
                    name) ||
                !Regex.IsMatch(
                    name,
                    @"_R\d{2}$",
                    RegexOptions.IgnoreCase))
            {
                continue;
            }

            TxTransformation location =
                frame.AbsoluteLocation;

            if (location == null)
                continue;

            double z =
                location.Translation.Z;

            if (
                Double.IsNaN(z) ||
                Double.IsInfinity(z) ||
                Math.Abs(z) > 10000.0)
            {
                continue;
            }

            result.Add(
                frame);
        }

        return result;
    }

    private static ITxObject FindObjectByName(
        TxPhysicalRoot root,
        string name)
    {
        TxObjectList objects =
            root.GetAllDescendants(
                new TxNoTypeFilter());

        foreach (
            ITxObject item
            in objects)
        {
            if (
                item != null &&
                String.Equals(
                    item.Name,
                    name,
                    StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }
        }

        return null;
    }

    private static TxRobot FindRobot(
        ITxComponent inserted)
    {
        TxRobot direct =
            inserted as TxRobot;

        if (direct != null)
            return direct;

        ITxObjectCollection collection =
            inserted as ITxObjectCollection;

        if (collection == null)
            return null;

        TxObjectList robots =
            collection.GetAllDescendants(
                new TxTypeFilter(
                    typeof(TxRobot)));

        foreach (
            ITxObject item
            in robots)
        {
            TxRobot robot =
                item as TxRobot;

            if (robot != null)
                return robot;
        }

        return null;
    }

    // ========================================================================
    // PACKAGE VALIDATION
    // ========================================================================

    private static void ValidatePackage(
        string packagePath,
        string modelTag)
    {
        if (!Directory.Exists(
            packagePath))
        {
            throw new DirectoryNotFoundException(
                modelTag +
                " COJT folder not found: " +
                packagePath);
        }

        string[] jtFiles =
            Directory.GetFiles(
                packagePath,
                "*.jt",
                SearchOption.TopDirectoryOnly);

        if (
            jtFiles == null ||
            jtFiles.Length == 0)
        {
            throw new FileNotFoundException(
                modelTag +
                " has no top-level JT file.",
                packagePath);
        }
    }

    // ========================================================================
    // COUNTERS / CLEANUP
    // ========================================================================

    private static int CountResolved(
        List<ResolvedTarget> list)
    {
        int count =
            0;

        for (
            int i = 0;
            i < list.Count;
            i++)
        {
            if (list[i].Frame != null)
                count++;
        }

        return count;
    }

    private static int CountUnresolved(
        List<ResolvedTarget> list)
    {
        return
            list.Count -
            CountResolved(list);
    }

    private static void RemoveIncompleteInsert(
        ITxComponent inserted)
    {
        try
        {
            if (
                inserted != null &&
                inserted.CanBeDeleted)
            {
                inserted.Delete();
            }
        }
        catch
        {
        }
    }

    private static double NormalizeAngle(
        double radians)
    {
        while (radians > Math.PI)
            radians -=
                2.0 *
                Math.PI;

        while (radians < -Math.PI)
            radians +=
                2.0 *
                Math.PI;

        return radians;
    }

    private static Exception Unwrap(
        Exception ex)
    {
        Exception detail =
            ex;

        while (
            detail != null &&
            detail.InnerException != null)
        {
            detail =
                detail.InnerException;
        }

        return detail == null ?
            ex :
            detail;
    }

    // ========================================================================
    // DATA CLASSES
    // ========================================================================

    private sealed class TargetPoint
    {
        public string SourceName;
        public double X;
        public double Y;
        public double Z;

        public TargetPoint(
            string sourceName,
            double x,
            double y,
            double z)
        {
            SourceName =
                sourceName;

            X = x;
            Y = y;
            Z = z;
        }
    }

    private sealed class ResolvedTarget
    {
        public string CadSourceName;
        public string CadClass;
        public string ModelTag;
        public TargetPoint TargetPoint;
        public TxFrame Frame;
        public double MatchDistance;
    }
}
