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
// Process Simulate 2512
//
// 3DRbotbuild PHASE 1 - FRAME BUILDER ONLY
//
// Purpose:
//   PHASE 1 ONLY.
//   Read DXF text for station/Rxx naming, then create/rename Frames.
//
// Robot Frame naming:
//   <LINE>_<STATION>_R<NN>
//   example: EC_EC40_R01
//
// Important:
//   - The 225 verified Frame coordinates below remain the source of placement.
//   - Factory/line BASE Frames keep their current names.
//   - NO Robot COJT is inserted in this phase.
//   - A FrameCatalog CSV is exported for Phase 2 RobotBuilder.
//
// Naming source:
//   The script reads TEXT/MTEXT from the factory DXF only to determine
//   Station and Robot number labels. It does NOT regenerate Frame coordinates
//   from DXF.
//
// If an old Frame/Robot already exists in the Study, the script attempts to
// rename/reuse it rather than create a duplicate.
// ============================================================================

public class MainScript
{
    // ------------------------------------------------------------------------
    // USER CONFIGURATION
    // ------------------------------------------------------------------------

    private const string DefaultDxfPath =
        @"D:\WEDING\MODEL\1013.dxf";

private const double MaxAbsRobotZMm = 10000.0;

    // Maximum search range from Robot Base to Station label.
    private const double StationSearchRadiusMm = 15000.0;

    // Maximum search range from Robot Base to CAD R01/R02/... label.
    private const double RobotLabelSearchRadiusMm = 4500.0;

    private static string DxfPath = DefaultDxfPath;

    // ------------------------------------------------------------------------
    // VERIFIED FRAME TABLE
    // ------------------------------------------------------------------------

    private sealed class FrameSeed
    {
        public string OriginalName;
        public double X;
        public double Y;
        public double Z;
        public double Rx;
        public double Ry;
        public double Rz;

        public string NewName;
        public string Line;
        public string Station;
        public int RobotNo;
        public bool IsRobot;

