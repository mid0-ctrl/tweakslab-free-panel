# TweaksLab Free Panel

Panel Community gratuit (WPF / .NET 8), construit localement à partir de maquettes.

## Isolation

Ce dépôt est **autonome**. Aucun code importé depuis `tweakslab-client-csharp`, `tweakslab-server`, `tweakslab-bot` ou tout autre repo TweaksLab.

Connexions réseau autorisées (voir `TweaksLabPanel/Core/PanelEndpoints.cs`) :

- Mises à jour signées
- Chatbot `/panel`

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
