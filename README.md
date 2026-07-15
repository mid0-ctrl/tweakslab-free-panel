# TweaksLab Free Panel

Panel Community gratuit (WPF / .NET 8), construit localement à partir de maquettes (captures d’écran = **design uniquement**).

## Règle n°1 — isolation

Ce dépôt est **autonome**. Tout le code a été **créé ici**.

**Interdit :** importer du code depuis `tweakslab-client-csharp`, `tweakslab-server`, `tweakslab-bot`, `tweakslab-installer-csharp` ou tout autre repo TweaksLab.

**Autorisé :** NuGet .NET, assets du panel, URLs publiques dans `TweaksLabPanel/Core/PanelEndpoints.cs` (chatbot + MAJ).

**Repo GitHub officiel :** [mid0-ctrl/tweakslab-free-panel](https://github.com/mid0-ctrl/tweakslab-free-panel) — un seul `origin`, pas de submodule.

Voir aussi `AGENTS.md` et `.cursor/rules/free-panel.mdc`.

## Lancer en local

```bat
go.bat
```

Ou :

```bat
Lancer-Panel.bat
```

## Prérequis

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
