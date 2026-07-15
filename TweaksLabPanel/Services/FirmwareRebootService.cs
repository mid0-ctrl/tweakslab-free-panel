using System.Diagnostics;

namespace TweaksLabPanel.Services;

public static class FirmwareRebootService
{
    public static bool TryRebootToFirmware(out string message)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "shutdown.exe",
                Arguments = "/r /fw /t 0",
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden,
            };

            Process.Start(psi);
            message = "Redémarrage vers le BIOS en cours…";
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            message = "Autorisation refusée. Accepte l'invite Windows pour redémarrer dans le BIOS.";
            return false;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return false;
        }
    }
}
