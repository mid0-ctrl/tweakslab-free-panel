namespace TweaksLabPanel.Core;

public static class ReviewCatalog
{
    public sealed record Review(string Quote, string User, string Date, string Initial, string Accent);

    public static readonly Review[] All =
    [
        new("opti nickel 0 delai un gros gain de fps je recommande fortement l opti", "malletnirka", "9 juil. 2026", "M", "#7C3AED"),
        new("opti nickel 0 delai god fps god opti finalement rapport qualité au top je recommande fortement", "midiakfrr", "6 juil. 2026", "M", "#7C3AED"),
        new("Merci pour l'opti ! gain de FPS incroyable, jeu plus fluide, je recommande à 100 % !", "93_zinx", "10 juil. 2026", "9", "#7C3AED"),
        new("Très bonne opti ! +100fps sur valo et mon délai s'est beaucoup réduit !", "kq.7690", "3 juil. 2026", "K", "#8B5CF6"),
        new("J'ai envie de pleurer tellement c'est une dinguerie, de 130 fps à 300 !", "vdt.enzo", "23 juin 2026", "V", "#6366F1"),
        new("Première fois que j'optimise chez vous et j'ai vue un gain incroyable", "cw_primeslayzie", "28 juin 2026", "C", "#7C3AED"),
        new("Merci a toutes l'équipe opti aux top très très bon boulot et très sérieux", "0120660168", "8 mai 2026", "0", "#6366F1"),
    ];
}
