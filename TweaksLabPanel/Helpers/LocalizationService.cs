using TweaksLabPanel.Core;

namespace TweaksLabPanel.Helpers;

public static class LocalizationService
{
    public static event Action? LanguageChanged;

    public static bool IsEnglish => AppSettingsStore.Instance.Language == "en";

    public static void SetLanguage(string language)
    {
        AppSettingsStore.Instance.SaveLanguage(language);
        LanguageChanged?.Invoke();
    }

    public static string T(string fr, string en) =>
        IsEnglish ? en : fr;

    public static string NavHome => T("Accueil", "Home");
    public static string NavAssistance => T("Assistance", "Support");
    public static string NavMonPc => T("Mon PC", "My PC");
    public static string NavJeux => T("Jeux", "Games");
    public static string NavApplication => T("Application", "Apps");
    public static string NavNettoyer => T("Nettoyer Windows", "Clean Windows");
    public static string NavReinstallation => T("Réinstallation", "Reinstall");
    public static string NavRecommandations => T("Recommandations", "Tips");
    public static string NavAstuces => T("Astuces", "Tweaks");
    public static string NavGpu => T("Optimiser votre GPU", "Optimize GPU");
    public static string NavParametres => T("Paramètres", "Settings");
    public static string NavProfil => T("Profil", "Profile");
    public static string NavCreator => T("Programme créateur", "Creator program");
    public static string FreePanel => T("Free Panel", "Free Panel");
    public static string Soon => T("Bientôt", "Soon");
    public static string Continue => T("Continuer", "Continue");
    public static string Apply => T("Appliquer", "Apply");
    public static string Copy => T("Copier", "Copy");
    public static string Share => T("Partager", "Share");
    public static string Refresh => T("Actualiser", "Refresh");
    public static string Clean => T("Nettoyer", "Clean");
    public static string TournamentMode => T("Avant tournoi", "Pre-match");
    public static string StreakDays(int days) => IsEnglish ? $"{days} day streak" : $"Série de {days} jours";
    public static string LanguageLabel => T("Langue", "Language");
    public static string KeyboardLabel => T("Disposition clavier", "Keyboard layout");
    public static string Azerty => "AZERTY";
    public static string Qwerty => "QWERTY";
    public static string French => T("Français", "French");
    public static string English => T("Anglais", "English");
    public static string DisplaySection => T("AFFICHAGE", "DISPLAY");
    public static string Resolution => T("Résolution", "Resolution");
    public static string RefreshRate => T("Fréquence de rafraîchissement", "Refresh rate");
    public static string ConfirmDisplayChange => T(
        "Appliquer cette résolution et cette fréquence ? L'écran peut clignoter brièvement.",
        "Apply this resolution and refresh rate? Your screen may blink briefly.");
    public static string DiskHealth => T("Santé du disque système", "System disk health");
    public static string DiskLowWarning => T("Espace disque faible (< 10 %)", "Low disk space (< 10%)");
    public static string RecoverableSpace => T("Espace récupérable estimé", "Estimated recoverable space");
    public static string SkipTour => T("Passer", "Skip");
    public static string Next => T("Suivant", "Next");
    public static string Finish => T("Terminer", "Finish");
    public static string BioLabel => T("Bio", "Bio");
    public static string GpuStepsSoon => T("Les étapes d'optimisation seront ajoutées ici.", "Optimization steps will be added here.");
    public static string BiosSection => T("BIOS / FIRMWARE", "BIOS / FIRMWARE");
    public static string BiosSectionHint => T(
        "Vérifie si TPM, Secure Boot et XMP/EXPO sont activés sur ton PC.",
        "Check whether TPM, Secure Boot and XMP/EXPO are enabled on your PC.");
    public static string TpmLabel => T("TPM 2.0", "TPM 2.0");
    public static string SecureBootLabel => T("Secure Boot", "Secure Boot");
    public static string XmpLabel => T("XMP / EXPO", "XMP / EXPO");
    public static string XmpTutorialLink => T("Tuto activer XMP", "XMP activation tutorial");
    public static string StatusEnabled => T("Activé", "Enabled");
    public static string StatusDisabled => T("Désactivé", "Disabled");
    public static string StatusUnknown => T("Inconnu", "Unknown");
    public static string StatusNotSupported => T("Non supporté", "Not supported");
    public static string RebootBiosButton => T("Redémarrer dans le BIOS", "Restart into BIOS");
    public static string RebootBiosConfirm => T(
        "Le PC va redémarrer directement dans le BIOS/UEFI. Enregistre ton travail avant de continuer.",
        "Your PC will restart straight into BIOS/UEFI. Save your work before continuing.");
    public static string RebootBiosTitle => T("Redémarrage BIOS", "BIOS restart");
    public static string OnboardingSection => T("VISITE GUIDÉE", "GUIDED TOUR");
    public static string OnboardingSectionHint => T(
        "Relance le tutoriel de première visite avec les zones mises en avant.",
        "Replay the first-visit tutorial with highlighted areas.");
    public static string ReplayOnboardingButton => T("Relancer la visite guidée", "Replay guided tour");
    public static string PerformanceScoreInfo => T(
        "Estimation des performances selon des critères par TweaksLab.",
        "Performance estimate based on TweaksLab criteria.");
    public static string ReviewsInfo => T(
        "Avis renouvelés toutes les 2 semaines.",
        "Reviews refreshed every 2 weeks.");
    public static string UpdateAvailableTitle => T("Mise à jour disponible", "Update available");
    public static string UpdateDefaultNotes => T("Une nouvelle version du panel est prête.", "A new panel version is ready.");
    public static string UpdateDownloading => T("Téléchargement en cours…", "Downloading…");
    public static string UpdateAvailableMessage(string version, string notes) => IsEnglish
        ? $"Version {version} is available.\n\n{notes}\n\nInstall now? The panel will restart."
        : $"La version {version} est disponible.\n\n{notes}\n\nInstaller maintenant ? Le panel va redémarrer.";
    public static string UpdateFailed(string detail) => IsEnglish
        ? $"Update failed.\n\n{detail}"
        : $"Échec de la mise à jour.\n\n{detail}";
}
