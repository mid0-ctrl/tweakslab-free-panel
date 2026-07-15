namespace TweaksLabPanel.Core;

public static class ReviewCatalog
{
    public sealed record Review(string Quote, string User, string Date, string Initial, string Accent);

    public static readonly Review[] All =
    [
        new("opti nickel 0 delai un gros gain de fps je recommande fortement l opti", "malletnirka", "9 juil. 2026", "M", "#7C3AED"),
        new("opti nickel 0 delai god fps god opti finalement rapport qualité au top je recommande fortement", "midiakfrr", "6 juil. 2026", "M", "#7C3AED"),
        new("chuis passer de 130 a 330 avec l'opti a 50 euros franchement nickel", "lechantdeshonneurs", "10 juil. 2026", "L", "#8B5CF6"),
        new("Chuis passer de 160 fps a 240 fps avec 20€ !", "ennix2k26.", "10 juil. 2026", "E", "#6366F1"),
        new("Merci pour l'opti ! gain de FPS incroyable, jeu plus fluide, je recommande à 100 % !", "93_zinx", "10 juil. 2026", "9", "#7C3AED"),
        new("Très bonne opti ! +100fps sur valo et mon délai s'est beaucoup réduit !", "kq.7690", "3 juil. 2026", "K", "#8B5CF6"),
        new("J'ai envie de pleurer tellement c'est une dinguerie, de 130 fps à 300 !", "vdt.enzo", "23 juin 2026", "V", "#6366F1"),
        new("Opti a 20 euro parfaite 0 delay et des fps plus stable je recomande 10/10", "vklyro", "13 juin 2026", "V", "#7C3AED"),
        new("Opti a 20 euro , + 150-200fps 0 latence vrmt au top du top", "403_exe", "11 juin 2026", "4", "#8B5CF6"),
        new("Première fois que j'optimise chez vous et j'ai vue un gain incroyable", "cw_primeslayzie", "28 juin 2026", "C", "#7C3AED"),
        new("Merci a toutes l'équipe opti aux top très très bon boulot et très sérieux", "0120660168", "8 mai 2026", "0", "#6366F1"),
    ];
}
