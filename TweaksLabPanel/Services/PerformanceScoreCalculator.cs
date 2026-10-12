using TweaksLabPanel.Core;

namespace TweaksLabPanel.Services;

public enum ScoreCategory
{
    Startup,
    Storage,
    Memory,
    Temperatures,
    GamingSettings,
    Stability,
}

/// <summary>Où corriger un critère : page du panel, réglage Windows ou tutoriel.</summary>
public enum ScoreFix
{
    None,
    TipsPage,
    CleanupPage,
    GpuPage,
    DisplaySettingsPage,
    XmpTutorial,
    WindowsGameMode,
    WindowsCaptures,
    WindowsGraphics,
    WindowsMouse,
    WindowsApps,
}

/// <summary>Un critère du score. Earned null = non mesurable sur ce PC (exclu du calcul).</summary>
public sealed record ScoreCriterion(
    ScoreCategory Category,
    int MaxPoints,
    double? Earned,
    string LabelFr,
    string LabelEn,
    string? ValueFr,
    string? ValueEn,
    string? TipFr,
    string? TipEn,
    ScoreFix Fix)
{
    public bool Measured => Earned is not null;
    public bool IsPerfect => Earned is { } earned && earned >= MaxPoints;
}

public sealed record ScoreBreakdown(IReadOnlyList<ScoreCriterion> Criteria, int Score)
{
    public int MaxPoints(ScoreCategory category) =>
        Criteria.Where(c => c.Category == category && c.Measured).Sum(c => c.MaxPoints);

    public double EarnedPoints(ScoreCategory category) =>
        Criteria.Where(c => c.Category == category && c.Measured).Sum(c => c.Earned!.Value);
}

/// <summary>
/// Score de santé de 0 à 90 (la perfection n'existe pas), calculé uniquement sur des mesures réelles du PC.
/// Score = 90 × points obtenus / points des critères mesurables. Un critère impossible à mesurer
/// (ex. pas de capteur de température) est retiré du calcul : ni pénalité, ni point offert.
/// </summary>
public static class PerformanceScoreCalculator
{
    public static int Calculate(PerformanceSnapshot snapshot) => Evaluate(snapshot).Score;