        public FrameSeed(
            string originalName,
            double x,
            double y,
            double z,
            double rx,
            double ry,
            double rz)
        {
            OriginalName = originalName;
            X = x;
            Y = y;
            Z = z;
            Rx = rx;
            Ry = ry;
            Rz = rz;
            NewName = originalName;
            IsRobot = originalName.IndexOf(
                "_ROBOT_",
                StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    private static readonly FrameSeed[] FrameData = new FrameSeed[]
    {
        new FrameSeed("FACTORY_BASE_01", 934617.216742, -1890583.332348, 0.000000, 0.000000, 0.000000, 0.000000),
        new FrameSeed("FACTORY_BASE_02", 1040617.216742, -1890583.332348, 0.000000, 0.000000, 0.000000, 0.000000),
        new FrameSeed("FACTORY_BASE_03", 1040617.216742, -2008583.332348, 0.000000, 0.000000, 0.000000, 0.000000),
        new FrameSeed("FACTORY_BASE_04", 934617.216742, -2008583.332348, 0.000000, 0.000000, 0.000000, 0.000000),
        new FrameSeed("BSL_BASE_01", 939617.149078, -1896582.839094, 0.000000, 0.000000, 0.000000, 0.000000),
        new FrameSeed("BSL_BASE_02", 959617.149078, -1896582.839094, 0.000000, 0.000000, 0.000000, 0.000000),
        new FrameSeed("BSL_BASE_03", 959617.149078, -1939582.839094, 0.000000, 0.000000, 0.000000, 0.000000),
        new FrameSeed("BSL_BASE_04", 939617.149078, -1939582.839094, 0.000000, 0.000000, 0.000000, 0.000000),
        new FrameSeed("BSR_BASE_01", 963617.149078, -1896582.839094, 0.000000, 0.000000, 0.000000, 0.000000),
        new FrameSeed("BSR_BASE_02", 983617.149078, -1896582.839094, 0.000000, 0.000000, 0.000000, 0.000000),
        new FrameSeed("BSR_BASE_03", 983617.149078, -1939582.839094, 0.000000, 0.000000, 0.000000, 0.000000),
        new FrameSeed("BSR_BASE_04", 963617.149078, -1939582.839094, 0.000000, 0.000000, 0.000000, 0.000000),
        new FrameSeed("EC_BASE_01", 988617.149078, -1896582.839094, 0.000000, 0.000000, 0.000000, 0.000000),
        new FrameSeed("EC_BASE_02", 1009017.149078, -1896582.839094, 0.000000, 0.000000, 0.000000, 0.000000),
        new FrameSeed("EC_BASE_03", 1009017.149078, -1952582.839094, 0.000000, 0.000000, 0.000000, 0.000000),
        new FrameSeed("EC_BASE_04", 988617.149078, -1952582.839094, 0.000000, 0.000000, 0.000000, 0.000000),
        new FrameSeed("RR_BASE_01", 1014017.149050, -1896582.839094, 0.000000, 0.000000, 0.000000, 0.000000),
        new FrameSeed("RR_BASE_02", 1030617.149050, -1896582.839094, 0.000000, 0.000000, 0.000000, 0.000000),
        new FrameSeed("RR_BASE_03", 1030617.149050, -1952582.839094, 0.000000, 0.000000, 0.000000, 0.000000),
        new FrameSeed("RR_BASE_04", 1014017.149050, -1952582.839094, 0.000000, 0.000000, 0.000000, 0.000000),
        new FrameSeed("BSL_AUX_BASE_01", 941617.149078, -1942582.839094, 0.000000, 0.000000, 0.000000, 0.000000),
        new FrameSeed("BSL_AUX_BASE_02", 959617.149078, -1942582.839094, 0.000000, 0.000000, 0.000000, 0.000000),
        new FrameSeed("BSL_AUX_BASE_03", 959617.149078, -1952582.839094, 0.000000, 0.000000, 0.000000, 0.000000),
        new FrameSeed("BSL_AUX_BASE_04", 941617.149078, -1952582.839094, 0.000000, 0.000000, 0.000000, 0.000000),
        new FrameSeed("BSR_AUX_BASE_01", 963617.149078, -1942582.839094, 0.000000, 0.000000, 0.000000, 0.000000),
        new FrameSeed("BSR_AUX_BASE_02", 983617.149078, -1942582.839094, 0.000000, 0.000000, 0.000000, 0.000000),
        new FrameSeed("BSR_AUX_BASE_03", 983617.149078, -1952582.839094, 0.000000, 0.000000, 0.000000, 0.000000),
        new FrameSeed("BSR_AUX_BASE_04", 963617.149078, -1952582.839094, 0.000000, 0.000000, 0.000000, 0.000000),
        new FrameSeed("MID_MAIN_BASE_01", 941617.149078, -1957582.839094, 0.000000, 0.000000, 0.000000, 0.000000),
        new FrameSeed("MID_MAIN_BASE_02", 1034617.149078, -1957582.839094, 0.000000, 0.000000, 0.000000, 0.000000),
        new FrameSeed("MID_MAIN_BASE_03", 1034617.149078, -1988582.839094, 0.000000, 0.000000, 0.000000, 0.000000),
        new FrameSeed("MID_MAIN_BASE_04", 941617.149078, -1988582.839094, 0.000000, 0.000000, 0.000000, 0.000000),
        new FrameSeed("MID_AUX_BASE_01", 1012617.149078, -1977582.787282, 0.000000, 0.000000, 0.000000, 0.000000),
        new FrameSeed("MID_AUX_BASE_02", 1034617.149078, -1977582.787282, 0.000000, 0.000000, 0.000000, 0.000000),
        new FrameSeed("MID_AUX_BASE_03", 1034617.149078, -1988582.839094, 0.000000, 0.000000, 0.000000, 0.000000),
        new FrameSeed("MID_AUX_BASE_04", 1012617.149078, -1988582.839094, 0.000000, 0.000000, 0.000000, 0.000000),
        new FrameSeed("BOTTOM_BASE_01", 941617.149078, -1994582.839094, 0.000000, 0.000000, 0.000000, 0.000000),
        new FrameSeed("BOTTOM_BASE_02", 1034617.149078, -1994582.839094, 0.000000, 0.000000, 0.000000, 0.000000),
        new FrameSeed("BOTTOM_BASE_03", 1034617.149078, -2001582.839094, 0.000000, 0.000000, 0.000000, 0.000000),
        new FrameSeed("BOTTOM_BASE_04", 941617.149078, -2001582.839094, 0.000000, 0.000000, 0.000000, 0.000000),
        new FrameSeed("BOTTOM_ROBOT_001", 1018038.580902, -1995990.208552, 0.000000, 0.000000, 0.000000, 270.000000),
        new FrameSeed("BOTTOM_ROBOT_002", 1017990.939870, -2000244.165033, 0.000000, 0.000000, 0.000000, 90.000000),
        new FrameSeed("BSL_ROBOT_001", 942662.445796, -1903367.490271, 0.000000, 0.000000, 0.000000, 270.000000),
        new FrameSeed("BSL_ROBOT_002", 944779.033949, -1903770.878401, 0.000000, 0.000000, 0.000000, 270.000000),
        new FrameSeed("BSL_ROBOT_003", 950171.369154, -1905158.231278, 0.000000, 0.000000, 0.000000, 270.000000),
        new FrameSeed("BSL_ROBOT_004", 947275.974878, -1905193.802291, 0.000000, 0.000000, 0.000000, 270.000000),
        new FrameSeed("BSL_ROBOT_005", 948430.617333, -1906351.221270, 0.000000, 0.000000, 0.000000, 180.000000),
        new FrameSeed("BSL_ROBOT_006", 950179.090945, -1906846.578782, 0.000000, 0.000000, 0.000000, 180.000000),
        new FrameSeed("BSL_ROBOT_007", 948099.742514, -1911443.204778, 0.000000, 0.000000, 0.000000, 270.000000),
        new FrameSeed("BSL_ROBOT_008", 956185.117480, -1912432.884294, 0.000000, 0.000000, 0.000000, 90.000000),
        new FrameSeed("BSL_ROBOT_009", 951103.075617, -1916788.331003, 0.000000, 0.000000, 0.000000, 180.000000),
        new FrameSeed("BSL_ROBOT_010", 953383.247350, -1917115.817350, 0.000000, 0.000000, 0.000000, 270.000000),
        new FrameSeed("BSL_ROBOT_011", 956386.166967, -1917116.975974, 0.000000, 0.000000, 0.000000, 270.000000),
        new FrameSeed("BSL_ROBOT_012", 954841.524474, -1918089.978725, 0.000000, 0.000000, 0.000000, 180.000000),
        new FrameSeed("BSL_ROBOT_013", 957056.442880, -1921098.365610, 0.000000, 0.000000, 0.000000, 0.000000),
        new FrameSeed("BSL_ROBOT_014", 951857.923254, -1921366.576599, 0.000000, 0.000000, 0.000000, 180.000000),
        new FrameSeed("BSL_ROBOT_015", 951857.170432, -1923595.557713, 0.000000, 0.000000, 0.000000, 180.000000),
        new FrameSeed("BSL_ROBOT_016", 949541.077879, -1923623.741412, 0.000000, 0.000000, 0.000000, 270.000000),
        new FrameSeed("BSL_ROBOT_017", 945982.386477, -1924178.256828, 0.000000, 0.000000, 0.000000, 270.000000),
        new FrameSeed("BSL_ROBOT_018", 950346.289889, -1931391.949873, 0.000000, 0.000000, 0.000000, 0.000000),
        new FrameSeed("BSL_ROBOT_019", 949055.188653, -1934101.735429, 0.000000, 0.000000, 0.000000, 0.000000),
        new FrameSeed("BSL_ROBOT_020", 947246.357465, -1935959.898566, 0.000000, 0.000000, 0.000000, 180.000000),
        new FrameSeed("BSL_ROBOT_021", 947247.060294, -1937736.359157, 0.000000, 0.000000, 0.000000, 180.000000),
        new FrameSeed("BSL_TRACK_ROBOT_001", 950078.244291, -1911733.599866, 0.000000, 0.000000, 0.000000, 0.000000),
        new FrameSeed("BSL_TRACK_ROBOT_002", 958624.274557, -1924754.159191, 0.000000, 0.000000, 0.000000, 0.000000),
        new FrameSeed("BSL_TRACK_ROBOT_003", 947725.476633, -1924774.987447, 0.000000, 0.000000, 0.000000, 0.000000),
        new FrameSeed("BSL_AUX_ROBOT_001", 947408.511856, -1945699.325316, -4.999970, 0.000000, 0.000000, 360.000000),
        new FrameSeed("BSL_AUX_ROBOT_002", 945441.069868, -1945741.197635, 0.000000, 0.000000, 0.000000, 360.000000),
        new FrameSeed("BSL_AUX_ROBOT_003", 954555.644941, -1945771.183089, -0.000032, 0.000000, 0.000000, 180.000000),
        new FrameSeed("BSL_AUX_ROBOT_004", 948972.443169, -1945788.740102, -0.000065, 0.000000, 0.000000, 180.000000),
        new FrameSeed("BSL_AUX_ROBOT_005", 952703.417418, -1945788.740102, -0.000065, 0.000000, 0.000000, 180.000000),
        new FrameSeed("BSL_AUX_TRACK_ROBOT_001", 950905.775737, -1948799.243758, 0.000000, 0.000000, 0.000000, 90.000000),
        new FrameSeed("BSR_ROBOT_001", 980588.167382, -1903367.490271, 0.000000, 0.000000, 0.000000, 270.000000),
        new FrameSeed("BSR_ROBOT_002", 978519.181158, -1904338.767439, 0.000000, 0.000000, 0.000000, 270.000000),
        new FrameSeed("BSR_ROBOT_003", 976282.036610, -1904882.125068, 0.000000, 0.000000, 0.000000, 270.000000),
        new FrameSeed("BSR_ROBOT_004", 973062.929002, -1905158.231278, 0.000000, 0.000000, 0.000000, 270.000000),
        new FrameSeed("BSR_ROBOT_005", 974839.183512, -1906149.980177, 0.000000, 0.000000, 0.000000, 360.000000),
        new FrameSeed("BSR_ROBOT_006", 973055.207211, -1906846.578782, 0.000000, 0.000000, 0.000000, 360.000000),
        new FrameSeed("BSR_ROBOT_007", 975134.555643, -1911443.204778, 0.000000, 0.000000, 0.000000, 270.000000),
        new FrameSeed("BSR_ROBOT_008", 967049.180677, -1912432.884294, 0.000000, 0.000000, 0.000000, 90.000000),
        new FrameSeed("BSR_ROBOT_009", 972131.222539, -1916788.331003, 0.000000, 0.000000, 0.000000, 360.000000),
        new FrameSeed("BSR_ROBOT_010", 969851.050806, -1917115.817350, 0.000000, 0.000000, 0.000000, 270.000000),
        new FrameSeed("BSR_ROBOT_011", 966848.131189, -1917116.975974, 0.000000, 0.000000, 0.000000, 270.000000),
        new FrameSeed("BSR_ROBOT_012", 968392.773683, -1918089.978725, 0.000000, 0.000000, 0.000000, 360.000000),
        new FrameSeed("BSR_ROBOT_013", 965937.403883, -1921023.747305, 0.000000, 0.000000, 0.000000, 180.000000),
        new FrameSeed("BSR_ROBOT_014", 971070.793542, -1921166.383311, 0.000000, 0.000000, 0.000000, 360.000000),
        new FrameSeed("BSR_ROBOT_015", 971071.546364, -1923395.364424, 0.000000, 0.000000, 0.000000, 360.000000),
        new FrameSeed("BSR_ROBOT_016", 977251.911680, -1924178.256828, 0.000000, 0.000000, 0.000000, 270.000000),
        new FrameSeed("BSR_ROBOT_017", 973494.086927, -1924183.597638, 0.000000, 0.000000, 0.000000, 270.000000),
        new FrameSeed("BSR_ROBOT_018", 972888.008268, -1931391.949873, 0.000000, 0.000000, 0.000000, 180.000000),
        new FrameSeed("BSR_ROBOT_019", 974179.109503, -1934101.735429, 0.000000, 0.000000, 0.000000, 180.000000),
        new FrameSeed("BSR_ROBOT_020", 975987.940691, -1935959.898566, 0.000000, 0.000000, 0.000000, 360.000000),
        new FrameSeed("BSR_ROBOT_021", 975987.237862, -1937736.359157, 0.000000, 0.000000, 0.000000, 360.000000),
        new FrameSeed("BSR_TRACK_ROBOT_001", 973156.053865, -1911733.599866, 0.000000, 0.000000, 0.000000, 180.000000),
        new FrameSeed("BSR_TRACK_ROBOT_002", 964606.907592, -1924748.314711, 0.000000, 0.000000, 0.000000, 180.000000),
        new FrameSeed("BSR_TRACK_ROBOT_003", 975508.821523, -1924774.987447, 0.000000, 0.000000, 0.000000, 180.000000),
        new FrameSeed("EC_ROBOT_001", 997150.963846, -1900019.427608, 0.000000, 0.000000, 0.000000, 270.000000),
        new FrameSeed("EC_ROBOT_002", 997150.963846, -1904402.768770, 0.000000, 0.000000, 0.000000, 90.000000),
        new FrameSeed("EC_ROBOT_003", 1002447.135581, -1906177.519231, 0.000000, 0.000000, 0.000000, 0.304606),
        new FrameSeed("EC_ROBOT_004", 997630.127838, -1907881.998754, 0.000000, 0.000000, 0.000000, 270.000000),
        new FrameSeed("EC_ROBOT_005", 999651.370561, -1907883.734856, 0.000000, 0.000000, 0.000000, 270.000000),
        new FrameSeed("EC_ROBOT_006", 999651.370561, -1912077.221467, 0.000000, 0.000000, 0.000000, 90.000000),
        new FrameSeed("EC_ROBOT_007", 997654.728636, -1912097.812518, 0.000000, 0.000000, 0.000000, 90.000000),
        new FrameSeed("EC_ROBOT_008", 1002484.446648, -1914471.310889, 0.000000, 0.000000, 0.000000, 29.304606),
        new FrameSeed("EC_ROBOT_009", 997298.779636, -1915689.077005, 0.000000, 0.000000, 0.000000, 270.000000),
        new FrameSeed("EC_ROBOT_010", 1000305.167933, -1915712.601966, 0.000000, 0.000000, 0.000000, 270.000000),
        new FrameSeed("EC_ROBOT_011", 1000413.170078, -1919742.935956, 0.000000, 0.000000, 0.000000, 90.000000),
        new FrameSeed("EC_ROBOT_012", 997298.779636, -1919882.563616, 0.000000, 0.000000, 0.000000, 90.000000),
        new FrameSeed("EC_ROBOT_013", 998900.336074, -1922176.932823, 0.000000, 0.000000, 0.000000, 15.040094),
        new FrameSeed("EC_ROBOT_014", 997143.203358, -1923248.896649, 0.000000, 0.000000, 0.000000, 270.000000),
        new FrameSeed("EC_ROBOT_015", 1000353.613275, -1923833.499186, 0.000000, 0.000000, 0.000000, 270.000000),
        new FrameSeed("EC_ROBOT_016", 1006620.947926, -1924751.190627, 0.000000, 0.000000, 0.000000, 270.000000),
        new FrameSeed("EC_ROBOT_017", 1002666.235321, -1925526.895095, 0.000000, 0.000000, 0.000000, 270.304606),
        new FrameSeed("EC_ROBOT_018", 1001013.522063, -1927616.888052, 0.000000, 0.000000, 0.000000, 0.304606),
        new FrameSeed("EC_ROBOT_019", 999410.452504, -1927733.964976, 0.000000, 0.000000, 0.000000, 90.000000),
        new FrameSeed("EC_ROBOT_020", 997143.203358, -1928002.903427, 0.000000, 0.000000, 0.000000, 90.000000),
        new FrameSeed("EC_ROBOT_021", 999543.203358, -1932048.896649, 0.000000, 0.000000, 0.000000, 270.000000),
        new FrameSeed("EC_ROBOT_022", 997322.024845, -1932355.405231, 0.000000, 0.000000, 0.000000, 270.000000),
        new FrameSeed("EC_ROBOT_023", 997412.813089, -1936437.380921, 0.000000, 0.000000, 0.000000, 90.000000),
        new FrameSeed("EC_ROBOT_024", 999543.203358, -1936802.903427, 0.000000, 0.000000, 0.000000, 90.000000),
        new FrameSeed("EC_ROBOT_025", 1002447.135581, -1938377.519231, 0.000000, 0.000000, 0.000000, 0.304606),
        new FrameSeed("EC_ROBOT_026", 997418.422687, -1940177.401493, 0.000000, 0.000000, 0.000000, 270.000000),
        new FrameSeed("EC_ROBOT_027", 999373.921752, -1940246.257094, 0.000000, 0.000000, 0.000000, 270.000000),
        new FrameSeed("EC_ROBOT_028", 997509.210931, -1944259.377183, 0.000000, 0.000000, 0.000000, 90.000000),
        new FrameSeed("EC_ROBOT_029", 999464.709996, -1944328.232784, 0.000000, 0.000000, 0.000000, 90.000000),
        new FrameSeed("EC_ROBOT_030", 1001255.157271, -1946186.018210, 0.000000, 0.000000, 0.000000, 0.304606),
        new FrameSeed("MID_AUX_ROBOT_001", 1018903.962286, -1980406.273880, 0.000000, 0.000000, 0.000000, 270.000000),
        new FrameSeed("MID_AUX_ROBOT_002", 1020713.926320, -1980407.121238, 0.000000, 0.000000, 0.000000, 270.000000),
        new FrameSeed("MID_AUX_ROBOT_003", 1029087.281751, -1981119.205316, 0.000000, 0.000000, 0.000000, 270.000000),
        new FrameSeed("MID_AUX_ROBOT_004", 1026584.536351, -1981895.792533, 0.000000, 0.000000, 0.000000, 181.977595),
        new FrameSeed("MID_AUX_ROBOT_005", 1017174.418713, -1983327.339464, 0.000000, 0.000000, 0.000000, 159.780468),
        new FrameSeed("MID_AUX_ROBOT_006", 1024867.149078, -1985032.787282, 0.000000, 0.000000, 0.000000, 105.000000),
        new FrameSeed("MID_AUX_ROBOT_007", 1018927.710943, -1986337.966630, 0.000000, 0.000000, 0.000000, 90.000000),
        new FrameSeed("MID_AUX_ROBOT_008", 1020727.710943, -1986337.966630, 0.000000, 0.000000, 0.000000, 90.000000),
        new FrameSeed("MID_MAIN_ROBOT_001", 954625.198838, -1961249.465187, -1.000101, 0.000000, 0.000000, 360.000000),
        new FrameSeed("MID_MAIN_ROBOT_002", 952487.127162, -1962252.910950, -4.999970, 0.000000, 0.000000, 360.000000),
        new FrameSeed("MID_MAIN_ROBOT_003", 956198.191699, -1962302.948692, -4.999938, 0.000000, 0.000000, 360.000000),
        new FrameSeed("MID_MAIN_ROBOT_004", 957706.481715, -1962482.561669, -4.999970, 0.000000, 0.000000, 360.000000),
        new FrameSeed("MID_MAIN_ROBOT_005", 1024100.465495, -1962517.676116, 0.049970, 0.000000, 0.000000, 270.000000),
        new FrameSeed("MID_MAIN_ROBOT_006", 1004711.313337, -1962719.617202, -0.911892, 0.000000, 0.000000, 270.000000),
        new FrameSeed("MID_MAIN_ROBOT_007", 1007015.533777, -1962719.617202, -0.911892, 0.000000, 0.000000, 270.000000),
        new FrameSeed("MID_MAIN_ROBOT_008", 977675.620084, -1963016.269726, -0.000032, 0.000000, 0.000000, 180.000000),
        new FrameSeed("MID_MAIN_ROBOT_009", 979240.280726, -1963016.269726, -0.000032, 0.000000, 0.000000, 180.000000),
        new FrameSeed("MID_MAIN_ROBOT_010", 980928.286065, -1963016.269726, -0.000032, 0.000000, 0.000000, 180.000000),
        new FrameSeed("MID_MAIN_ROBOT_011", 997981.734781, -1963016.269726, -0.000032, 0.000000, 0.000000, 180.000000),
        new FrameSeed("MID_MAIN_ROBOT_012", 1000421.046333, -1963028.881210, -0.000002, 0.000000, 0.000000, 0.000000),
        new FrameSeed("MID_MAIN_ROBOT_013", 992937.273979, -1963136.441078, 0.000000, 0.000000, 0.000000, 180.000000),
        new FrameSeed("MID_MAIN_ROBOT_014", 1019234.562907, -1963860.529094, -18686004.694183, 0.000000, 0.000000, 180.000000),
        new FrameSeed("MID_MAIN_ROBOT_015", 1027298.939711, -1965339.503355, 0.049970, 0.000000, 0.000000, 41.919875),
        new FrameSeed("MID_MAIN_ROBOT_016", 1015519.626676, -1967385.526941, 0.049970, 0.000000, 0.000000, 180.000000),
        new FrameSeed("MID_MAIN_ROBOT_017", 1000421.046333, -1967397.598324, -0.000002, 0.000000, 0.000000, 360.000000),
        new FrameSeed("MID_MAIN_ROBOT_018", 977683.849341, -1967411.131081, -0.000032, 0.000000, 0.000000, 180.000000),
        new FrameSeed("MID_MAIN_ROBOT_019", 979248.509983, -1967411.131081, -0.000032, 0.000000, 0.000000, 180.000000),
        new FrameSeed("MID_MAIN_ROBOT_020", 980936.515322, -1967411.131081, -0.000032, 0.000000, 0.000000, 180.000000),
        new FrameSeed("MID_MAIN_ROBOT_021", 997989.964039, -1967411.131081, -0.000032, 0.000000, 0.000000, 180.000000),
        new FrameSeed("MID_MAIN_ROBOT_022", 1008057.543129, -1967480.083423, -0.911892, 0.000000, 0.000000, 90.000000),
        new FrameSeed("MID_MAIN_ROBOT_023", 1024301.996841, -1967591.756587, 0.000000, 0.000000, 0.000000, 180.000000),
        new FrameSeed("MID_MAIN_ROBOT_024", 1004562.060980, -1967699.646201, 0.000000, 0.000000, 0.000000, 90.000000),
        new FrameSeed("MID_MAIN_ROBOT_025", 952528.421164, -1968294.315886, -4.999938, 0.000000, 0.000000, 0.000000),
        new FrameSeed("MID_MAIN_ROBOT_026", 957778.934320, -1968473.871688, -4.999970, 0.000000, 0.000000, 0.000000),
        new FrameSeed("MID_MAIN_ROBOT_027", 956216.460398, -1968518.629041, -1.000166, 0.000000, 0.000000, 0.000000),
        new FrameSeed("MID_MAIN_ROBOT_028", 954601.699612, -1969357.657558, -1.000133, 0.000000, 0.000000, 0.000000),
        new FrameSeed("MID_MAIN_ROBOT_029", 1006411.167466, -1969529.309180, 0.049970, 0.000000, 0.000000, 0.000000),
        new FrameSeed("MID_MAIN_ROBOT_030", 1014024.544848, -1970905.006528, 0.049970, 0.000000, 0.000000, 180.000000),
        new FrameSeed("MID_MAIN_ROBOT_031", 957096.778188, -1980488.883793, -0.000002, 0.000000, 0.000000, 180.000000),
        new FrameSeed("MID_MAIN_ROBOT_032", 976232.053269, -1980510.880365, -0.000032, 0.000000, 0.000000, 180.000000),
        new FrameSeed("MID_MAIN_ROBOT_033", 978321.491971, -1980510.880365, -0.000032, 0.000000, 0.000000, 180.000000),
        new FrameSeed("MID_MAIN_ROBOT_034", 949897.138965, -1980510.883123, -0.000002, 0.000000, 0.000000, 180.000000),
        new FrameSeed("MID_MAIN_ROBOT_035", 952113.982793, -1980510.883123, -0.000002, 0.000000, 0.000000, 180.000000),
        new FrameSeed("MID_MAIN_ROBOT_036", 963232.053269, -1980510.883123, -0.000002, 0.000000, 0.000000, 180.000000),
        new FrameSeed("MID_MAIN_ROBOT_037", 965321.491971, -1980510.883123, -0.000002, 0.000000, 0.000000, 180.000000),
        new FrameSeed("MID_MAIN_ROBOT_038", 969246.903961, -1980510.883123, -0.000032, 0.000000, 0.000000, 180.000000),
        new FrameSeed("MID_MAIN_ROBOT_039", 970990.083836, -1980510.883123, -0.000032, 0.000000, 0.000000, 180.000000),
        new FrameSeed("MID_MAIN_ROBOT_040", 972639.343396, -1980510.883123, -0.000032, 0.000000, 0.000000, 180.000000),
        new FrameSeed("MID_MAIN_ROBOT_041", 983052.865636, -1980510.883123, -0.000032, 0.000000, 0.000000, 180.000000),
        new FrameSeed("MID_MAIN_ROBOT_042", 995499.157030, -1980510.883123, -0.000002, 0.000000, 0.000000, 180.000000),
        new FrameSeed("MID_MAIN_ROBOT_043", 997942.972307, -1980510.883123, -0.000002, 0.000000, 0.000000, 180.000000),
        new FrameSeed("MID_MAIN_ROBOT_044", 959629.794156, -1980548.883793, -0.000002, 0.000000, 0.000000, 180.000000),
        new FrameSeed("MID_MAIN_ROBOT_045", 976232.053269, -1984905.741720, -0.000032, 0.000000, 0.000000, 180.000000),
        new FrameSeed("MID_MAIN_ROBOT_046", 978321.491971, -1984905.741720, -0.000032, 0.000000, 0.000000, 180.000000),
        new FrameSeed("MID_MAIN_ROBOT_047", 963232.053269, -1984905.744478, -0.000002, 0.000000, 0.000000, 180.000000),
        new FrameSeed("MID_MAIN_ROBOT_048", 965321.491971, -1984905.744478, -0.000002, 0.000000, 0.000000, 180.000000),
        new FrameSeed("MID_MAIN_ROBOT_049", 970990.083836, -1984905.744478, -0.000032, 0.000000, 0.000000, 180.000000),
        new FrameSeed("MID_MAIN_ROBOT_050", 972639.343396, -1984905.744478, -0.000032, 0.000000, 0.000000, 180.000000),
        new FrameSeed("MID_MAIN_ROBOT_051", 995499.157030, -1984905.744478, -0.000002, 0.000000, 0.000000, 180.000000),
        new FrameSeed("MID_MAIN_ROBOT_052", 997942.972307, -1984905.744478, -0.000002, 0.000000, 0.000000, 180.000000),
        new FrameSeed("MID_MAIN_ROBOT_053", 952029.037422, -1984921.941969, -0.000002, 0.000000, 0.000000, 180.000000),
        new FrameSeed("MID_MAIN_ROBOT_054", 956381.220768, -1984985.854763, -0.000002, 0.000000, 0.000000, 0.000000),
        new FrameSeed("MID_MAIN_ROBOT_055", 985141.324233, -1984989.246943, 0.000000, 0.000000, 0.000000, 180.000000),
        new FrameSeed("MID_MAIN_ROBOT_056", 969174.786596, -1985034.375544, -0.000032, 0.000000, 0.000000, 180.000000),
        new FrameSeed("MID_MAIN_ROBOT_057", 982980.748271, -1985034.375544, -0.000032, 0.000000, 0.000000, 180.000000),
        new FrameSeed("MID_MAIN_ROBOT_058", 959629.794156, -1985051.800731, -0.000002, 0.000000, 0.000000, 0.000000),
        new FrameSeed("MID_MAIN_ROBOT_059", 949812.193594, -1985052.874176, -0.000002, 0.000000, 0.000000, 180.000000),
        new FrameSeed("MID_MAIN_TRACK_ROBOT_001", 971647.288625, -1962788.230904, 0.000000, 0.000000, 0.000000, 270.000000),
        new FrameSeed("MID_MAIN_TRACK_ROBOT_002", 970954.856998, -1967404.486438, 0.000000, 0.000000, 0.000000, 90.000000),
        new FrameSeed("MID_MAIN_TRACK_ROBOT_003", 972818.688496, -1967631.890467, 0.000000, 0.000000, 0.000000, 90.000000),
        new FrameSeed("MID_MAIN_TRACK_ROBOT_004", 958012.260667, -1985348.362206, 0.000000, 0.000000, 0.000000, 90.000000),
        new FrameSeed("RR_ROBOT_001", 1017567.149050, -1898613.418682, 0.000000, 0.000000, 0.000000, 359.863453),
        new FrameSeed("RR_ROBOT_002", 1017567.149050, -1900613.418682, 0.000000, 0.000000, 0.000000, 0.136547),
        new FrameSeed("RR_ROBOT_003", 1022710.976094, -1903053.458348, 0.000000, 0.000000, 0.000000, 0.000000),
        new FrameSeed("RR_ROBOT_004", 1018025.719127, -1903151.632632, 0.000000, 0.000000, 0.000000, 0.304606),
        new FrameSeed("RR_ROBOT_005", 1017567.149050, -1905482.616385, 0.000000, 0.000000, 0.000000, 359.863453),
        new FrameSeed("RR_ROBOT_006", 1017567.149050, -1907482.616385, 0.000000, 0.000000, 0.000000, 0.136547),
        new FrameSeed("RR_ROBOT_007", 1023382.126320, -1910142.099293, 0.000000, 0.000000, 0.000000, 180.304606),
        new FrameSeed("RR_ROBOT_008", 1019741.023306, -1918959.784100, 0.000000, 0.000000, 0.000000, 51.154348),
        new FrameSeed("RR_ROBOT_009", 1023975.719127, -1919866.769669, 0.000000, 0.000000, 0.000000, 0.304606),
        new FrameSeed("RR_ROBOT_010", 1017289.137676, -1921901.787053, 0.000000, 0.000000, 0.000000, 359.863453),
        new FrameSeed("RR_ROBOT_011", 1017157.425641, -1923296.362906, 0.000000, 0.000000, 0.000000, 360.000000),
        new FrameSeed("RR_ROBOT_012", 1017305.842873, -1924933.850467, 0.000000, 0.000000, 0.000000, 359.863453),
        new FrameSeed("RR_ROBOT_013", 1023821.486748, -1926769.742920, 0.000000, 0.000000, 0.000000, 0.304606),
        new FrameSeed("RR_ROBOT_014", 1019789.861625, -1927918.902481, 0.000000, 0.000000, 0.000000, 270.136547),
        new FrameSeed("RR_ROBOT_015", 1022124.420782, -1928066.062691, 0.000000, 0.000000, 0.000000, 270.000000),
        new FrameSeed("RR_ROBOT_016", 1023821.486748, -1931834.713645, 0.000000, 0.000000, 0.000000, 0.304606),
        new FrameSeed("RR_ROBOT_017", 1022100.424070, -1932181.440939, 0.000000, 0.000000, 0.000000, 90.000000),
        new FrameSeed("RR_ROBOT_018", 1018664.604115, -1932273.901503, 0.000000, 0.000000, 0.000000, 89.863453),
        new FrameSeed("RR_ROBOT_019", 1020443.954298, -1935367.267380, 0.000000, 0.000000, 0.000000, 0.304606),
        new FrameSeed("RR_ROBOT_020", 1016996.620752, -1936838.776249, 0.000000, 0.000000, 0.000000, 360.000000),
        new FrameSeed("RR_ROBOT_021", 1016996.620752, -1938377.135565, 0.000000, 0.000000, 0.000000, 360.000000),
        new FrameSeed("RR_ROBOT_022", 1022181.842875, -1938431.143339, 0.000000, 0.000000, 0.000000, 270.000000),
        new FrameSeed("RR_ROBOT_023", 1023815.245903, -1940724.801914, 0.000000, 0.000000, 0.000000, 180.304606),
        new FrameSeed("RR_ROBOT_024", 1018983.264204, -1942585.756809, 0.000000, 0.000000, 0.000000, 90.000000),
        new FrameSeed("RR_ROBOT_025", 1026219.423187, -1945536.597332, 0.000000, 0.000000, 0.000000, 270.000000),
        new FrameSeed("RR_ROBOT_026", 1022425.173788, -1946463.356594, 0.000000, 0.000000, 0.000000, 0.718891),
        new FrameSeed("RR_ROBOT_027", 1022289.402934, -1948282.878404, 0.000000, 0.000000, 0.000000, 224.718891),
        new FrameSeed("RR_ROBOT_028", 1026147.589769, -1949716.073885, 0.000000, 0.000000, 0.000000, 90.000000),
    };

    // ------------------------------------------------------------------------
    // DXF TEXT DATA
    // ------------------------------------------------------------------------

    private sealed class CadText
    {
        public string Text;
        public double X;
        public double Y;
    }

    private sealed class StationLabel
    {
        public string Name;
        public double X;
        public double Y;
    }

    private sealed class RobotLabel
    {
        public int Id;
        public int Number;
        public double X;
        public double Y;
    }

    private sealed class MatchCandidate
    {
        public double Distance;
        public int SeedIndex;
        public int LabelId;
        public int RobotNo;
    }

    public static void Main()
    {
        StringWriter output = new StringWriter();

        try
        {
            Run(ref output);
        }
        catch (Exception ex)
        {
            Exception detail = ex;
            while (detail.InnerException != null)
                detail = detail.InnerException;

            output.WriteLine("");
            output.WriteLine(
                "FATAL: " +
                detail.GetType().Name +
                " - " +
                detail.Message);
        }

        TxMessageBox.Show(
            output.ToString(),
            "3DRbotbuild Phase 1 - Frame Builder",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private static void Run(ref StringWriter output)
    {
        TxDocument document = TxApplication.ActiveDocument;
        if (document == null)
            throw new InvalidOperationException(
                "No active Process Simulate study.");

        TxPhysicalRoot physicalRoot = document.PhysicalRoot;
        if (physicalRoot == null)
            throw new InvalidOperationException(
                "Active study has no PhysicalRoot.");

        ResolveDxfPath();

        output.WriteLine("DXF naming source : " + DxfPath);
        output.WriteLine("Mode              : FRAME BUILDER ONLY");
        output.WriteLine("");

        // ----------------------------------------------------
        // 1) Read CAD station / Rxx text
        // ----------------------------------------------------
        List<CadText> cadTexts = ReadDxfText(DxfPath);
        List<StationLabel> stations = BuildStationLabels(cadTexts);
        List<RobotLabel> robotLabels = BuildRobotLabels(cadTexts);

        output.WriteLine("CAD text records  : " + cadTexts.Count);
        output.WriteLine("Station labels    : " + stations.Count);
        output.WriteLine("Bare Rxx labels   : " + robotLabels.Count);
        output.WriteLine("");

        // ----------------------------------------------------
        // 2) Resolve names for ALL Robot Frames
        // ----------------------------------------------------
        ResolveEngineeringNames(
            FrameData,
            stations,
            robotLabels,
            ref output);

        // ----------------------------------------------------
        // 3) Create / rename Frames
        // ----------------------------------------------------
        List<string> robotFrameNames = new List<string>();
        Dictionary<string, string> oldToNew =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

        int created = 0;
        int renamed = 0;
        int skipped = 0;
        int failed = 0;

        for (int i = 0; i < FrameData.Length; i++)
        {
            FrameSeed seed = FrameData[i];

            if (seed.IsRobot)
            {
                oldToNew[seed.OriginalName] = seed.NewName;
                robotFrameNames.Add(seed.NewName);
            }

            try
            {
                TxFrame newFrame =
                    FindFrame(
                        physicalRoot,
                        seed.NewName);

                if (newFrame != null)
                {
                    skipped++;
                    continue;
                }

                TxFrame oldFrame =
                    FindFrame(
                        physicalRoot,
                        seed.OriginalName);

                if (oldFrame != null &&
                    !String.Equals(
                        seed.OriginalName,
                        seed.NewName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    oldFrame.Name = seed.NewName;

                    TxTransformation target =
                        BuildTransform(seed);

                    oldFrame.AbsoluteLocation = target;
                    renamed++;

                    output.WriteLine(
                        "RENAME FRAME " +
                        seed.OriginalName +
                        " -> " +
                        seed.NewName);

                    continue;
                }

                TxFrame createdFrame =
                    CreateFrame(
                        physicalRoot,
                        seed.NewName,
                        BuildTransform(seed));

                if (createdFrame != null)
                    created++;
                else
                    failed++;
            }
            catch (Exception ex)
            {
                failed++;

                output.WriteLine(
                    "FAIL FRAME " +
                    seed.NewName +
                    ": " +
                    Unwrap(ex).Message);
            }
        }

        output.WriteLine("");
        output.WriteLine(
            "Frame result: CREATE=" +
            created +
            " RENAME=" +
            renamed +
            " SKIP=" +
            skipped +
            " FAIL=" +
            failed);

        // ----------------------------------------------------
        // 4) Export FrameCatalog.csv for Phase 2 RobotBuilder.
        //    Function is intentionally UNASSIGNED in Phase 1.
        // ----------------------------------------------------
        string catalogPath =
            ExportFrameCatalog(
                FrameData);

        output.WriteLine("");
        output.WriteLine("========== PHASE 1 COMPLETE ==========");
        output.WriteLine("Frames only. No Robot resource was inserted.");
        output.WriteLine("Frame catalog: " + catalogPath);
        output.WriteLine("======================================");
    }

    // ========================================================================
    // ENGINEERING NAMING
    // ========================================================================

    private static void ResolveEngineeringNames(
        FrameSeed[] seeds,
        List<StationLabel> stations,
        List<RobotLabel> robotLabels,
        ref StringWriter output)
    {
        // First: station assignment.
        for (int i = 0; i < seeds.Length; i++)
        {
            FrameSeed seed = seeds[i];

            if (!seed.IsRobot)
                continue;

            seed.Station =
                FindNearestStation(
                    seed,
                    stations);

            if (String.IsNullOrEmpty(seed.Station))
            {
                seed.Station =
                    BuildFallbackStation(seed);

                output.WriteLine(
                    "WARNING STATION FALLBACK: " +
                    seed.OriginalName +
                    " -> " +
                    seed.Station);
            }

            seed.Line =
                ResolveLineName(
                    seed.OriginalName,
                    seed.Station);
        }

        // Group Robot Frames by LINE + STATION.
        Dictionary<string, List<int>> groups =
            new Dictionary<string, List<int>>(
                StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < seeds.Length; i++)
        {
            if (!seeds[i].IsRobot)
                continue;

            string key =
                seeds[i].Line +
                "|" +
                seeds[i].Station;

            List<int> list;
            if (!groups.TryGetValue(key, out list))
            {
                list = new List<int>();
                groups[key] = list;
            }

            list.Add(i);
        }

        Dictionary<int, bool> globallyUsedLabelIds =
            new Dictionary<int, bool>();

        foreach (
            KeyValuePair<string, List<int>> groupPair
            in groups)
        {
            List<int> group = groupPair.Value;

            // Build every valid Robot <-> bare Rxx candidate.
            List<MatchCandidate> candidates =
                new List<MatchCandidate>();

            for (int g = 0; g < group.Count; g++)
            {
                int seedIndex = group[g];
                FrameSeed seed = seeds[seedIndex];

                for (int l = 0; l < robotLabels.Count; l++)
                {
                    RobotLabel label = robotLabels[l];

                    double dx = seed.X - label.X;
                    double dy = seed.Y - label.Y;

                    double distance =
                        Math.Sqrt(
                            dx * dx +
                            dy * dy);

                    if (distance >
                        RobotLabelSearchRadiusMm)
                        continue;

                    MatchCandidate c =
                        new MatchCandidate();

                    c.Distance = distance;
                    c.SeedIndex = seedIndex;
                    c.LabelId = label.Id;
                    c.RobotNo = label.Number;

                    candidates.Add(c);
                }
            }

            candidates.Sort(
                delegate(
                    MatchCandidate a,
                    MatchCandidate b)
                {
                    return a.Distance.CompareTo(
                        b.Distance);
                });

            Dictionary<int, bool> usedSeeds =
                new Dictionary<int, bool>();

            Dictionary<int, bool> usedNumbers =
                new Dictionary<int, bool>();

            // Greedy nearest unique CAD Rxx assignment.
            for (int c = 0; c < candidates.Count; c++)
            {
                MatchCandidate candidate =
                    candidates[c];

                if (usedSeeds.ContainsKey(
                    candidate.SeedIndex))
                    continue;

                if (usedNumbers.ContainsKey(
                    candidate.RobotNo))
                    continue;

                if (globallyUsedLabelIds.ContainsKey(
                    candidate.LabelId))
                    continue;

                seeds[candidate.SeedIndex].RobotNo =
                    candidate.RobotNo;

                usedSeeds[candidate.SeedIndex] = true;
                usedNumbers[candidate.RobotNo] = true;
                globallyUsedLabelIds[candidate.LabelId] = true;
            }

            // Deterministic fallback numbering for Robots whose Rxx
            // text could not be reliably matched.
            group.Sort(
                delegate(int a, int b)
                {
                    int cy =
                        seeds[b].Y.CompareTo(
                            seeds[a].Y);

                    if (cy != 0)
                        return cy;

                    int cx =
                        seeds[a].X.CompareTo(
                            seeds[b].X);

                    if (cx != 0)
                        return cx;

                    return String.Compare(
                        seeds[a].OriginalName,
                        seeds[b].OriginalName,
                        StringComparison.OrdinalIgnoreCase);
                });

            int nextNo = 1;

            for (int g = 0; g < group.Count; g++)
            {
                FrameSeed seed =
                    seeds[group[g]];

                if (seed.RobotNo > 0)
                    continue;

                while (
                    usedNumbers.ContainsKey(
                        nextNo))
                {
                    nextNo++;
                }

                seed.RobotNo = nextNo;
                usedNumbers[nextNo] = true;
                nextNo++;
            }
        }

        // Final name.
        Dictionary<string, bool> finalNames =
            new Dictionary<string, bool>(
                StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < seeds.Length; i++)
        {
            FrameSeed seed = seeds[i];

            if (!seed.IsRobot)
                continue;

            seed.NewName =
                seed.Line +
                "_" +
                seed.Station +
                "_R" +
                seed.RobotNo.ToString("00");

            if (finalNames.ContainsKey(
                seed.NewName))
            {
                throw new InvalidDataException(
                    "Duplicate engineering Robot Frame name: " +
                    seed.NewName);
            }

            finalNames[seed.NewName] = true;

            output.WriteLine(
                "NAME " +
                seed.OriginalName +
                " -> " +
                seed.NewName);
        }

        output.WriteLine("");
    }

    private static string FindNearestStation(
        FrameSeed seed,
        List<StationLabel> stations)
    {
        string best = null;
        double bestDistance = Double.MaxValue;

        for (int i = 0; i < stations.Count; i++)
        {
            StationLabel station = stations[i];

            if (!StationAllowedForSeed(
                seed,
                station.Name))
                continue;

            double dx = seed.X - station.X;
            double dy = seed.Y - station.Y;

            double d =
                Math.Sqrt(
                    dx * dx +
                    dy * dy);

            if (d >
                StationSearchRadiusMm)
                continue;

            if (d < bestDistance)
            {
                bestDistance = d;
                best = station.Name;
            }
        }

        return best;
    }

    private static bool StationAllowedForSeed(
        FrameSeed seed,
        string station)
    {
        if (String.IsNullOrEmpty(station))
            return false;

        string s =
            station.ToUpperInvariant();

        string n =
            seed.OriginalName.ToUpperInvariant();

        if (n.StartsWith("EC_"))
            return s.StartsWith("EC");

        if (n.StartsWith("MID_MAIN_"))
            return s.StartsWith("BM") ||
                   s.StartsWith("FM") ||
                   s.StartsWith("FR");

        if (n.StartsWith("BSL_") ||
            n.StartsWith("BSR_"))
        {
            return s.StartsWith("OP") ||
                   s.StartsWith("WH_OP") ||
                   s.StartsWith("WHOP") ||
                   s.StartsWith("INR") ||
                   s.StartsWith("BSL") ||
                   s.StartsWith("BSR");
        }

        if (n.StartsWith("RR_"))
        {
            return s.StartsWith("RR") ||
                   s.StartsWith("OP");
        }

        if (n.StartsWith("MID_AUX_") ||
            n.StartsWith("BOTTOM_"))
        {
            return s.StartsWith("BM") ||
                   s.StartsWith("FM") ||
                   s.StartsWith("FR") ||
                   s.StartsWith("ST") ||
                   s.StartsWith("OP");
        }

        return true;
    }

    private static string ResolveLineName(
        string originalName,
        string station)
    {
        string n =
            originalName.ToUpperInvariant();

        if (n.StartsWith("EC_"))
            return "EC";

        if (n.StartsWith("BSL_"))
            return "BSL";

        if (n.StartsWith("BSR_"))
            return "BSR";

        if (n.StartsWith("RR_"))
            return "RR";

        // MID_MAIN / MID_AUX / BOTTOM belong to the
        // actual BM/FM/FR/ST line indicated by the station label.
        string stationPrefix =
            LeadingLetters(station);

        if (!String.IsNullOrEmpty(
            stationPrefix))
            return stationPrefix;

        if (n.StartsWith("MID_MAIN_"))
            return "MID";

        if (n.StartsWith("MID_AUX_"))
            return "MID";

        if (n.StartsWith("BOTTOM_"))
            return "BOTTOM";

        return "LINE";
    }

    private static string BuildFallbackStation(
        FrameSeed seed)
    {
        string n =
            seed.OriginalName.ToUpperInvariant();

        if (n.StartsWith("EC_"))
            return "EC_UNKNOWN";

        if (n.StartsWith("BSL_"))
            return "OP_UNKNOWN";

        if (n.StartsWith("BSR_"))
            return "OP_UNKNOWN";

        if (n.StartsWith("RR_"))
            return "RR_UNKNOWN";

        if (n.StartsWith("MID_MAIN_"))
            return "MID_UNKNOWN";

        if (n.StartsWith("MID_AUX_"))
            return "MID_AUX_UNKNOWN";

        if (n.StartsWith("BOTTOM_"))
            return "BOTTOM_UNKNOWN";

        return "UNKNOWN";
    }

    private static string LeadingLetters(
        string value)
    {
        if (String.IsNullOrEmpty(value))
            return String.Empty;

        Match m =
            Regex.Match(
                value,
                @"^[A-Z]+");

        if (!m.Success)
            return String.Empty;

        return m.Value;
    }

    // ========================================================================
    // DXF TEXT PARSER
    // ========================================================================

    private static void ResolveDxfPath()
    {
        if (File.Exists(DxfPath))
            return;

        using (
            OpenFileDialog dialog =
                new OpenFileDialog())
        {
            dialog.Title =
                "Select factory DXF for station naming";

            dialog.Filter =
                "DXF files (*.dxf)|*.dxf|All files (*.*)|*.*";

            dialog.CheckFileExists = true;
            dialog.Multiselect = false;

            if (
                dialog.ShowDialog() !=
                DialogResult.OK)
            {
                throw new FileNotFoundException(
                    "A DXF is required to resolve station-based Robot names.");
            }

            DxfPath = dialog.FileName;
        }
    }

    private static List<CadText> ReadDxfText(
        string path)
    {
        List<CadText> result =
            new List<CadText>();

        using (
            FileStream probe =
                File.OpenRead(path))
        {
            byte[] header =
                new byte[22];

            int read =
                probe.Read(
                    header,
                    0,
                    header.Length);

            string h =
                Encoding.ASCII.GetString(
                    header,
                    0,
                    read);

            if (
                h.StartsWith(
                    "AutoCAD Binary DXF",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException(
                    "Binary DXF is not supported for station naming. Export ASCII DXF.");
            }
        }

        using (
            StreamReader reader =
                new StreamReader(
                    path,
                    Encoding.Default,
                    true,
                    65536))
        {
            string section =
                String.Empty;

            bool waitingSectionName =
                false;

            string entityType =
                null;

            List<int> codes =
                new List<int>();

            List<string> values =
                new List<string>();

            while (true)
            {
                string codeLine =
                    reader.ReadLine();

                if (codeLine == null)
                    break;

                string valueLine =
                    reader.ReadLine();

                if (valueLine == null)
                    break;

                int code;
                if (!Int32.TryParse(
                    codeLine.Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out code))
                {
                    continue;
                }

                string value =
                    valueLine;

                if (
                    code == 0 &&
                    String.Equals(
                        value.Trim(),
                        "SECTION",
                        StringComparison.OrdinalIgnoreCase))
                {
                    FlushTextEntity(
                        entityType,
                        codes,
                        values,
                        result);

                    entityType = null;
                    codes.Clear();
                    values.Clear();

                    waitingSectionName = true;
                    continue;
                }

                if (waitingSectionName)
                {
                    if (code == 2)
                    {
                        section =
                            value.Trim().ToUpperInvariant();

                        waitingSectionName =
                            false;
                    }

                    continue;
                }

                if (
                    code == 0 &&
                    String.Equals(
                        value.Trim(),
                        "ENDSEC",
                        StringComparison.OrdinalIgnoreCase))
                {
                    FlushTextEntity(
                        entityType,
                        codes,
                        values,
                        result);

                    entityType = null;
                    codes.Clear();
                    values.Clear();
                    section = String.Empty;
                    continue;
                }

                if (!String.Equals(
                    section,
                    "ENTITIES",
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (code == 0)
                {
                    FlushTextEntity(
                        entityType,
                        codes,
                        values,
                        result);

                    entityType =
                        value.Trim().ToUpperInvariant();

                    codes.Clear();
                    values.Clear();
                    continue;
                }

                if (entityType != null)
                {
                    codes.Add(code);
                    values.Add(value);
                }
            }

            FlushTextEntity(
                entityType,
                codes,
                values,
                result);
        }

        return result;
    }

    private static void FlushTextEntity(
        string entityType,
        List<int> codes,
        List<string> values,
        List<CadText> output)
    {
        if (
            !String.Equals(
                entityType,
                "TEXT",
                StringComparison.OrdinalIgnoreCase) &&
            !String.Equals(
                entityType,
                "MTEXT",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        double x = 0.0;
        double y = 0.0;
        int paperSpace = 0;

        StringBuilder text =
            new StringBuilder();

        for (int i = 0; i < codes.Count; i++)
        {
            int code = codes[i];
            string value = values[i];

            if (code == 10)
                Double.TryParse(
                    value.Trim(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out x);

            else if (code == 20)
                Double.TryParse(
                    value.Trim(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out y);

            else if (code == 67)
                Int32.TryParse(
                    value.Trim(),
                    out paperSpace);

            else if (
                code == 1 ||
                code == 3)
                text.Append(value);
        }

        if (paperSpace == 1)
            return;

        CadText item =
            new CadText();

        item.Text =
            NormalizeCadText(
                text.ToString());

        item.X = x;
        item.Y = y;

        if (!String.IsNullOrWhiteSpace(
            item.Text))
            output.Add(item);
    }

    private static string NormalizeCadText(
        string value)
    {
        if (value == null)
            return String.Empty;

        string result =
            value.Replace("\\P", " ");

        result =
            Regex.Replace(
                result,
                @"\\[A-Za-z][^;]*;",
                String.Empty);

        result =
            result.Replace("{", "").
                   Replace("}", "").
                   Trim();

        return result;
    }

    private static List<StationLabel> BuildStationLabels(
        List<CadText> texts)
    {
        List<StationLabel> result =
            new List<StationLabel>();

        Dictionary<string, bool> seen =
            new Dictionary<string, bool>(
                StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < texts.Count; i++)
        {
            string station =
                ExtractStationToken(
                    texts[i].Text);

            if (String.IsNullOrEmpty(
                station))
                continue;

            string key =
                station +
                "|" +
                Math.Round(texts[i].X).ToString(
                    CultureInfo.InvariantCulture) +
                "|" +
                Math.Round(texts[i].Y).ToString(
                    CultureInfo.InvariantCulture);

            if (seen.ContainsKey(key))
                continue;

            seen[key] = true;

            StationLabel item =
                new StationLabel();

            item.Name = station;
            item.X = texts[i].X;
            item.Y = texts[i].Y;

            result.Add(item);
        }

        return result;
    }

    private static string ExtractStationToken(
        string text)
    {
        if (String.IsNullOrWhiteSpace(text))
            return null;

        string value =
            text.ToUpperInvariant();

        // Important: R01 / RT01 / robot models are deliberately
        // excluded from Station matching.
        string pattern =
            @"(?:WH[\s_\-]*OP|WHOP|INR|EC|OP|BM|FM|FR|ST|RR|BSL|BSR)" +
            @"[\s_\-]*0*\d{1,3}";

        Match m =
            Regex.Match(
                value,
                pattern,
                RegexOptions.IgnoreCase);

        if (!m.Success)
            return null;

        string token =
            Regex.Replace(
                m.Value,
                @"[\s\-]+",
                "_");

        token =
            Regex.Replace(
                token,
                @"_+",
                "_");

        // EC 040 -> EC40, OP 010 -> OP10, BM 02 -> BM02.
        Match split =
            Regex.Match(
                token,
                @"^([A-Z_]+?)_?0*(\d{1,3})$");

        if (split.Success)
        {
            string prefix =
                split.Groups[1].Value.
                TrimEnd('_');

            int number;
            if (Int32.TryParse(
                split.Groups[2].Value,
                out number))
            {
                if (prefix == "WHOP")
                    prefix = "WH_OP";

                return
                    prefix +
                    number.ToString("00");
            }
        }

        return token;
    }

    private static List<RobotLabel> BuildRobotLabels(
        List<CadText> texts)
    {
        List<RobotLabel> result =
            new List<RobotLabel>();

        int id = 0;

        for (int i = 0; i < texts.Count; i++)
        {
            Match m =
                Regex.Match(
                    texts[i].Text.Trim(),
                    @"^R\s*0*(\d{1,2})$",
                    RegexOptions.IgnoreCase);

            if (!m.Success)
                continue;

            int number;
            if (!Int32.TryParse(
                m.Groups[1].Value,
                out number))
                continue;

            RobotLabel label =
                new RobotLabel();

            label.Id = id++;
            label.Number = number;
            label.X = texts[i].X;
            label.Y = texts[i].Y;

            result.Add(label);
        }

        return result;
    }

    // ========================================================================
    // FRAME CREATION / UPGRADE
    // ========================================================================

    private static TxTransformation BuildTransform(
        FrameSeed seed)
    {
        double z = seed.Z;

        if (
            seed.IsRobot &&
            (Double.IsNaN(z) ||
             Double.IsInfinity(z) ||
             Math.Abs(z) > MaxAbsRobotZMm))
        {
            z = 0.0;
        }

        double d2r =
            Math.PI / 180.0;

        return new TxTransformation(
            new TxVector(
                seed.X,
                seed.Y,
                z),
            new TxVector(
                seed.Rx * d2r,
                seed.Ry * d2r,
                seed.Rz * d2r),
            TxTransformation.TxRotationType.RPY_XYZ);
    }

    private static TxFrame CreateFrame(
        TxPhysicalRoot root,
        string name,
        TxTransformation tr)
    {
        Type frameDataType =
            FindType(
                "TxFrameCreationData");

        if (frameDataType == null)
            throw new InvalidOperationException(
                "TxFrameCreationData type not found.");

        object data =
            MakeCreationData(
                frameDataType,
                name,
                tr);

        if (data == null)
            throw new InvalidOperationException(
                "Cannot construct TxFrameCreationData.");

        MethodInfo create =
            FindCreateFrameMethod(
                root.GetType(),
                frameDataType);

        if (create == null)
            throw new InvalidOperationException(
                "No compatible CreateFrame method.");

        object created =
            create.Invoke(
                root,
                new object[] { data });

        if (created == null)
            throw new InvalidOperationException(
                "CreateFrame returned null.");

        SetProperty(
            created,
            "AbsoluteLocation",
            tr);

        SetProperty(
            created,
            "Location",
            tr);

        TxFrame frame =
            created as TxFrame;

        if (frame == null)
            throw new InvalidOperationException(
                "Created object is not a TxFrame.");

        return frame;
    }

    private static TxFrame FindFrame(
        TxPhysicalRoot root,
        string name)
    {
        TxObjectList frames =
            root.GetAllDescendants(
                new TxTypeFilter(
                    typeof(TxFrame)));

        foreach (ITxObject item in frames)
        {
            TxFrame frame =
                item as TxFrame;

            if (
                frame != null &&
                String.Equals(
                    frame.Name,
                    name,
                    StringComparison.OrdinalIgnoreCase))
            {
                return frame;
            }
        }

        return null;
    }

    // ========================================================================
    // ROBOT PLACEMENT
    // ========================================================================




    // ========================================================================
    // PHASE 1 OUTPUT - FRAME CATALOG
    // ========================================================================

    private static string ExportFrameCatalog(
        FrameSeed[] seeds)
    {
        string folder =
            Path.GetDirectoryName(
                DxfPath);

        if (String.IsNullOrEmpty(folder))
            folder =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.DesktopDirectory);

        string path =
            Path.Combine(
                folder,
                "3DRbotbuild_FrameCatalog.csv");

        using (
            StreamWriter writer =
                new StreamWriter(
                    path,
                    false,
                    new UTF8Encoding(true)))
        {
            writer.WriteLine(
                "FrameName,OriginalName,Line,Station,RobotNo,FrameType,Function,X,Y,Z,Rx,Ry,Rz");

            for (int i = 0;
                 i < seeds.Length;
                 i++)
            {
                FrameSeed seed =
                    seeds[i];

                string frameType =
                    seed.IsRobot ?
                    (
                        seed.OriginalName.IndexOf(
                            "_TRACK_ROBOT_",
                            StringComparison.OrdinalIgnoreCase) >= 0 ?
                        "TRACK_ROBOT" :
                        "ROBOT"
                    ) :
                    "BASE";

                string function =
                    seed.IsRobot ?
                    "UNASSIGNED" :
                    "";

                writer.WriteLine(
                    Csv(seed.NewName) + "," +
                    Csv(seed.OriginalName) + "," +
                    Csv(seed.Line) + "," +
                    Csv(seed.Station) + "," +
                    (
                        seed.RobotNo > 0 ?
                        ("R" + seed.RobotNo.ToString("00")) :
                        ""
                    ) + "," +
                    Csv(frameType) + "," +
                    Csv(function) + "," +
                    seed.X.ToString("F6", CultureInfo.InvariantCulture) + "," +
                    seed.Y.ToString("F6", CultureInfo.InvariantCulture) + "," +
                    seed.Z.ToString("F6", CultureInfo.InvariantCulture) + "," +
                    seed.Rx.ToString("F6", CultureInfo.InvariantCulture) + "," +
                    seed.Ry.ToString("F6", CultureInfo.InvariantCulture) + "," +
                    seed.Rz.ToString("F6", CultureInfo.InvariantCulture));
            }
        }

        return path;
    }

    private static string Csv(
        string value)
    {
        if (value == null)
            value = String.Empty;

        bool quote =
            value.IndexOf(',') >= 0 ||
            value.IndexOf('"') >= 0 ||
            value.IndexOf('\n') >= 0 ||
            value.IndexOf('\r') >= 0;

        value =
            value.Replace(
                "\"",
                "\"\"");

        return quote ?
            "\"" + value + "\"" :
            value;
    }

    // ========================================================================
    // RESOURCE / GENERIC HELPERS
    // ========================================================================



    private static ITxObject FindObjectByName(
        TxPhysicalRoot root,
        string name)
    {
        TxObjectList objects =
            root.GetAllDescendants(
                new TxNoTypeFilter());

        foreach (ITxObject item in objects)
        {
            if (
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

    private static Exception Unwrap(
        Exception ex)
    {
        Exception detail = ex;

        while (
            detail.InnerException != null)
        {
            detail =
                detail.InnerException;
        }

        return detail;
    }

    private static double NormalizeAngle(
        double radians)
    {
        while (radians > Math.PI)
            radians -= 2.0 * Math.PI;

        while (radians < -Math.PI)
            radians += 2.0 * Math.PI;

        return radians;
    }

    private static Type FindType(
        string shortName)
    {
        Assembly asm =
            typeof(TxApplication).Assembly;

        Type[] types =
            asm.GetTypes();

        for (int i = 0;
             i < types.Length;
             i++)
        {
            if (
                types[i].Name ==
                shortName)
            {
                return types[i];
            }
        }

        return null;
    }

    private static object MakeCreationData(
        Type t,
        string name,
        TxTransformation tr)
    {
        ConstructorInfo[] ctors =
            t.GetConstructors();

        for (int i = 0;
             i < ctors.Length;
             i++)
        {
            ParameterInfo[] ps =
                ctors[i].GetParameters();

            object[] args =
                new object[ps.Length];

            try
            {
                for (int j = 0;
                     j < ps.Length;
                     j++)
                {
                    Type pt =
                        ps[j].ParameterType;

                    if (pt ==
                        typeof(string))
                        args[j] = name;

                    else if (
                        pt ==
                        typeof(TxTransformation))
                        args[j] = tr;

                    else if (
                        ps[j].HasDefaultValue)
                        args[j] =
                            ps[j].DefaultValue;

                    else if (
                        pt.IsValueType)
                        args[j] =
                            Activator.CreateInstance(
                                pt);

                    else
                        args[j] = null;
                }

                object data =
                    ctors[i].Invoke(
                        args);

                SetProperty(
                    data,
                    "Name",
                    name);

                SetProperty(
                    data,
                    "AbsoluteLocation",
                    tr);

                SetProperty(
                    data,
                    "Location",
                    tr);

                SetProperty(
                    data,
                    "Transformation",
                    tr);

                return data;
            }
            catch
            {
            }
        }

        return null;
    }

    private static MethodInfo FindCreateFrameMethod(
        Type rootType,
        Type dataType)
    {
        MethodInfo[] methods =
            rootType.GetMethods(
                BindingFlags.Instance |
                BindingFlags.Public);

        for (int i = 0;
             i < methods.Length;
             i++)
        {
            MethodInfo method =
                methods[i];

            if (
                method.Name !=
                "CreateFrame")
                continue;

            ParameterInfo[] ps =
                method.GetParameters();

            if (
                ps.Length == 1 &&
                ps[0].ParameterType.
                    IsAssignableFrom(
                        dataType))
            {
                return method;
            }
        }

        return null;
    }

    private static void SetProperty(
        object obj,
        string name,
        object value)
    {
        if (
            obj == null ||
            value == null)
            return;

        PropertyInfo p =
            obj.GetType().GetProperty(
                name,
                BindingFlags.Instance |
                BindingFlags.Public);

        if (
            p == null ||
            !p.CanWrite)
            return;

        if (
            !p.PropertyType.
                IsAssignableFrom(
                    value.GetType()))
            return;

        try
        {
            p.SetValue(
                obj,
                value,
                null);
        }
        catch
        {
        }
    }
}
