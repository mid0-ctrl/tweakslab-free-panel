# Publier une mise à jour

## Workflow (le plus simple)

1. Monte la version dans `TweaksLabPanel/TweaksLabPanel.csproj` (`<Version>1.0.1</Version>`)
2. Commit + push sur `main`
3. Crée et pousse un tag :

```powershell
git tag v1.0.1
git push origin v1.0.1
```

4. **GitHub Actions** build automatiquement le zip et publie la Release
5. Les utilisateurs reçoivent la MAJ au prochain lancement du panel (vérifie toutes les 6 h)

## Côté utilisateur

- Popup « Mise à jour disponible »
- **Oui** → téléchargement, remplacement des fichiers, redémarrage
- **Non** → rappel ignoré pour cette version

## Capacité (~5000 users)

- Téléchargements : CDN GitHub Releases
- Vérification version : API GitHub `releases/latest` (1 appel / user / 6 h max)
