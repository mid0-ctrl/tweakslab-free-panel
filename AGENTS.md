# Instructions pour les agents IA

## Règle n°1 — isolation totale

Ce dépôt est le **TweaksLab Free Panel**. Il doit rester **100 % autonome**.

### Ne jamais

- Copier ou lire du code depuis un autre repo TweaksLab (`client`, `bot`, `server`, `installer`, etc.)
- Cloner, merger ou ajouter un submodule depuis un autre dépôt GitHub
- Importer de la logique premium (auth, licence, HWID, Stripe, Supabase)
- Committer des secrets ou tokens

### Autorisé

- **Code écrit ici** → C#, XAML, services, vues
- **NuGet** → packages .NET officiels
- **URLs** → uniquement via `TweaksLabPanel/Core/PanelEndpoints.cs`

### Git

- Repo officiel : `mid0-ctrl/tweakslab-free-panel`
- Un seul remote `origin`
- Implémenter ici ce qui manque — ne pas chercher ailleurs

Règle détaillée : `.cursor/rules/free-panel.mdc` (alwaysApply).
