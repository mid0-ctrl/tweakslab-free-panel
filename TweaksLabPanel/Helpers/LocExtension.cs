using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Markup;

namespace TweaksLabPanel.Helpers;

/// <summary>
/// Texte bilingue en XAML, mis à jour en direct au changement de langue :
/// <c>Text="{h:Loc Fr='Accueil', En='Home'}"</c>.
/// Dans les textes français, utiliser l’apostrophe typographique (’) pour éviter l’échappement.
/// </summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class LocExtension : MarkupExtension
{
    public LocExtension()
    {
    }

    public LocExtension(string fr, string en)
    {
        Fr = fr;
        En = en;
    }

    public string Fr { get; set; } = string.Empty;

    public string En { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding(nameof(LocalizedText.Value))
        {
            Source = new LocalizedText(Fr, En),
            Mode = BindingMode.OneWay,
        };
        return binding.ProvideValue(serviceProvider);
    }
}

/// <summary>Source de liaison d’un texte bilingue ; notifie tous les textes vivants au changement de langue.</summary>
public sealed class LocalizedText : INotifyPropertyChanged
{
    private static readonly List<WeakReference<LocalizedText>> Instances = [];
    private static readonly object Gate = new();
    private static readonly PropertyChangedEventArgs ValueChangedArgs = new(nameof(Value));

    private readonly string _fr;
    private readonly string _en;

    static LocalizedText()
    {
        LocalizationService.LanguageChanged += RaiseAll;
    }

    public LocalizedText(string fr, string en)
    {
        _fr = fr;
        _en = string.IsNullOrEmpty(en) ? fr : en;

        lock (Gate)
        {
            if (Instances.Count > 0 && Instances.Count % 256 == 0)
                Instances.RemoveAll(w => !w.TryGetTarget(out _));
            Instances.Add(new WeakReference<LocalizedText>(this));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Value => LocalizationService.T(_fr, _en);

    private static void RaiseAll()
    {
        List<LocalizedText> alive = [];
        lock (Gate)
        {
            Instances.RemoveAll(w => !w.TryGetTarget(out _));
            foreach (var weak in Instances)
            {
                if (weak.TryGetTarget(out var text))
                    alive.Add(text);
            }
        }

        foreach (var text in alive)
            text.PropertyChanged?.Invoke(text, ValueChangedArgs);
    }
}
