using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Tecnomatix.Engineering;

public class MainScript
{
    private static readonly string[] FrameData = new string[]
    {
        "FACTORY_BASE_01|934617.216742|-1890583.332348|0.000000|0.000000",
        "FACTORY_BASE_02|1040617.216742|-1890583.332348|0.000000|0.000000",
        "FACTORY_BASE_03|1040617.216742|-2008583.332348|0.000000|0.000000",
        "FACTORY_BASE_04|934617.216742|-2008583.332348|0.000000|0.000000",
        "BSL_BASE_01|939617.149078|-1896582.839094|0.000000|0.000000",
        "BSL_BASE_02|959617.149078|-1896582.839094|0.000000|0.000000",
        "BSL_BASE_03|959617.149078|-1939582.839094|0.000000|0.000000",
        "BSL_BASE_04|939617.149078|-1939582.839094|0.000000|0.000000",
        "BSR_BASE_01|963617.149078|-1896582.839094|0.000000|0.000000",
        "BSR_BASE_02|983617.149078|-1896582.839094|0.000000|0.000000",
        "BSR_BASE_03|983617.149078|-1939582.839094|0.000000|0.000000",
        "BSR_BASE_04|963617.149078|-1939582.839094|0.000000|0.000000",
        "EC_BASE_01|988617.149078|-1896582.839094|0.000000|0.000000",
        "EC_BASE_02|1009017.149078|-1896582.839094|0.000000|0.000000",
        "EC_BASE_03|1009017.149078|-1952582.839094|0.000000|0.000000",
        "EC_BASE_04|988617.149078|-1952582.839094|0.000000|0.000000",
        "RR_BASE_01|1014017.149050|-1896582.839094|0.000000|0.000000",
        "RR_BASE_02|1030617.149050|-1896582.839094|0.000000|0.000000",
        "RR_BASE_03|1030617.149050|-1952582.839094|0.000000|0.000000",
        "RR_BASE_04|1014017.149050|-1952582.839094|0.000000|0.000000",
        "BSL_AUX_BASE_01|941617.149078|-1942582.839094|0.000000|0.000000",
        "BSL_AUX_BASE_02|959617.149078|-1942582.839094|0.000000|0.000000",
        "BSL_AUX_BASE_03|959617.149078|-1952582.839094|0.000000|0.000000",
        "BSL_AUX_BASE_04|941617.149078|-1952582.839094|0.000000|0.000000",
        "BSR_AUX_BASE_01|963617.149078|-1942582.839094|0.000000|0.000000",
        "BSR_AUX_BASE_02|983617.149078|-1942582.839094|0.000000|0.000000",
        "BSR_AUX_BASE_03|983617.149078|-1952582.839094|0.000000|0.000000",
        "BSR_AUX_BASE_04|963617.149078|-1952582.839094|0.000000|0.000000",
        "MID_MAIN_BASE_01|941617.149078|-1957582.839094|0.000000|0.000000",
        "MID_MAIN_BASE_02|1034617.149078|-1957582.839094|0.000000|0.000000",
        "MID_MAIN_BASE_03|1034617.149078|-1988582.839094|0.000000|0.000000",
        "MID_MAIN_BASE_04|941617.149078|-1988582.839094|0.000000|0.000000",
        "MID_AUX_BASE_01|1012617.149078|-1977582.787282|0.000000|0.000000",
        "MID_AUX_BASE_02|1034617.149078|-1977582.787282|0.000000|0.000000",
        "MID_AUX_BASE_03|1034617.149078|-1988582.839094|0.000000|0.000000",
        "MID_AUX_BASE_04|1012617.149078|-1988582.839094|0.000000|0.000000",
        "BOTTOM_BASE_01|941617.149078|-1994582.839094|0.000000|0.000000",
        "BOTTOM_BASE_02|1034617.149078|-1994582.839094|0.000000|0.000000",
        "BOTTOM_BASE_03|1034617.149078|-2001582.839094|0.000000|0.000000",
        "BOTTOM_BASE_04|941617.149078|-2001582.839094|0.000000|0.000000",
        "BOTTOM_ROBOT_001|1018038.580902|-1995990.208552|0.000000|270.000000",
        "BOTTOM_ROBOT_002|1017990.939870|-2000244.165033|0.000000|90.000000",
        "BSL_ROBOT_001|942662.445796|-1903367.490271|0.000000|270.000000",
        "BSL_ROBOT_002|944779.033949|-1903770.878401|0.000000|270.000000",
        "BSL_ROBOT_003|950171.369154|-1905158.231278|0.000000|270.000000",
        "BSL_ROBOT_004|947275.974878|-1905193.802291|0.000000|270.000000",
        "BSL_ROBOT_005|948430.617333|-1906351.221270|0.000000|180.000000",
        "BSL_ROBOT_006|950179.090945|-1906846.578782|0.000000|180.000000",
        "BSL_ROBOT_007|948099.742514|-1911443.204778|0.000000|270.000000",
        "BSL_ROBOT_008|956185.117480|-1912432.884294|0.000000|90.000000",
        "BSL_ROBOT_009|951103.075617|-1916788.331003|0.000000|180.000000",
        "BSL_ROBOT_010|953383.247350|-1917115.817350|0.000000|270.000000",
        "BSL_ROBOT_011|956386.166967|-1917116.975974|0.000000|270.000000",
        "BSL_ROBOT_012|954841.524474|-1918089.978725|0.000000|180.000000",
        "BSL_ROBOT_013|957056.442880|-1921098.365610|0.000000|0.000000",
        "BSL_ROBOT_014|951857.923254|-1921366.576599|0.000000|180.000000",
        "BSL_ROBOT_015|951857.170432|-1923595.557713|0.000000|180.000000",
        "BSL_ROBOT_016|949541.077879|-1923623.741412|0.000000|270.000000",
        "BSL_ROBOT_017|945982.386477|-1924178.256828|0.000000|270.000000",
        "BSL_ROBOT_018|950346.289889|-1931391.949873|0.000000|0.000000",
        "BSL_ROBOT_019|949055.188653|-1934101.735429|0.000000|0.000000",
        "BSL_ROBOT_020|947246.357465|-1935959.898566|0.000000|180.000000",
        "BSL_ROBOT_021|947247.060294|-1937736.359157|0.000000|180.000000",
        "BSL_TRACK_ROBOT_001|950078.244291|-1911733.599866|0.000000|0.000000",
        "BSL_TRACK_ROBOT_002|958624.274557|-1924754.159191|0.000000|0.000000",
        "BSL_TRACK_ROBOT_003|947725.476633|-1924774.987447|0.000000|0.000000",
        "BSL_AUX_ROBOT_001|947408.511856|-1945699.325316|-4.999970|360.000000",
        "BSL_AUX_ROBOT_002|945441.069868|-1945741.197635|0.000000|360.000000",
        "BSL_AUX_ROBOT_003|954555.644941|-1945771.183089|-0.000032|180.000000",
        "BSL_AUX_ROBOT_004|948972.443169|-1945788.740102|-0.000065|180.000000",
        "BSL_AUX_ROBOT_005|952703.417418|-1945788.740102|-0.000065|180.000000",
        "BSL_AUX_TRACK_ROBOT_001|950905.775737|-1948799.243758|0.000000|90.000000",
        "BSR_ROBOT_001|980588.167382|-1903367.490271|0.000000|270.000000",
        "BSR_ROBOT_002|978519.181158|-1904338.767439|0.000000|270.000000",
        "BSR_ROBOT_003|976282.036610|-1904882.125068|0.000000|270.000000",
        "BSR_ROBOT_004|973062.929002|-1905158.231278|0.000000|270.000000",
        "BSR_ROBOT_005|974839.183512|-1906149.980177|0.000000|360.000000",
        "BSR_ROBOT_006|973055.207211|-1906846.578782|0.000000|360.000000",
        "BSR_ROBOT_007|975134.555643|-1911443.204778|0.000000|270.000000",
        "BSR_ROBOT_008|967049.180677|-1912432.884294|0.000000|90.000000",
        "BSR_ROBOT_009|972131.222539|-1916788.331003|0.000000|360.000000",
        "BSR_ROBOT_010|969851.050806|-1917115.817350|0.000000|270.000000",
        "BSR_ROBOT_011|966848.131189|-1917116.975974|0.000000|270.000000",
        "BSR_ROBOT_012|968392.773683|-1918089.978725|0.000000|360.000000",
        "BSR_ROBOT_013|965937.403883|-1921023.747305|0.000000|180.000000",
        "BSR_ROBOT_014|971070.793542|-1921166.383311|0.000000|360.000000",
        "BSR_ROBOT_015|971071.546364|-1923395.364424|0.000000|360.000000",
        "BSR_ROBOT_016|977251.911680|-1924178.256828|0.000000|270.000000",
        "BSR_ROBOT_017|973494.086927|-1924183.597638|0.000000|270.000000",
        "BSR_ROBOT_018|972888.008268|-1931391.949873|0.000000|180.000000",
        "BSR_ROBOT_019|974179.109503|-1934101.735429|0.000000|180.000000",
        "BSR_ROBOT_020|975987.940691|-1935959.898566|0.000000|360.000000",
        "BSR_ROBOT_021|975987.237862|-1937736.359157|0.000000|360.000000",
        "BSR_TRACK_ROBOT_001|973156.053865|-1911733.599866|0.000000|180.000000",
        "BSR_TRACK_ROBOT_002|964606.907592|-1924748.314711|0.000000|180.000000",
        "BSR_TRACK_ROBOT_003|975508.821523|-1924774.987447|0.000000|180.000000",
        "EC_ROBOT_001|997150.963846|-1900019.427608|0.000000|270.000000",
        "EC_ROBOT_002|997150.963846|-1904402.768770|0.000000|90.000000",
        "EC_ROBOT_003|1002447.135581|-1906177.519231|0.000000|0.304606",
        "EC_ROBOT_004|997630.127838|-1907881.998754|0.000000|270.000000",
        "EC_ROBOT_005|999651.370561|-1907883.734856|0.000000|270.000000",
        "EC_ROBOT_006|999651.370561|-1912077.221467|0.000000|90.000000",
        "EC_ROBOT_007|997654.728636|-1912097.812518|0.000000|90.000000",
        "EC_ROBOT_008|1002484.446648|-1914471.310889|0.000000|29.304606",
        "EC_ROBOT_009|997298.779636|-1915689.077005|0.000000|270.000000",
        "EC_ROBOT_010|1000305.167933|-1915712.601966|0.000000|270.000000",
        "EC_ROBOT_011|1000413.170078|-1919742.935956|0.000000|90.000000",
        "EC_ROBOT_012|997298.779636|-1919882.563616|0.000000|90.000000",
        "EC_ROBOT_013|998900.336074|-1922176.932823|0.000000|15.040094",
        "EC_ROBOT_014|997143.203358|-1923248.896649|0.000000|270.000000",
        "EC_ROBOT_015|1000353.613275|-1923833.499186|0.000000|270.000000",
        "EC_ROBOT_016|1006620.947926|-1924751.190627|0.000000|270.000000",
        "EC_ROBOT_017|1002666.235321|-1925526.895095|0.000000|270.304606",
        "EC_ROBOT_018|1001013.522063|-1927616.888052|0.000000|0.304606",
        "EC_ROBOT_019|999410.452504|-1927733.964976|0.000000|90.000000",
        "EC_ROBOT_020|997143.203358|-1928002.903427|0.000000|90.000000",
        "EC_ROBOT_021|999543.203358|-1932048.896649|0.000000|270.000000",
        "EC_ROBOT_022|997322.024845|-1932355.405231|0.000000|270.000000",
        "EC_ROBOT_023|997412.813089|-1936437.380921|0.000000|90.000000",
        "EC_ROBOT_024|999543.203358|-1936802.903427|0.000000|90.000000",
        "EC_ROBOT_025|1002447.135581|-1938377.519231|0.000000|0.304606",
        "EC_ROBOT_026|997418.422687|-1940177.401493|0.000000|270.000000",
        "EC_ROBOT_027|999373.921752|-1940246.257094|0.000000|270.000000",
        "EC_ROBOT_028|997509.210931|-1944259.377183|0.000000|90.000000",
        "EC_ROBOT_029|999464.709996|-1944328.232784|0.000000|90.000000",
        "EC_ROBOT_030|1001255.157271|-1946186.018210|0.000000|0.304606",
        "MID_AUX_ROBOT_001|1018903.962286|-1980406.273880|0.000000|270.000000",
        "MID_AUX_ROBOT_002|1020713.926320|-1980407.121238|0.000000|270.000000",
        "MID_AUX_ROBOT_003|1029087.281751|-1981119.205316|0.000000|270.000000",
        "MID_AUX_ROBOT_004|1026584.536351|-1981895.792533|0.000000|181.977595",
        "MID_AUX_ROBOT_005|1017174.418713|-1983327.339464|0.000000|159.780468",
        "MID_AUX_ROBOT_006|1024867.149078|-1985032.787282|0.000000|105.000000",
        "MID_AUX_ROBOT_007|1018927.710943|-1986337.966630|0.000000|90.000000",
        "MID_AUX_ROBOT_008|1020727.710943|-1986337.966630|0.000000|90.000000",
        "MID_MAIN_ROBOT_001|954625.198838|-1961249.465187|-1.000101|360.000000",
        "MID_MAIN_ROBOT_002|952487.127162|-1962252.910950|-4.999970|360.000000",
        "MID_MAIN_ROBOT_003|956198.191699|-1962302.948692|-4.999938|360.000000",
        "MID_MAIN_ROBOT_004|957706.481715|-1962482.561669|-4.999970|360.000000",
        "MID_MAIN_ROBOT_005|1024100.465495|-1962517.676116|0.049970|270.000000",
        "MID_MAIN_ROBOT_006|1004711.313337|-1962719.617202|-0.911892|270.000000",
        "MID_MAIN_ROBOT_007|1007015.533777|-1962719.617202|-0.911892|270.000000",
        "MID_MAIN_ROBOT_008|977675.620084|-1963016.269726|-0.000032|180.000000",
        "MID_MAIN_ROBOT_009|979240.280726|-1963016.269726|-0.000032|180.000000",
        "MID_MAIN_ROBOT_010|980928.286065|-1963016.269726|-0.000032|180.000000",
        "MID_MAIN_ROBOT_011|997981.734781|-1963016.269726|-0.000032|180.000000",
        "MID_MAIN_ROBOT_012|1000421.046333|-1963028.881210|-0.000002|0.000000",
        "MID_MAIN_ROBOT_013|992937.273979|-1963136.441078|0.000000|180.000000",
        "MID_MAIN_ROBOT_014|1019234.562907|-1963860.529094|-18686004.694183|180.000000",
        "MID_MAIN_ROBOT_015|1027298.939711|-1965339.503355|0.049970|41.919875",
        "MID_MAIN_ROBOT_016|1015519.626676|-1967385.526941|0.049970|180.000000",
        "MID_MAIN_ROBOT_017|1000421.046333|-1967397.598324|-0.000002|360.000000",
        "MID_MAIN_ROBOT_018|977683.849341|-1967411.131081|-0.000032|180.000000",
        "MID_MAIN_ROBOT_019|979248.509983|-1967411.131081|-0.000032|180.000000",
        "MID_MAIN_ROBOT_020|980936.515322|-1967411.131081|-0.000032|180.000000",
        "MID_MAIN_ROBOT_021|997989.964039|-1967411.131081|-0.000032|180.000000",
        "MID_MAIN_ROBOT_022|1008057.543129|-1967480.083423|-0.911892|90.000000",
        "MID_MAIN_ROBOT_023|1024301.996841|-1967591.756587|0.000000|180.000000",
        "MID_MAIN_ROBOT_024|1004562.060980|-1967699.646201|0.000000|90.000000",
        "MID_MAIN_ROBOT_025|952528.421164|-1968294.315886|-4.999938|0.000000",
        "MID_MAIN_ROBOT_026|957778.934320|-1968473.871688|-4.999970|0.000000",
        "MID_MAIN_ROBOT_027|956216.460398|-1968518.629041|-1.000166|0.000000",
        "MID_MAIN_ROBOT_028|954601.699612|-1969357.657558|-1.000133|0.000000",
        "MID_MAIN_ROBOT_029|1006411.167466|-1969529.309180|0.049970|0.000000",
        "MID_MAIN_ROBOT_030|1014024.544848|-1970905.006528|0.049970|180.000000",
        "MID_MAIN_ROBOT_031|957096.778188|-1980488.883793|-0.000002|180.000000",
        "MID_MAIN_ROBOT_032|976232.053269|-1980510.880365|-0.000032|180.000000",
        "MID_MAIN_ROBOT_033|978321.491971|-1980510.880365|-0.000032|180.000000",
        "MID_MAIN_ROBOT_034|949897.138965|-1980510.883123|-0.000002|180.000000",
        "MID_MAIN_ROBOT_035|952113.982793|-1980510.883123|-0.000002|180.000000",
        "MID_MAIN_ROBOT_036|963232.053269|-1980510.883123|-0.000002|180.000000",
        "MID_MAIN_ROBOT_037|965321.491971|-1980510.883123|-0.000002|180.000000",
        "MID_MAIN_ROBOT_038|969246.903961|-1980510.883123|-0.000032|180.000000",
        "MID_MAIN_ROBOT_039|970990.083836|-1980510.883123|-0.000032|180.000000",
        "MID_MAIN_ROBOT_040|972639.343396|-1980510.883123|-0.000032|180.000000",
        "MID_MAIN_ROBOT_041|983052.865636|-1980510.883123|-0.000032|180.000000",
        "MID_MAIN_ROBOT_042|995499.157030|-1980510.883123|-0.000002|180.000000",
        "MID_MAIN_ROBOT_043|997942.972307|-1980510.883123|-0.000002|180.000000",
        "MID_MAIN_ROBOT_044|959629.794156|-1980548.883793|-0.000002|180.000000",
        "MID_MAIN_ROBOT_045|976232.053269|-1984905.741720|-0.000032|180.000000",
        "MID_MAIN_ROBOT_046|978321.491971|-1984905.741720|-0.000032|180.000000",
        "MID_MAIN_ROBOT_047|963232.053269|-1984905.744478|-0.000002|180.000000",
        "MID_MAIN_ROBOT_048|965321.491971|-1984905.744478|-0.000002|180.000000",
        "MID_MAIN_ROBOT_049|970990.083836|-1984905.744478|-0.000032|180.000000",
        "MID_MAIN_ROBOT_050|972639.343396|-1984905.744478|-0.000032|180.000000",
        "MID_MAIN_ROBOT_051|995499.157030|-1984905.744478|-0.000002|180.000000",
        "MID_MAIN_ROBOT_052|997942.972307|-1984905.744478|-0.000002|180.000000",
        "MID_MAIN_ROBOT_053|952029.037422|-1984921.941969|-0.000002|180.000000",
        "MID_MAIN_ROBOT_054|956381.220768|-1984985.854763|-0.000002|0.000000",
        "MID_MAIN_ROBOT_055|985141.324233|-1984989.246943|0.000000|180.000000",
        "MID_MAIN_ROBOT_056|969174.786596|-1985034.375544|-0.000032|180.000000",
        "MID_MAIN_ROBOT_057|982980.748271|-1985034.375544|-0.000032|180.000000",
        "MID_MAIN_ROBOT_058|959629.794156|-1985051.800731|-0.000002|0.000000",
        "MID_MAIN_ROBOT_059|949812.193594|-1985052.874176|-0.000002|180.000000",
        "MID_MAIN_TRACK_ROBOT_001|971647.288625|-1962788.230904|0.000000|270.000000",
        "MID_MAIN_TRACK_ROBOT_002|970954.856998|-1967404.486438|0.000000|90.000000",
        "MID_MAIN_TRACK_ROBOT_003|972818.688496|-1967631.890467|0.000000|90.000000",
        "MID_MAIN_TRACK_ROBOT_004|958012.260667|-1985348.362206|0.000000|90.000000",
        "RR_ROBOT_001|1017567.149050|-1898613.418682|0.000000|359.863453",
        "RR_ROBOT_002|1017567.149050|-1900613.418682|0.000000|0.136547",
        "RR_ROBOT_003|1022710.976094|-1903053.458348|0.000000|0.000000",
        "RR_ROBOT_004|1018025.719127|-1903151.632632|0.000000|0.304606",
        "RR_ROBOT_005|1017567.149050|-1905482.616385|0.000000|359.863453",
        "RR_ROBOT_006|1017567.149050|-1907482.616385|0.000000|0.136547",
        "RR_ROBOT_007|1023382.126320|-1910142.099293|0.000000|180.304606",
        "RR_ROBOT_008|1019741.023306|-1918959.784100|0.000000|51.154348",
        "RR_ROBOT_009|1023975.719127|-1919866.769669|0.000000|0.304606",
        "RR_ROBOT_010|1017289.137676|-1921901.787053|0.000000|359.863453",
        "RR_ROBOT_011|1017157.425641|-1923296.362906|0.000000|360.000000",
        "RR_ROBOT_012|1017305.842873|-1924933.850467|0.000000|359.863453",
        "RR_ROBOT_013|1023821.486748|-1926769.742920|0.000000|0.304606",
        "RR_ROBOT_014|1019789.861625|-1927918.902481|0.000000|270.136547",
        "RR_ROBOT_015|1022124.420782|-1928066.062691|0.000000|270.000000",
        "RR_ROBOT_016|1023821.486748|-1931834.713645|0.000000|0.304606",
        "RR_ROBOT_017|1022100.424070|-1932181.440939|0.000000|90.000000",
        "RR_ROBOT_018|1018664.604115|-1932273.901503|0.000000|89.863453",
        "RR_ROBOT_019|1020443.954298|-1935367.267380|0.000000|0.304606",
        "RR_ROBOT_020|1016996.620752|-1936838.776249|0.000000|360.000000",
        "RR_ROBOT_021|1016996.620752|-1938377.135565|0.000000|360.000000",
        "RR_ROBOT_022|1022181.842875|-1938431.143339|0.000000|270.000000",
        "RR_ROBOT_023|1023815.245903|-1940724.801914|0.000000|180.304606",
        "RR_ROBOT_024|1018983.264204|-1942585.756809|0.000000|90.000000",
        "RR_ROBOT_025|1026219.423187|-1945536.597332|0.000000|270.000000",
        "RR_ROBOT_026|1022425.173788|-1946463.356594|0.000000|0.718891",
        "RR_ROBOT_027|1022289.402934|-1948282.878404|0.000000|224.718891",
        "RR_ROBOT_028|1026147.589769|-1949716.073885|0.000000|90.000000"
    };