    public static ScoreBreakdown Evaluate(PerformanceSnapshot s)
    {
        var criteria = new List<ScoreCriterion>
        {
            // Démarrage et arrière-plan (20)
            Graded(ScoreCategory.Startup, 8, s.StartupAppsCount,
                [(3, 8), (5, 6), (8, 4), (12, 2)],
                "Applications au démarrage", "Startup apps",
                $"{s.StartupAppsCount} active(s)", $"{s.StartupAppsCount} enabled",
                "Désactive celles dont tu n’as pas besoin dans Astuces.", "Turn off the ones you don’t need in Tweaks.",
                ScoreFix.TipsPage),
            Graded(ScoreCategory.Startup, 4, s.RunningProcessCount,
                [(150, 4), (190, 3), (230, 2), (280, 1)],
                "Processus en cours", "Running processes",
                $"{s.RunningProcessCount} processus", $"{s.RunningProcessCount} processes",
                "Ferme les logiciels ouverts en arrière-plan avant de jouer.", "Close background apps before gaming.",
                ScoreFix.None),
            Graded(ScoreCategory.Startup, 4, s.InstalledAppsCount,
                [(40, 4), (70, 3), (110, 2), (160, 1)],
                "Logiciels installés", "Installed software",
                $"{s.InstalledAppsCount} logiciels", $"{s.InstalledAppsCount} programs",
                "Désinstalle ce que tu n’utilises plus.", "Uninstall what you no longer use.",
                ScoreFix.WindowsApps),
            s.UptimeDays is { } uptime
                ? Criterion(ScoreCategory.Startup, 4, uptime < 7 ? 4 : uptime < 14 ? 2 : 0,
                    "Redémarré depuis moins de 7 jours", "Restarted within the last 7 days",
                    FormatUptimeFr(uptime), FormatUptimeEn(uptime),
                    "Utilise « Redémarrer » : avec le démarrage rapide, « Arrêter » ne remet pas Windows à zéro.",
                    "Use “Restart”: with Fast Startup, “Shut down” doesn’t fully reset Windows.",
                    ScoreFix.None)
                : Unmeasured(ScoreCategory.Startup, 4, "Redémarré depuis moins de 7 jours", "Restarted within the last 7 days"),

            // Stockage (14)
            Graded(ScoreCategory.Storage, 7, -s.SystemDriveFreePercent,
                [(-30, 7), (-20, 5), (-12, 3), (-6, 1)],
                "Espace libre sur le disque système", "Free space on the system drive",
                $"{s.SystemDriveFreePercent} % libre", $"{s.SystemDriveFreePercent}% free",
                "Libère de la place avec Nettoyer Windows.", "Free up space with Clean Windows.",
                ScoreFix.CleanupPage),
            Graded(ScoreCategory.Storage, 4, s.TempFolderSizeMb,
                [(500, 4), (1500, 3), (3000, 2), (6000, 1)],
                "Fichiers inutiles", "Junk files",
                FormatSizeFr(s.TempFolderSizeMb), FormatSizeEn(s.TempFolderSizeMb),
                "Supprime-les avec Nettoyer Windows.", "Remove them with Clean Windows.",
                ScoreFix.CleanupPage),
            s.SystemDriveIsSsd is { } ssd
                ? Criterion(ScoreCategory.Storage, 3, ssd ? 3 : 0,
                    "Windows installé sur un SSD", "Windows installed on an SSD",
                    ssd ? "SSD" : "Disque dur", ssd ? "SSD" : "Hard drive",
                    "Un SSD accélère fortement le démarrage et les chargements.", "An SSD greatly speeds up boot and loading times.",
                    ScoreFix.None)
                : Unmeasured(ScoreCategory.Storage, 3, "Windows installé sur un SSD", "Windows installed on an SSD"),

            // Mémoire (14)
            s.Xmp is FeatureActivationState.Enabled or FeatureActivationState.Disabled
                ? Criterion(ScoreCategory.Memory, 8, s.Xmp == FeatureActivationState.Enabled ? 8 : 0,
                    "XMP / EXPO activé", "XMP / EXPO enabled",
                    s.Xmp == FeatureActivationState.Enabled ? $"Activé · {s.XmpDetail}" : $"Désactivé · {s.XmpDetail}",
                    s.Xmp == FeatureActivationState.Enabled ? $"Enabled · {s.XmpDetail}" : $"Disabled · {s.XmpDetail}",
                    "Active le profil XMP / EXPO dans le BIOS pour que ta RAM tourne à sa vitesse prévue.",
                    "Enable the XMP / EXPO profile in the BIOS so your RAM runs at its rated speed.",
                    ScoreFix.XmpTutorial)
                : Unmeasured(ScoreCategory.Memory, 8, "XMP / EXPO activé", "XMP / EXPO enabled"),
            s.RamTotalGb > 0
                ? Graded(ScoreCategory.Memory, 6, (int)Math.Round(s.RamUsedGb / s.RamTotalGb * 100),
                    [(50, 6), (65, 5), (75, 3), (85, 1)],
                    "RAM utilisée", "RAM in use",
                    $"{s.RamUsedGb / s.RamTotalGb * 100:0} % ({s.RamUsedGb:0.#} / {s.RamTotalGb:0.#} Go)",
                    $"{s.RamUsedGb / s.RamTotalGb * 100:0}% ({s.RamUsedGb:0.#} / {s.RamTotalGb:0.#} GB)",
                    "Ferme les applis gourmandes (navigateur, launchers) avant de jouer.", "Close heavy apps (browser, launchers) before gaming.",
                    ScoreFix.None)
                : Unmeasured(ScoreCategory.Memory, 6, "RAM utilisée", "RAM in use"),

            // Températures (10)
            s.GpuTempC is { } gpuTemp
                ? Graded(ScoreCategory.Temperatures, 5, (int)Math.Round(gpuTemp),
                    [(60, 5), (70, 4), (78, 3), (85, 1)],
                    "Température GPU", "GPU temperature",
                    $"{gpuTemp:0} °C", $"{gpuTemp:0} °C",
                    "Nettoie la poussière et vérifie la ventilation du boîtier.", "Clean out dust and check your case airflow.",
                    ScoreFix.None)
                : Unmeasured(ScoreCategory.Temperatures, 5, "Température GPU", "GPU temperature"),
            s.CpuTempC is { } cpuTemp
                ? Graded(ScoreCategory.Temperatures, 5, (int)Math.Round(cpuTemp),
                    [(55, 5), (65, 4), (75, 2), (85, 1)],
                    "Température CPU", "CPU temperature",
                    $"{cpuTemp:0} °C", $"{cpuTemp:0} °C",
                    "Nettoie la poussière et vérifie le ventirad / la pâte thermique.", "Clean out dust and check your cooler / thermal paste.",
                    ScoreFix.None)
                : Unmeasured(ScoreCategory.Temperatures, 5, "Température CPU", "CPU temperature"),

            // Réglages gaming Windows (20)
            s.RefreshRateHz is { } hz && s.MaxRefreshRateHz is { } maxHz
                ? Criterion(ScoreCategory.GamingSettings, 5, hz >= maxHz - 1 ? 5 : 0,
                    "Écran à sa fréquence maximale", "Display at its maximum refresh rate",
                    $"{hz} Hz sur {maxHz} Hz possibles", $"{hz} Hz of {maxHz} Hz available",
                    "Règle la fréquence au maximum dans Paramètres → Affichage.", "Set the refresh rate to the maximum in Settings → Display.",
                    ScoreFix.DisplaySettingsPage)
                : Unmeasured(ScoreCategory.GamingSettings, 5, "Écran à sa fréquence maximale", "Display at its maximum refresh rate"),
            Toggle(ScoreCategory.GamingSettings, 4, s.PerformancePowerPlan,
                "Plan d’alimentation performances", "Performance power plan",
                s.PowerPlanName ?? "Plan standard", s.PowerPlanName ?? "Standard plan",
                "Installe et active le plan TweaksLab Community dans Astuces.", "Install and turn on the TweaksLab Community plan in Tweaks.",
                ScoreFix.TipsPage),
            Toggle(ScoreCategory.GamingSettings, 3, s.BackgroundCaptureOff,
                "Captures Xbox en arrière-plan coupées", "Xbox background capture off",
                s.BackgroundCaptureOff == true ? "Coupées" : "Actives", s.BackgroundCaptureOff == true ? "Off" : "On",
                "Coupe les captures dans Paramètres Windows → Jeux → Captures.", "Turn off captures in Windows Settings → Gaming → Captures.",
                ScoreFix.WindowsCaptures),
            Toggle(ScoreCategory.GamingSettings, 2, s.GameModeEnabled,
                "Mode Jeu Windows", "Windows Game Mode",
                s.GameModeEnabled == true ? "Activé" : "Désactivé", s.GameModeEnabled == true ? "On" : "Off",
                "Active-le dans Paramètres Windows → Jeux → Mode Jeu.", "Turn it on in Windows Settings → Gaming → Game Mode.",
                ScoreFix.WindowsGameMode),
            Toggle(ScoreCategory.GamingSettings, 2, s.HardwareGpuScheduling,
                "Planification GPU accélérée", "Hardware-accelerated GPU scheduling",
                s.HardwareGpuScheduling == true ? "Activée" : "Désactivée", s.HardwareGpuScheduling == true ? "On" : "Off",
                "Active-la dans Paramètres Windows → Affichage → Graphiques.", "Turn it on in Windows Settings → Display → Graphics.",
                ScoreFix.WindowsGraphics),
            Toggle(ScoreCategory.GamingSettings, 2, s.VisualEffectsOptimized,
                "Effets visuels optimisés", "Optimized visual effects",
                s.VisualEffectsOptimized == true ? "Optimisés" : "Par défaut", s.VisualEffectsOptimized == true ? "Optimized" : "Default",
                "Active l’optimisation de l’apparence dans Astuces.", "Turn on the visual effects optimization in Tweaks.",
                ScoreFix.TipsPage),
            Toggle(ScoreCategory.GamingSettings, 2, s.MouseAccelerationOff,
                "Accélération de la souris coupée", "Mouse acceleration off",
                s.MouseAccelerationOff == true ? "Coupée" : "Active", s.MouseAccelerationOff == true ? "Off" : "On",
                "Décoche « Améliorer la précision du pointeur » dans les options de la souris.",
                "Uncheck “Enhance pointer precision” in the mouse options.",
                ScoreFix.WindowsMouse),

            // Stabilité et pilotes (12)
            Graded(ScoreCategory.Stability, 6, s.RecentErrorCount,
                [(3, 6), (10, 4), (25, 2), (60, 1)],
                "Erreurs système (7 derniers jours)", "System errors (last 7 days)",
                $"{s.RecentErrorCount} erreur(s)", $"{s.RecentErrorCount} error(s)",
                "Beaucoup d’erreurs peuvent venir d’un pilote ou d’un logiciel instable.", "Lots of errors often come from an unstable driver or program.",
                ScoreFix.None),
            s.GpuDriverAgeDays is { } driverAge
                ? Criterion(ScoreCategory.Stability, 6, driverAge <= 180 ? 6 : driverAge <= 365 ? 3 : 0,
                    "Pilote graphique récent", "Recent graphics driver",
                    FormatAgeFr(driverAge), FormatAgeEn(driverAge),
                    "Mets à jour ton pilote graphique (guide dans Optimiser ton GPU).", "Update your graphics driver (guide in Optimize GPU).",
                    ScoreFix.GpuPage)
                : Unmeasured(ScoreCategory.Stability, 6, "Pilote graphique récent", "Recent graphics driver"),
        };

        var measuredMax = criteria.Where(c => c.Measured).Sum(c => c.MaxPoints);
        var earned = criteria.Where(c => c.Measured).Sum(c => c.Earned!.Value);
        var score = measuredMax > 0
            ? (int)Math.Round(PerformanceScoreDefaults.MaxScore * earned / measuredMax)
            : 0;

        return new ScoreBreakdown(criteria, Math.Clamp(score, PerformanceScoreDefaults.MinScore, PerformanceScoreDefaults.MaxScore));
    }

    /// <summary>Barème par paliers : (seuil maximum, points). Au-delà du dernier palier : 0 point.</summary>
    private static ScoreCriterion Graded(
        ScoreCategory category, int maxPoints, long value, (long Max, double Points)[] steps,
        string labelFr, string labelEn, string valueFr, string valueEn, string tipFr, string tipEn, ScoreFix fix)
    {
        var points = steps.FirstOrDefault(step => value <= step.Max).Points;
        return Criterion(category, maxPoints, points, labelFr, labelEn, valueFr, valueEn, tipFr, tipEn, fix);
    }

    private static ScoreCriterion Toggle(
        ScoreCategory category, int maxPoints, bool? state,
        string labelFr, string labelEn, string valueFr, string valueEn, string tipFr, string tipEn, ScoreFix fix) =>
        state is { } on
            ? Criterion(category, maxPoints, on ? maxPoints : 0, labelFr, labelEn, valueFr, valueEn, tipFr, tipEn, fix)
            : Unmeasured(category, maxPoints, labelFr, labelEn);

    private static ScoreCriterion Criterion(
        ScoreCategory category, int maxPoints, double points,
        string labelFr, string labelEn, string valueFr, string valueEn, string tipFr, string tipEn, ScoreFix fix) =>
        new(category, maxPoints, Math.Clamp(points, 0, maxPoints), labelFr, labelEn, valueFr, valueEn,
            points >= maxPoints ? null : tipFr, points >= maxPoints ? null : tipEn,
            points >= maxPoints ? ScoreFix.None : fix);