    public static void Main()
    {
        StringWriter output = new StringWriter();
        MainWithOutput(ref output);
        TxMessageBox.Show(output.ToString(), "Factory Robot Base Frames + Batch Robot", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    public static void MainWithOutput(ref StringWriter output)
    {
        object doc = TxApplication.ActiveDocument;
        if (doc == null) { output.WriteLine("ERROR: No active study."); return; }
        object physicalRoot = GetProperty(doc, "PhysicalRoot");
        if (physicalRoot == null) { output.WriteLine("ERROR: ActiveDocument.PhysicalRoot was not found."); return; }

        for (int i=0; i<FrameData.Length; i++)
        {
            string[] p = FrameData[i].Split('|');
            CreateOne(physicalRoot, p[0], D(p[1]), D(p[2]), D(p[3]), 0.0, 0.0, D(p[4]), ref output);
        }

        output.WriteLine("");
        output.WriteLine("Frame generation finished.");
        output.WriteLine("Starting batch robot placement...");
        BatchRobotImporter.Run(ref output);
    }

    private static double D(string s)
    { return Double.Parse(s, System.Globalization.CultureInfo.InvariantCulture); }

    private static void CreateOne(object physicalRoot, string name, double x, double y, double z, double rxDeg, double ryDeg, double rzDeg, ref StringWriter output)
    {
        try
        {
            if (FrameExists(physicalRoot, name)) { output.WriteLine("SKIP " + name + ": frame already exists."); return; }
            Type frameDataType = FindType("TxFrameCreationData");
            if (frameDataType == null) { output.WriteLine("FAIL " + name + ": TxFrameCreationData type not found."); return; }
            double d2r = Math.PI / 180.0;
            TxTransformation tr = new TxTransformation(new TxVector(x,y,z), new TxVector(rxDeg*d2r, ryDeg*d2r, rzDeg*d2r), TxTransformation.TxRotationType.RPY_XYZ);
            object data = MakeCreationData(frameDataType, name, tr);
            if (data == null) { output.WriteLine("FAIL " + name + ": cannot construct TxFrameCreationData."); return; }
            MethodInfo create = FindCreateFrameMethod(physicalRoot.GetType(), frameDataType);
            if (create == null) { output.WriteLine("FAIL " + name + ": no compatible CreateFrame method."); return; }
            object frame = create.Invoke(physicalRoot, new object[] { data });
            if (frame == null) { output.WriteLine("FAIL " + name + ": CreateFrame returned null."); return; }
            SetProperty(frame, "AbsoluteLocation", tr); SetProperty(frame, "Location", tr);
            output.WriteLine("OK   " + name);
        }
        catch (Exception ex)
        {
            Exception e = ex; if (ex is TargetInvocationException && ex.InnerException != null) e = ex.InnerException;
            output.WriteLine("FAIL " + name + ": " + e.GetType().Name + " - " + e.Message);
        }
    }

    private static bool FrameExists(object physicalRoot, string name)
    {
        TxPhysicalRoot root = physicalRoot as TxPhysicalRoot; if (root == null) return false;
        TxObjectList frames = root.GetAllDescendants(new TxTypeFilter(typeof(TxFrame)));
        foreach (ITxObject item in frames) { TxFrame frame = item as TxFrame; if (frame != null && String.Equals(frame.Name,name,StringComparison.OrdinalIgnoreCase)) return true; }
        return false;
    }

    private static Type FindType(string shortName)
    {
        Type[] types = typeof(TxApplication).Assembly.GetTypes(); for (int i=0;i<types.Length;i++) if (types[i].Name==shortName) return types[i]; return null;
    }

    private static object MakeCreationData(Type t, string name, TxTransformation tr)
    {
        ConstructorInfo[] ctors=t.GetConstructors();
        for(int i=0;i<ctors.Length;i++)
        {
            ParameterInfo[] ps=ctors[i].GetParameters(); object[] args=new object[ps.Length];
            try
            {
                for(int j=0;j<ps.Length;j++) { Type pt=ps[j].ParameterType; if(pt==typeof(string)) args[j]=name; else if(pt==typeof(TxTransformation)) args[j]=tr; else if(ps[j].HasDefaultValue) args[j]=ps[j].DefaultValue; else if(pt.IsValueType) args[j]=Activator.CreateInstance(pt); else args[j]=null; }
                object data=ctors[i].Invoke(args); SetProperty(data,"Name",name); SetProperty(data,"AbsoluteLocation",tr); SetProperty(data,"Location",tr); SetProperty(data,"Transformation",tr); return data;
            } catch { }
        }
        return null;
    }

    private static MethodInfo FindCreateFrameMethod(Type rootType, Type dataType)
    {
        MethodInfo[] ms=rootType.GetMethods(BindingFlags.Instance|BindingFlags.Public);
        for(int i=0;i<ms.Length;i++) { if(ms[i].Name!="CreateFrame") continue; ParameterInfo[] ps=ms[i].GetParameters(); if(ps.Length==1 && ps[0].ParameterType.IsAssignableFrom(dataType)) return ms[i]; }
        return null;
    }

    private static object GetProperty(object obj,string name)
    {
        if(obj==null) return null; PropertyInfo p=obj.GetType().GetProperty(name,BindingFlags.Instance|BindingFlags.Public); if(p==null) return null; try { return p.GetValue(obj,null); } catch { return null; }
    }

    private static void SetProperty(object obj,string name,object value)
    {
        if(obj==null||value==null) return; PropertyInfo p=obj.GetType().GetProperty(name,BindingFlags.Instance|BindingFlags.Public); if(p==null||!p.CanWrite||!p.PropertyType.IsAssignableFrom(value.GetType())) return; try { p.SetValue(obj,value,null); } catch { }
    }
}

internal static class BatchRobotImporter
{
    private const string RobotPackagePath = @"D:\WEDING\MOVING\work261007\Library\BSR_OP10_R01_KR210_R2700.cojt";
    private const string RobotModelTag = "KR210_R2700";
    private const bool IncludeTrackRobotFrames = true;
    private const double MaxAbsTargetZMm = 10000.0;

    public static void Run(ref StringWriter output)
    {
        StringBuilder report=new StringBuilder();
        try { InsertRobotsOnAllRobotFrames(report); }
        catch(Exception ex) { Exception detail=ex; while(detail.InnerException!=null) detail=detail.InnerException; report.AppendLine("BATCH ROBOT IMPORT ERROR: "+detail.GetType().Name+" - "+detail.Message); }
        output.Write(report.ToString());
    }

    private static void InsertRobotsOnAllRobotFrames(StringBuilder report)
    {
        TxDocument document=TxApplication.ActiveDocument; if(document==null) throw new InvalidOperationException("No active study.");
        TxPhysicalRoot physicalRoot=document.PhysicalRoot; if(physicalRoot==null) throw new InvalidOperationException("Active study has no PhysicalRoot.");
        ValidateRobotPackage();
        string activeSystemRoot=TxApplication.SystemRootDirectory;
        if(!IsInsideDirectory(RobotPackagePath,activeSystemRoot)) { report.AppendLine("WARNING: Robot package is outside the active System Root."); report.AppendLine("System Root : "+activeSystemRoot); report.AppendLine("Robot       : "+RobotPackagePath); report.AppendLine("Trying the absolute COJT path..."); report.AppendLine(""); }
        TxObjectList frames=physicalRoot.GetAllDescendants(new TxTypeFilter(typeof(TxFrame)));
        int targetCount=0,successCount=0,skipCount=0,failCount=0;
        foreach(ITxObject item in frames)
        {
            TxFrame targetFrame=item as TxFrame; if(targetFrame==null) continue;
            if(!IsRobotFrameName(targetFrame.Name)) continue;
            if(!IncludeTrackRobotFrames && targetFrame.Name.IndexOf("_TRACK_ROBOT_",StringComparison.OrdinalIgnoreCase)>=0) continue;
            targetCount++;
            double z=targetFrame.AbsoluteLocation.Translation.Z;
            if(Double.IsNaN(z)||Double.IsInfinity(z)||Math.Abs(z)>MaxAbsTargetZMm) { report.AppendLine("SKIP "+targetFrame.Name+": suspicious Frame Z = "+z.ToString("F3")+" mm."); skipCount++; continue; }
            string instanceName="AUTO_"+targetFrame.Name+"_"+RobotModelTag;
            if(FindObjectByName(physicalRoot,instanceName)!=null) { report.AppendLine("SKIP "+targetFrame.Name+": "+instanceName+" already exists."); skipCount++; continue; }
            ITxComponent inserted=null;
            try
            {
                using(TxInsertComponentCreationData data=new TxInsertComponentCreationData(instanceName,RobotPackagePath)) { data.AbsoluteLocation=new TxTransformation(targetFrame.AbsoluteLocation); inserted=physicalRoot.InsertComponent(data); }
                if(inserted==null) throw new InvalidOperationException("InsertComponent returned null.");
                ITxLocatableObject insertedRoot=inserted as ITxLocatableObject; TxRobot robot=FindRobot(inserted);
                if(insertedRoot==null) throw new InvalidOperationException("Inserted component is not locatable.");
                if(!insertedRoot.CanLocate()) throw new InvalidOperationException("Inserted component cannot be relocated.");
                if(robot==null) throw new InvalidOperationException("COJT was not recognized as a Robot. Do not import only the inner JT file.");
                AlignRobotBase(insertedRoot,robot,targetFrame);
                int jointCount=robot.Joints==null?0:robot.Joints.Count; int drivingJointCount=robot.DrivingJoints==null?0:robot.DrivingJoints.Count;
                if(jointCount<6||drivingJointCount<6) throw new InvalidOperationException("Fewer than six Robot joints were detected.");
                report.AppendLine("OK   "+targetFrame.Name+" -> "+instanceName); successCount++;
            }
            catch(Exception ex)
            {
                RemoveIncompleteInsert(inserted,report); Exception detail=ex; while(detail.InnerException!=null) detail=detail.InnerException; report.AppendLine("FAIL "+targetFrame.Name+": "+detail.GetType().Name+" - "+detail.Message); failCount++;
            }
        }
        report.AppendLine(""); report.AppendLine("========== BATCH ROBOT RESULT =========="); report.AppendLine("Robot Frames found : "+targetCount); report.AppendLine("Success            : "+successCount); report.AppendLine("Skipped            : "+skipCount); report.AppendLine("Failed             : "+failCount); report.AppendLine("========================================");
    }

    private static bool IsRobotFrameName(string name)
    { return !String.IsNullOrEmpty(name) && name.IndexOf("_ROBOT_",StringComparison.OrdinalIgnoreCase)>=0; }

    private static void ValidateRobotPackage()
    {
        if(!Directory.Exists(RobotPackagePath)) throw new DirectoryNotFoundException("COJT package folder was not found: "+RobotPackagePath);
        string[] requiredFiles=new string[]{"BSR_OP10_R01_KR210_R2700.jt","kin_graph.xml","motionparameters.e","TuneData.xml"};
        for(int i=0;i<requiredFiles.Length;i++) { string path=Path.Combine(RobotPackagePath,requiredFiles[i]); if(!File.Exists(path)) throw new FileNotFoundException("COJT package is incomplete.",path); }
    }

    private static void AlignRobotBase(ITxLocatableObject insertedRoot,TxRobot robot,TxFrame targetFrame)
    {
        TxFrame robotBase=robot.Baseframe; if(robotBase==null) throw new InvalidOperationException("Robot Baseframe is missing.");
        TxTransformation baseRelativeToRoot=robotBase.GetLocationRelativeToObject(insertedRoot); insertedRoot.AbsoluteLocation=targetFrame.AbsoluteLocation*baseRelativeToRoot.Inverse;
        TxTransformation residual=robotBase.GetLocationRelativeToObject(targetFrame); TxVector delta=residual.Translation; TxVector rotation=residual.RotationRPY_XYZ;
        double positionError=Math.Sqrt(delta.X*delta.X+delta.Y*delta.Y+delta.Z*delta.Z); double angleErrorRad=Math.Max(Math.Abs(NormalizeAngle(rotation.X)),Math.Max(Math.Abs(NormalizeAngle(rotation.Y)),Math.Abs(NormalizeAngle(rotation.Z)))); double angleErrorDeg=angleErrorRad*180.0/Math.PI;
        if(positionError>0.1||angleErrorDeg>0.01) throw new InvalidOperationException("Robot Baseframe alignment outside tolerance: "+positionError.ToString("F6")+" mm, "+angleErrorDeg.ToString("F6")+" deg.");
    }

    private static ITxObject FindObjectByName(TxPhysicalRoot physicalRoot,string name)
    {
        TxObjectList objects=physicalRoot.GetAllDescendants(new TxNoTypeFilter()); foreach(ITxObject item in objects) if(String.Equals(item.Name,name,StringComparison.OrdinalIgnoreCase)) return item; return null;
    }

    private static TxRobot FindRobot(ITxComponent inserted)
    {
        TxRobot robot=inserted as TxRobot; if(robot!=null) return robot; ITxObjectCollection collection=inserted as ITxObjectCollection; if(collection==null) return null; TxObjectList robots=collection.GetAllDescendants(new TxTypeFilter(typeof(TxRobot))); foreach(ITxObject item in robots) { robot=item as TxRobot; if(robot!=null) return robot; } return null;
    }

    private static double NormalizeAngle(double radians)
    { while(radians>Math.PI) radians-=2.0*Math.PI; while(radians<-Math.PI) radians+=2.0*Math.PI; return radians; }

    private static void RemoveIncompleteInsert(ITxComponent inserted,StringBuilder report)
    {
        try { if(inserted!=null&&inserted.CanBeDeleted) inserted.Delete(); else if(inserted!=null) report.AppendLine("WARNING: delete incomplete Robot manually."); } catch(Exception cleanupError) { report.AppendLine("WARNING: Robot cleanup failed: "+cleanupError.Message); }
    }

    private static bool IsInsideDirectory(string candidate,string directory)
    {
        if(String.IsNullOrEmpty(candidate)||String.IsNullOrEmpty(directory)) return false; string child=Path.GetFullPath(candidate).TrimEnd('\\','/')+"\\"; string parent=Path.GetFullPath(directory).TrimEnd('\\','/')+"\\"; return child.StartsWith(parent,StringComparison.OrdinalIgnoreCase);
    }
}