    private static ScoreCriterion Unmeasured(ScoreCategory category, int maxPoints, string labelFr, string labelEn) =>
        new(category, maxPoints, null, labelFr, labelEn,
            "Non mesurable sur ce PC", "Not measurable on this PC", null, null, ScoreFix.None);

    private static string FormatUptimeFr(double days) =>
        days < 1 ? $"allumé depuis {Math.Max(1, days * 24):0} h" : $"allumé depuis {days:0} j";

    private static string FormatUptimeEn(double days) =>
        days < 1 ? $"up for {Math.Max(1, days * 24):0} h" : $"up for {days:0} days";

    private static string FormatSizeFr(long mb) => mb >= 1024 ? $"{mb / 1024.0:0.#} Go à nettoyer" : $"{mb} Mo à nettoyer";

    private static string FormatSizeEn(long mb) => mb >= 1024 ? $"{mb / 1024.0:0.#} GB to clean" : $"{mb} MB to clean";

    private static string FormatAgeFr(int days) =>
        days < 31 ? "moins d’un mois" : days < 365 ? $"{days / 30} mois" : $"{days / 365} an(s)";

    private static string FormatAgeEn(int days) =>
        days < 31 ? "under a month old" : days < 365 ? $"{days / 30} months old" : $"{days / 365} year(s) old";
}
